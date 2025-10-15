///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace GriffinPlus.Lib.Logging;

partial class LogFile
{
	/// <summary>
	/// Base class for the 'recording' file format and 'analysis' file format.
	/// </summary>
	internal abstract class DatabaseAccessor : IDisposable
	{
		private          bool mDisposed;
		private readonly bool mCanRollback;

		// dictionaries caching mappings from names to corresponding ids used to reference these names
		private readonly OverlayDictionary<string, long> mProcessNameToId     = [];
		private readonly OverlayDictionary<string, long> mApplicationNameToId = [];
		private readonly OverlayDictionary<string, long> mLogWriterNameToId   = [];
		private readonly OverlayDictionary<string, long> mLogLevelNameToId    = [];
		private readonly OverlayDictionary<string, long> mTagToId             = [];

		// sqlite specific commands
		private readonly List<SQLiteCommand> mCommands;                                                   //
		private readonly SQLiteCommand       mBeginTransactionCommand;                                    // transactions
		private readonly SQLiteCommand       mCommitTransactionCommand;                                   //
		private readonly SQLiteCommand       mRollbackTransactionCommand;                                 //
		private readonly SQLiteCommand       mVacuumCommand;                                              // vacuum
		private readonly SQLiteCommand       mVacuumIntoCommand;                                          //
		private readonly SQLiteParameter     mVacuumIntoCommand_FileParameter;                            //
		private readonly SQLiteCommand       mSelectAllProcessNamesCommand;                               // processes
		private readonly SQLiteCommand       mInsertProcessNameCommand;                                   //
		private readonly SQLiteParameter     mInsertProcessNameCommand_NameParameter;                     //
		private readonly SQLiteCommand       mSelectProcessNameIdCommand;                                 //
		private readonly SQLiteParameter     mSelectProcessNameIdCommand_NameParameter;                   //
		private readonly SQLiteCommand       mSelectAllApplicationNamesCommand;                           // applications
		private readonly SQLiteCommand       mInsertApplicationNameCommand;                               //
		private readonly SQLiteParameter     mInsertApplicationNameCommand_NameParameter;                 //
		private readonly SQLiteCommand       mSelectApplicationNameIdCommand;                             //
		private readonly SQLiteParameter     mSelectApplicationNameIdCommand_NameParameter;               //
		private readonly SQLiteCommand       mSelectAllLogWriterNamesCommand;                             // log writers
		private readonly SQLiteCommand       mInsertLogWriterNameCommand;                                 //
		private readonly SQLiteParameter     mInsertLogWriterNameCommand_NameParameter;                   //
		private readonly SQLiteCommand       mSelectLogWriterIdCommand;                                   //
		private readonly SQLiteParameter     mSelectLogWriterIdCommand_NameParameter;                     //
		private readonly SQLiteCommand       mSelectAllLogLevelNamesCommand;                              // log levels
		private readonly SQLiteCommand       mInsertLogLevelNameCommand;                                  //
		private readonly SQLiteParameter     mInsertLogLevelNameCommand_NameParameter;                    //
		private readonly SQLiteCommand       mSelectLogLevelIdCommand;                                    //
		private readonly SQLiteParameter     mSelectLogLevelIdCommand_NameParameter;                      //
		private readonly SQLiteCommand       mSelectAllTagsCommand;                                       // tags
		private readonly SQLiteCommand       mInsertTagCommand;                                           //
		private readonly SQLiteParameter     mInsertTagCommand_NameParameter;                             //
		private readonly SQLiteCommand       mSelectTagIdCommand;                                         //
		private readonly SQLiteParameter     mSelectTagIdCommand_NameParameter;                           //
		private readonly SQLiteCommand       mInsertTagToMessageMappingCommand;                           // tag to message mapping
		private readonly SQLiteParameter     mInsertTagToMessageMappingCommand_TagIdParameter;            //
		private readonly SQLiteParameter     mInsertTagToMessageMappingCommand_MessageIdParameter;        //
		private readonly SQLiteCommand       mSelectAllTagsOfMessageByIdCommand;                          //
		private readonly SQLiteParameter     mSelectAllTagsOfMessageByIdCommand_MessageIdParameter;       //
		private readonly SQLiteCommand       mDeleteTagToMessageMappingsUpToIdCommand;                    //
		private readonly SQLiteParameter     mDeleteTagToMessageMappingsUpToIdCommand_MessageIdParameter; //

		/// <summary>
		/// Application id used within a sqlite database to recognize it as a log file
		/// </summary>
		// ReSharper disable once CommentTypo
		public const uint LogFileApplicationId = 0x47504C47; // GPLG = [G]riffin [P]lus [L]o[G])

		/// <summary>
		/// SQL commands that create the database structure common to all file formats.
		/// </summary>
		protected static readonly string[] CreateDatabaseCommands_CommonStructure =
		[
			$"PRAGMA application_id = {LogFileApplicationId};",
			"PRAGMA encoding = 'UTF-8';",
			"PRAGMA page_size = 65536;",
			"CREATE TABLE processes (id INTEGER PRIMARY KEY, name TEXT);",
			"CREATE TABLE applications (id INTEGER PRIMARY KEY, name TEXT);",
			"CREATE TABLE writers (id INTEGER PRIMARY KEY, name TEXT);",
			"CREATE TABLE levels (id INTEGER PRIMARY KEY, name TEXT);",
			"CREATE TABLE tags (id INTEGER PRIMARY KEY, name TEXT);",
			"CREATE TABLE tag2msg (id INTEGER PRIMARY KEY, tag_id INTEGER, message_id INTEGER);"
		];

		/// <summary>
		/// SQL commands that add common indices to the database.
		/// </summary>
		protected static readonly string[] CreateDatabaseCommands_CommonIndices =
		[
			"CREATE UNIQUE INDEX processes_name_index ON processes (name);",
			"CREATE UNIQUE INDEX applications_name_index ON applications (name);",
			"CREATE UNIQUE INDEX writers_name_index ON writers (name);",
			"CREATE UNIQUE INDEX levels_name_index ON levels (name);",
			"CREATE UNIQUE INDEX tags_name_index ON tags (name);",
			"CREATE INDEX tag2msg_tag_id_index ON tag2msg (tag_id);",
			"CREATE INDEX tag2msg_message_id_index ON tag2msg (message_id);"
		];

		/// <summary>
		/// SQL commands that delete all data from all common tables.
		/// </summary>
		private static readonly string[] sDropCommonTablesCommands =
		[
			"DROP TABLE processes;",
			"DROP TABLE applications;",
			"DROP TABLE writers;",
			"DROP TABLE levels;",
			"DROP TABLE tags;",
			"DROP TABLE tag2msg;"
		];

		/// <summary>
		/// Initializes the <see cref="DatabaseAccessor"/> class.
		/// </summary>
		static DatabaseAccessor()
		{
			// retrieve the version of the sqlite implementation
			using var connection = new SQLiteConnection("Data Source=:memory:");
			connection.Open();
			using var command = new SQLiteCommand("SELECT SQLITE_VERSION();", connection);
			SqliteVersion = command.ExecuteScalar().ToString();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DatabaseAccessor"/> class.
		/// </summary>
		/// <param name="connection">Database connection to use.</param>
		/// <param name="writeMode">Write mode that determines whether the database should be operating in robust mode or as fast as possible.</param>
		/// <param name="isReadOnly">
		/// <see langword="true"/> if the log file is opened in read-only mode;<br/>
		/// <see langword="false"/> if the log file is opened in read/write mode.
		/// </param>
		protected DatabaseAccessor(
			SQLiteConnection connection,
			LogFileWriteMode writeMode,
			bool             isReadOnly)
		{
			Connection = connection;
			mCommands = [];
			IsReadOnly = isReadOnly;
			WriteMode = writeMode;

			// commands to begin, commit and roll back a transaction
			mBeginTransactionCommand = PrepareCommand("BEGIN IMMEDIATE TRANSACTION;");
			mCommitTransactionCommand = PrepareCommand("COMMIT TRANSACTION;");
			mRollbackTransactionCommand = PrepareCommand("ROLLBACK TRANSACTION;");

			// vacuum

			// command to vacuum the database
			mVacuumCommand = PrepareCommand("VACUUM;");

			// command to vacuum the database and write the resulting database into another file
			mVacuumIntoCommand = PrepareCommand("VACUUM INTO @file;");
			mVacuumIntoCommand.Parameters.Add(mVacuumIntoCommand_FileParameter = new SQLiteParameter("@file", DbType.String));

			// processes

			// command to get all process names in ascending order
			mSelectAllProcessNamesCommand = PrepareCommand("SELECT name FROM processes ORDER BY name ASC;");

			// command to add a process name (id is assigned automatically)
			mInsertProcessNameCommand = PrepareCommand("INSERT OR IGNORE INTO processes (name) VALUES (@name);");
			mInsertProcessNameCommand.Parameters.Add(mInsertProcessNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// command to get the id of a process name
			mSelectProcessNameIdCommand = PrepareCommand("SELECT id FROM processes WHERE name = @name;");
			mSelectProcessNameIdCommand.Parameters.Add(mSelectProcessNameIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// applications

			// command to get all application names in ascending order
			mSelectAllApplicationNamesCommand = PrepareCommand("SELECT name FROM applications ORDER BY name ASC;");

			// command to add an application name (id is assigned automatically)
			mInsertApplicationNameCommand = PrepareCommand("INSERT OR IGNORE INTO applications (name) VALUES (@name);");
			mInsertApplicationNameCommand.Parameters.Add(mInsertApplicationNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// command to get the id of an application name
			mSelectApplicationNameIdCommand = PrepareCommand("SELECT id FROM applications WHERE name = @name;");
			mSelectApplicationNameIdCommand.Parameters.Add(mSelectApplicationNameIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// writers

			// command to get all log writer names in ascending order
			mSelectAllLogWriterNamesCommand = PrepareCommand("SELECT name FROM writers ORDER BY name ASC;");

			// command to add a log writer name (id is assigned automatically)
			mInsertLogWriterNameCommand = PrepareCommand("INSERT OR IGNORE INTO writers (name) VALUES (@name);");
			mInsertLogWriterNameCommand.Parameters.Add(mInsertLogWriterNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// command to get the id of a log writer name
			mSelectLogWriterIdCommand = PrepareCommand("SELECT id FROM writers WHERE name = @name;");
			mSelectLogWriterIdCommand.Parameters.Add(mSelectLogWriterIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// levels

			// command to get all log writer names in ascending order
			mSelectAllLogLevelNamesCommand = PrepareCommand("SELECT name FROM levels ORDER BY name ASC;");

			// command to add a log level name (id is assigned automatically)
			mInsertLogLevelNameCommand = PrepareCommand("INSERT OR IGNORE INTO levels (name) VALUES (@name);");
			mInsertLogLevelNameCommand.Parameters.Add(mInsertLogLevelNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// command to get the id of a log level name
			mSelectLogLevelIdCommand = PrepareCommand("SELECT id FROM levels WHERE name = @name;");
			mSelectLogLevelIdCommand.Parameters.Add(mSelectLogLevelIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// tags

			// command to get all tags in ascending order
			mSelectAllTagsCommand = PrepareCommand("SELECT name FROM tags ORDER BY name ASC;");

			// command to add a tag (id is assigned automatically)
			mInsertTagCommand = PrepareCommand("INSERT OR IGNORE INTO tags (name) VALUES (@name);");
			mInsertTagCommand.Parameters.Add(mInsertTagCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// command to get the id of a tag
			mSelectTagIdCommand = PrepareCommand("SELECT id FROM tags WHERE name = @name;");
			mSelectTagIdCommand.Parameters.Add(mSelectTagIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// tag2msg

			// command to add a tag-to-message mapping (id is assigned automatically)
			mInsertTagToMessageMappingCommand = PrepareCommand("INSERT INTO tag2msg (tag_id, message_id) VALUES (@tag_id, @message_id);");
			mInsertTagToMessageMappingCommand.Parameters.Add(mInsertTagToMessageMappingCommand_TagIdParameter = new SQLiteParameter("@tag_id", DbType.Int64));
			mInsertTagToMessageMappingCommand.Parameters.Add(mInsertTagToMessageMappingCommand_MessageIdParameter = new SQLiteParameter("@message_id", DbType.Int64));

			// command to get all tags associated with a specific message id
			mSelectAllTagsOfMessageByIdCommand = PrepareCommand(
				"SELECT name FROM tags AS t" +
				" INNER JOIN tag2msg AS tm ON tm.tag_id = t.id" +
				" WHERE tm.message_id = @message_id;");
			mSelectAllTagsOfMessageByIdCommand.Parameters.Add(mSelectAllTagsOfMessageByIdCommand_MessageIdParameter = new SQLiteParameter("@message_id", DbType.Int64));

			// command to delete all tag-to-message mappings up to a specific message id
			mDeleteTagToMessageMappingsUpToIdCommand = PrepareCommand("DELETE FROM tag2msg as tm WHERE tm.message_id <= @message_id;");
			mDeleteTagToMessageMappingsUpToIdCommand.Parameters.Add(mDeleteTagToMessageMappingsUpToIdCommand_MessageIdParameter = new SQLiteParameter("@message_id", DbType.Int64));

			// store temporary data in memory
			ExecuteNonQueryCommand(connection, "PRAGMA temp_store = MEMORY;");

			// set busy timeout to 5 seconds
			ExecuteNonQueryCommand(connection, "PRAGMA busy_timeout = 5000;");

			// enable exclusive locking
			// (does not lock the database, yet - see below)
			ExecuteNonQueryCommand(connection, "PRAGMA locking_mode = EXCLUSIVE;");

			if (!isReadOnly)
			{
				// configure mode of operation (robust or fast)
				switch (WriteMode)
				{
					case LogFileWriteMode.Robust:
						ExecuteNonQueryCommand(connection, "PRAGMA synchronous = NORMAL;"); // FULL is not needed for preserving consistency in WAL mode (see http://www.sqlite.org/pragma.html#pragma_synchronous)
						ExecuteNonQueryCommand(connection, "PRAGMA journal_mode = WAL;");
						mCanRollback = true; // robust mode uses a journal and can therefore roll back
						break;

					case LogFileWriteMode.Fast:
						ExecuteNonQueryCommand(connection, "PRAGMA synchronous = OFF;");
						ExecuteNonQueryCommand(connection, "PRAGMA journal_mode = OFF;");
						mCanRollback = false; // fast mode does not use a journal and cannot roll back, behavior is undefined in these cases
						break;

					case LogFileWriteMode.NotSpecified:
					default:
						throw new NotSupportedException($"The specified write mode({WriteMode}) is not supported.");
				}

				// put the exclusive lock in place
				ExecuteNonQueryCommand(connection, "BEGIN EXCLUSIVE; COMMIT;");
			}
		}

		/// <summary>
		/// Disposes the current log file format.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		/// <summary>
		/// Does the actual disposal work.
		/// </summary>
		protected virtual void Dispose(bool disposing)
		{
			if (!mDisposed)
			{
				// if the database file has been opened in WAL mode, revert to delete mode, before closing the file
				// (opening a file that has been opened in WAL mode read-only, creates -shm and -wal files that are
				// not removed when the database is closed)
				if (WriteMode == LogFileWriteMode.Robust)
				{
					try
					{
						ExecuteNonQueryCommand(Connection, "PRAGMA journal_mode = delete");
					}
					catch
					{
						/* swallow */
					}
				}

				// dispose prepared statements
				foreach (SQLiteCommand command in mCommands) command.Dispose();

				// close connection to the database
				Connection?.Dispose();

				// the accessor is disposed now...
				mDisposed = true;
			}
		}

		/// <summary>
		/// Gets the version of the sqlite implementation.
		/// </summary>
		/// <returns>Version of the sqlite implementation.</returns>
		// ReSharper disable once MemberHidesStaticFromOuterClass
		public static string SqliteVersion { get; }

		/// <summary>
		/// Gets the sqlite database connection the log file works on.
		/// </summary>
		public SQLiteConnection Connection { get; }

		/// <summary>
		/// Gets a value determining whether the log file is opened for reading and writing (<see langword="false"/>) or for reading only (<see langword="true"/>
		/// ).
		/// </summary>
		public bool IsReadOnly { get; }

		/// <summary>
		/// Gets the purpose the log format is used for.
		/// </summary>
		public abstract LogFilePurpose Purpose { get; }

		/// <summary>
		/// Gets the write-mode of the log file.
		/// </summary>
		public LogFileWriteMode WriteMode { get; }

		/// <summary>
		/// Get the id of the oldest message in the file (-1, if the file is empty).
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public long OldestMessageId { get; protected set; }

		/// <summary>
		/// Get the id of the newest message in the file (-1, if the file is empty).
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public long NewestMessageId { get; protected set; }

		/// <summary>
		/// Get the number of messages in the file (-1, if the file is empty).
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public long MessageCount => NewestMessageId >= 0 ? NewestMessageId - OldestMessageId + 1 : 0;

		/// <summary>
		/// Gets the string pool that can be used to pool common strings to reduce memory consumption.
		/// </summary>
		protected StringPool StringPool { get; } = new();

		#region GetProcessNames()

		/// <summary>
		/// Gets the name of processes that are/were associated with log messages.
		/// </summary>
		/// <param name="usedOnly">
		/// <see langword="true"/> to get the name of processes that are referenced by messages in the log file only;<br/>
		/// <see langword="false"/> to get all process names (even if referencing log messages have been removed after clearing/pruning).
		/// </param>
		/// <returns>A list of process names.</returns>
		public string[] GetProcessNames(bool usedOnly)
		{
			return usedOnly
				       ? GetUsedProcessNames()
				       : ExecuteSingleColumnStringQuery(mSelectAllProcessNamesCommand, true);
		}

		/// <summary>
		/// Gets the name of processes that are associated with log messages.
		/// </summary>
		/// <returns>A list of process names.</returns>
		protected abstract string[] GetUsedProcessNames();

		#endregion

		#region GetProcessIds()

		/// <summary>
		/// Gets the ids of processes that are associated with log messages.
		/// </summary>
		/// <returns>A list of process ids.</returns>
		public abstract int[] GetProcessIds();

		#endregion

		#region GetApplicationNames()

		/// <summary>
		/// Gets the name of applications that are/were associated with log messages.
		/// </summary>
		/// <param name="usedOnly">
		/// <see langword="true"/> to get the name of applications that are referenced by messages in the log file only;<br/>
		/// <see langword="false"/> to get all application names (even if referencing log messages have been removed after clearing/pruning).
		/// </param>
		/// <returns>A list of application names.</returns>
		public string[] GetApplicationNames(bool usedOnly)
		{
			return usedOnly
				       ? GetUsedApplicationNames()
				       : ExecuteSingleColumnStringQuery(mSelectAllApplicationNamesCommand, true);
		}

		/// <summary>
		/// Gets the name of applications that are associated with log messages.
		/// </summary>
		/// <returns>A list of application names.</returns>
		protected abstract string[] GetUsedApplicationNames();

		#endregion

		#region GetLogWriterNames()

		/// <summary>
		/// Gets the name of log writers that are/were associated with log messages.
		/// </summary>
		/// <param name="usedOnly">
		/// <see langword="true"/> to get the name of log writers that are referenced by messages in the log file only;<br/>
		/// <see langword="false"/> to get all log writer names (even if referencing log messages have been removed after clearing/pruning).
		/// </param>
		/// <returns>A list of log writer names.</returns>
		public string[] GetLogWriterNames(bool usedOnly)
		{
			return usedOnly
				       ? GetUsedLogWriterNames()
				       : ExecuteSingleColumnStringQuery(mSelectAllLogWriterNamesCommand, true);
		}

		/// <summary>
		/// Gets the name of log writers that are associated with log messages.
		/// </summary>
		/// <returns>A list of log writer names.</returns>
		protected abstract string[] GetUsedLogWriterNames();

		#endregion

		#region GetLogLevelNames()

		/// <summary>
		/// Gets the name of log levels that are/were associated with log messages.
		/// </summary>
		/// <param name="usedOnly">
		/// <see langword="true"/> to get the name of log writers that are referenced by messages in the log file only;<br/>
		/// <see langword="false"/> to get all log writer names (even if referencing log messages have been removed after clearing/pruning).
		/// </param>
		/// <returns>A list of log level names.</returns>
		public string[] GetLogLevelNames(bool usedOnly)
		{
			return usedOnly
				       ? GetUsedLogLevelNames()
				       : ExecuteSingleColumnStringQuery(mSelectAllLogLevelNamesCommand, true);
		}

		/// <summary>
		/// Gets the name of log levels that are associated with log messages.
		/// </summary>
		/// <returns>A list of log level names.</returns>
		protected abstract string[] GetUsedLogLevelNames();

		#endregion

		#region GetTags()

		/// <summary>
		/// Gets the tags that are/were associated with log messages.
		/// </summary>
		/// <param name="usedOnly">
		/// <see langword="true"/> to get the tags that are referenced by messages in the log file only;<br/>
		/// <see langword="false"/> to get all tags (even if referencing log messages have been removed after clearing/pruning).
		/// </param>
		/// <returns>A list of tags.</returns>
		public string[] GetTags(bool usedOnly)
		{
			return usedOnly
				       ? GetUsedTags()
				       : ExecuteSingleColumnStringQuery(mSelectAllTagsCommand, true);
		}

		/// <summary>
		/// Gets the tags that are associated with log messages.
		/// </summary>
		/// <returns>A list of tags.</returns>
		protected abstract string[] GetUsedTags();

		#endregion

		#region Clear()

		/// <summary>
		/// Removes all data from the log file.
		/// </summary>
		/// <param name="messagesOnly">
		/// <see langword="true"/> to remove messages only;<br/>
		/// <see langword="false"/> to remove processes, applications, log writers, log levels and tags as well.
		/// </param>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public virtual void Clear(bool messagesOnly)
		{
			CheckReadOnly();

			ExecuteInTransaction(Operation);

			OldestMessageId = -1;
			NewestMessageId = -1;

			return;

			void Operation()
			{
				if (!messagesOnly)
				{
					// drop all common tables in the database and create new ones
					ExecuteNonQueryCommands(sDropCommonTablesCommands);
					ExecuteNonQueryCommands(CreateDatabaseCommands_CommonStructure);
					ExecuteNonQueryCommands(CreateDatabaseCommands_CommonIndices);

					// clear cache dictionaries
					mProcessNameToId.Clear();
					mApplicationNameToId.Clear();
					mLogWriterNameToId.Clear();
					mLogLevelNameToId.Clear();
					mTagToId.Clear();
				}

				ClearSpecific(messagesOnly);
			}
		}

		/// <summary>
		/// Removes all schema specific data from the log file.
		/// </summary>
		/// <param name="messagesOnly">
		/// <see langword="true"/> to remove messages only;<br/>
		/// <see langword="false"/> to remove processes, applications, log writers, log levels and tags as well.
		/// </param>
		protected abstract void ClearSpecific(bool messagesOnly);

		#endregion

		#region Read()

		/// <summary>
		/// Gets a number of log messages starting at the specified message id.
		/// </summary>
		/// <param name="fromId">ID of the message to start at.</param>
		/// <param name="count">Maximum number of log messages to get.</param>
		/// <returns>The requested log messages.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromId"/> is not in the interval [OldestMessageId,NewestMessageId].</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> must be positive.</exception>
		public virtual LogFileMessage[] Read(long fromId, int count)
		{
			if (fromId < 0) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, "The log message id must be positive.");
			if (fromId < OldestMessageId || fromId > NewestMessageId) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, $"The log message id must be in the interval [{OldestMessageId},{NewestMessageId}].");
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "The number of log messages must be positive.");

			var messages = new List<LogFileMessage>(count);

			Read(fromId, count, Callback);

			return messages.ToArray();

			bool Callback(LogFileMessage message)
			{
				messages.Add(message);
				return true;
			}
		}

		/// <summary>
		/// Gets a number of log messages starting at the specified message id.
		/// </summary>
		/// <param name="fromId">ID of the message to start at.</param>
		/// <param name="count">Number of log messages to get.</param>
		/// <param name="callback">Callback to invoke for every read message</param>
		/// <returns>
		/// <see langword="true"/> if reading ran to completion;<br/>
		/// <see langword="false"/> if reading was cancelled.
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromId"/> is not in the interval [OldestMessageId,NewestMessageId].</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> must be positive.</exception>
		public abstract bool Read(long fromId, long count, ReadMessageCallback callback);

		#endregion

		#region Write()

		/// <summary>
		/// Writes a log message into the log file.
		/// </summary>
		/// <param name="message">Log message to write.</param>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public virtual void Write(ILogMessage message)
		{
			CheckReadOnly();

			long messageId = NewestMessageId;

			ExecuteInTransaction(Operation);

			if (OldestMessageId < 0) OldestMessageId = messageId;
			NewestMessageId = messageId;

			return;

			void Operation()
			{
				WriteLogMessage(message, ++messageId);
			}
		}

		/// <summary>
		/// Writes multiple log messages into the log file.
		/// </summary>
		/// <param name="messages">Log messages to write.</param>
		/// <returns>Number of messages written (should always be all messages).</returns>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public virtual long Write(IEnumerable<ILogMessage> messages)
		{
			CheckReadOnly();

			long count = 0;
			ExecuteInTransaction(Operation);
			return count;

			void Operation()
			{
				// ReSharper disable once PossibleMultipleEnumeration
				foreach (ILogMessage message in messages)
				{
					long messageId = NewestMessageId + 1;
					WriteLogMessage(message, messageId);
					if (OldestMessageId < 0) OldestMessageId = messageId;
					NewestMessageId = messageId;
					count++;
				}
			}
		}

		/// <summary>
		/// Writes a single log message (must run in transaction for consistency).
		/// </summary>
		/// <param name="message">Message to write.</param>
		/// <param name="messageId">ID of the message in the log file.</param>
		protected abstract void WriteLogMessage(ILogMessage message, long messageId);

		#endregion

		#region Prune()

		/// <summary>
		/// Removes log messages that are above the specified message limit -or- have a timestamp before the specified point in time.
		/// </summary>
		/// <param name="maximumMessageCount">
		/// Maximum number of messages to keep;
		/// -1 to disable removing messages by maximum message count.
		/// </param>
		/// <param name="minimumMessageTimestamp">
		/// Point in time (UTC) to keep messages after (includes the exact point in time);
		/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
		/// </param>
		/// <returns>
		/// Number of removed messages.
		/// If <see cref="int.MaxValue"/> is returned <see cref="Prune(long, DateTime)"/> should be called once again
		/// to ensure all messages matching the criteria are removed.
		/// </returns>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public abstract int Prune(long maximumMessageCount, DateTime minimumMessageTimestamp);

		/// <summary>
		/// Removes log messages that are above the specified message limit -or- have a timestamp before the specified point in time.
		/// </summary>
		/// <param name="maximumMessageCount">
		/// Maximum number of messages to keep;
		/// -1 to disable removing messages by maximum message count.
		/// </param>
		/// <param name="minimumMessageTimestamp">
		/// Point in time (UTC) to keep messages after (includes the exact point in time);
		/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
		/// </param>
		/// <param name="removedMessages">Receives the log messages that have been removed.</param>
		/// <returns>
		/// Number of removed messages.
		/// If <see cref="int.MaxValue"/> is returned <see cref="Prune(long, DateTime, out LogFileMessage[])"/> should
		/// be called once again to ensure all messages matching the criteria are removed.
		/// </returns>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public abstract int Prune(
			long                 maximumMessageCount,
			DateTime             minimumMessageTimestamp,
			out LogFileMessage[] removedMessages);

		#endregion

		#region GetApplicationId()

		/// <summary>
		/// Gets the application id stored in the sqlite database ('PRAGMA application_id').
		/// </summary>
		/// <param name="connection">Connection to use.</param>
		/// <returns>The application id.</returns>
		public static ulong GetApplicationId(SQLiteConnection connection)
		{
			return Convert.ToUInt64(ExecuteScalarCommand(connection, "PRAGMA application_id;"));
		}

		#endregion

		#region GetSchemaVersion()

		/// <summary>
		/// Gets the version of the database schema ('PRAGMA user_version').
		/// </summary>
		/// <param name="connection">Connection to use.</param>
		/// <returns>Version of the database schema.</returns>
		public static ulong GetSchemaVersion(SQLiteConnection connection)
		{
			return Convert.ToUInt64(ExecuteScalarCommand(connection, "PRAGMA user_version;"));
		}

		#endregion

		#region Vacuum()

		/// <summary>
		/// Compacts the database file.
		/// </summary>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public void Vacuum()
		{
			CheckReadOnly();
			ExecuteNonQueryCommand(mVacuumCommand);
		}

		#endregion

		#region SaveSnapshot()

		/// <summary>
		/// Saves a snapshot of the log file
		/// (uses 'VACUUM INTO' that shrinks the database, cannot be cancelled).
		/// </summary>
		/// <param name="path">Path of the file to save the snapshot to.</param>
		public void SaveSnapshot(string path)
		{
			// determine the full path and convert it to a URI
			path = Path.GetFullPath(path);
			var pathUri = new Uri(path);

			// delete file, if it exists already
			File.Delete(path);

			// vacuum the database and stream the result into the specified file
			mVacuumIntoCommand_FileParameter.Value = pathUri.AbsoluteUri;
			ExecuteNonQueryCommand(mVacuumIntoCommand);
		}

		/// <summary>
		/// Saves a snapshot of the log file
		/// (uses the backup API that supports progress callbacks followed by a 'VACUUM' command).
		/// </summary>
		/// <param name="path">Path of the file to save the snapshot to.</param>
		/// <param name="progressCallback">
		/// Callback method receiving progress information
		/// (may be <see langword="null"/>, the callback may be called multiple times with the same progress, if database locking issues occur).
		/// </param>
		public void SaveSnapshot(string path, ProgressCallback progressCallback)
		{
			// determine the full path
			path = Path.GetFullPath(path);

			// delete file, if it exists already
			File.Delete(path);

			// indicate backup is starting
			progressCallback?.Invoke(0.0f, false);

			// open database file (creates a new one, if it does not exist)
			bool backupCompleted = false;
			using (var backupFileConnection = new SQLiteConnection($"Data Source={path};Version=3"))
			{
				backupFileConnection.Open();

				// adjust the page size of the backup database to the same page size of the source database
				int pageSize = GetPageSize(Connection);
				ExecuteNonQueryCommand(backupFileConnection, $"PRAGMA page_size = {pageSize};");

				// disable journal and synchronous mode to speed up writing the backup file
				ExecuteNonQueryCommand(backupFileConnection, "PRAGMA journal_mode = OFF;");
				ExecuteNonQueryCommand(backupFileConnection, "PRAGMA synchronous = OFF;");

				// define callback method that is invoked, if progress notifications are desired
				bool SqliteCallback(
					SQLiteConnection source,
					string           sourceName,
					SQLiteConnection destination,
					string           destinationName,
					int              pages,
					int              remainingPages,
					int              totalPages,
					bool             retry)
				{
					// notify caller about the progress and allow to cancel the operation
					// (may be called with the same progress, if the operation needs to be retried due to database locking issues)
					float progress = Math.Min((float)(totalPages - remainingPages) / totalPages, 1.0f);
					bool proceed = progressCallback(progress, false);
					backupCompleted = proceed;
					return proceed;
				}

				// determine the number of pages to copy at once
				// (influences the granularity of progress notifications and backup performance)
				// backing up approx. 1 mb at a time seems to be a good compromise
				const int maxCopySliceSize = 1 * 1024 * 1024;
				int pagesToCopyAtOnce = (int)((double)(maxCopySliceSize + pageSize - 1) / pageSize);

				// run the backup
				Connection.BackupDatabase(
					backupFileConnection,
					"main",
					"main",
					pagesToCopyAtOnce,
					progressCallback != null ? new SQLiteBackupCallback(SqliteCallback) : null,
					100);

				// vacuum the backup database to reduce its size,
				// if the backup ran to completion
				if (backupCompleted) ExecuteNonQueryCommand(backupFileConnection, "VACUUM;");
			}

			// delete incomplete file, if necessary
			if (!backupCompleted)
				File.Delete(path);

			// indicate backup has completed
			progressCallback?.Invoke(1.0f, !backupCompleted);
		}

		#endregion

		#region Internal Methods

		/// <summary>
		/// Gets the size of a database page the specified connection belongs to ('PRAGMA page_size').
		/// </summary>
		/// <param name="connection">Connection to use.</param>
		/// <returns>The page size (in bytes).</returns>
		private static int GetPageSize(SQLiteConnection connection)
		{
			return Convert.ToInt32(ExecuteScalarCommand(connection, "PRAGMA page_size;"));
		}

		/// <summary>
		/// Adds the specified process name to the database.
		/// </summary>
		/// <param name="name">Name of the process to add.</param>
		/// <returns>ID associated with the process name.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="name"/> parameter must not be <see langword="null"/>.</exception>
		protected long AddProcessName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			if (!mProcessNameToId.TryGetValue(name, out long id))
			{
				// insert process name into the table
				mInsertProcessNameCommand_NameParameter.Value = name;
				ExecuteNonQueryCommand(mInsertProcessNameCommand);

				// get id associated with the process name
				mSelectProcessNameIdCommand_NameParameter.Value = name;
				id = (long)ExecuteScalarCommand(mSelectProcessNameIdCommand);

				// cache mapping
				mProcessNameToId.Add(name, id);
			}

			return id;
		}

		/// <summary>
		/// Adds the specified application name to the database.
		/// </summary>
		/// <param name="name">Name of the application to add.</param>
		/// <returns>ID associated with the application name.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="name"/> parameter must not be <see langword="null"/>.</exception>
		protected long AddApplicationName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			if (!mApplicationNameToId.TryGetValue(name, out long id))
			{
				// insert application name into the table
				mInsertApplicationNameCommand_NameParameter.Value = name;
				ExecuteNonQueryCommand(mInsertApplicationNameCommand);

				// get id associated with the application name
				mSelectApplicationNameIdCommand_NameParameter.Value = name;
				id = (long)ExecuteScalarCommand(mSelectApplicationNameIdCommand);

				// cache mapping
				mApplicationNameToId.Add(name, id);
			}

			return id;
		}

		/// <summary>
		/// Adds the specified log writer name to the database.
		/// </summary>
		/// <param name="name">Name of the log writer to add.</param>
		/// <returns>ID associated with the log writer name.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="name"/> parameter must not be <see langword="null"/>.</exception>
		protected long AddLogWriterName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			if (!mLogWriterNameToId.TryGetValue(name, out long id))
			{
				// insert log writer name into the table
				mInsertLogWriterNameCommand_NameParameter.Value = name;
				ExecuteNonQueryCommand(mInsertLogWriterNameCommand);

				// get id associated with the log writer name
				mSelectLogWriterIdCommand_NameParameter.Value = name;
				id = (long)ExecuteScalarCommand(mSelectLogWriterIdCommand);

				// cache mapping
				mLogWriterNameToId.Add(name, id);
			}

			return id;
		}

		/// <summary>
		/// Adds the specified log level name to the database.
		/// </summary>
		/// <param name="name">Name of the log level to add.</param>
		/// <returns>ID associated with the log level name.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="name"/> parameter must not be <see langword="null"/>.</exception>
		protected long AddLogLevelName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			if (!mLogLevelNameToId.TryGetValue(name, out long id))
			{
				// insert log level name into the table
				mInsertLogLevelNameCommand_NameParameter.Value = name;
				ExecuteNonQueryCommand(mInsertLogLevelNameCommand);

				// get id associated with the log level name
				mSelectLogLevelIdCommand_NameParameter.Value = name;
				id = (long)ExecuteScalarCommand(mSelectLogLevelIdCommand);

				// cache mapping
				mLogLevelNameToId.Add(name, id);
			}

			return id;
		}

		/// <summary>
		/// Adds the specified tag to the database.
		/// </summary>
		/// <param name="tag">Tag to add.</param>
		/// <returns>ID associated with the tag.</returns>
		/// <exception cref="ArgumentNullException">The <paramref name="tag"/> parameter must not be <see langword="null"/>.</exception>
		protected long AddTag(string tag)
		{
			if (tag == null) throw new ArgumentNullException(nameof(tag));

			if (!mTagToId.TryGetValue(tag, out long id))
			{
				// insert tag into the table
				mInsertTagCommand_NameParameter.Value = tag;
				ExecuteNonQueryCommand(mInsertTagCommand);

				// get id associated with the tag
				mSelectTagIdCommand_NameParameter.Value = tag;
				id = (long)ExecuteScalarCommand(mSelectTagIdCommand);

				// cache mapping
				mTagToId.Add(tag, id);
			}

			return id;
		}

		/// <summary>
		/// Gets the set of tags associated with the message with the specified id.
		/// </summary>
		/// <param name="messageId">ID of the message to get the tags for.</param>
		/// <returns>Tag set containing all tags associated with the specified message.</returns>
		public TagSet GetTagsOfMessage(long messageId)
		{
			mSelectAllTagsOfMessageByIdCommand_MessageIdParameter.Value = messageId;
			return new TagSet(ExecuteSingleColumnStringQuery(mSelectAllTagsOfMessageByIdCommand, true));
		}

		/// <summary>
		/// Attaches the tag with the specified id to the message with the specified id.
		/// </summary>
		/// <param name="tagId">ID of the tag attach.</param>
		/// <param name="messageId">ID of the message to attach the tag to.</param>
		protected void AttachTagToMessage(long tagId, long messageId)
		{
			mInsertTagToMessageMappingCommand_TagIdParameter.Value = tagId;
			mInsertTagToMessageMappingCommand_MessageIdParameter.Value = messageId;
			ExecuteNonQueryCommand(mInsertTagToMessageMappingCommand);
		}

		/// <summary>
		/// Removes tag associations for messages up to the specified id (including the message with the specified id).
		/// </summary>
		/// <param name="messageId">ID of the message to remove tags up to.</param>
		protected void RemoveTagAssociations(long messageId)
		{
			mDeleteTagToMessageMappingsUpToIdCommand_MessageIdParameter.Value = messageId;
			ExecuteNonQueryCommand(mDeleteTagToMessageMappingsUpToIdCommand);
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Prepares the specified sqlite command.
		/// </summary>
		/// <param name="commandText">The command text.</param>
		/// <returns>The prepared command.</returns>
		public SQLiteCommand PrepareCommand(string commandText)
		{
			var command = new SQLiteCommand(commandText, Connection);

			// command.Prepare(); // commands are automatically prepared as they are used the first time and kept in prepared state
			mCommands.Add(command);
			return command;
		}

		/// <summary>
		/// Releases the specified command that has been prepared using <see cref="PrepareCommand"/> to release resources associated
		/// with this command.
		/// </summary>
		/// <param name="command">The command to release.</param>
		public void ReleasePreparedCommand(ref SQLiteCommand command)
		{
			if (command == null) return;
			command.Dispose();
			mCommands.Remove(command);
			command = null;
		}

		/// <summary>
		/// Executes the specified action in a sqlite transaction.
		/// </summary>
		/// <param name="action">Action to execute.</param>
		public void ExecuteInTransaction(Action action)
		{
			// begin transaction
			ExecuteNonQueryCommand(mBeginTransactionCommand);

			try
			{
				// execute the action
				action();

				// commit changes to the database
				ExecuteNonQueryCommand(mCommitTransactionCommand);

				// the database has successfully committed changes
				// => commit changes to cache dictionaries as well
				mProcessNameToId.Commit();
				mApplicationNameToId.Commit();
				mLogWriterNameToId.Commit();
				mLogLevelNameToId.Commit();
				mTagToId.Commit();
			}
			catch
			{
				// discard changes to cache dictionaries
				mProcessNameToId.Discard();
				mApplicationNameToId.Discard();
				mLogWriterNameToId.Discard();
				mLogLevelNameToId.Discard();
				mTagToId.Discard();

				// roll back changes to the database
				if (mCanRollback) ExecuteNonQueryCommand(mRollbackTransactionCommand);

				throw;
			}
		}

		/// <summary>
		/// Executes the specified sqlite command and returns the first column and the first row of the result set,
		/// or <see langword="null"/>, if the result set is empty.
		/// </summary>
		/// <param name="connection">Connection to use.</param>
		/// <param name="commandText">Command text to execute.</param>
		/// <returns>
		/// The result of the query;<br/>
		/// <see langword="null"/> if the result was empty.
		/// </returns>
		public static object ExecuteScalarCommand(SQLiteConnection connection, string commandText)
		{
			using var command = new SQLiteCommand(commandText, connection);
			return command.ExecuteScalar();
		}

		/// <summary>
		/// Executes the specified sqlite non-query command (expecting no result).
		/// </summary>
		/// <param name="connection">Connection to use.</param>
		/// <param name="commandText">Command text to execute.</param>
		public static void ExecuteNonQueryCommand(SQLiteConnection connection, string commandText)
		{
			using var command = new SQLiteCommand(commandText, connection);
			command.ExecuteNonQuery();
		}

		/// <summary>
		/// Executes the specified prepared sqlite command and returns the first column and the first row of the result set,
		/// or <see langword="null"/>, if the result set is empty.
		/// </summary>
		/// <param name="command">Command to execute.</param>
		/// <returns>
		/// The result of the query;<br/>
		/// <see langword="null"/> if the result was empty.
		/// </returns>
		public static object ExecuteScalarCommand(SQLiteCommand command)
		{
			command.Reset();
			return command.ExecuteScalar();
		}

		/// <summary>
		/// Executes the specified prepared sqlite non-query command (expecting no result).
		/// </summary>
		/// <param name="command">Command to execute.</param>
		public static void ExecuteNonQueryCommand(SQLiteCommand command)
		{
			command.Reset();
			command.ExecuteNonQuery();
		}

		/// <summary>
		/// Executes the specified sqlite commands (expecting no result).
		/// </summary>
		/// <param name="commandTexts">Command texts to execute.</param>
		public void ExecuteNonQueryCommands(params string[] commandTexts)
		{
			foreach (string commandText in commandTexts)
			{
				using var command = new SQLiteCommand(commandText, Connection);
				command.ExecuteNonQuery();
			}
		}

		/// <summary>
		/// Gets the result set of the specified command, expecting a single string field only.
		/// </summary>
		/// <returns>A list of strings.</returns>
		public string[] ExecuteSingleColumnStringQuery(SQLiteCommand command, bool pool)
		{
			var list = new List<string>();

			command.Reset();
			using (SQLiteDataReader reader = command.ExecuteReader())
			{
				while (reader.Read())
				{
					string value = reader.GetString(0);
					if (pool) value = StringPool.Intern(value);
					list.Add(value);
				}
			}

			return list.ToArray();
		}

		/// <summary>
		/// Gets the result set of the specified command, expecting a single integer field only.
		/// </summary>
		/// <returns>A list of integers.</returns>
		public static int[] ExecuteSingleColumnIntQuery(SQLiteCommand command)
		{
			var list = new List<int>();

			command.Reset();
			using (SQLiteDataReader reader = command.ExecuteReader())
			{
				while (reader.Read())
				{
					int value = reader.GetInt32(0);
					list.Add(value);
				}
			}

			return list.ToArray();
		}

		/// <summary>
		/// Checks whether the log file is read-only and throws an exception, if so.
		/// </summary>
		/// <exception cref="NotSupportedException">The log file is read-only.</exception>
		protected void CheckReadOnly()
		{
			if (IsReadOnly) throw new NotSupportedException("The log file is read-only.");
		}

		#endregion
	}
}
