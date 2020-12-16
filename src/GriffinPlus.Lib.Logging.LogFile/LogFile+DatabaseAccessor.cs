///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;

namespace GriffinPlus.Lib.Logging
{

	partial class LogFile
	{
		/// <summary>
		/// Base class for the 'recording' file format and 'analysis' file format.
		/// </summary>
		abstract class DatabaseAccessor : IDisposable
		{
			private static bool sIsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

			protected readonly StringPool mStringPool = new StringPool();
			private            bool       mDisposed;
			private readonly   bool       mCanRollback;

			// dictionaries caching mappings from names to corresponding ids used to reference these names
			private readonly Dictionary<string, long> mProcessNameToId     = new Dictionary<string, long>();
			private readonly Dictionary<string, long> mApplicationNameToId = new Dictionary<string, long>();
			private readonly Dictionary<string, long> mLogWriterNameToId   = new Dictionary<string, long>();
			private readonly Dictionary<string, long> mLogLevelNameToId    = new Dictionary<string, long>();

			// sqlite specific commands
			private readonly SQLiteConnection    mConnection;
			private readonly List<SQLiteCommand> mCommands;
			private readonly SQLiteCommand       mBeginTransactionCommand;
			private readonly SQLiteCommand       mCommitTransactionCommand;
			private readonly SQLiteCommand       mRollbackTransactionCommand;
			private readonly SQLiteCommand       mVacuumCommand;
			private readonly SQLiteCommand       mVacuumIntoCommand;
			private readonly SQLiteParameter     mVacuumIntoCommand_FileParameter;
			private readonly SQLiteCommand       mSelectAllProcessNamesCommand;
			private readonly SQLiteCommand       mInsertProcessNameCommand;
			private readonly SQLiteParameter     mInsertProcessNameCommand_NameParameter;
			private readonly SQLiteCommand       mSelectProcessNameIdCommand;
			private readonly SQLiteParameter     mSelectProcessNameIdCommand_NameParameter;
			private readonly SQLiteCommand       mSelectAllApplicationNamesCommand;
			private readonly SQLiteCommand       mInsertApplicationNameCommand;
			private readonly SQLiteParameter     mInsertApplicationNameCommand_NameParameter;
			private readonly SQLiteCommand       mSelectApplicationNameIdCommand;
			private readonly SQLiteParameter     mSelectApplicationNameIdCommand_NameParameter;
			private readonly SQLiteCommand       mSelectAllLogWriterNamesCommand;
			private readonly SQLiteCommand       mInsertLogWriterNameCommand;
			private readonly SQLiteParameter     mInsertLogWriterNameCommand_NameParameter;
			private readonly SQLiteCommand       mSelectLogWriterIdCommand;
			private readonly SQLiteParameter     mSelectLogWriterIdCommand_NameParameter;
			private readonly SQLiteCommand       mSelectAllLogLevelNamesCommand;
			private readonly SQLiteCommand       mInsertLogLevelNameCommand;
			private readonly SQLiteParameter     mInsertLogLevelNameCommand_NameParameter;
			private readonly SQLiteCommand       mSelectLogLevelIdCommand;
			private readonly SQLiteParameter     mSelectLogLevelIdCommand_NameParameter;

			/// <summary>
			/// Application id used within a sqlite database to recognize it as a log file
			/// (GPLG = [G]riffin [P]lus [L]o[G])
			/// </summary>
			public const uint LogFileApplicationId = 0x47504C47;

			/// <summary>
			/// SQL commands that create the database structure common to all file formats.
			/// </summary>
			private static readonly string[] sCreateDatabaseCommands_CommonStructure =
			{
				$"PRAGMA application_id = {LogFileApplicationId};",
				"PRAGMA encoding = 'UTF-8';",
				"PRAGMA page_size = 65536;",
				"CREATE TABLE levels (id INTEGER PRIMARY KEY, name TEXT);",
				"CREATE TABLE writers (id INTEGER PRIMARY KEY, name TEXT);",
				"CREATE TABLE processes (id INTEGER PRIMARY KEY, name TEXT);",
				"CREATE TABLE applications (id INTEGER PRIMARY KEY, name TEXT);"
			};

			/// <summary>
			/// SQL commands that add common indices to the database.
			/// </summary>
			private static readonly string[] sCreateDatabaseCommands_CommonIndices =
			{
				"CREATE UNIQUE INDEX levels_index ON levels (name);",
				"CREATE UNIQUE INDEX writers_index ON writers (name);",
				"CREATE UNIQUE INDEX processes_index ON processes (name);",
				"CREATE UNIQUE INDEX applications_index ON applications (name);"
			};

			/// <summary>
			/// SQL commands that delete all data from all common tables.
			/// </summary>
			private static readonly string[] sDeleteEverythingCommands_CommonTables =
			{
				"DELETE FROM levels;",
				"DELETE FROM writers;",
				"DELETE FROM processes;",
				"DELETE FROM applications;"
			};

			/// <summary>
			/// Commands that are needed to configure the database to run in 'robust' mode.
			/// </summary>
			private static readonly string[] sSetRobustWriteModeCommands =
			{
				"PRAGMA synchronous = NORMAL;", // FULL is not needed for preserving consistency in WAL mode (see http://www.sqlite.org/pragma.html#pragma_synchronous)
				"PRAGMA journal_mode = WAL;",
				"PRAGMA temp_store = MEMORY;",
				"PRAGMA busy_timeout = 5000;",
				"PRAGMA locking_mode = EXCLUSIVE;",
				"BEGIN EXCLUSIVE;", // first access acquires the file lock exclusively as long as the connection is opened
				"COMMIT;"
			};

			/// <summary>
			/// Commands that are needed to configure the database to run in 'fast' mode.
			/// </summary>
			private static readonly string[] sSetFastWriteModeCommands =
			{
				"PRAGMA synchronous = OFF;",
				"PRAGMA journal_mode = OFF;",
				"PRAGMA temp_store = MEMORY;",
				"PRAGMA busy_timeout = 5000;",
				"PRAGMA locking_mode = EXCLUSIVE;",
				"BEGIN EXCLUSIVE;", // first access acquires the file lock exclusively as long as the connection is opened
				"COMMIT;"
			};

			/// <summary>
			/// Initializes the <see cref="DatabaseAccessor" /> class.
			/// </summary>
			static DatabaseAccessor()
			{
				// retrieve the version of the sqlite implementation
				using (var connection = new SQLiteConnection("Data Source=:memory:"))
				{
					connection.Open();
					using (var command = new SQLiteCommand("SELECT SQLITE_VERSION();", connection))
					{
						SqliteVersion = command.ExecuteScalar().ToString();
					}
				}
			}

			/// <summary>
			/// Initializes a new instance of the <see cref="DatabaseAccessor" /> class.
			/// </summary>
			/// <param name="connection">Database connection to use.</param>
			/// <param name="writeMode">Write mode that determines whether the database should be operating in robust mode or as fast as possible.</param>
			/// <param name="create">
			/// true to create the database;
			/// false to just use it.
			/// </param>
			protected DatabaseAccessor(
				SQLiteConnection connection,
				LogFileWriteMode writeMode,
				bool             create)
			{
				mConnection = connection;
				mCommands = new List<SQLiteCommand>();
				WriteMode = writeMode;

				// commands to begin, commit and roll back a transaction
				mBeginTransactionCommand = PrepareCommand("BEGIN IMMEDIATE TRANSACTION;");
				mCommitTransactionCommand = PrepareCommand("COMMIT TRANSACTION;");
				mRollbackTransactionCommand = PrepareCommand("ROLLBACK TRANSACTION;");

				// command to vacuum the database
				mVacuumCommand = PrepareCommand("VACUUM;");

				// command to vacuum the database and write the resulting database into another file
				mVacuumIntoCommand = PrepareCommand("VACUUM INTO @file;");
				mVacuumIntoCommand.Parameters.Add(mVacuumIntoCommand_FileParameter = new SQLiteParameter(DbType.String, "@file"));

				// command to get all process names in ascending order
				mSelectAllProcessNamesCommand = PrepareCommand("SELECT name FROM processes ORDER BY name ASC;");

				// command to add a process name (id is assigned automatically)
				mInsertProcessNameCommand = PrepareCommand("INSERT OR IGNORE INTO processes (name) VALUES (@name);");
				mInsertProcessNameCommand.Parameters.Add(mInsertProcessNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get the id of a process name
				mSelectProcessNameIdCommand = PrepareCommand("SELECT id FROM processes WHERE name = @name;");
				mSelectProcessNameIdCommand.Parameters.Add(mSelectProcessNameIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get all application names in ascending order
				mSelectAllApplicationNamesCommand = PrepareCommand("SELECT name FROM applications ORDER BY name ASC;");

				// command to add an application name (id is assigned automatically)
				mInsertApplicationNameCommand = PrepareCommand("INSERT OR IGNORE INTO applications (name) VALUES (@name);");
				mInsertApplicationNameCommand.Parameters.Add(mInsertApplicationNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get the id of an application name
				mSelectApplicationNameIdCommand = PrepareCommand("SELECT id FROM applications WHERE name = @name;");
				mSelectApplicationNameIdCommand.Parameters.Add(mSelectApplicationNameIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get all log writer names in ascending order
				mSelectAllLogWriterNamesCommand = PrepareCommand("SELECT name FROM writers ORDER BY name ASC;");

				// command to add a log writer name (id is assigned automatically)
				mInsertLogWriterNameCommand = PrepareCommand("INSERT OR IGNORE INTO writers (name) VALUES (@name);");
				mInsertLogWriterNameCommand.Parameters.Add(mInsertLogWriterNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get the id of a log writer name
				mSelectLogWriterIdCommand = PrepareCommand("SELECT id FROM writers WHERE name = @name;");
				mSelectLogWriterIdCommand.Parameters.Add(mSelectLogWriterIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get all log writer names in ascending order
				mSelectAllLogLevelNamesCommand = PrepareCommand("SELECT name FROM levels ORDER BY name ASC;");

				// command to add a log level name (id is assigned automatically)
				mInsertLogLevelNameCommand = PrepareCommand("INSERT OR IGNORE INTO levels (name) VALUES (@name);");
				mInsertLogLevelNameCommand.Parameters.Add(mInsertLogLevelNameCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// command to get the id of a log level name
				mSelectLogLevelIdCommand = PrepareCommand("SELECT id FROM levels WHERE name = @name;");
				mSelectLogLevelIdCommand.Parameters.Add(mSelectLogLevelIdCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

				// create common database tables and indices, if requested
				if (create)
				{
					ExecuteNonQueryCommands(sCreateDatabaseCommands_CommonStructure);
					ExecuteNonQueryCommands(sCreateDatabaseCommands_CommonIndices);
				}

				// configure mode of operation (robust or fast)
				switch (WriteMode)
				{
					case LogFileWriteMode.Robust:
						ExecuteNonQueryCommands(sSetRobustWriteModeCommands);
						mCanRollback = true; // robust mode uses a journal and can therefore roll back
						break;

					case LogFileWriteMode.Fast:
						ExecuteNonQueryCommands(sSetFastWriteModeCommands);
						mCanRollback = false; // fast mode does not use a journal and cannot roll back, behavior is undefined in these cases
						break;

					default:
						throw new NotSupportedException($"The specified write mode({WriteMode}) is not supported.");
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
					foreach (var command in mCommands) command.Dispose();
					mConnection?.Dispose();
					mDisposed = true;
				}
			}

			/// <summary>
			/// Gets the version of the sqlite implementation.
			/// </summary>
			/// <returns>Version of the sqlite implementation.</returns>
			public static string SqliteVersion { get; }

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

			#region Public Methods

			/// <summary>
			/// Gets the name of log writers that are/were associated with log messages.
			/// </summary>
			/// <param name="usedOnly">
			/// true to get the name of log writers that are referenced by messages in the log file only;
			/// false to get all log writer names (even if referencing log messages have been removed after clearing/pruning).
			/// </param>
			/// <returns>A list of log writer names.</returns>
			public string[] GetLogWriterNames(bool usedOnly)
			{
				return usedOnly
					       ? GetUsedLogWriterNames()
					       : ExecuteSingleColumnStringQuery(mSelectAllLogWriterNamesCommand);
			}

			/// <summary>
			/// Gets the name of log levels that are/were associated with log messages.
			/// </summary>
			/// <param name="usedOnly">
			/// true to get the name of log writers that are referenced by messages in the log file only;
			/// false to get all log writer names (even if referencing log messages have been removed after clearing/pruning).
			/// </param>
			/// <returns>A list of log level names.</returns>
			public string[] GetLogLevelNames(bool usedOnly)
			{
				return usedOnly
					       ? GetUsedLogLevelNames()
					       : ExecuteSingleColumnStringQuery(mSelectAllLogLevelNamesCommand);
			}

			/// <summary>
			/// Gets the name of processes that are/were associated with log messages.
			/// </summary>
			/// <param name="usedOnly">
			/// true to get the name of processes that are referenced by messages in the log file only;
			/// false to get all process names (even if referencing log messages have been removed after clearing/pruning).
			/// </param>
			/// <returns>A list of process names.</returns>
			public string[] GetProcessNames(bool usedOnly)
			{
				return usedOnly
					       ? GetUsedProcessNames()
					       : ExecuteSingleColumnStringQuery(mSelectAllProcessNamesCommand);
			}

			/// <summary>
			/// Gets the name of applications that are/were associated with log messages.
			/// </summary>
			/// <param name="usedOnly">
			/// true to get the name of applications that are referenced by messages in the log file only;
			/// false to get all application names (even if referencing log messages have been removed after clearing/pruning).
			/// </param>
			/// <returns>A list of application names.</returns>
			public string[] GetApplicationNames(bool usedOnly)
			{
				return usedOnly
					       ? GetUsedApplicationNames()
					       : ExecuteSingleColumnStringQuery(mSelectAllApplicationNamesCommand);
			}

			/// <summary>
			/// Removes all data from the log file.
			/// </summary>
			/// <param name="messagesOnly">
			/// true to remove messages only;
			/// false to remove processes, applications, log writers, log levels and tags as well.
			/// </param>
			public virtual void Clear(bool messagesOnly)
			{
				BeginTransaction();
				try
				{
					if (!messagesOnly) ExecuteNonQueryCommands(sDeleteEverythingCommands_CommonTables);
					ClearSpecific(messagesOnly);
					CommitTransaction();
				}
				catch
				{
					RollbackTransaction();
				}

				OldestMessageId = -1;
				NewestMessageId = -1;
			}

			/// <summary>
			/// Gets a number of log messages starting at the specified message id.
			/// </summary>
			/// <param name="fromId">Id of the message to start at.</param>
			/// <param name="count">Maximum number of log messages to get.</param>
			/// <returns>The requested log messages.</returns>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromId" /> is not in the interval [OldestMessageId,NewestMessageId].</exception>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> must be positive.</exception>
			public virtual LogMessage[] Read(long fromId, int count)
			{
				if (fromId < 0) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, "The log message id must be positive.");
				if (fromId < OldestMessageId || fromId > NewestMessageId) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, $"The log message id must be in the interval [{OldestMessageId},{NewestMessageId}].");
				if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "The number of log messages must be positive.");

				var messages = new List<LogMessage>(count);

				bool Callback(LogMessage message)
				{
					messages.Add(message);
					return true;
				}

				Read(fromId, count, Callback);

				return messages.ToArray();
			}

			/// <summary>
			/// Gets a number of log messages starting at the specified message id.
			/// </summary>
			/// <param name="fromId">Id of the message to start at.</param>
			/// <param name="count">Number of log messages to get.</param>
			/// <param name="callback">Callback to invoke for every read message</param>
			/// <returns>
			/// true, if reading ran to completion;
			/// false, if reading was cancelled.
			/// </returns>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromId" /> is not in the interval [OldestMessageId,NewestMessageId].</exception>
			/// <exception cref="ArgumentOutOfRangeException"><paramref name="count" /> must be positive.</exception>
			public abstract bool Read(long fromId, long count, ReadMessageCallback callback);

			/// <summary>
			/// Writes a log message into the log file.
			/// </summary>
			/// <param name="message">Log message to write.</param>
			public virtual void Write(ILogMessage message)
			{
				long messageId = NewestMessageId;

				BeginTransaction();
				try
				{
					WriteLogMessage(message, ++messageId);
					CommitTransaction();
				}
				catch
				{
					RollbackTransaction();
					throw;
				}

				if (OldestMessageId < 0) OldestMessageId = messageId;
				NewestMessageId = messageId;
			}

			/// <summary>
			/// Writes multiple log messages into the log file.
			/// </summary>
			/// <param name="messages">Log messages to write.</param>
			/// <returns>Number of messages written (should always be all messages).</returns>
			public virtual long Write(IEnumerable<ILogMessage> messages)
			{
				long count = 0;

				BeginTransaction();
				try
				{
					foreach (var message in messages)
					{
						WriteLogMessage(message, NewestMessageId + count + 1);
						count++;
					}

					CommitTransaction();
				}
				catch
				{
					RollbackTransaction();
					throw;
				}

				NewestMessageId = NewestMessageId + count;
				if (OldestMessageId < 0) OldestMessageId = NewestMessageId - count + 1;
				return count;
			}

			/// <summary>
			/// Removes log messages that are above the specified message limit -or- have a timestamp before the specified point in time.
			/// </summary>
			/// <param name="maximumMessageCount">
			/// Maximum number of messages to keep;
			/// -1 to disable removing messages by maximum message count.
			/// </param>
			/// <param name="minimumMessageTimestamp">
			/// Point in time (UTC) to keep messages after (includes the exact point in time);
			/// <seealso cref="DateTime.MinValue" /> to disable removing messages by age.
			/// </param>
			public abstract void Prune(long maximumMessageCount, DateTime minimumMessageTimestamp);

			/// <summary>
			/// Gets the application id stored in the sqlite database ('PRAGMA application_id').
			/// </summary>
			/// <param name="connection">Connection to use.</param>
			/// <returns>The application id.</returns>
			public static ulong GetApplicationId(SQLiteConnection connection)
			{
				return Convert.ToUInt64(ExecuteScalarCommand(connection, "PRAGMA application_id;"));
			}

			/// <summary>
			/// Gets the version of the database schema ('PRAGMA user_version').
			/// </summary>
			/// <param name="connection">Connection to use.</param>
			/// <returns>Version of the database schema.</returns>
			public static ulong GetSchemaVersion(SQLiteConnection connection)
			{
				return Convert.ToUInt64(ExecuteScalarCommand(connection, "PRAGMA user_version;"));
			}

			/// <summary>
			/// Compacts the database file.
			/// </summary>
			public void Vacuum()
			{
				ExecuteNonQueryCommand(mVacuumCommand);
			}

			/// <summary>
			/// Saves a snapshot of the log file
			/// (uses 'VACUUM INTO' that shrinks the database, cannot be cancelled).
			/// </summary>
			/// <param name="path">Path of the file to save the snapshot to.</param>
			public void SaveSnapshot(string path)
			{
				// determine the full path and convert it to an URI
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
			/// (may be null, the callback may be called multiple times with the same progress, if database locking issues occur).
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
					int pageSize = GetPageSize(mConnection);
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
					int maxCopySliceSize = 1 * 1024 * 1024;
					int pagesToCopyAtOnce = (int)((double)(maxCopySliceSize + pageSize - 1) / pageSize);

					// run the backup
					mConnection.BackupDatabase(
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
			/// Begins a transaction.
			/// </summary>
			protected void BeginTransaction()
			{
				ExecuteNonQueryCommand(mBeginTransactionCommand);
			}

			/// <summary>
			/// Commits a transaction.
			/// </summary>
			protected void CommitTransaction()
			{
				ExecuteNonQueryCommand(mCommitTransactionCommand);
			}

			/// <summary>
			/// Rolls the running transaction back.
			/// </summary>
			protected void RollbackTransaction()
			{
				if (mCanRollback) ExecuteNonQueryCommand(mRollbackTransactionCommand);
			}

			/// <summary>
			/// Gets the name of log writers that are associated with log messages.
			/// </summary>
			/// <returns>A list of log writer names.</returns>
			protected abstract string[] GetUsedLogWriterNames();

			/// <summary>
			/// Gets the name of log levels that are associated with log messages.
			/// </summary>
			/// <returns>A list of log level names.</returns>
			protected abstract string[] GetUsedLogLevelNames();

			/// <summary>
			/// Gets the name of processes that are associated with log messages.
			/// </summary>
			/// <returns>A list of process names.</returns>
			protected abstract string[] GetUsedProcessNames();

			/// <summary>
			/// Gets the name of applications that are associated with log messages.
			/// </summary>
			/// <returns>A list of application names.</returns>
			protected abstract string[] GetUsedApplicationNames();

			/// <summary>
			/// Removes all schema specific data from the log file.
			/// </summary>
			/// <param name="messagesOnly">
			/// true to remove messages only;
			/// false to remove processes, applications, log writers, log levels and tags as well.
			/// </param>
			protected abstract void ClearSpecific(bool messagesOnly);

			/// <summary>
			/// Writes a single log message (must run in transaction for consistency).
			/// </summary>
			/// <param name="message">Message to write.</param>
			/// <param name="messageId">Id of the message in the log file.</param>
			protected abstract void WriteLogMessage(ILogMessage message, long messageId);

			/// <summary>
			/// Adds the specified process name to the database.
			/// </summary>
			/// <param name="name">Name of the process to add.</param>
			/// <returns>Id associated with the process name.</returns>
			/// <exception cref="ArgumentNullException">The <paramref name="name" /> parameter must not be <c>null</c>.</exception>
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
			/// <returns>Id associated with the application name.</returns>
			/// <exception cref="ArgumentNullException">The <paramref name="name" /> parameter must not be <c>null</c>.</exception>
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
			/// <returns>Id associated with the log writer name.</returns>
			/// <exception cref="ArgumentNullException">The <paramref name="name" /> parameter must not be <c>null</c>.</exception>
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
			/// <returns>Id associated with the log level name.</returns>
			/// <exception cref="ArgumentNullException">The <paramref name="name" /> parameter must not be <c>null</c>.</exception>
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

			#endregion

			#region Helpers

			/// <summary>
			/// Prepares the specified sqlite command.
			/// </summary>
			/// <param name="commandText">The command text.</param>
			/// <returns>The prepared command.</returns>
			protected SQLiteCommand PrepareCommand(string commandText)
			{
				var command = new SQLiteCommand(commandText, mConnection);

				// command.Prepare(); // commands are automatically prepared as they are used the first time and kept in prepared state
				mCommands.Add(command);
				return command;
			}

			/// <summary>
			/// Executes the specified sqlite command and returns the first column and the first row of the result set,
			/// or <c>null</c>, if the result set is empty.
			/// </summary>
			/// <param name="connection">Connection to use.</param>
			/// <param name="commandText">Command text to execute.</param>
			/// <returns>
			/// The result of the query;
			/// null, if the result was empty.
			/// </returns>
			private static object ExecuteScalarCommand(SQLiteConnection connection, string commandText)
			{
				using (var command = new SQLiteCommand(commandText, connection))
				{
					return command.ExecuteScalar();
				}
			}

			/// <summary>
			/// Executes the specified sqlite non-query command (expecting no result).
			/// </summary>
			/// <param name="connection">Connection to use.</param>
			/// <param name="commandText">Command text to execute.</param>
			private static void ExecuteNonQueryCommand(SQLiteConnection connection, string commandText)
			{
				using (var command = new SQLiteCommand(commandText, connection))
				{
					command.ExecuteNonQuery();
				}
			}

			/// <summary>
			/// Executes the specified prepared sqlite command and returns the first column and the first row of the result set,
			/// or <c>null</c>, if the result set is empty.
			/// </summary>
			/// <param name="command">Command to execute.</param>
			/// <returns>
			/// The result of the query;
			/// null, if the result was empty.
			/// </returns>
			protected object ExecuteScalarCommand(SQLiteCommand command)
			{
				command.Reset();
				return command.ExecuteScalar();
			}

			/// <summary>
			/// Executes the specified prepared sqlite non-query command (expecting no result).
			/// </summary>
			/// <param name="command">Command to execute.</param>
			protected void ExecuteNonQueryCommand(SQLiteCommand command)
			{
				command.Reset();
				command.ExecuteNonQuery();
			}

			/// <summary>
			/// Executes the specified sqlite commands (expecting no result).
			/// </summary>
			/// <param name="commandTexts">Command texts to execute.</param>
			protected void ExecuteNonQueryCommands(params string[] commandTexts)
			{
				foreach (string commandText in commandTexts)
				{
					using (var command = new SQLiteCommand(commandText, mConnection))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			/// <summary>
			/// Gets the name of all applications that are/were associated with log messages
			/// (may contain application names that are not used by log messages any more after clearing/pruning).
			/// </summary>
			/// <returns>A list of application names.</returns>
			protected string[] ExecuteSingleColumnStringQuery(SQLiteCommand command)
			{
				var list = new List<string>();

				command.Reset();
				using (var reader = command.ExecuteReader())
				{
					while (reader.Read())
					{
						list.Add(reader.GetString(0));
					}
				}

				return list.ToArray();
			}

			#endregion
		}
	}

}
