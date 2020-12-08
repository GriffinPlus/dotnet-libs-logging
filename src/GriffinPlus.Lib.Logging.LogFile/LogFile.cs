///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log file based on an SQLite database file with optimizations for recording and analysis scenarios.
	/// </summary>
	public partial class LogFile : IDisposable
	{
		private readonly string                         mFilePath;
		private          DatabaseAccessor               mDatabaseAccessor;
		private readonly FileBackedLogMessageCollection mMessageCollection;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFile"/> class.
		/// </summary>
		/// <param name="path">Log file to open/create.</param>
		/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
		/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		public LogFile(
			string path,
			LogFilePurpose purpose,
			LogFileWriteMode writeMode = LogFileWriteMode.Robust) : this(path, purpose, writeMode, null)
		{
			mMessageCollection = new FileBackedLogMessageCollection(this);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogFile"/> class.
		/// </summary>
		/// <param name="path">Log file to open/create.</param>
		/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
		/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		/// <param name="collection">Collection that works upon the log file.</param>
		/// <exception cref="LogFileException">Opening/Creating the log file failed.</exception>
		internal LogFile(
			string path,
			LogFilePurpose purpose,
			LogFileWriteMode writeMode,
			FileBackedLogMessageCollection collection)
		{
			if (path == null) throw new ArgumentNullException(nameof(path));

			// initialize information about the operating mode
			mFilePath = Path.GetFullPath(path);
			mMessageCollection = collection;

			// check whether the log file exists already
			bool fileExists = File.Exists(mFilePath);

			try
			{
				// open database file (creates a new one, if it does not exist)
				SQLiteConnection connection = new SQLiteConnection($"Data Source={mFilePath};Version=3");
				connection.Open();

				// open/create the database
				if (fileExists)
				{
					// check application id
					ulong applicationId = DatabaseAccessor.GetApplicationId(connection);
					if (applicationId != DatabaseAccessor.LogFileApplicationId)
						throw new InvalidLogFileFormatException($"Application id in the sqlite database is 0x{applicationId:08x}, expecting not 0x{DatabaseAccessor.LogFileApplicationId:08x}");

					// check user version
					// (indicates its purpose, directly corresponds to the database schema)
					ulong userVersion = DatabaseAccessor.GetSchemaVersion(connection);
					switch (userVersion)
					{
						case 1:
							mDatabaseAccessor = new RecordingDatabaseAccessor(connection, writeMode, false);
							break;
						case 2:
							mDatabaseAccessor = new AnalysisDatabaseAccessor(connection, writeMode, false);
							break;
						default:
							throw new FileVersionNotSupportedException();
					}
				}
				else
				{
					switch (purpose)
					{
						case LogFilePurpose.Recording:
							mDatabaseAccessor = new RecordingDatabaseAccessor(connection, writeMode, true);
							break;
						case LogFilePurpose.Analysis:
							mDatabaseAccessor = new AnalysisDatabaseAccessor(connection, writeMode, true);
							break;
						default:
							throw new NotSupportedException($"The specified purpose ({purpose}) is not supported.");
					}
				}
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Opening/Creating the log file failed: {ex.Message}",
					ex);
			}
		}

		/// <summary>
		/// Disposes the current log file.
		/// </summary>
		public void Dispose()
		{
			mDatabaseAccessor?.Dispose();
			mDatabaseAccessor = null;
		}

		/// <summary>
		/// Gets the version of the used SQLite implementation.
		/// </summary>
		public static string SqliteVersion => DatabaseAccessor.SqliteVersion;

		/// <summary>
		/// Gets the full path of the log file.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public string FilePath
		{
			get
			{
				CheckDisposed();
				return mFilePath;
			}
		}

		/// <summary>
		/// Gets the purpose of the log file.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public LogFilePurpose Purpose
		{
			get
			{
				CheckDisposed();
				return mDatabaseAccessor.Purpose;
			}
		}

		/// <summary>
		/// Gets the write-mode of the log file.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public LogFileWriteMode WriteMode
		{
			get
			{
				CheckDisposed();
				return mDatabaseAccessor.WriteMode;
			}
		}

		/// <summary>
		/// Gets the messages in the log file as a collection.
		/// </summary>
		/// <remarks>
		/// The returned collection will load log messages into memory on demand, so accessing the collection can take some time.
		/// </remarks>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public FileBackedLogMessageCollection Messages
		{
			get
			{
				CheckDisposed();
				return mMessageCollection;
			}
		}

		/// <summary>
		/// Gets the total number of messages in the log file.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public long MessageCount
		{
			get
			{
				CheckDisposed();
				if (mDatabaseAccessor.OldestMessageId < 0) return 0;
				return mDatabaseAccessor.NewestMessageId - mDatabaseAccessor.OldestMessageId + 1;
			}
		}

		/// <summary>
		/// Get the id of the oldest message in the file (-1, if the file is empty).
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public long OldestMessageId
		{
			get
			{
				CheckDisposed();
				return mDatabaseAccessor.OldestMessageId;
			}
		}

		/// <summary>
		/// Get the id of the newest message in the file (-1, if the file is empty).
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		public long NewestMessageId
		{
			get
			{
				CheckDisposed();
				return mDatabaseAccessor.NewestMessageId;
			}
		}

		/// <summary>
		/// Removes all log messages from the log file.
		/// </summary>
		/// <param name="messagesOnly">
		/// true to remove messages only;
		/// false to remove processes, applications, log writers, log levels and tags as well.
		/// </param>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Clearing the log file failed (see inner exception for details).</exception>
		public void Clear(bool messagesOnly = true)
		{
			CheckDisposed();

			try
			{
				// clear and shrink the log file
				mDatabaseAccessor.Clear(messagesOnly);
				mDatabaseAccessor.Vacuum();
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Clearing failed: {ex.Message}",
					ex);
			}

			// tell the log message collection about the change
			Messages.ResetCollectionInternal();
		}

		/// <summary>
		/// Gets a number of log messages starting at the specified message id.
		/// </summary>
		/// <param name="fromId">Id of the message to start at.</param>
		/// <param name="count">Number of log messages to get.</param>
		/// <returns>The requested log messages.</returns>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Reading failed (see inner exception for details).</exception>
		public LogMessage[] Read(long fromId, int count)
		{
			CheckDisposed();

			try
			{
				return mDatabaseAccessor.Read(fromId, count);
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Reading failed: {ex.Message}",
					ex);
			}
		}

		/// <summary>
		/// Gets a number of log messages starting at the specified message id.
		/// </summary>
		/// <param name="fromId">Id of the message to start at.</param>
		/// <param name="count">Number of log messages to get.</param>
		/// <param name="callback">Callback to invoke for every read message</param>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Reading failed (see inner exception for details).</exception>
		public void Read(long fromId, long count, ReadMessageCallback callback)
		{
			CheckDisposed();

			try
			{
				mDatabaseAccessor.Read(fromId, count, callback);
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Reading failed: {ex.Message}",
					ex);
			}
		}

		/// <summary>
		/// Writes a log message into the log file.
		/// </summary>
		/// <param name="message">Log message to write.</param>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Writing failed (see inner exception for details).</exception>
		public void Write(ILogMessage message)
		{
			CheckDisposed();

			try
			{
				mDatabaseAccessor.Write(message);
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Writing failed: {ex.Message}",
					ex);
			}

			mMessageCollection.ProcessMessageAdded(1);
		}

		/// <summary>
		/// Writes multiple log messages into the log file.
		/// </summary>
		/// <param name="messages">Log messages to write.</param>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Writing failed (see inner exception for details).</exception>
		public void Write(IEnumerable<ILogMessage> messages)
		{
			CheckDisposed();

			long count;
			try
			{
				count = mDatabaseAccessor.Write(messages);
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Writing failed: {ex.Message}",
					ex);
			}

			mMessageCollection.ProcessMessageAdded((int) count);
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
		/// <seealso cref="TimeSpan.Zero"/> to disable removing messages by age.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">The message limit must be > 0 to limit the number of message or -1 to disable the limit.</exception>
		/// <exception cref="ArgumentOutOfRangeException">The maximum message age must be positive to limit the number of messages or TimeSpan.Empty to disable the limit.</exception>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Cleaning up failed (see inner exception for details).</exception>
		public void Cleanup(long maximumMessageCount, TimeSpan maximumMessageAge)
		{
			if (maximumMessageCount < -1 || maximumMessageCount == 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(maximumMessageCount),
					"The maximum message count must be > 0 to limit the number of messages or -1 to disable the limit.");
			}

			if (maximumMessageAge < TimeSpan.Zero)
			{
				throw new ArgumentOutOfRangeException(
					nameof(maximumMessageAge),
					"The maximum message age must be positive to limit the number of messages or TimeSpan.Empty to disable the limit.");
			}

			CheckDisposed();

			try
			{
				mDatabaseAccessor.Cleanup(maximumMessageCount, maximumMessageAge);
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Cleaning up failed: {ex.Message}",
					ex);
			}

			mMessageCollection.ResetCollectionInternal();
		}

		/// <summary>
		/// Saves a snapshot of the log file.
		/// </summary>
		/// <param name="path">Path of the file to save the snapshot to.</param>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		/// <exception cref="LogFileException">Saving snapshot failed (see inner exception for details).</exception>
		public void SaveSnapshot(string path)
		{
			CheckDisposed();

			try
			{
				mDatabaseAccessor.SaveSnapshot(path);
			}
			catch (SQLiteException ex)
			{
				throw new LogFileException(
					$"Saving snapshot failed: {ex.Message}",
					ex);
			}
		}

		#region Helpers

		/// <summary>
		/// Checks whether the log file has been disposed an throws an exception.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
		private void CheckDisposed()
		{
			if (mDatabaseAccessor == null) throw new ObjectDisposedException($"Log File ({FilePath})");
		}

		#endregion
	}
}