///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

namespace GriffinPlus.Lib.Logging;

partial class LogFile
{
	/// <summary>
	/// The database accessor for the 'recording' file format.
	/// The format is optimized for throughput of written log messages, not for analysis.
	/// </summary>
	private sealed class RecordingDatabaseAccessor : DatabaseAccessor
	{
		private readonly SQLiteCommand   mGetOldestMessageIdCommand;
		private readonly SQLiteCommand   mGetNewestMessageIdCommand;
		private readonly SQLiteCommand   mSelectUsedProcessNamesCommand;
		private readonly SQLiteCommand   mSelectUsedProcessIdsCommand;
		private readonly SQLiteCommand   mSelectUsedApplicationNamesCommand;
		private readonly SQLiteCommand   mSelectUsedLogWriterNamesCommand;
		private readonly SQLiteCommand   mSelectUsedLogLevelNamesCommand;
		private readonly SQLiteCommand   mSelectUsedTagsCommand;
		private readonly SQLiteCommand   mInsertMessageCommand;
		private readonly SQLiteParameter mInsertMessageCommand_IdParameter;
		private readonly SQLiteParameter mInsertMessageCommand_TimestampParameter;
		private readonly SQLiteParameter mInsertMessageCommand_TimezoneOffsetParameter;
		private readonly SQLiteParameter mInsertMessageCommand_HighPrecisionTimestampParameter;
		private readonly SQLiteParameter mInsertMessageCommand_LostMessageCountParameter;
		private readonly SQLiteParameter mInsertMessageCommand_ProcessIdParameter;
		private readonly SQLiteParameter mInsertMessageCommand_ProcessNameIdParameter;
		private readonly SQLiteParameter mInsertMessageCommand_ApplicationNameIdParameter;
		private readonly SQLiteParameter mInsertMessageCommand_WriterNameIdParameter;
		private readonly SQLiteParameter mInsertMessageCommand_LevelNameIdParameter;
		private readonly SQLiteParameter mInsertMessageCommand_HasTagsParameter;
		private readonly SQLiteParameter mInsertMessageCommand_TextParameter;
		private readonly SQLiteCommand   mSelectMessageIdByTimestampForDeleteMessagesCommand;
		private readonly SQLiteParameter mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter;
		private readonly SQLiteCommand   mDeleteMessagesUpToIdCommand;
		private readonly SQLiteParameter mDeleteMessagesUpToIdCommand_IdParameter;
		private readonly SQLiteCommand   mSelectContinuousMessagesCommand;
		private readonly SQLiteParameter mSelectContinuousMessagesCommand_FromIdParameter;
		private readonly SQLiteParameter mSelectContinuousMessagesCommand_CountParameter;

		/// <summary>
		/// SQL commands that create the database structure specific for the 'recording' format.
		/// </summary>
		private static readonly string[] sCreateDatabaseCommands_SpecificStructure =
		[
			"PRAGMA user_version = 1;", // <- 'recording' format
			"CREATE TABLE messages (id INTEGER PRIMARY KEY, timestamp INTEGER, timezone_offset INTEGER, high_precision_timestamp INTEGER, lost_message_count INTEGER, process_id INTEGER, process_name_id INTEGER, application_name_id INTEGER, writer_name_id INTEGER, level_name_id INTEGER, has_tags BOOLEAN, text TEXT);"
		];

		/// <summary>
		/// SQL commands that add indices specific for the 'recording' format to a database.
		/// </summary>
		private static readonly string[] sCreateDatabaseCommands_SpecificIndices =
		[
			"CREATE INDEX messages_timestamp_index ON messages (timestamp);"
		];

		/// <summary>
		/// SQL commands that drop the specific database structure for the 'recording' format.
		/// </summary>
		private static readonly string[] sDropIndicesAndTables_Specific =
		[
			"DROP TABLE messages;"
		];

		/// <summary>
		/// Initializes a new instance of the <see cref="RecordingDatabaseAccessor"/> class.
		/// </summary>
		/// <param name="connection">Database connection to use.</param>
		/// <param name="writeMode">Write mode that determines whether the database should be operating in robust mode or as fast as possible.</param>
		/// <param name="isReadOnly">
		/// <see langword="true"/> if the log file is opened in read-only mode;<br/>
		/// <see langword="false"/> if the log file is opened in read/write mode.
		/// </param>
		/// <param name="create">
		/// <see langword="true"/> to create the database;<br/>
		/// <see langword="false"/> to just use it.
		/// </param>
		/// <param name="messages">Messages to populate the file with (works for new files only).</param>
		public RecordingDatabaseAccessor(
			SQLiteConnection         connection,
			LogFileWriteMode         writeMode,
			bool                     isReadOnly,
			bool                     create,
			IEnumerable<ILogMessage> messages = null) : base(connection, writeMode, isReadOnly)
		{
			// commands to get the lowest and the highest message id
			mGetOldestMessageIdCommand = PrepareCommand("SELECT id FROM messages ORDER BY id ASC  LIMIT 1;");
			mGetNewestMessageIdCommand = PrepareCommand("SELECT id FROM messages ORDER BY id DESC LIMIT 1;");

			// command to get names of referenced processes, applications, log writers, log levels and tags in ascending order
			mSelectUsedProcessNamesCommand = PrepareCommand(
				"SELECT DISTINCT name" +
				" FROM processes" +
				" INNER JOIN messages ON messages.process_name_id = processes.id" +
				" ORDER BY name ASC;");
			mSelectUsedProcessIdsCommand = PrepareCommand("SELECT DISTINCT process_id FROM messages ORDER BY process_id ASC;");
			mSelectUsedApplicationNamesCommand = PrepareCommand(
				"SELECT DISTINCT name" +
				" FROM applications" +
				" INNER JOIN messages ON messages.application_name_id = applications.id" +
				" ORDER BY name ASC;");
			mSelectUsedLogWriterNamesCommand = PrepareCommand(
				"SELECT DISTINCT name" +
				" FROM writers" +
				" INNER JOIN messages ON messages.writer_name_id = writers.id" +
				" ORDER BY name ASC;");
			mSelectUsedLogLevelNamesCommand = PrepareCommand(
				"SELECT DISTINCT name" +
				" FROM levels" +
				" INNER JOIN messages ON messages.level_name_id = levels.id" +
				" ORDER BY name ASC;");
			mSelectUsedTagsCommand = PrepareCommand(
				"SELECT DISTINCT name" +
				" FROM tags" +
				" INNER JOIN tag2msg ON tag2msg.tag_id = tags.id" +
				" INNER JOIN messages ON messages.id = tag2msg.message_id" +
				" ORDER BY name ASC;");

			// command to add a log message
			mInsertMessageCommand = PrepareCommand(
				"INSERT INTO messages (id, timestamp, timezone_offset, high_precision_timestamp, lost_message_count, process_id, process_name_id, application_name_id, writer_name_id, level_name_id, has_tags, text)" +
				" VALUES (@id, @timestamp, @timezone_offset, @high_precision_timestamp, @lost_message_count, @process_id, @process_name_id, @application_name_id, @writer_name_id, @level_name_id, @has_tags, @text);");
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_IdParameter = new SQLiteParameter("@id", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_TimestampParameter = new SQLiteParameter("@timestamp", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_TimezoneOffsetParameter = new SQLiteParameter("@timezone_offset", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_HighPrecisionTimestampParameter = new SQLiteParameter("@high_precision_timestamp", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_LostMessageCountParameter = new SQLiteParameter("@lost_message_count", DbType.Int32));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_ProcessIdParameter = new SQLiteParameter("@process_id", DbType.Int32));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_ProcessNameIdParameter = new SQLiteParameter("@process_name_id", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_ApplicationNameIdParameter = new SQLiteParameter("@application_name_id", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_WriterNameIdParameter = new SQLiteParameter("@writer_name_id", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_LevelNameIdParameter = new SQLiteParameter("@level_name_id", DbType.Int64));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_HasTagsParameter = new SQLiteParameter("@has_tags", DbType.Boolean));
			mInsertMessageCommand.Parameters.Add(mInsertMessageCommand_TextParameter = new SQLiteParameter("@text", DbType.String));

			// query to get the id of the first message that is next a specific point in time
			mSelectMessageIdByTimestampForDeleteMessagesCommand = PrepareCommand("SELECT id FROM messages WHERE timestamp < @timestamp ORDER BY timestamp DESC LIMIT 1;");
			mSelectMessageIdByTimestampForDeleteMessagesCommand.Parameters.Add(mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter = new SQLiteParameter("@timestamp", DbType.Int64));

			// command to delete all messages up to the specified id (incl. the message with the specified id)
			mDeleteMessagesUpToIdCommand = PrepareCommand("DELETE FROM messages WHERE id <= @id;");
			mDeleteMessagesUpToIdCommand.Parameters.Add(mDeleteMessagesUpToIdCommand_IdParameter = new SQLiteParameter("@id", DbType.Int64));

			// query to get a number of log messages starting at a specific log message id
			mSelectContinuousMessagesCommand = PrepareCommand(
				"SELECT m.id, timestamp, m.timezone_offset, m.high_precision_timestamp, m.lost_message_count, m.process_id, p.name, a.name, w.name, l.name, m.has_tags, m.text" +
				" FROM messages as m" +
				" INNER JOIN processes as p ON p.id = m.process_name_id" +
				" INNER JOIN applications as a ON a.id = m.application_name_id" +
				" INNER JOIN writers as w ON w.id = m.writer_name_id" +
				" INNER JOIN levels as l ON l.id = m.level_name_id" +
				" WHERE m.id >= @from_id" +
				" ORDER BY m.id ASC" +
				" LIMIT @count;");
			mSelectContinuousMessagesCommand.Parameters.Add(mSelectContinuousMessagesCommand_FromIdParameter = new SQLiteParameter("@from_id", DbType.Int64));
			mSelectContinuousMessagesCommand.Parameters.Add(mSelectContinuousMessagesCommand_CountParameter = new SQLiteParameter("@count", DbType.Int64));

			// create database tables and indices, if requested
			if (create)
			{
				// create structure first
				ExecuteNonQueryCommands(CreateDatabaseCommands_CommonStructure);
				ExecuteNonQueryCommands(sCreateDatabaseCommands_SpecificStructure);

				// retrieve the ids of the oldest and newest message
				OldestMessageId = GetOldestMessageId();
				NewestMessageId = GetNewestMessageId();

				// populate the database with messages
				if (messages != null) Write(messages);

				// create indices at last (is much faster than updating indices when inserting)
				ExecuteNonQueryCommands(CreateDatabaseCommands_CommonIndices);
				ExecuteNonQueryCommands(sCreateDatabaseCommands_SpecificIndices);
			}
			else
			{
				// retrieve the ids of the oldest and newest message
				OldestMessageId = GetOldestMessageId();
				NewestMessageId = GetNewestMessageId();
			}
		}

		/// <summary>
		/// Gets the purpose the log format is used for.
		/// </summary>
		public override LogFilePurpose Purpose => LogFilePurpose.Recording;

		/// <summary>
		/// Gets the id of the oldest message.
		/// </summary>
		/// <returns>
		/// ID of the oldest message;<br/>
		/// -1, if the database is empty.
		/// </returns>
		private long GetOldestMessageId()
		{
			object result = ExecuteScalarCommand(mGetOldestMessageIdCommand);
			return result != null ? Convert.ToInt64(result) : -1;
		}

		/// <summary>
		/// Gets the id of the newest message.
		/// </summary>
		/// <returns>
		/// ID of the newest message;<br/>
		/// -1, if the database is empty.
		/// </returns>
		private long GetNewestMessageId()
		{
			object result = ExecuteScalarCommand(mGetNewestMessageIdCommand);
			return result != null ? Convert.ToInt64(result) : -1;
		}

		/// <summary>
		/// Gets the name of processes that are associated with log messages.
		/// </summary>
		/// <returns>A list of process names.</returns>
		protected override string[] GetUsedProcessNames()
		{
			return ExecuteSingleColumnStringQuery(mSelectUsedProcessNamesCommand, true);
		}

		/// <summary>
		/// Gets the ids of processes that are associated with log messages.
		/// </summary>
		/// <returns>A list of process ids.</returns>
		public override int[] GetProcessIds()
		{
			return ExecuteSingleColumnIntQuery(mSelectUsedProcessIdsCommand);
		}

		/// <summary>
		/// Gets the name of applications that are associated with log messages.
		/// </summary>
		/// <returns>A list of application names.</returns>
		protected override string[] GetUsedApplicationNames()
		{
			return ExecuteSingleColumnStringQuery(mSelectUsedApplicationNamesCommand, true);
		}

		/// <summary>
		/// Gets the name of log writers that are associated with log messages.
		/// </summary>
		/// <returns>A list of log writer names.</returns>
		protected override string[] GetUsedLogWriterNames()
		{
			return ExecuteSingleColumnStringQuery(mSelectUsedLogWriterNamesCommand, true);
		}

		/// <summary>
		/// Gets the name of log levels that are associated with log messages.
		/// </summary>
		/// <returns>A list of log level names.</returns>
		protected override string[] GetUsedLogLevelNames()
		{
			return ExecuteSingleColumnStringQuery(mSelectUsedLogLevelNamesCommand, true);
		}

		/// <summary>
		/// Gets the tags that are associated with log messages.
		/// </summary>
		/// <returns>A list of tags.</returns>
		protected override string[] GetUsedTags()
		{
			return ExecuteSingleColumnStringQuery(mSelectUsedTagsCommand, true);
		}

		/// <summary>
		/// Removes all schema specific data from the log file.
		/// </summary>
		/// <param name="messagesOnly">
		/// <see langword="true"/> to remove messages only;<br/>
		/// <see langword="false"/> to remove processes, applications, log writers, log levels and tags as well.
		/// </param>
		protected override void ClearSpecific(bool messagesOnly)
		{
			ExecuteNonQueryCommands(sDropIndicesAndTables_Specific);
			ExecuteNonQueryCommands(sCreateDatabaseCommands_SpecificStructure);
			ExecuteNonQueryCommands(sCreateDatabaseCommands_SpecificIndices);
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
		public override bool Read(long fromId, long count, ReadMessageCallback callback)
		{
			if (fromId < 0) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, "The log message id must be positive.");
			if (fromId < OldestMessageId || fromId > NewestMessageId) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, $"The log message id must be in the interval [{OldestMessageId},{NewestMessageId}].");
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), count, "The number of log messages must be positive.");

			mSelectContinuousMessagesCommand.Reset();
			mSelectContinuousMessagesCommand_FromIdParameter.Value = fromId;
			mSelectContinuousMessagesCommand_CountParameter.Value = count;
			using SQLiteDataReader reader = mSelectContinuousMessagesCommand.ExecuteReader();
			while (reader.Read())
			{
				// read message and invoke processing callback
				LogFileMessage message = ReadLogMessage(reader);
				if (!callback(message))
					return false;
			}

			return true;
		}

		/// <summary>
		/// Reads a log message from the specified sqlite reader.
		/// </summary>
		/// <param name="reader">Sqlite reader to read from.</param>
		/// <returns>The read log message (is protected).</returns>
		private LogFileMessage ReadLogMessage(IDataRecord reader)
		{
			// columns in result:
			// 0 = message id
			// 1 = timestamp
			// 2 = timezone offset
			// 3 = high precision timestamp
			// 4 = lost message count
			// 5 = process id
			// 6 = process name
			// 7 = application name
			// 8 = log writer name
			// 9 = log level name
			// 10 = has tags
			// 11 = text name

			long messageId = reader.GetInt64(0);
			TimeSpan timezoneOffset = TimeSpan.FromTicks(reader.GetInt64(2));
			var timestamp = new DateTimeOffset(reader.GetInt64(1) + timezoneOffset.Ticks, timezoneOffset);
			long highPrecisionTimestamp = reader.GetInt64(3);
			int lostMessageCount = reader.GetInt32(4);
			int processId = reader.GetInt32(5);
			string processName = reader.GetString(6);
			string applicationName = reader.GetString(7);
			string logWriterName = reader.GetString(8);
			string logLevelName = reader.GetString(9);
			bool hasTags = reader.GetBoolean(10);
			string text = reader.GetString(11);

			LogFileMessage message = new LogFileMessage().InitWith(
				messageId,
				timestamp,
				highPrecisionTimestamp,
				lostMessageCount,
				StringPool.Intern(logWriterName),
				StringPool.Intern(logLevelName),
				TagSet.Empty,
				StringPool.Intern(applicationName),
				StringPool.Intern(processName),
				processId,
				text);

			// initialize tags, if there are tags associated with the message
			if (hasTags) message.Tags = GetTagsOfMessage(messageId);

			// protect message from changes
			message.Protect();
			return message;
		}

		/// <summary>
		/// Writes a single log message (must run in a transaction for consistency).
		/// </summary>
		/// <param name="message">Message to write.</param>
		/// <param name="messageId">ID of the message in the log file.</param>
		protected override void WriteLogMessage(ILogMessage message, long messageId)
		{
			// insert common data
			long processNameId = AddProcessName(message.ProcessName);
			long applicationNameId = AddApplicationName(message.ApplicationName);
			long writerNameId = AddLogWriterName(message.LogWriterName);
			long levelNameId = AddLogLevelName(message.LogLevelName);

			// insert tags
			bool hasTags = false;
			if (message.Tags != null)
			{
				foreach (string tag in message.Tags)
				{
					long tagId = AddTag(tag);
					AttachTagToMessage(tagId, messageId);
					hasTags = true;
				}
			}

			// insert message
			mInsertMessageCommand_IdParameter.Value = messageId;
			mInsertMessageCommand_TimestampParameter.Value = message.Timestamp.UtcTicks;
			mInsertMessageCommand_TimezoneOffsetParameter.Value = message.Timestamp.Offset.Ticks;
			mInsertMessageCommand_HighPrecisionTimestampParameter.Value = message.HighPrecisionTimestamp;
			mInsertMessageCommand_LostMessageCountParameter.Value = message is LogMessage msg ? msg.LostMessageCount : 0;
			mInsertMessageCommand_ProcessIdParameter.Value = message.ProcessId;
			mInsertMessageCommand_ProcessNameIdParameter.Value = processNameId;
			mInsertMessageCommand_ApplicationNameIdParameter.Value = applicationNameId;
			mInsertMessageCommand_WriterNameIdParameter.Value = writerNameId;
			mInsertMessageCommand_LevelNameIdParameter.Value = levelNameId;
			mInsertMessageCommand_HasTagsParameter.Value = hasTags;
			mInsertMessageCommand_TextParameter.Value = message.Text;
			ExecuteNonQueryCommand(mInsertMessageCommand);
		}

		/// <summary>
		/// Represents the state and parameters required to perform a prune operation on a database.
		/// </summary>
		private readonly struct PruneOperationState1
		{
			public required long                      MaximumMessageCount     { get; init; }
			public required DateTime                  MinimumMessageTimestamp { get; init; }
			public required RecordingDatabaseAccessor Accessor                { get; init; }
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
		/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
		/// </param>
		/// <returns>
		/// Number of removed messages.
		/// If <see cref="int.MaxValue"/> is returned <see cref="Prune(long, DateTime)"/> should be called once again
		/// to ensure all messages matching the criteria are removed.
		/// </returns>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public override int Prune(long maximumMessageCount, DateTime minimumMessageTimestamp)
		{
			CheckReadOnly();

			// abort, if the database is empty
			if (OldestMessageId < 0)
				return 0;

			// execute the operation in a transaction
			var state = new PruneOperationState1
			{
				MaximumMessageCount = maximumMessageCount,
				MinimumMessageTimestamp = minimumMessageTimestamp,
				Accessor = this
			};
			int count = ExecuteInTransaction(Operation, state);

			// update the id of the oldest and the newest log message
			OldestMessageId = GetOldestMessageId();
			NewestMessageId = GetNewestMessageId();

			return count;

			static int Operation(PruneOperationState1 state)
			{
				long removeCount = 0;

				// determine the id of the first message older than the specified timestamp
				long deleteByTimestampMessageId = -1;
				if (state.MinimumMessageTimestamp > DateTime.MinValue)
				{
					state.Accessor.mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter.Value = state.MinimumMessageTimestamp.Ticks;
					object result = ExecuteScalarCommand(state.Accessor.mSelectMessageIdByTimestampForDeleteMessagesCommand);
					deleteByTimestampMessageId = result != null ? Convert.ToInt64(result) : -1;
				}

				// determine the id of the first message to delete due to the maximum message count limit
				// (determined row is included)
				long totalMessageCount = state.Accessor.NewestMessageId - state.Accessor.OldestMessageId + 1;
				long deleteByMaxMessageCountMessageId = -1;
				if (state.MaximumMessageCount >= 0)
				{
					long messagesToDeleteCount = Math.Max(totalMessageCount - state.MaximumMessageCount, 0);
					deleteByMaxMessageCountMessageId = messagesToDeleteCount > 0 ? state.Accessor.OldestMessageId + messagesToDeleteCount - 1 : -1;
				}

				// combine selection conditions
				long messageId = -1;
				if (deleteByTimestampMessageId >= 0) messageId = deleteByTimestampMessageId;
				if (deleteByMaxMessageCountMessageId >= 0) messageId = Math.Max(messageId, deleteByMaxMessageCountMessageId);

				// delete old messages and associated tags up to the determined message id
				// (including the message with the determined id)
				if (messageId >= 0)
				{
					// limit the number of messages to int.MaxValue to ensure that clients have a chance to put the messages into an array
					messageId = messageId - state.Accessor.OldestMessageId + 1 > int.MaxValue
						            ? state.Accessor.OldestMessageId + int.MaxValue - 1
						            : messageId;

					// calculate the number of messages to remove
					Debug.Assert(messageId - state.Accessor.OldestMessageId + 1 <= int.MaxValue);
					removeCount = (int)(messageId - state.Accessor.OldestMessageId + 1);

					// delete old messages
					state.Accessor.mDeleteMessagesUpToIdCommand_IdParameter.Value = messageId;
					ExecuteNonQueryCommand(state.Accessor.mDeleteMessagesUpToIdCommand);

					// remove tags associated with the messages
					state.Accessor.RemoveTagAssociations(messageId);
				}

				Debug.Assert(removeCount <= int.MaxValue);
				return (int)removeCount;
			}
		}

		/// <summary>
		/// Represents the state and associated commands required to perform a prune operation on a message database.
		/// </summary>
		private readonly struct PruneOperationState2
		{
			public required long                      MaximumMessageCount     { get; init; }
			public required DateTime                  MinimumMessageTimestamp { get; init; }
			public required RecordingDatabaseAccessor Accessor                { get; init; }
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
		/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
		/// </param>
		/// <param name="removedMessages">Receives the log messages that have been removed.</param>
		/// <returns>
		/// Number of removed messages.
		/// If <see cref="int.MaxValue"/> is returned <see cref="Prune(long, DateTime, out LogFileMessage[])"/> should
		/// be called once again to ensure all messages matching the criteria are removed.
		/// </returns>
		/// <exception cref="NotSupportedException">The file is read-only.</exception>
		public override int Prune(
			long                 maximumMessageCount,
			DateTime             minimumMessageTimestamp,
			out LogFileMessage[] removedMessages)
		{
			CheckReadOnly();

			// abort, if the database is empty
			if (OldestMessageId < 0)
			{
				removedMessages = [];
				return 0;
			}

			// execute the operation in a transaction
			var state = new PruneOperationState2
			{
				MaximumMessageCount = maximumMessageCount,
				MinimumMessageTimestamp = minimumMessageTimestamp,
				Accessor = this
			};

			// assign list of removed messages
			LogFileMessage[] messages = ExecuteInTransaction(Operation, state);

			// update the id of the oldest and the newest log message
			OldestMessageId = GetOldestMessageId();
			NewestMessageId = GetNewestMessageId();

			// assign list of removed messages
			removedMessages = messages;

			return removedMessages.Length;

			static LogFileMessage[] Operation(PruneOperationState2 state)
			{
				// determine the id of the first message older than the specified timestamp
				long deleteByTimestampMessageId = -1;
				if (state.MinimumMessageTimestamp > DateTime.MinValue)
				{
					state.Accessor.mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter.Value = state.MinimumMessageTimestamp.Ticks;
					object result = ExecuteScalarCommand(state.Accessor.mSelectMessageIdByTimestampForDeleteMessagesCommand);
					deleteByTimestampMessageId = result != null ? Convert.ToInt64(result) : -1;
				}

				// determine the id of the first message to delete due to the maximum message count limit
				// (determined row is included)
				long totalMessageCount = state.Accessor.NewestMessageId - state.Accessor.OldestMessageId + 1;
				long deleteByMaxMessageCountMessageId = -1;
				if (state.MaximumMessageCount >= 0)
				{
					long messagesToDeleteCount = Math.Max(totalMessageCount - state.MaximumMessageCount, 0);
					deleteByMaxMessageCountMessageId = messagesToDeleteCount > 0 ? state.Accessor.OldestMessageId + messagesToDeleteCount - 1 : -1;
				}

				// combine selection conditions
				long messageId = -1;
				if (deleteByTimestampMessageId >= 0) messageId = deleteByTimestampMessageId;
				if (deleteByMaxMessageCountMessageId >= 0) messageId = Math.Max(messageId, deleteByMaxMessageCountMessageId);

				List<LogFileMessage> messages = null;
				if (messageId >= 0)
				{
					// limit the number of messages to int.MaxValue to ensure that clients have a chance to put the messages into an array
					messageId = messageId - state.Accessor.OldestMessageId + 1 > int.MaxValue
						            ? state.Accessor.OldestMessageId + int.MaxValue - 1
						            : messageId;

					// calculate the number of messages to remove
					Debug.Assert(messageId - state.Accessor.OldestMessageId + 1 <= int.MaxValue);
					int removeCount = (int)(messageId - state.Accessor.OldestMessageId + 1);

					// read messages that are about to be removed
					messages = new List<LogFileMessage>(removeCount);
					state.Accessor.mSelectContinuousMessagesCommand.Reset();
					state.Accessor.mSelectContinuousMessagesCommand_FromIdParameter.Value = state.Accessor.OldestMessageId;
					state.Accessor.mSelectContinuousMessagesCommand_CountParameter.Value = removeCount;
					using (SQLiteDataReader reader = state.Accessor.mSelectContinuousMessagesCommand.ExecuteReader())
					{
						while (reader.Read())
						{
							messages.Add(state.Accessor.ReadLogMessage(reader));
						}
					}

					// delete old messages and associated tags up to the determined message id
					// (including the message with the determined id)

					// delete old messages
					state.Accessor.mDeleteMessagesUpToIdCommand_IdParameter.Value = messageId;
					ExecuteNonQueryCommand(state.Accessor.mDeleteMessagesUpToIdCommand);

					// remove tags associated with the messages
					state.Accessor.RemoveTagAssociations(messageId);
				}

				return messages?.ToArray() ?? [];
			}
		}
	}
}
