///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;

using GriffinPlus.Lib.Logging.Collections;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A log file based on an SQLite database file with optimizations for recording and analysis scenarios.
/// </summary>
public partial class LogFile : IDisposable
{
	private readonly string                         mFilePath;
	private          DatabaseAccessor               mDatabaseAccessor;
	private readonly FileBackedLogMessageCollection mMessageCollection;

	/// <summary>
	/// Creates a new log file.
	/// </summary>
	/// <param name="path">Log file to create.</param>
	/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <param name="messages">Messages to populate the log file with.</param>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	private LogFile(
		string                   path,
		LogFilePurpose           purpose,
		LogFileWriteMode         writeMode,
		IEnumerable<ILogMessage> messages) : this(path, true, true, purpose, writeMode, false, null, messages)
	{
		mMessageCollection = new FileBackedLogMessageCollection(this);
	}

	/// <summary>
	/// Creates a new log file for reading and writing and populates it with the specified message set.
	/// Throws an exception, if the file exists already.
	/// </summary>
	/// <param name="path">Log file to open/create.</param>
	/// <param name="purpose">
	/// Purpose of the log file determining whether the log file is primarily used for recording or for analysis
	/// (does not have any effect, if the log file exists already).
	/// </param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	private LogFile(
		string           path,
		LogFilePurpose   purpose,
		LogFileWriteMode writeMode) : this(path, true, false, purpose, writeMode, false, null, null)
	{
		mMessageCollection = new FileBackedLogMessageCollection(this);
	}

	/// <summary>
	/// Opens an existing log file for reading and writing.
	/// Throws an exception, if the file does not exist.
	/// </summary>
	/// <param name="path">Log file to open.</param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	private LogFile(
		string           path,
		LogFileWriteMode writeMode) : this(path, false, false, LogFilePurpose.NotSpecified, writeMode, false, null, null)
	{
		mMessageCollection = new FileBackedLogMessageCollection(this);
	}

	/// <summary>
	/// Opens an existing log file for reading only.
	/// Throws an exception, if the file does not exist.
	/// </summary>
	/// <param name="path">Log file to open.</param>
	/// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	private LogFile(string path) : this(path, false, false, LogFilePurpose.NotSpecified, LogFileWriteMode.Fast, true, null, null)
	{
		mMessageCollection = new FileBackedLogMessageCollection(this);
	}

	/// <summary>
	/// Constructor providing functionality common to other constructors (for internal use only).
	/// </summary>
	/// <param name="path">Log file to open/create.</param>
	/// <param name="createIfNotExist">
	/// <c>true</c> to create the specified log file, if it does not exist, yet;<br/>
	/// <c>false</c> to throw an exception, if the file does not exist.
	/// </param>
	/// <param name="fileMustNotExist">
	/// <c>true</c> if the specified file must not exist;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <param name="isReadOnly">
	/// <c>true</c> to open the log file in read-only mode;<br/>
	/// <c>false</c> to open the log file in read/write mode.
	/// </param>
	/// <param name="collection">Collection that works upon the log file.</param>
	/// <param name="messages">Messages to put into the log file (should only be used for new files).</param>
	/// <exception cref="FileNotFoundException"><paramref name="createIfNotExist"/> is <c>false</c> and the specified file does not exist.</exception>
	/// <exception cref="LogFileException">Opening/Creating the log file failed.</exception>
	internal LogFile(
		string                         path,
		bool                           createIfNotExist,
		bool                           fileMustNotExist,
		LogFilePurpose                 purpose,
		LogFileWriteMode               writeMode,
		bool                           isReadOnly,
		FileBackedLogMessageCollection collection,
		IEnumerable<ILogMessage>       messages)
	{
		if (path == null) throw new ArgumentNullException(nameof(path));

		// initialize information about the operating mode
		mFilePath = Path.GetFullPath(path);
		mMessageCollection = collection;

		// check whether the log file exists already
		bool fileExists = File.Exists(mFilePath);

		// abort, if the file does not exist and creating a new file is not allowed
		if (!fileExists && !createIfNotExist)
			throw new FileNotFoundException($"Log file ({path}) does not exist.");

		// abort, if the file exists, but the caller expects it does not
		if (fileExists && fileMustNotExist)
			throw new LogFileException("Log file ({path}) exists already, but it should not.");

		// abort, if the file exists, but an initial message set is specified (internal error)
		if (fileExists && messages != null)
			throw new InvalidOperationException("Log file ({path}) exists already, but it should not as an initial message set is specified.");

		SQLiteConnection connection = null;
		try
		{
			// open database file (creates a new one, if it does not exist)
			connection = new SQLiteConnection($"Data Source={mFilePath};Version=3;Read Only={isReadOnly}");
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
						mDatabaseAccessor = new RecordingDatabaseAccessor(connection, writeMode, isReadOnly, false);
						break;

					case 2:
						mDatabaseAccessor = new AnalysisDatabaseAccessor(connection, writeMode, isReadOnly, false);
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
						mDatabaseAccessor = new RecordingDatabaseAccessor(connection, writeMode, isReadOnly, true, messages);
						break;

					case LogFilePurpose.Analysis:
						mDatabaseAccessor = new AnalysisDatabaseAccessor(connection, writeMode, isReadOnly, true, messages);
						break;

					case LogFilePurpose.NotSpecified:
					default:
						throw new NotSupportedException($"The specified purpose ({purpose}) is not supported.");
				}
			}
		}
		catch (SQLiteException ex)
		{
			Dispose();
			connection?.Dispose();
			throw new LogFileException(
				$"Opening/Creating the log file failed: {ex.Message}",
				ex);
		}
		catch (Exception)
		{
			Dispose();
			connection?.Dispose();
			throw;
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
	/// Creates a new log file and populates it with the specified log messages.
	/// </summary>
	/// <param name="path">Log file to create.</param>
	/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <param name="messages">Messages to populate the new log file with (may be <c>null</c>).</param>
	/// <exception cref="LogFileException">Creating the log file failed (see message and inner exception for details).</exception>
	public static LogFile Create(
		string                   path,
		LogFilePurpose           purpose,
		LogFileWriteMode         writeMode,
		IEnumerable<ILogMessage> messages = null)
	{
		return new LogFile(path, purpose, writeMode, messages);
	}

	/// <summary>
	/// Opens an existing log file in read/write mode, creates a new log file, if the file does not exist, yet.
	/// </summary>
	/// <param name="path">Log file to open/create.</param>
	/// <param name="purpose">
	/// Purpose of the log file determining whether the log file is primarily used for recording or for analysis
	/// (does not have any effect, if the log file exists already).
	/// </param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	public static LogFile OpenOrCreate(
		string           path,
		LogFilePurpose   purpose,
		LogFileWriteMode writeMode)
	{
		return new LogFile(path, purpose, writeMode);
	}

	/// <summary>
	/// Opens an existing log file for reading and writing.
	/// </summary>
	/// <param name="path">Log file to open.</param>
	/// <param name="writeMode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
	/// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	public static LogFile Open(string path, LogFileWriteMode writeMode)
	{
		return new LogFile(path, writeMode);
	}

	/// <summary>
	/// Opens an existing log file for reading only.
	/// </summary>
	/// <param name="path">Path of the log file to open.</param>
	/// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
	/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
	public static LogFile OpenReadOnly(string path)
	{
		return new LogFile(path);
	}

	/// <summary>
	/// Gets the version of the used SQLite implementation.
	/// </summary>
	public static string SqliteVersion => DatabaseAccessor.SqliteVersion;

	/// <summary>
	/// Gets the database accessor used to access the database.
	/// </summary>
	internal DatabaseAccessor Accessor
	{
		get
		{
			CheckDisposed();
			return mDatabaseAccessor;
		}
	}

	/// <summary>
	/// Gets the sqlite database connection the log file works on.
	/// </summary>
	public SQLiteConnection Connection
	{
		get
		{
			CheckDisposed();
			return mDatabaseAccessor.Connection;
		}
	}

	/// <summary>
	/// Gets a value determining whether the log file is opened for reading and writing (<c>false</c>) or for reading only (<c>true</c>).
	/// </summary>
	public bool IsReadOnly
	{
		get
		{
			CheckDisposed();
			return mDatabaseAccessor.IsReadOnly;
		}
	}

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
	/// Gets the name of processes that are/were associated with log messages.
	/// </summary>
	/// <param name="usedOnly">
	/// <c>true</c> to get the name of processes that are referenced by messages in the log file only;<br/>
	/// <c>false</c> to get all process names (even if referencing log messages have been removed after clearing/pruning).
	/// </param>
	/// <returns>A list of process names.</returns>
	public string[] GetProcessNames(bool usedOnly)
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.GetProcessNames(usedOnly);
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Getting process names failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Gets the ids of processes that are associated with log messages.
	/// </summary>
	/// <returns>A list of process ids.</returns>
	public int[] GetProcessIds()
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.GetProcessIds();
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Getting process ids failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Gets the name of applications that are/were associated with log messages.
	/// </summary>
	/// <param name="usedOnly">
	/// <c>true</c> to get the name of applications that are referenced by messages in the log file only;<br/>
	/// <c>false</c> to get all application names (even if referencing log messages have been removed after clearing/pruning).
	/// </param>
	/// <returns>A list of application names.</returns>
	public string[] GetApplicationNames(bool usedOnly)
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.GetApplicationNames(usedOnly);
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Getting application names failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Gets the name of log writers that are/were associated with log messages.
	/// </summary>
	/// <param name="usedOnly">
	/// <c>true</c> to get the name of log writers that are referenced by messages in the log file only;<br/>
	/// <c>false</c> to get all log writer names (even if referencing log messages have been removed after clearing/pruning).
	/// </param>
	/// <returns>A list of log writer names.</returns>
	public string[] GetLogWriterNames(bool usedOnly)
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.GetLogWriterNames(usedOnly);
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Getting log writers failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Gets the name of log levels that are/were associated with log messages.
	/// </summary>
	/// <param name="usedOnly">
	/// <c>true</c> to get the name of log levels that are referenced by messages in the log file only;<br/>
	/// <c>false</c> to get all log level names (even if referencing log messages have been removed after clearing/pruning).
	/// </param>
	/// <returns>A list of log level names.</returns>
	public string[] GetLogLevelNames(bool usedOnly)
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.GetLogLevelNames(usedOnly);
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Getting log levels failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Gets the tags that are/were associated with log messages.
	/// </summary>
	/// <param name="usedOnly">
	/// <c>true</c> to get the tags that are referenced by messages in the log file only;<br/>
	/// <c>false</c> to get all tags (even if referencing log messages have been removed after clearing/pruning).
	/// </param>
	/// <returns>A list of tags.</returns>
	public string[] GetTags(bool usedOnly)
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.GetTags(usedOnly);
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Getting tags failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Removes all log messages from the log file.
	/// </summary>
	/// <param name="messagesOnly">
	/// <c>true</c> to remove messages only;<br/>
	/// <c>false</c> to remove processes, applications, log writers, log levels and tags as well (default).
	/// </param>
	/// <param name="compact">
	/// <c>true</c> to compact the log file after clearing (default);<br/>
	/// <c>false</c> to clear the log file, but do not compact it.
	/// </param>
	/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
	/// <exception cref="LogFileException">Clearing the log file failed (see inner exception for details).</exception>
	public void Clear(bool messagesOnly = false, bool compact = true)
	{
		CheckDisposed();

		// determine the number of messages in the database
		long count = mDatabaseAccessor.MessageCount;

		try
		{
			// clear and shrink the log file
			mDatabaseAccessor.Clear(messagesOnly);
			if (compact) mDatabaseAccessor.Vacuum();
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Clearing failed: {ex.Message}",
				ex);
		}

		// tell the log message collection about the change
		mMessageCollection.ProcessMessagesRemoved(count, null);
	}

	/// <summary>
	/// Gets a number of log messages starting at the specified message id.
	/// </summary>
	/// <param name="fromId">Id of the message to start at.</param>
	/// <param name="count">Number of log messages to get.</param>
	/// <returns>The requested log messages.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromId"/> is not in the interval [OldestMessageId,NewestMessageId].</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> must be positive.</exception>
	/// <exception cref="LogFileException">Reading failed (see inner exception for details).</exception>
	/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
	public LogFileMessage[] Read(long fromId, int count)
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
	/// <returns>
	/// <c>true</c> if reading ran to completion;<br/>
	/// <c>false</c> if reading was cancelled.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromId"/> is not in the interval [OldestMessageId,NewestMessageId].</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> must be positive.</exception>
	/// <exception cref="LogFileException">Reading failed (see inner exception for details).</exception>
	/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
	public bool Read(long fromId, long count, ReadMessageCallback callback)
	{
		CheckDisposed();

		try
		{
			return mDatabaseAccessor.Read(fromId, count, callback);
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
	/// <exception cref="NotSupportedException">The file is read-only.</exception>
	/// <exception cref="LogFileException">Writing failed (see inner exception for details).</exception>
	public void Write(ILogMessage message)
	{
		if (message == null) throw new ArgumentNullException(nameof(message));

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
	/// <exception cref="NotSupportedException">The file is read-only.</exception>
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

		if (count > 0) mMessageCollection.ProcessMessageAdded((int)count);
	}

	/// <summary>
	/// Removes log messages that are above the specified message limit -or- older than the specified age.
	/// </summary>
	/// <param name="maximumMessageCount">
	/// Maximum number of messages to keep;
	/// -1 to disable removing messages by maximum message count.
	/// </param>
	/// <param name="minimumMessageTimestamp">
	/// Point in time (UTC) to keep messages after (includes the exact point in time);
	/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
	/// </param>
	/// <param name="compact">
	/// <c>true</c> to compact the log file after removing log messages;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	/// <returns>
	/// Number of removed messages.
	/// If <see cref="int.MaxValue"/> is returned <see cref="Prune(long, DateTime, bool)"/> should be called once again
	/// to ensure all messages matching the criteria are removed.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The message limit must be > 0 to limit the number of message or -1 to disable the limit.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
	/// <exception cref="NotSupportedException">The file is read-only.</exception>
	/// <exception cref="LogFileException">Cleaning up failed (see inner exception for details).</exception>
	public int Prune(
		long     maximumMessageCount,
		DateTime minimumMessageTimestamp,
		bool     compact)
	{
		if (maximumMessageCount < -1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maximumMessageCount),
				"The maximum message count must be > 0 to limit the number of messages or -1 to disable the limit.");
		}

		CheckDisposed();

		bool pruned;
		LogFileMessage[] removedMessages = null;
		int removedMessageCount;
		try
		{
			long oldestMessageId = mDatabaseAccessor.OldestMessageId;
			removedMessageCount = mMessageCollection.NeedsRemovedMessagesWhenPruning
				                      ? mDatabaseAccessor.Prune(maximumMessageCount, minimumMessageTimestamp, out removedMessages)
				                      : mDatabaseAccessor.Prune(maximumMessageCount, minimumMessageTimestamp);
			pruned = oldestMessageId != mDatabaseAccessor.OldestMessageId;
			if (compact && pruned) mDatabaseAccessor.Vacuum();
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Cleaning up failed: {ex.Message}",
				ex);
		}

		if (pruned)
			mMessageCollection.ProcessMessagesRemoved(removedMessageCount, removedMessages);

		return removedMessageCount;
	}

	/// <summary>
	/// Removes log messages that are above the specified message limit -or- older than the specified age.
	/// </summary>
	/// <param name="maximumMessageCount">
	/// Maximum number of messages to keep;
	/// -1 to disable removing messages by maximum message count.
	/// </param>
	/// <param name="minimumMessageTimestamp">
	/// Point in time (UTC) to keep messages after (includes the exact point in time);
	/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
	/// </param>
	/// <param name="compact">
	/// <c>true</c> to compact the log file after removing log messages;<br/>
	/// otherwise <c>false</c>.
	/// </param>
	/// <param name="removedMessages">Receives the log messages that have been removed.</param>
	/// <returns>
	/// Number of removed messages.
	/// If <see cref="int.MaxValue"/> is returned <see cref="Prune(long, DateTime, bool, out LogFileMessage[])"/>
	/// should be called once again to ensure all messages matching the criteria are removed.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The message limit must be > 0 to limit the number of message or -1 to disable the limit.
	/// </exception>
	/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
	/// <exception cref="NotSupportedException">The file is read-only.</exception>
	/// <exception cref="LogFileException">Cleaning up failed (see inner exception for details).</exception>
	public int Prune(
		long                 maximumMessageCount,
		DateTime             minimumMessageTimestamp,
		bool                 compact,
		out LogFileMessage[] removedMessages)
	{
		if (maximumMessageCount < -1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maximumMessageCount),
				"The maximum message count must be > 0 to limit the number of messages or -1 to disable the limit.");
		}

		CheckDisposed();

		bool pruned;
		int removedMessageCount;
		try
		{
			long oldestMessageId = mDatabaseAccessor.OldestMessageId;
			removedMessageCount = mDatabaseAccessor.Prune(maximumMessageCount, minimumMessageTimestamp, out removedMessages);
			pruned = oldestMessageId != mDatabaseAccessor.OldestMessageId;
			if (compact && pruned) mDatabaseAccessor.Vacuum();
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Cleaning up failed: {ex.Message}",
				ex);
		}

		if (pruned)
			mMessageCollection.ProcessMessagesRemoved(removedMessageCount, removedMessages);

		return removedMessageCount;
	}

	/// <summary>
	/// Compacts the log file.
	/// </summary>
	/// <exception cref="NotSupportedException">The file is read-only.</exception>
	public void Compact()
	{
		CheckDisposed();

		try
		{
			mDatabaseAccessor.Vacuum();
		}
		catch (SQLiteException ex)
		{
			throw new LogFileException(
				$"Saving snapshot failed: {ex.Message}",
				ex);
		}
	}

	/// <summary>
	/// Saves a snapshot of the log file.
	/// </summary>
	/// <param name="path">Path of the file to save the snapshot to.</param>
	/// <exception cref="ObjectDisposedException">The log file has been disposed.</exception>
	/// <exception cref="NotSupportedException">The file is read-only.</exception>
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
		if (mDatabaseAccessor == null) throw new ObjectDisposedException($"Log File ({mFilePath})");
	}

	#endregion
}
