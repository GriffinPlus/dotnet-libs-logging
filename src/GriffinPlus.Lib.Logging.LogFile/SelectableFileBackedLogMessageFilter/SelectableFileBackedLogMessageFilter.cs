///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// A log message filter for the <see cref="FileBackedLogMessageCollection"/> class that provides collections of fields
	/// that can be selected to be filtered. The filter supports data-binding and is therefore a perfect fit for user interfaces
	/// that present a list of selectable items for log writers, levels, processes etc.
	/// </summary>
	public class SelectableFileBackedLogMessageFilter :
		SelectableLogMessageFilterBase<LogMessage, FileBackedLogMessageCollection>,
		IFileBackedLogMessageCollectionFilter
	{
		private readonly StringPool               mStringPool = new StringPool();
		private readonly string                   mFilterDatabaseName;
		private readonly string                   mLogWriterFilterTableName;
		private readonly string                   mLogLevelFilterTableName;
		private readonly string                   mTagFilterTableName;
		private readonly string                   mApplicationNameFilterTableName;
		private readonly string                   mProcessNameFilterTableName;
		private readonly string                   mProcessIdFilterTableName;
		private          LogFile.DatabaseAccessor mAccessor;
		private          SQLiteCommand            mSelectContinuousMessagesCommand_Forward;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Forward_TextLikeParameter;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Forward_FromIdParameter;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Forward_CountParameter;
		private          SQLiteCommand            mSelectContinuousMessagesCommand_Backwards;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Backwards_TextLikeParameter;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Backwards_FromIdParameter;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Backwards_CountParameter;
		private          SQLiteCommand            mSelectContinuousMessagesCommand_Range;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Range_TextLikeParameter;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Range_FromIdParameter;
		private          SQLiteParameter          mSelectContinuousMessagesCommand_Range_ToIdParameter;
		private          SQLiteCommand            mAddProcessIdToFilterCommand;
		private          SQLiteParameter          mAddProcessIdToFilterCommand_IdParameter;
		private          SQLiteCommand            mAddProcessNameToFilterCommand;
		private          SQLiteParameter          mAddProcessNameToFilterCommand_NameParameter;
		private          SQLiteCommand            mAddApplicationNameToFilterCommand;
		private          SQLiteParameter          mAddApplicationNameToFilterCommand_NameParameter;
		private          SQLiteCommand            mAddLogWriterToFilterCommand;
		private          SQLiteParameter          mAddLogWriterToFilterCommand_NameParameter;
		private          SQLiteCommand            mAddLogLevelToFilterCommand;
		private          SQLiteParameter          mAddLogLevelToFilterCommand_NameParameter;
		private          SQLiteCommand            mAddTagToFilterCommand;
		private          SQLiteParameter          mAddTagToFilterCommand_NameParameter;

		/// <summary>
		/// Initializes a new instance of the <see cref="SelectableFileBackedLogMessageFilter"/> class.
		/// </summary>
		public SelectableFileBackedLogMessageFilter()
		{
			var filterGuid = Guid.NewGuid();
			mFilterDatabaseName = $"filter_{filterGuid:N}";
			mProcessIdFilterTableName = "process_id";
			mProcessNameFilterTableName = "process_name";
			mApplicationNameFilterTableName = "application_name";
			mLogWriterFilterTableName = "log_writer";
			mLogLevelFilterTableName = "log_level";
			mTagFilterTableName = "tag";
		}

		/// <summary>
		/// Is called after the base class has attached the filter to the collection.
		/// </summary>
		protected override void OnAttachToCollection()
		{
			mAccessor = Collection.LogFile.Accessor;

			// create an in-memory database for filter settings
			mAccessor.ExecuteNonQueryCommands($"ATTACH DATABASE 'file::{mFilterDatabaseName}?mode=memory' AS {mFilterDatabaseName};");

			// create temporary tables that will store the selected log writers, levels, tags, application names and process names/ids
			mAccessor.ExecuteNonQueryCommands($"CREATE TABLE {mFilterDatabaseName}.{mProcessIdFilterTableName} (id INTEGER);");
			mAccessor.ExecuteNonQueryCommands($"CREATE TABLE {mFilterDatabaseName}.{mProcessNameFilterTableName} (id INTEGER);");
			mAccessor.ExecuteNonQueryCommands($"CREATE TABLE {mFilterDatabaseName}.{mApplicationNameFilterTableName} (id INTEGER);");
			mAccessor.ExecuteNonQueryCommands($"CREATE TABLE {mFilterDatabaseName}.{mLogWriterFilterTableName} (id INTEGER);");
			mAccessor.ExecuteNonQueryCommands($"CREATE TABLE {mFilterDatabaseName}.{mLogLevelFilterTableName} (id INTEGER);");
			mAccessor.ExecuteNonQueryCommands($"CREATE TABLE {mFilterDatabaseName}.{mTagFilterTableName} (id INTEGER);");

			// create indices on the filter tables to accelerate lookups
			mAccessor.ExecuteNonQueryCommands($"CREATE UNIQUE INDEX {mFilterDatabaseName}.{mProcessIdFilterTableName}_index ON {mProcessIdFilterTableName} (id);");
			mAccessor.ExecuteNonQueryCommands($"CREATE UNIQUE INDEX {mFilterDatabaseName}.{mProcessNameFilterTableName}_index ON {mProcessNameFilterTableName} (id);");
			mAccessor.ExecuteNonQueryCommands($"CREATE UNIQUE INDEX {mFilterDatabaseName}.{mApplicationNameFilterTableName}_index ON {mApplicationNameFilterTableName} (id);");
			mAccessor.ExecuteNonQueryCommands($"CREATE UNIQUE INDEX {mFilterDatabaseName}.{mLogWriterFilterTableName}_index ON {mApplicationNameFilterTableName} (id);");
			mAccessor.ExecuteNonQueryCommands($"CREATE UNIQUE INDEX {mFilterDatabaseName}.{mLogLevelFilterTableName}_index ON {mLogLevelFilterTableName} (id);");
			mAccessor.ExecuteNonQueryCommands($"CREATE UNIQUE INDEX {mFilterDatabaseName}.{mTagFilterTableName}_index ON {mTagFilterTableName} (id);");

			// prepare query to retrieve log messages
			SetupMessageQuery();

			// prepare queries to add enabled filter items
			mAddProcessIdToFilterCommand = mAccessor.PrepareCommand($"INSERT OR IGNORE INTO {mFilterDatabaseName}.{mProcessIdFilterTableName} VALUES (@id);");
			mAddProcessIdToFilterCommand.Parameters.Add(mAddProcessIdToFilterCommand_IdParameter = new SQLiteParameter("@id", DbType.Int32));
			mAddProcessNameToFilterCommand = mAccessor.PrepareCommand($"INSERT OR IGNORE INTO {mFilterDatabaseName}.{mProcessNameFilterTableName} SELECT id FROM processes WHERE name = @name;");
			mAddProcessNameToFilterCommand.Parameters.Add(mAddProcessNameToFilterCommand_NameParameter = new SQLiteParameter("@name", DbType.String));
			mAddApplicationNameToFilterCommand = mAccessor.PrepareCommand($"INSERT OR IGNORE INTO {mFilterDatabaseName}.{mApplicationNameFilterTableName} SELECT id FROM applications WHERE name = @name;");
			mAddApplicationNameToFilterCommand.Parameters.Add(mAddApplicationNameToFilterCommand_NameParameter = new SQLiteParameter("@name", DbType.String));
			mAddLogWriterToFilterCommand = mAccessor.PrepareCommand($"INSERT OR IGNORE INTO {mFilterDatabaseName}.{mLogWriterFilterTableName} SELECT id FROM writers WHERE name = @name;");
			mAddLogWriterToFilterCommand.Parameters.Add(mAddLogWriterToFilterCommand_NameParameter = new SQLiteParameter("@name", DbType.String));
			mAddLogLevelToFilterCommand = mAccessor.PrepareCommand($"INSERT OR IGNORE INTO {mFilterDatabaseName}.{mLogLevelFilterTableName} SELECT id FROM levels WHERE name = @name;");
			mAddLogLevelToFilterCommand.Parameters.Add(mAddLogLevelToFilterCommand_NameParameter = new SQLiteParameter("@name", DbType.String));
			mAddTagToFilterCommand = mAccessor.PrepareCommand($"INSERT OR IGNORE INTO {mFilterDatabaseName}.{mTagFilterTableName} SELECT id FROM tags WHERE name = @name;");
			mAddTagToFilterCommand.Parameters.Add(mAddTagToFilterCommand_NameParameter = new SQLiteParameter("@name", DbType.String));

			// set up filter tables
			RebuildFilterTables();
		}

		/// <summary>
		/// Is called before the base class detaches the filter from the collection.
		/// </summary>
		protected override void OnDetachFromCollection()
		{
			// release prepared statements
			mAccessor.ReleasePreparedCommand(ref mSelectContinuousMessagesCommand_Forward);
			mAccessor.ReleasePreparedCommand(ref mSelectContinuousMessagesCommand_Backwards);
			mAccessor.ReleasePreparedCommand(ref mAddProcessIdToFilterCommand);
			mAccessor.ReleasePreparedCommand(ref mAddProcessNameToFilterCommand);
			mAccessor.ReleasePreparedCommand(ref mAddApplicationNameToFilterCommand);
			mAccessor.ReleasePreparedCommand(ref mAddLogWriterToFilterCommand);
			mAccessor.ReleasePreparedCommand(ref mAddLogLevelToFilterCommand);
			mAccessor.ReleasePreparedCommand(ref mAddTagToFilterCommand);

			// drop temporary tables (drops indexes as well)
			// (should not be necessary for an in-memory database, but it is cleaner so)
			mAccessor.ExecuteNonQueryCommands($"DROP TABLE IF EXISTS {mFilterDatabaseName}.{mProcessIdFilterTableName};");
			mAccessor.ExecuteNonQueryCommands($"DROP TABLE IF EXISTS {mFilterDatabaseName}.{mProcessNameFilterTableName};");
			mAccessor.ExecuteNonQueryCommands($"DROP TABLE IF EXISTS {mFilterDatabaseName}.{mApplicationNameFilterTableName};");
			mAccessor.ExecuteNonQueryCommands($"DROP TABLE IF EXISTS {mFilterDatabaseName}.{mLogWriterFilterTableName};");
			mAccessor.ExecuteNonQueryCommands($"DROP TABLE IF EXISTS {mFilterDatabaseName}.{mLogLevelFilterTableName};");
			mAccessor.ExecuteNonQueryCommands($"DROP TABLE IF EXISTS {mFilterDatabaseName}.{mTagFilterTableName};");

			// detach from in-memory database
			mAccessor.ExecuteNonQueryCommands($"DETACH DATABASE {mFilterDatabaseName};");

			mAccessor = null;
		}

		#region Message Query Setup

		/// <summary>
		/// Prepares the query that is used to fetch filtered log messages.
		/// </summary>
		private void SetupMessageQuery()
		{
			mAccessor.ReleasePreparedCommand(ref mSelectContinuousMessagesCommand_Forward);
			mAccessor.ReleasePreparedCommand(ref mSelectContinuousMessagesCommand_Backwards);

			long fromTimestamp = TimestampFilter.From.UtcTicks;
			long toTimestamp = TimestampFilter.To.UtcTicks;

			string likeCollate = TextFilter.IsCaseSensitive ? "" : "COLLATE NOCASE";
			string likeExpression = $"LIKE @text_like ESCAPE '\\' {likeCollate}";

			if (mAccessor.Purpose == LogFilePurpose.Recording)
			{
				string queryFormat =
					"SELECT DISTINCT m.id, timestamp, m.timezone_offset, m.high_precision_timestamp, m.lost_message_count, m.process_id, p.name, a.name, w.name, l.name, m.has_tags, m.text" +
					" FROM messages as m" +
					" INNER JOIN processes as p ON p.id = m.process_name_id" +
					" INNER JOIN applications as a ON a.id = m.application_name_id" +
					" INNER JOIN writers as w ON w.id = m.writer_name_id" +
					" INNER JOIN levels as l ON l.id = m.level_name_id" +
					(Enabled && ProcessIdFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mProcessIdFilterTableName} ON {mFilterDatabaseName}.{mProcessIdFilterTableName}.id = m.process_id" : "") +
					(Enabled && ProcessNameFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mProcessNameFilterTableName} ON {mFilterDatabaseName}.{mProcessNameFilterTableName}.id = p.id" : "") +
					(Enabled && ApplicationNameFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mApplicationNameFilterTableName} ON {mFilterDatabaseName}.{mApplicationNameFilterTableName}.id = a.id" : "") +
					(Enabled && LogWriterFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mLogWriterFilterTableName} ON {mFilterDatabaseName}.{mLogWriterFilterTableName}.id = w.id" : "") +
					(Enabled && LogLevelFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mLogLevelFilterTableName} ON {mFilterDatabaseName}.{mLogLevelFilterTableName}.id = l.id" : "") +
					(Enabled && TagFilter.Enabled
						 ? " INNER JOIN tag2msg as tm ON tm.message_id = m.id" +
						   $" INNER JOIN {mFilterDatabaseName}.{mTagFilterTableName} ON {mFilterDatabaseName}.{mTagFilterTableName}.id = tm.tag_id"
						 : "") +
					" WHERE {0}" +
					(Enabled && TimestampFilter.Enabled ? $" AND m.timestamp BETWEEN {fromTimestamp} AND {toTimestamp}" : "") +
					(Enabled && TextFilter.Enabled ? $" AND m.text {likeExpression}" : "") +
					" ORDER BY m.id {1}" +
					" {2};";

				mSelectContinuousMessagesCommand_Forward = mAccessor.PrepareCommand(string.Format(queryFormat, "m.id >= @from_id", "ASC", "LIMIT @count"));
				mSelectContinuousMessagesCommand_Backwards = mAccessor.PrepareCommand(string.Format(queryFormat, "m.id <= @from_id", "DESC", "LIMIT @count"));
				mSelectContinuousMessagesCommand_Range = mAccessor.PrepareCommand(string.Format(queryFormat, "m.id >= @from_id AND m.id <= @to_id", "ASC", ""));
			}
			else if (mAccessor.Purpose == LogFilePurpose.Analysis)
			{
				string queryFormat =
					"SELECT DISTINCT m.id, timestamp, m.timezone_offset, m.high_precision_timestamp, m.lost_message_count, m.process_id, p.name, a.name, w.name, l.name, m.has_tags, t.text" +
					" FROM messages as m" +
					" INNER JOIN processes as p ON p.id = m.process_name_id" +
					" INNER JOIN applications as a ON a.id = m.application_name_id" +
					" INNER JOIN writers as w ON w.id = m.writer_name_id" +
					" INNER JOIN levels as l ON l.id = m.level_name_id" +
					" INNER JOIN texts as t ON t.id = m.id" +
					(Enabled && ProcessIdFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mProcessIdFilterTableName} ON {mFilterDatabaseName}.{mProcessIdFilterTableName}.id = m.process_id" : "") +
					(Enabled && ProcessNameFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mProcessNameFilterTableName} ON {mFilterDatabaseName}.{mProcessNameFilterTableName}.id = p.id" : "") +
					(Enabled && ApplicationNameFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mApplicationNameFilterTableName} ON {mFilterDatabaseName}.{mApplicationNameFilterTableName}.id = a.id" : "") +
					(Enabled && LogWriterFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mLogWriterFilterTableName} ON {mFilterDatabaseName}.{mLogWriterFilterTableName}.id = w.id" : "") +
					(Enabled && LogLevelFilter.Enabled ? $" INNER JOIN {mFilterDatabaseName}.{mLogLevelFilterTableName} ON {mFilterDatabaseName}.{mLogLevelFilterTableName}.id = l.id" : "") +
					(Enabled && TagFilter.Enabled
						 ? " INNER JOIN tag2msg as tm ON tm.message_id = m.id" +
						   $" INNER JOIN {mFilterDatabaseName}.{mTagFilterTableName} ON {mFilterDatabaseName}.{mTagFilterTableName}.id = tm.tag_id"
						 : "") +
					" WHERE {0}" +
					(Enabled && TimestampFilter.Enabled ? $" AND m.timestamp BETWEEN {fromTimestamp} AND {toTimestamp}" : "") +
					(Enabled && TextFilter.Enabled ? $" AND t.text {likeExpression}" : "") +
					" ORDER BY m.id {1}" +
					" {2};";

				mSelectContinuousMessagesCommand_Forward = mAccessor.PrepareCommand(string.Format(queryFormat, "m.id >= @from_id", "ASC", "LIMIT @count"));
				mSelectContinuousMessagesCommand_Backwards = mAccessor.PrepareCommand(string.Format(queryFormat, "m.id <= @from_id", "DESC", "LIMIT @count"));
				mSelectContinuousMessagesCommand_Range = mAccessor.PrepareCommand(string.Format(queryFormat, "m.id >= @from_id AND m.id <= @to_id", "ASC", ""));
			}
			else
			{
				throw new NotSupportedException($"The database schema for log file purpose ({mAccessor.Purpose}) is not supported.");
			}

			string textLikeArgument = "%" + TextFilter.SearchText
				.Replace("\\", "\\\\")
				.Replace("%", "\\%")
				.Replace("_", "\\_") + "%";

			mSelectContinuousMessagesCommand_Forward.Parameters.Add(mSelectContinuousMessagesCommand_Forward_TextLikeParameter = new SQLiteParameter("@text_like", DbType.String));
			mSelectContinuousMessagesCommand_Forward.Parameters.Add(mSelectContinuousMessagesCommand_Forward_FromIdParameter = new SQLiteParameter("@from_id", DbType.Int64));
			mSelectContinuousMessagesCommand_Forward.Parameters.Add(mSelectContinuousMessagesCommand_Forward_CountParameter = new SQLiteParameter("@count", DbType.Int64));
			mSelectContinuousMessagesCommand_Forward_TextLikeParameter.Value = textLikeArgument;

			mSelectContinuousMessagesCommand_Backwards.Parameters.Add(mSelectContinuousMessagesCommand_Backwards_TextLikeParameter = new SQLiteParameter("@text_like", DbType.String));
			mSelectContinuousMessagesCommand_Backwards.Parameters.Add(mSelectContinuousMessagesCommand_Backwards_FromIdParameter = new SQLiteParameter("@from_id", DbType.Int64));
			mSelectContinuousMessagesCommand_Backwards.Parameters.Add(mSelectContinuousMessagesCommand_Backwards_CountParameter = new SQLiteParameter("@count", DbType.Int64));
			mSelectContinuousMessagesCommand_Backwards_TextLikeParameter.Value = textLikeArgument;

			mSelectContinuousMessagesCommand_Range.Parameters.Add(mSelectContinuousMessagesCommand_Range_TextLikeParameter = new SQLiteParameter("@text_like", DbType.String));
			mSelectContinuousMessagesCommand_Range.Parameters.Add(mSelectContinuousMessagesCommand_Range_FromIdParameter = new SQLiteParameter("@from_id", DbType.Int64));
			mSelectContinuousMessagesCommand_Range.Parameters.Add(mSelectContinuousMessagesCommand_Range_ToIdParameter = new SQLiteParameter("@to_id", DbType.Int64));
			mSelectContinuousMessagesCommand_Range_TextLikeParameter.Value = textLikeArgument;
		}

		#endregion

		#region Processing Filter Changes

		/// <summary>
		/// Raises the <see cref="SelectableLogMessageFilterBase{TMessage,TUnfilteredCollection}.FilterChanged"/> event.
		/// </summary>
		/// <param name="changeEffectsFilterResult">
		/// <c>true</c> if the change to the filter may change the set of filtered messages;
		/// otherwise <c>false</c>.
		/// </param>
		protected override void OnFilterChanged(bool changeEffectsFilterResult)
		{
			if (mAccessor != null && changeEffectsFilterResult)
			{
				RebuildFilterTables();
				SetupMessageQuery();
			}

			base.OnFilterChanged(changeEffectsFilterResult);
		}

		/// <summary>
		/// Builds up the filter tables from scratch, so they reflect the current settings in the filter collections
		/// of the base class.
		/// </summary>
		private void RebuildFilterTables()
		{
			// set up process id filter
			mAccessor.ExecuteNonQueryCommands($"DELETE FROM {mFilterDatabaseName}.{mProcessIdFilterTableName};");
			if (Enabled && ProcessIdFilter.Enabled)
			{
				foreach (var item in ProcessIdFilter.Items)
				{
					if (item.Selected)
					{
						mAddProcessIdToFilterCommand_IdParameter.Value = item.Value;
						LogFile.DatabaseAccessor.ExecuteNonQueryCommand(mAddProcessIdToFilterCommand);
					}
				}
			}

			// set up process name filter
			mAccessor.ExecuteNonQueryCommands($"DELETE FROM {mFilterDatabaseName}.{mProcessNameFilterTableName};");
			if (Enabled && ProcessNameFilter.Enabled)
			{
				foreach (var item in ProcessNameFilter.Items)
				{
					if (item.Selected)
					{
						mAddProcessNameToFilterCommand_NameParameter.Value = item.Value;
						LogFile.DatabaseAccessor.ExecuteNonQueryCommand(mAddProcessNameToFilterCommand);
					}
				}
			}

			// set up application name filter
			mAccessor.ExecuteNonQueryCommands($"DELETE FROM {mFilterDatabaseName}.{mApplicationNameFilterTableName};");
			if (Enabled && ApplicationNameFilter.Enabled)
			{
				foreach (var item in ApplicationNameFilter.Items)
				{
					if (item.Selected)
					{
						mAddApplicationNameToFilterCommand_NameParameter.Value = item.Value;
						LogFile.DatabaseAccessor.ExecuteNonQueryCommand(mAddApplicationNameToFilterCommand);
					}
				}
			}

			// set up log writer filter
			mAccessor.ExecuteNonQueryCommands($"DELETE FROM {mFilterDatabaseName}.{mLogWriterFilterTableName};");
			if (Enabled && LogWriterFilter.Enabled)
			{
				foreach (var item in LogWriterFilter.Items)
				{
					if (item.Selected)
					{
						mAddLogWriterToFilterCommand_NameParameter.Value = item.Value;
						LogFile.DatabaseAccessor.ExecuteNonQueryCommand(mAddLogWriterToFilterCommand);
					}
				}
			}

			// set up log level filter
			mAccessor.ExecuteNonQueryCommands($"DELETE FROM {mFilterDatabaseName}.{mLogLevelFilterTableName};");
			if (Enabled && LogLevelFilter.Enabled)
			{
				foreach (var item in LogLevelFilter.Items)
				{
					if (item.Selected)
					{
						mAddLogLevelToFilterCommand_NameParameter.Value = item.Value;
						LogFile.DatabaseAccessor.ExecuteNonQueryCommand(mAddLogLevelToFilterCommand);
					}
				}
			}

			// set up tags filter
			mAccessor.ExecuteNonQueryCommands($"DELETE FROM {mFilterDatabaseName}.{mTagFilterTableName};");
			if (Enabled && TagFilter.Enabled)
			{
				foreach (var item in TagFilter.Items)
				{
					if (item.Selected)
					{
						mAddTagToFilterCommand_NameParameter.Value = item.Value;
						LogFile.DatabaseAccessor.ExecuteNonQueryCommand(mAddTagToFilterCommand);
					}
				}
			}
		}

		#endregion

		#region Getting Messages Matching the Filter

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified position in the log file going backwards.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="InvalidOperationException">The filter is not attached to a collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		public LogFileMessage GetPreviousMessage(long fromMessageId)
		{
			if (mAccessor == null)
				throw new InvalidOperationException("The filter is not attached to a collection.");

			if (fromMessageId < mAccessor.OldestMessageId || fromMessageId > mAccessor.NewestMessageId)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromMessageId),
					fromMessageId,
					"The start message id exceeds the bounds of the log file.");
			}

			mSelectContinuousMessagesCommand_Backwards_FromIdParameter.Value = fromMessageId;
			mSelectContinuousMessagesCommand_Backwards_CountParameter.Value = 1;
			using (var reader = mSelectContinuousMessagesCommand_Backwards.ExecuteReader())
			{
				while (reader.Read())
				{
					ReadMessage(reader, out var message);
					return message;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the log file going backwards.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <param name="reverse">
		/// <c>true</c> to reverse the list of returned messages, so the order of the messages is the same as in the log file;
		/// <c>false</c> to return the list of messages in the opposite order.
		/// </param>
		/// <returns>Log messages matching the filter.</returns>
		/// <exception cref="InvalidOperationException">The filter is not attached to a collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		public LogFileMessage[] GetPreviousMessages(
			long fromMessageId,
			long count,
			bool reverse)
		{
			if (mAccessor == null)
				throw new InvalidOperationException("The filter is not attached to a collection.");

			if (fromMessageId < mAccessor.OldestMessageId || fromMessageId > mAccessor.NewestMessageId)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromMessageId),
					fromMessageId,
					"The start message id exceeds the bounds of the unfiltered collection.");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(count),
					count,
					"The count must be positive.");
			}

			var messages = new List<LogFileMessage>();

			mSelectContinuousMessagesCommand_Backwards_FromIdParameter.Value = fromMessageId;
			mSelectContinuousMessagesCommand_Backwards_CountParameter.Value = count;
			using (var reader = mSelectContinuousMessagesCommand_Backwards.ExecuteReader())
			{
				while (reader.Read())
				{
					ReadMessage(reader, out var message);
					messages.Add(message);
				}
			}

			if (reverse) messages.Reverse();
			return messages.ToArray();
		}

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified position in the log file going forward.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="InvalidOperationException">The filter is not attached to a collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		public LogFileMessage GetNextMessage(long fromMessageId)
		{
			if (mAccessor == null)
				throw new InvalidOperationException("The filter is not attached to a collection.");

			if (fromMessageId < mAccessor.OldestMessageId || fromMessageId > mAccessor.NewestMessageId)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromMessageId),
					fromMessageId,
					"The start message id exceeds the bounds of the log file.");
			}

			mSelectContinuousMessagesCommand_Forward_FromIdParameter.Value = fromMessageId;
			mSelectContinuousMessagesCommand_Forward_CountParameter.Value = 1;
			using (var reader = mSelectContinuousMessagesCommand_Forward.ExecuteReader())
			{
				while (reader.Read())
				{
					ReadMessage(reader, out var message);
					return message;
				}
			}

			return null;
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the log file going forward.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <returns>Log messages matching the filter.</returns>
		/// <exception cref="InvalidOperationException">The filter is not attached to a collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		public LogFileMessage[] GetNextMessages(long fromMessageId, long count)
		{
			if (mAccessor == null)
				throw new InvalidOperationException("The filter is not attached to a collection.");

			if (fromMessageId < mAccessor.OldestMessageId || fromMessageId > mAccessor.NewestMessageId)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromMessageId),
					fromMessageId,
					"The start message id exceeds the bounds of the unfiltered collection.");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(count),
					count,
					"The count must be positive.");
			}

			var messages = new List<LogFileMessage>();

			mSelectContinuousMessagesCommand_Forward_FromIdParameter.Value = fromMessageId;
			mSelectContinuousMessagesCommand_Forward_CountParameter.Value = count;
			using (var reader = mSelectContinuousMessagesCommand_Forward.ExecuteReader())
			{
				while (reader.Read())
				{
					ReadMessage(reader, out var message);
					messages.Add(message);
				}
			}

			return messages.ToArray();
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the log file.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <param name="toMessageId">Id of the log message in the log file to stop at.</param>
		/// <returns>Log messages matching the filter.</returns>
		/// <exception cref="InvalidOperationException">The filter is not attached to a collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="fromMessageId"/> or <paramref name="toMessageId"/> exceeds the bounds of the log file.
		/// </exception>
		public LogFileMessage[] GetMessageRange(long fromMessageId, long toMessageId)
		{
			if (mAccessor == null)
				throw new InvalidOperationException("The filter is not attached to a collection.");

			if (fromMessageId < mAccessor.OldestMessageId || fromMessageId > mAccessor.NewestMessageId)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromMessageId),
					fromMessageId,
					"The start message id exceeds the bounds of the log file.");
			}

			if (toMessageId < mAccessor.OldestMessageId || toMessageId > mAccessor.NewestMessageId)
			{
				throw new ArgumentOutOfRangeException(
					nameof(toMessageId),
					toMessageId,
					"The end message id exceeds the bounds of the log file.");
			}

			var messages = new List<LogFileMessage>();

			mSelectContinuousMessagesCommand_Range_FromIdParameter.Value = fromMessageId;
			mSelectContinuousMessagesCommand_Range_ToIdParameter.Value = toMessageId;
			using (var reader = mSelectContinuousMessagesCommand_Range.ExecuteReader())
			{
				while (reader.Read())
				{
					ReadMessage(reader, out var message);
					messages.Add(message);
				}
			}

			return messages.ToArray();
		}

		/// <summary>
		/// Reads a log message from the specified sqlite reader.
		/// </summary>
		/// <param name="reader">Sqlite reader to read from.</param>
		/// <param name="message">Receives the read log message.</param>
		private void ReadMessage(SQLiteDataReader reader, out LogFileMessage message)
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
			var timezoneOffset = TimeSpan.FromTicks(reader.GetInt64(2));
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

			message = new LogFileMessage().InitWith(
				messageId,
				timestamp,
				highPrecisionTimestamp,
				lostMessageCount,
				mStringPool.Intern(logWriterName),
				mStringPool.Intern(logLevelName),
				TagSet.Empty,
				mStringPool.Intern(applicationName),
				mStringPool.Intern(processName),
				processId,
				text);

			// initialize tags, if there are tags associated with the message
			if (hasTags) message.Tags = mAccessor.GetTagsOfMessage(messageId);

			// protect message from changes
			message.Protect();
		}

		#endregion
	}

}
