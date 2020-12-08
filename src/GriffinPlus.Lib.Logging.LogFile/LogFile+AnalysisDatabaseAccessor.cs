﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;

namespace GriffinPlus.Lib.Logging
{
	partial class LogFile
	{
		/// <summary>
		/// The database accessor for the 'analysis' file format.
		/// The format is optimized for analyzing log messages.
		/// </summary>
		class AnalysisDatabaseAccessor : DatabaseAccessor
		{
			private readonly SQLiteCommand   mGetOldestMessageIdCommand;
			private readonly SQLiteCommand   mGetNewestMessageIdCommand;
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
			private readonly SQLiteCommand   mInsertTextCommand;
			private readonly SQLiteParameter mInsertTextCommand_IdParameter;
			private readonly SQLiteParameter mInsertTextCommand_TextParameter;
			private readonly SQLiteCommand   mSelectMessageIdByTimestampForDeleteMessagesCommand;
			private readonly SQLiteParameter mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter;
			private readonly SQLiteCommand   mDeleteMessagesUpToIdCommand;
			private readonly SQLiteParameter mDeleteMessagesUpToIdCommand_IdParameter;
			private readonly SQLiteCommand   mDeleteTextsUpToIdCommand;
			private readonly SQLiteParameter mDeleteTextsUpToIdCommand_IdParameter;
			private readonly SQLiteCommand   mDeleteAllMessagesCommand;
			private readonly SQLiteCommand   mDeleteAllTextsCommand;
			private readonly SQLiteCommand   mSelectContinuousMessagesCommand;
			private readonly SQLiteParameter mSelectContinuousMessagesCommand_FromIdParameter;
			private readonly SQLiteParameter mSelectContinuousMessagesCommand_CountParameter;

			/// <summary>
			/// SQL commands that create the database structure specific for the 'analysis' format.
			/// </summary>
			private static readonly string[] sCreateDatabaseCommands_SpecificStructure =
			{
				"CREATE TABLE messages (id INTEGER PRIMARY KEY, timestamp INTEGER, timezone_offset INTEGER, high_precision_timestamp INTEGER, lost_message_count INTEGER, process_id INTEGER, process_name_id INTEGER, application_name_id INTEGER, writer_name_id INTEGER, level_name_id INTEGER);",
				"CREATE TABLE texts (id INTEGER PRIMARY KEY, text STRING);"
			};

			/// <summary>
			/// SQL commands that add indices specific for the 'recording' format to a database.
			/// </summary>
			private static readonly string[] sCreateDatabaseCommands_SpecificIndices =
			{
				"CREATE INDEX messages_timestamp_index ON messages (timestamp);",
				"CREATE INDEX messages_process_id_index ON messages (process_id);",
				"CREATE INDEX messages_process_name_id_index ON messages (process_name_id);",
				"CREATE INDEX messages_application_name_id_index ON messages (application_name_id);",
				"CREATE INDEX messages_writer_name_id_index ON messages (writer_name_id);",
				"CREATE INDEX messages_level_name_id_index ON messages (level_name_id);"
			};

			/// <summary>
			/// Initializes a new instance of the <see cref="AnalysisDatabaseAccessor"/> class.
			/// </summary>
			/// <param name="connection">Database connection to use.</param>
			/// <param name="writeMode">Write mode that determines whether the database should be operating in robust mode or as fast as possible.</param>
			/// <param name="create">
			/// true to create the database;
			/// false to just use it.
			/// </param>
			public AnalysisDatabaseAccessor(SQLiteConnection connection, LogFileWriteMode writeMode, bool create) :
				base(connection, writeMode, create)
			{
				// commands to get the lowest and the highest message id
				mGetOldestMessageIdCommand = PrepareCommand("SELECT id FROM messages ORDER BY id ASC  LIMIT 1;");
				mGetNewestMessageIdCommand = PrepareCommand("SELECT id FROM messages ORDER BY id DESC LIMIT 1;");

				// command to add a log message metadata record (everything, but the actual message text)
				mInsertMessageCommand = PrepareCommand(
					"INSERT INTO messages (id, timestamp, timezone_offset, high_precision_timestamp, lost_message_count, process_id, process_name_id, application_name_id, writer_name_id, level_name_id)" +
					" VALUES (@id, @timestamp, @timezone_offset, @high_precision_timestamp, @lost_message_count, @process_id, @process_name_id, @application_name_id, @writer_name_id, @level_name_id);");
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

				// command to add the text of a log message
				mInsertTextCommand = PrepareCommand("INSERT INTO texts (id, text) VALUES (@id, @text);");
				mInsertTextCommand.Parameters.Add(mInsertTextCommand_IdParameter = new SQLiteParameter("@id", DbType.Int64));
				mInsertTextCommand.Parameters.Add(mInsertTextCommand_TextParameter = new SQLiteParameter("@text", DbType.String));

				// query to get the id of the first message that is next a specific point in time
				mSelectMessageIdByTimestampForDeleteMessagesCommand = PrepareCommand("SELECT id FROM messages WHERE timestamp < @timestamp ORDER BY timestamp DESC LIMIT 1;");
				mSelectMessageIdByTimestampForDeleteMessagesCommand.Parameters.Add(mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter = new SQLiteParameter("@timestamp", DbType.Int64));

				// command to delete all message metadata up to the specified id (incl. the specified id)
				mDeleteMessagesUpToIdCommand = PrepareCommand("DELETE FROM messages WHERE id <= @id;");
				mDeleteMessagesUpToIdCommand.Parameters.Add(mDeleteMessagesUpToIdCommand_IdParameter = new SQLiteParameter("@id", DbType.Int64));

				// command to delete all message texts up to the specified id (incl. the specified id)
				mDeleteTextsUpToIdCommand = PrepareCommand("DELETE FROM texts WHERE id <= @id;");
				mDeleteTextsUpToIdCommand.Parameters.Add(mDeleteTextsUpToIdCommand_IdParameter = new SQLiteParameter("@id", DbType.Int64));

				// command to delete all message metadata
				mDeleteAllMessagesCommand = PrepareCommand("DELETE FROM messages;");

				// command to delete all message texts
				mDeleteAllTextsCommand = PrepareCommand("DELETE FROM texts;");

				// query to get a number of log messages starting at a specific log message id
				mSelectContinuousMessagesCommand = PrepareCommand(
					"SELECT m.id, timestamp, m.timezone_offset, m.high_precision_timestamp, m.lost_message_count, m.process_id, p.name, a.name, w.name, l.name, t.text" +
					" FROM messages as m, texts as t, processes as p, applications as a, writers as w, levels as l"                                                         +
					" WHERE m.id >= @from_id AND m.id == t.id AND m.process_name_id == p.id AND m.application_name_id == a.id AND m.writer_name_id == w.id AND m.level_name_id == l.id"         +
					" LIMIT @count;");
				mSelectContinuousMessagesCommand.Parameters.Add(mSelectContinuousMessagesCommand_FromIdParameter = new SQLiteParameter("@from_id", DbType.Int64));
				mSelectContinuousMessagesCommand.Parameters.Add(mSelectContinuousMessagesCommand_CountParameter = new SQLiteParameter("@count", DbType.Int64));

				// create database tables and indices, if requested
				if (create)
				{
					ExecuteNonQueryCommands(sCreateDatabaseCommands_SpecificStructure);
					ExecuteNonQueryCommands(sCreateDatabaseCommands_SpecificIndices);
				}

				// retrieve the ids of the oldest and newest message
				OldestMessageId = GetOldestMessageId();
				NewestMessageId = GetNewestMessageId();
			}

			/// <summary>
			/// Gets the purpose the log format is used for.
			/// </summary>
			public override LogFilePurpose Purpose => LogFilePurpose.Analysis;

			/// <summary>
			/// Gets the id of the oldest message.
			/// </summary>
			/// <returns>
			/// Id of the oldest message;
			/// -1, if the database is empty.
			/// </returns>
			private long GetOldestMessageId()
			{
				var result = ExecuteScalarCommand(mGetOldestMessageIdCommand);
				return result != null ? Convert.ToInt64(result) : -1;
			}

			/// <summary>
			/// Gets the id of the newest message.
			/// </summary>
			/// <returns>
			/// Id of the newest message;
			/// -1, if the database is empty.
			/// </returns>
			private long GetNewestMessageId()
			{
				var result = ExecuteScalarCommand(mGetNewestMessageIdCommand);
				return result != null ? Convert.ToInt64(result) : -1;
			}

			/// <summary>
			/// Removes all schema specific data from the log file.
			/// </summary>
			/// <param name="messagesOnly">
			/// true to remove messages only;
			/// false to remove processes, applications, log writers, log levels and tags as well.
			/// </param>
			protected override void ClearSpecific(bool messagesOnly)
			{
				ExecuteNonQueryCommand(mDeleteAllMessagesCommand);
				ExecuteNonQueryCommand(mDeleteAllTextsCommand);
				Debug.Assert(GetOldestMessageId() == -1);
				Debug.Assert(GetNewestMessageId() == -1);
			}

			/// <summary>
			/// Gets a number of log messages starting at the specified message id.
			/// </summary>
			/// <param name="fromId">Id of the message to start at.</param>
			/// <param name="count">Number of log messages to get.</param>
			/// <param name="callback">Callback to invoke for every read message</param>
			public override void Read(long fromId, long count, ReadMessageCallback callback)
			{
				if (fromId < 0) throw new ArgumentOutOfRangeException(nameof(fromId), fromId, "The log message id must be positive.");
				if (count  < 0) throw new ArgumentOutOfRangeException(nameof(count),  count,  "The number of log messages must be positive.");

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
				// 10 = text name
				mSelectContinuousMessagesCommand_FromIdParameter.Value = fromId;
				mSelectContinuousMessagesCommand_CountParameter.Value = count;
				using (var reader = mSelectContinuousMessagesCommand.ExecuteReader())
				{
					while (reader.Read())
					{
						var messageId              = reader.GetInt64(0);
						var timezoneOffset         = TimeSpan.FromTicks(reader.GetInt64(2));
						var timestamp              = new DateTimeOffset(reader.GetInt64(1) + timezoneOffset.Ticks, timezoneOffset);
						var highPrecisionTimestamp = reader.GetInt64(3);
						var lostMessageCount       = reader.GetInt32(4);
						var processId              = reader.GetInt32(5);
						var processName            = reader.GetString(6);
						var applicationName        = reader.GetString(7);
						var logWriterName          = reader.GetString(8);
						var logLevelName           = reader.GetString(9);
						var text                   = reader.GetString(10);

						LogMessage message = LogMessagePool.Default.GetMessage(
							messageId,
							timestamp,
							highPrecisionTimestamp,
							lostMessageCount,
							mStringPool.Intern(logWriterName),
							mStringPool.Intern(logLevelName),
							null, // tags
							mStringPool.Intern(applicationName),
							mStringPool.Intern(processName),
							processId,
							text);

						callback(message);
					}
				}
			}

			/// <summary>
			/// Writes a single log message (must run in a transaction for consistency).
			/// </summary>
			/// <param name="message">Message to write.</param>
			/// <param name="messageId">Id of the message in the log file.</param>
			protected override void WriteLogMessage(ILogMessage message, long messageId)
			{
				// insert common data
				long processNameId     = AddProcessName(message.ProcessName);
				long applicationNameId = AddApplicationName(message.ApplicationName);
				long writerNameId      = AddLogWriterName(message.LogWriterName);
				long levelNameId       = AddLogLevelName(message.LogLevelName);

				// insert message metadata
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
				ExecuteNonQueryCommand(mInsertMessageCommand);

				// insert message text
				mInsertTextCommand_IdParameter.Value = messageId;
				mInsertTextCommand_TextParameter.Value = message.Text;
				ExecuteNonQueryCommand(mInsertTextCommand);
			}

			/// <summary>
			/// Removes log messages that are above the specified message limit -or- older than the specified age.
			/// </summary>
			/// <param name="maximumMessageCount">
			/// Maximum number of messages to enforce;
			/// -1 to disable removing messages by maximum message count.
			/// </param>
			/// <param name="maximumMessageAge">
			/// Maximum age of log messages to keep;
			/// <seealso cref="TimeSpan.Zero"/> or a negative timespan to disable removing messages by age.
			/// </param>
			public override void Cleanup(long maximumMessageCount, TimeSpan maximumMessageAge)
			{
				// abort, if the database is empty
				if (OldestMessageId < 0)
					return;

				// calculate the timestamp of the first message to keep (older messages will be deleted)
				long deleteByTimestampTicks = maximumMessageAge > TimeSpan.Zero ? DateTime.UtcNow.Ticks - maximumMessageAge.Ticks : -1;

				BeginTransaction();
				try
				{
					// determine the id of the first message older than the specified timestamp
					long deleteByTimestampMessageId = -1;
					if (deleteByTimestampTicks >= 0)
					{
						mSelectMessageIdByTimestampForDeleteMessagesCommand_TimestampParameter.Value = DateTime.UtcNow.Ticks - maximumMessageAge.Ticks;
						var result = ExecuteScalarCommand(mSelectMessageIdByTimestampForDeleteMessagesCommand);
						deleteByTimestampMessageId = result != null ? Convert.ToInt64(result) : -1;
					}

					// determine the id of the first message to delete due to the maximum message count limit
					// (determined row is included)
					long totalMessageCount                = NewestMessageId - OldestMessageId + 1;
					long messagesToDeleteCount            = Math.Max(totalMessageCount - maximumMessageCount, 0);
					long deleteByMaxMessageCountMessageId = messagesToDeleteCount > 0 ? OldestMessageId + messagesToDeleteCount - 1 : -1;

					// combine selection conditions
					long messageId = -1;
					if (deleteByTimestampMessageId       >= 0) messageId = deleteByTimestampMessageId;
					if (deleteByMaxMessageCountMessageId >= 0) messageId = Math.Max(messageId, deleteByMaxMessageCountMessageId);

					// delete old messages up to the determined message id
					// (including the message with the determined id)
					if (messageId >= 0)
					{
						// delete texts associated with messages
						mDeleteTextsUpToIdCommand_IdParameter.Value = messageId;
						ExecuteNonQueryCommand(mDeleteTextsUpToIdCommand);

						// delete message metadata
						mDeleteMessagesUpToIdCommand_IdParameter.Value = messageId;
						ExecuteNonQueryCommand(mDeleteMessagesUpToIdCommand);
					}

					CommitTransaction();
				}
				catch
				{
					RollbackTransaction();
				}

				// update the id of the oldest and the newest log message
				OldestMessageId = GetOldestMessageId();
				NewestMessageId = GetNewestMessageId();
			}
		}
	}
}