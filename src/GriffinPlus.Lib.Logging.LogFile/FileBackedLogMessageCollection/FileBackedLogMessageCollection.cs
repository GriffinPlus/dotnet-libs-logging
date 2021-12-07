///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;

using GriffinPlus.Lib.Collections;

// ReSharper disable ExplicitCallerInfoArgument

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// A log message collection that uses a log file to keep log entries that are currently not needed
	/// (frequently used log messages are cached to reduce i/o load).
	/// </summary>
	public partial class FileBackedLogMessageCollection : LogMessageCollectionBase<LogMessage>
	{
		#region Defaults / Class Variables

		/// <summary>
		/// The default number of cache pages.
		/// </summary>
		internal const int DefaultMaxCachePageCount = 20;

		/// <summary>
		/// The default capacity of cache pages.
		/// </summary>
		internal const int DefaultCachePageCapacity = 100;

		/// <summary>
		/// The number of log messages to copy at once when copying them from one collection to another.
		/// </summary>
		internal const int CopySliceSize = 1000;

		/// <summary>
		/// A dummy message that is used as a placeholder for real messages, if <see cref="ReturnDummyMessagesWhenPruning"/>
		/// is set to <c>true</c> when pruning old log messages.
		/// </summary>
		private static readonly LogFileMessage sDummyMessage = (LogFileMessage)new LogFileMessage().InitWith(
				-1,
				DateTimeOffset.Parse("2000-01-01T00:00:00Z"),
				-1,
				0,
				"Dummy",
				"Dummy",
				TagSet.Empty,
				"Dummy",
				"Dummy",
				0,
				"Dummy")
			.Protect();

		#endregion

		#region Member Variables

		private readonly LinkedList<CachePage> mCachePages = new LinkedList<CachePage>();
		private          long                  mCacheStartMessageId;
		private          int                   mMaxCachePageCount = DefaultMaxCachePageCount;
		private          int                   mCachePageCapacity = DefaultCachePageCapacity;
		private          int                   mChangeCounter;

		#endregion

		#region Construction and Disposal

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogMessageCollection"/> class backed by
		/// a <see cref="LogFile"/>.
		/// </summary>
		/// <param name="file">Log file to work on.</param>
		/// <exception cref="ArgumentNullException"><paramref name="file"/> is <c>null</c>.</exception>
		internal FileBackedLogMessageCollection(LogFile file)
		{
			LogFile = file ?? throw new ArgumentNullException(nameof(file));
			mCacheStartMessageId = LogFile.OldestMessageId;
			if (mCacheStartMessageId < 0) mCacheStartMessageId = 0;

			// populate overview collections
			foreach (string name in LogFile.GetLogWriterNames(true)) UsedLogWritersWritable.Add(name);
			foreach (string name in LogFile.GetLogLevelNames(true)) UsedLogLevelsWritable.Add(name);
			foreach (string name in LogFile.GetTags(true)) UsedTagsWritable.Add(name);
			foreach (string name in LogFile.GetApplicationNames(true)) UsedApplicationNamesWritable.Add(name);
			foreach (string name in LogFile.GetProcessNames(true)) UsedProcessNamesWritable.Add(name);
			foreach (int id in LogFile.GetProcessIds()) UsedProcessIdsWritable.Add(id);
		}

		/// <summary>
		/// Disposes the collection closing the underlying file.
		/// </summary>
		/// <param name="disposing">
		/// true if the object is being disposed;
		/// false, if it is being finalized.
		/// </param>
		protected override void Dispose(bool disposing)
		{
			string path = LogFile.FilePath;
			LogFile.Dispose();
			if (AutoDelete)
			{
				try { File.Delete(path); }
				catch
				{
					/* swallow */
				}
			}

			mCachePages.Clear();
		}

		#endregion

		#region Creating a Collection

		/// <summary>
		/// Creates a <see cref="FileBackedLogMessageCollection"/> backed by the specified log file and populates it with the specified messages.
		/// The log file must not exist, yet.
		/// </summary>
		/// <param name="path">Path of the log file to create.</param>
		/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
		/// <param name="mode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		/// <param name="messages">Messages to populate the log file with (may be null).</param>
		/// <exception cref="LogFileException">Creating the log file failed (see message and inner exception for details).</exception>
		public static FileBackedLogMessageCollection Create(
			string                   path,
			LogFilePurpose           purpose,
			LogFileWriteMode         mode,
			IEnumerable<ILogMessage> messages = null)
		{
			return LogFile.Create(path, purpose, mode, messages).Messages;
		}

		/// <summary>
		/// Creates a <see cref="FileBackedLogMessageCollection"/> backed by the specified log file.
		/// The log file is created, if it does not exist, yet.
		/// </summary>
		/// <param name="path">Path of the log file to open/create.</param>
		/// <param name="purpose">
		/// Purpose of the log file determining whether the log file is primarily used for recording or for analysis
		/// (does not have any effect, if the log file exists already).
		/// </param>
		/// <param name="mode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
		public static FileBackedLogMessageCollection OpenOrCreate(
			string           path,
			LogFilePurpose   purpose,
			LogFileWriteMode mode)
		{
			return LogFile.OpenOrCreate(path, purpose, mode).Messages;
		}

		/// <summary>
		/// Creates a <see cref="FileBackedLogMessageCollection"/> backed by an existing log file.
		/// The log file is opened for reading and writing.
		/// </summary>
		/// <param name="path">Path of the log file to open.</param>
		/// <param name="mode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		/// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
		/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
		public static FileBackedLogMessageCollection Open(string path, LogFileWriteMode mode)
		{
			return LogFile.Open(path, mode).Messages;
		}

		/// <summary>
		/// Creates a <see cref="FileBackedLogMessageCollection"/> backed by an existing log file.
		/// The log file is opened read-only.
		/// </summary>
		/// <param name="path">Path of the log file to open.</param>
		/// <exception cref="FileNotFoundException">The specified file does not exist.</exception>
		/// <exception cref="LogFileException">Opening the log file failed (see message and inner exception for details).</exception>
		public static FileBackedLogMessageCollection OpenReadOnly(string path)
		{
			return LogFile.OpenReadOnly(path).Messages;
		}

		#endregion

		#region Creating Temporary Collections

		/// <summary>
		/// Creates a new instance of the <see cref="FileBackedLogMessageCollection"/> with a file in the temporary directory
		/// optionally marking the file for auto-deletion.
		/// </summary>
		/// <param name="deleteAutomatically">
		/// true to delete the file automatically when the collection is disposed (or the next time, a temporary collection is created in the same directory);
		/// false to keep it after the collection is disposed.
		/// </param>
		/// <param name="temporaryDirectoryPath">
		/// Path of the temporary directory to use;
		/// null to use the default temporary directory (default).
		/// </param>
		/// <param name="purpose">
		/// Purpose of the log file determining whether the log file is primarily used for recording or for analysis (default).
		/// </param>
		/// <param name="mode">
		/// Write mode determining whether to open the log file in 'robust' or 'fast' mode (default).
		/// </param>
		/// <param name="messages">Messages to populate the temporary collection with (may be null).</param>
		/// <returns>The created collection.</returns>
		public static FileBackedLogMessageCollection CreateTemporaryCollection(
			bool                     deleteAutomatically,
			string                   temporaryDirectoryPath = null,
			LogFilePurpose           purpose                = LogFilePurpose.Analysis,
			LogFileWriteMode         mode                   = LogFileWriteMode.Fast,
			IEnumerable<ILogMessage> messages               = null)
		{
			string path = TemporaryFileManager.GetTemporaryFileName(deleteAutomatically, temporaryDirectoryPath);
			var file = LogFile.Create(path, purpose, mode, messages);
			file.Messages.AutoDelete = deleteAutomatically;
			return file.Messages;
		}

		#endregion

		#region Connection

		/// <summary>
		/// Gets the sqlite database connection the log file works on.
		/// </summary>
		public SQLiteConnection Connection => LogFile.Connection;

		#endregion

		#region FilePath

		/// <summary>
		/// Gets the path of the log file backing the collection.
		/// </summary>
		public string FilePath => LogFile.FilePath;

		#endregion

		#region LogFile

		/// <summary>
		/// Gets the log file backing the collection.
		/// </summary>
		public LogFile LogFile { get; }

		#endregion

		#region AutoDelete

		/// <summary>
		/// Gets or sets a value indicating whether the backing log file is deleted when the collection is disposed.
		/// </summary>
		public bool AutoDelete { get; set; }

		#endregion

		#region MaxCachePageCount

		/// <summary>
		/// Gets or sets the maximum number of cache pages buffering messages in memory.
		/// </summary>
		public int MaxCachePageCount
		{
			get => mMaxCachePageCount;
			set
			{
				if (value < 0) throw new ArgumentException("The maximum cache page size must be positive.");

				if (mMaxCachePageCount != value)
				{
					mMaxCachePageCount = value;

					// remove pages that are not needed any more
					while (mCachePages.Count > mMaxCachePageCount) mCachePages.RemoveLast();
				}
			}
		}

		#endregion

		#region CachePageCapacity

		/// <summary>
		/// Gets or sets the capacity of the cache pages (in number of messages).
		/// </summary>
		public int CachePageCapacity
		{
			get => mCachePageCapacity;
			set
			{
				if (value < 0) throw new ArgumentException("The cache page capacity must be positive.");

				if (mCachePageCapacity != value)
				{
					mCachePageCapacity = value;

					// changing the page capacity changes the alignment of pages
					// => discard cache
					mCachePages.Clear();
				}
			}
		}

		#endregion

		#region IsReadOnly

		/// <summary>
		/// Gets a value indicating whether the collection is read-only.
		/// </summary>
		public override bool IsReadOnly => LogFile.IsReadOnly;

		#endregion

		#region Count

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		public override long Count => LogFile.MessageCount;

		#endregion

		#region ReturnDummyMessagesWhenPruning

		/// <summary>
		/// Gets or sets a value determining whether the collection returns dummy messages when pruning the backing log file.
		/// This is <c>false</c> by default to provide the behavior expected by users of the <see cref="INotifyCollectionChanged"/> interface.
		/// It can be set to <c>true</c> to avoid reading messages that are about to be removed anyway.
		/// </summary>
		public bool ReturnDummyMessagesWhenPruning { get; set; } = false;

		#endregion

		#region Indexer

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
		public override LogMessage this[long index]
		{
			get
			{
				if (index < 0 || index >= LogFile.MessageCount) throw new ArgumentOutOfRangeException(nameof(index));
				long messageId = LogFile.OldestMessageId + index;
				return GetMessage(messageId);
			}
		}

		#endregion

		#region GetEnumerator()

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		/// <returns>Enumerator for iterating over the collection.</returns>
		public override IEnumerator<LogMessage> GetEnumerator()
		{
			return new Enumerator(this);
		}

		#endregion

		#region Contains()

		/// <summary>
		/// Checks whether the collection contains the specified log message
		/// (the specified message must be associated with the collection as the check works with message ids).
		/// </summary>
		/// <param name="message">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		public override bool Contains(LogMessage message)
		{
			if (message is LogFileMessage fileLogMessage)
			{
				// the message cannot be in the collection, if the message id is not in the range between
				// the oldest and the newest message
				long oldestMessageId = LogFile.OldestMessageId;
				long newestMessageId = LogFile.NewestMessageId;
				if (fileLogMessage.Id < oldestMessageId || fileLogMessage.Id > newestMessageId)
					return false;

				// message could be in the collection, check whether the specified message equals the message in
				// the collection with the same id
				var other = GetMessage(fileLogMessage.Id);
				return message.Equals(other);
			}

			return false;
		}

		#endregion

		#region IndexOf()

		/// <summary>
		/// Gets the index of the specified log message
		/// (the specified message must be associated with the collection as the check works with message ids).
		/// </summary>
		/// <param name="message">Log message to locate in the collection (may be null).</param>
		/// <returns>
		/// Index of the log message;
		/// -1, if the specified message is not in the collection.
		/// </returns>
		public override long IndexOf(LogMessage message)
		{
			if (message is LogFileMessage fileLogMessage)
			{
				// the message cannot be in the collection, if the message id is not in the range between
				// the oldest and the newest message
				long oldestMessageId = LogFile.OldestMessageId;
				long newestMessageId = LogFile.NewestMessageId;
				if (fileLogMessage.Id < oldestMessageId || fileLogMessage.Id > newestMessageId)
					return -1;

				// message could be in the collection, check whether the specified message equals the message in
				// the collection with the same id
				var other = GetMessage(fileLogMessage.Id);
				if (message.Equals(other)) return other.Id - oldestMessageId;
			}

			// the collection does not contain the message
			return -1;
		}

		#endregion

		#region Add()

		/// <summary>
		/// Adds a log message to the collection
		/// (does not add the message itself, it simply writes the message into the underlying file; a following read will return a new instance)
		/// </summary>
		/// <param name="message">Log message to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public override void Add(LogMessage message)
		{
			if (message == null) throw new ArgumentNullException(nameof(message));
			if (IsReadOnly) throw new NotSupportedException("The collection is read-only.");
			LogFile.Write(message);
		}

		#endregion

		#region AddRange()

		/// <summary>
		/// Adds multiple log messages to the collection at once
		/// (does not add the messages themselves, it simply writes the messages into the underlying file; a following read will return a new instance)
		/// </summary>
		/// <param name="messages">Log messages to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="messages"/> is <c>null</c>.</exception>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public override void AddRange(IEnumerable<LogMessage> messages)
		{
			if (messages == null) throw new ArgumentNullException(nameof(messages));
			if (IsReadOnly) throw new NotSupportedException("The collection is read-only.");
			LogFile.Write(messages);
		}

		#endregion

		#region Clear()

		/// <summary>
		/// Removes all log messages from the collection.
		/// </summary>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public override void Clear()
		{
			if (IsReadOnly) throw new NotSupportedException("The collection is read-only.");

			if (Count > 0)
			{
				LogFile.Clear(false, true); // invokes callback that raises the appropriate events
			}
		}

		#endregion

		#region Prune()

		/// <summary>
		/// Removes the oldest log messages that are above the specified message limit -or- log messages that are older
		/// than the specified age.
		/// </summary>
		/// <param name="maximumMessageCount">
		/// Maximum number of messages to keep;
		/// -1 to disable removing messages by maximum message count.
		/// </param>
		/// <param name="minimumMessageTimestamp">
		/// Point in time (UTC) to keep messages after (includes the exact point in time);
		/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The message limit must be > 0 to limit the number of message or -1 to disable the limit.
		/// </exception>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public override void Prune(long maximumMessageCount, DateTime minimumMessageTimestamp)
		{
			if (IsReadOnly) throw new NotSupportedException("The collection is read-only.");
			LogFile.Prune(maximumMessageCount, minimumMessageTimestamp, false); // invokes callback that raises the appropriate events
		}

		#endregion

		#region CopyTo()

		/// <summary>
		/// Copies all log messages into the specified array.
		/// </summary>
		/// <param name="array">Array to copy the log messages into.</param>
		/// <param name="arrayIndex">Index in the array to start copying to.</param>
		/// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException"><paramref name="array"/> is no a one-dimensional array or the array is too small to store all messages.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is out of bounds.</exception>
		public override void CopyTo(LogMessage[] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException(nameof(array));
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "The array index is negative.");
			if (array.Rank != 1) throw new ArgumentException("The specified array is multi-dimensional.");
			if (arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "The array index is outside the specified array.");
			if (Count > array.Length - arrayIndex) throw new ArgumentException("The specified array is too small to receive all log messages.");

			int currentArrayIndex = arrayIndex;
			long firstId = LogFile.OldestMessageId;
			long lastId = LogFile.NewestMessageId;

			// abort, if the collection is empty
			if (firstId < 0) return;

			for (long id = firstId; id <= lastId; id++)
			{
				array[currentArrayIndex++] = GetMessage(id);
			}
		}

		/// <summary>
		/// Adds the specified range of messages to the specified collection (does not influence cached log messages).
		/// </summary>
		/// <param name="destination">Collection to copy log messages into.</param>
		/// <param name="firstIndex">Index of the first message to copy.</param>
		/// <param name="count">Number of messages to copy.</param>
		/// <exception cref="ArgumentNullException"><paramref name="destination"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="firstIndex"/> or <paramref name="firstIndex"/> + <paramref name="count"/> is out of bounds.
		/// </exception>
		public void CopyTo(FileBackedLogMessageCollection destination, int firstIndex, int count)
		{
			if (destination == null) throw new ArgumentNullException(nameof(destination));
			if (firstIndex < 0 || firstIndex >= Count) throw new ArgumentOutOfRangeException(nameof(firstIndex));
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
			if ((long)firstIndex + count > Count) throw new ArgumentOutOfRangeException(nameof(count));

			int index = firstIndex;
			int remaining = count;
			while (index < firstIndex + count)
			{
				int copyCount = Math.Min(CopySliceSize, remaining);
				var messages = LogFile.Read(LogFile.OldestMessageId + index, copyCount);
				destination.AddRange(messages);
				index += copyCount;
				remaining -= copyCount;
			}
		}

		#endregion

		#region GetFilteringAccessor()

		/// <summary>
		/// Gets a filtering accessor that matches log messages using the specified filter.
		/// </summary>
		/// <param name="filter">Filter to use.</param>
		/// <returns>The filtering accessor.</returns>
		public FileBackedLogMessageCollectionFilteringAccessor GetFilteringAccessor(IFileBackedLogMessageCollectionFilter filter)
		{
			return new FileBackedLogMessageCollectionFilteringAccessor(this, filter);
		}

		#endregion

		#region Interaction with the LogFile Class

		/// <summary>
		/// Gets a value indicating whether the collection needs the removed messages when pruning the old log messages from the backing log file.
		/// </summary>
		internal bool NeedsRemovedMessagesWhenPruning => IsCollectionChangedRegistered && !ReturnDummyMessagesWhenPruning;

		/// <summary>
		/// Is called when the underlying file removes messages.
		/// </summary>
		/// <param name="count">Number of messages that has been removed.</param>
		/// <param name="removedMessages">The messages that have been removed (may be <c>null</c>).</param>
		internal void ProcessMessagesRemoved(long count, LogFileMessage[] removedMessages)
		{
			Debug.Assert(removedMessages == null || removedMessages.Length == count);

			mChangeCounter++;

			if (LogFile.MessageCount == 0)
			{
				// the collection has been cleared entirely
				// => flush the cache to discard buffered messages
				mCachePages.Clear();
				mCacheStartMessageId = 0; // inserting the next message will start at message id 0

				// notify clients about the change
				if (IsCollectionChangedRegistered)
					OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
			}
			else
			{
				// some message might have been removed from the log file
				// => flush cache pages that contain only messages that have been removed
				var node = mCachePages.First;
				while (node != null)
				{
					long firstMessageIdInPage = node.Value.FirstMessageId;
					long lastMessageIdInPage = firstMessageIdInPage + node.Value.Messages.Count - 1;
					bool pageContainsExistingMessages = firstMessageIdInPage >= LogFile.OldestMessageId && lastMessageIdInPage <= LogFile.NewestMessageId;
					if (pageContainsExistingMessages)
					{
						// the page contains at least one message that is still in the log file
						// => keep the page
						node = node.Next;
					}
					else
					{
						// the page does not contain any messages that are still in the log file
						// => remove the page
						var next = node.Next;
						mCachePages.Remove(node);
						node = next;
					}
				}

				// notify clients about the change
				if (IsCollectionChangedRegistered)
				{
					if (removedMessages != null)
					{
						OnCollectionChanged(
							new NotifyCollectionChangedEventArgs(
								NotifyCollectionChangedAction.Remove,
								removedMessages,
								0));
					}
					else
					{
						OnCollectionChanged(
							new NotifyCollectionChangedEventArgs(
								NotifyCollectionChangedAction.Remove,
								new FixedItemReadOnlyList<LogFileMessage>(sDummyMessage, (int)count),
								0));
					}
				}
			}

			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");

			// update overview collections
			SynchronizeOverviewCollection(UsedLogWritersWritable, LogFile.GetLogWriterNames(true));
			SynchronizeOverviewCollection(UsedLogLevelsWritable, LogFile.GetLogLevelNames(true));
			SynchronizeOverviewCollection(UsedTagsWritable, LogFile.GetTags(true));
			SynchronizeOverviewCollection(UsedApplicationNamesWritable, LogFile.GetApplicationNames(true));
			SynchronizeOverviewCollection(UsedProcessNamesWritable, LogFile.GetProcessNames(true));
			SynchronizeOverviewCollection(UsedProcessIdsWritable, LogFile.GetProcessIds());
		}

		/// <summary>
		/// Is called when the underlying log file adds messages.
		/// </summary>
		internal void ProcessMessageAdded(int count)
		{
			mChangeCounter++;

			// read changed messages, but do not keep these messages in the cache
			// (a continuous update would drop the performance since frequently requested messages are kicked out the cache)
			var messages = LogFile.Read(LogFile.NewestMessageId - count + 1, count);

			// notify clients about the change
			if (IsCollectionChangedRegistered)
			{
				if (UseMultiItemNotifications)
				{
					OnCollectionChanged(
						new NotifyCollectionChangedEventArgs(
							NotifyCollectionChangedAction.Add,
							messages,
							(int)(LogFile.NewestMessageId - LogFile.OldestMessageId - count + 1)));
				}
				else
				{
					for (int i = 0; i < messages.Length; i++)
					{
						var message = messages[i];

						OnCollectionChanged(
							new NotifyCollectionChangedEventArgs(
								NotifyCollectionChangedAction.Add,
								message,
								(int)(LogFile.NewestMessageId - LogFile.OldestMessageId - count + i + 1)));
					}
				}
			}

			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");

			// update overview collections
			AddToOverviewCollection(UsedLogWritersWritable, messages.Select(x => x.LogWriterName));
			AddToOverviewCollection(UsedLogLevelsWritable, messages.Select(x => x.LogLevelName));
			AddToOverviewCollection(UsedTagsWritable, messages.SelectMany(x => x.Tags));
			AddToOverviewCollection(UsedApplicationNamesWritable, messages.Select(x => x.ApplicationName));
			AddToOverviewCollection(UsedProcessNamesWritable, messages.Select(x => x.ProcessName));
			AddToOverviewCollection(UsedProcessIdsWritable, messages.Select(x => x.ProcessId));
		}

		/// <summary>
		/// Synchronizes the specified overview collection with the specified item set.
		/// This removes items from the overview collection that are not in the specified item set
		/// and adds items that are not in the item set, yet.
		/// </summary>
		/// <typeparam name="T">Type of items in the overview collection.</typeparam>
		/// <param name="collection">The overview collection.</param>
		/// <param name="items">New items set.</param>
		private static void SynchronizeOverviewCollection<T>(ObservableCollection<T> collection, T[] items)
		{
			// remove items from the collection that are not in the new item set
			var itemSet = new HashSet<T>(items);
			for (int i = collection.Count - 1; i >= 0; i--)
			{
				if (!itemSet.Contains(collection[i]))
					collection.RemoveAt(i);
			}

			// add new items that are not in the collection, yet
			itemSet = new HashSet<T>(collection);
			foreach (var item in items)
			{
				if (!itemSet.Contains(item))
				{
					itemSet.Add(item);
					collection.Add(item);
				}
			}
		}

		/// <summary>
		/// Adds items to the specified overview collection, if the collection does not contain them, yet.
		/// </summary>
		/// <typeparam name="T">Type of the items in the overview collection.</typeparam>
		/// <param name="collection">The overview collection.</param>
		/// <param name="items">Items to add.</param>
		private static void AddToOverviewCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
		{
			// add new items that are not in the collection, yet
			var itemSet = new HashSet<T>(collection);
			foreach (var item in items)
			{
				if (!itemSet.Contains(item))
				{
					itemSet.Add(item);
					collection.Add(item);
				}
			}
		}

		#endregion

		#region Caching Messages

		/// <summary>
		/// Loads the page containing the message with the specified id into the cache.
		/// </summary>
		/// <param name="id">Id of the message to load.</param>
		private LogFileMessage GetMessage(long id)
		{
			// Debug.WriteLine("Fetching message: {0}", id);

			long firstMessageId;
			LogFileMessage[] messages;
			var node = mCachePages.First;
			while (node != null)
			{
				firstMessageId = node.Value.FirstMessageId;
				if (id >= firstMessageId)
				{
					if (id < firstMessageId + node.Value.Messages.Count)
					{
						// found message in the cache
						// => return cached message
						mCachePages.Remove(node);
						mCachePages.AddFirst(node);
						return node.Value.Messages[(int)(id - firstMessageId)];
					}

					if (id < firstMessageId + mCachePageCapacity)
					{
						// message should be in the page, but page is not loaded entirely
						// => update page and return message
						messages = LogFile.Read(firstMessageId + node.Value.Messages.Count, mCachePageCapacity - node.Value.Messages.Count);
						node.Value.Messages.AddRange(messages);
						mCachePages.Remove(node);
						mCachePages.AddFirst(node);
						return node.Value.Messages[(int)(id - firstMessageId)];
					}
				}

				node = node.Next;
			}

			// cache does not contain the page with the requested message
			// => insert page into the cache
			firstMessageId = mCacheStartMessageId + mCachePageCapacity * ((id - mCacheStartMessageId) / mCachePageCapacity);
			if (mCachePages.Count >= mMaxCachePageCount)
			{
				// cache is full
				// => remove least requested page, keep specified page instead.
				messages = LogFile.Read(firstMessageId >= LogFile.OldestMessageId ? firstMessageId : LogFile.OldestMessageId, mCachePageCapacity);
				node = mCachePages.Last;
				node.Value.FirstMessageId = firstMessageId;
				node.Value.Messages.Clear();
				for (int i = 0; i < messages[0].Id - firstMessageId; i++) node.Value.Messages.Add(null);
				node.Value.Messages.AddRange(messages);
				mCachePages.RemoveLast();
				mCachePages.AddFirst(node);
				return node.Value.Messages[(int)(id - firstMessageId)];
			}

			// cache is not full, yet
			// => add page...
			var page = new CachePage(firstMessageId, mCachePageCapacity);
			messages = LogFile.Read(firstMessageId >= LogFile.OldestMessageId ? firstMessageId : LogFile.OldestMessageId, mCachePageCapacity);
			for (int i = 0; i < messages[0].Id - firstMessageId; i++) page.Messages.Add(null);
			page.Messages.AddRange(messages);
			mCachePages.AddFirst(page);
			return page.Messages[(int)(id - firstMessageId)];
		}

		#endregion
	}

}
