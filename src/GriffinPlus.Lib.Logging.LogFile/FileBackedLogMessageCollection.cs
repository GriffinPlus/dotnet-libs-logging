///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message collection that uses a log file to keep log entries that are currently not needed
	/// (the most frequently used log messages are cached to reduce i/o load).
	/// </summary>
	public partial class FileBackedLogMessageCollection : ILogMessageCollection<LogMessage>
	{
		#region Defaults / Class Variables

		/// <summary>
		/// The default number of cache pages.
		/// </summary>
		private const int DefaultMaxCachePageCount = 20;

		/// <summary>
		/// The default capacity of cache pages.
		/// </summary>
		private const int DefaultCachePageCapacity = 100;

		/// <summary>
		/// The number of log messages to copy at once when copying them from one collection to another.
		/// </summary>
		private const int CopySliceSize = 1000;

		/// <summary>
		/// Regex matching the name of a temporary log file that should be deleted automatically when not used any more.
		/// </summary>
		private static readonly Regex sAutoDeleteFileRegex = new Regex(
			@"^\[LOG-BUFFER\] (?<guid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}) \[AUTO DELETE\]$",
			RegexOptions.Compiled);

		#endregion

		#region Member Variables

		private readonly LinkedList<CachePage> mCachePages = new LinkedList<CachePage>();
		private          long                  mCacheStartMessageId;
		private          int                   mMaxCachePageCount = DefaultMaxCachePageCount;
		private          int                   mCachePageCapacity = DefaultCachePageCapacity;
		private          int                   mChangeCounter;
		private          bool                  mAutoDelete;

		#endregion

		#region Events

		/// <summary>
		/// Occurs when the collection has changed.
		/// </summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary>
		/// Occurs when a property has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		#region Construction and Disposal

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogMessageCollection" /> class.
		/// </summary>
		/// <param name="path">Path of the log file to open/create.</param>
		/// <param name="purpose">Purpose of the log file determining whether the log file is primarily used for recording or for analysis.</param>
		/// <param name="mode">Write mode determining whether to open the log file in 'robust' or 'fast' mode.</param>
		public FileBackedLogMessageCollection(
			string           path,
			LogFilePurpose   purpose = LogFilePurpose.Analysis,
			LogFileWriteMode mode    = LogFileWriteMode.Fast)
		{
			LogFile = new LogFile(path, purpose, mode, this);
			FilePath = LogFile.FilePath;
			mCacheStartMessageId = LogFile.OldestMessageId;
			if (mCacheStartMessageId < 0) mCacheStartMessageId = 0;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogMessageCollection" /> class.
		/// </summary>
		/// <param name="file">Log file to work on.</param>
		public FileBackedLogMessageCollection(LogFile file)
		{
			LogFile = file ?? throw new ArgumentNullException(nameof(file));
			FilePath = LogFile.FilePath;
			mCacheStartMessageId = LogFile.OldestMessageId;
			if (mCacheStartMessageId < 0) mCacheStartMessageId = 0;
		}

		/// <summary>
		/// Disposes the collection closing the underlying file.
		/// </summary>
		public void Dispose()
		{
			LogFile.Dispose();
			mCachePages.Clear();
			if (mAutoDelete)
			{
				try { File.Delete(LogFile.FilePath); }
				catch
				{
					/* swallow */
				}
			}
		}

		#endregion

		#region Creating/Managing Temporary Collections

		/// <summary>
		/// Creates a new instance of the <see cref="FileBackedLogMessageCollection" /> with a file in the temporary directory
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
		/// <returns>The created collection.</returns>
		public static FileBackedLogMessageCollection CreateTemporaryCollection(
			bool             deleteAutomatically,
			string           temporaryDirectoryPath = null,
			LogFilePurpose   purpose                = LogFilePurpose.Analysis,
			LogFileWriteMode mode                   = LogFileWriteMode.Fast)
		{
			// init temporary directory path, if not specified explicitly
			if (temporaryDirectoryPath == null) temporaryDirectoryPath = Path.GetTempPath();

			// delete temporary files that are not needed any more
			CleanupTemporaryDirectory(temporaryDirectoryPath);

			// create a collection with a temporary database backing the collection
			string path = Path.Combine(temporaryDirectoryPath, "[LOG-BUFFER] " + Guid.NewGuid().ToString("D").ToUpper());
			if (deleteAutomatically) path += " [AUTO DELETE]";
			var collection = new FileBackedLogMessageCollection(path, purpose, mode) { mAutoDelete = deleteAutomatically };
			return collection;
		}

		/// <summary>
		/// Scans the specified directory for orphaned temporary files that are marked for auto-deletion, but have not been deleted, yet.
		/// </summary>
		/// <param name="directoryPath">Path of the directory to scan.</param>
		private static void CleanupTemporaryDirectory(string directoryPath)
		{
			try
			{
				foreach (string filePath in Directory.GetFiles(directoryPath))
				{
					string fileName = Path.GetFileName(filePath);
					var match = sAutoDeleteFileRegex.Match(fileName);
					if (match.Success)
					{
						try { File.Delete(filePath); }
						catch
						{
							/* swallow */
						}
					}
				}
			}
			catch
			{
				// some error regarding the directory itself occurred
				// => swallow
			}
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the path of the log file backing the collection.
		/// </summary>
		public string FilePath { get; }

		/// <summary>
		/// Gets the log file backing the collection.
		/// </summary>
		public LogFile LogFile { get; }

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

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		public long Count => LogFile.MessageCount;

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="NotSupportedException">Setting a log message is not supported.</exception>
		public LogMessage this[long index]
		{
			get
			{
				long messageId = LogFile.OldestMessageId + index;
				return GetMessage(messageId);
			}

			set => throw new NotSupportedException("Setting a log message is not supported.");
		}

		#endregion

		#region Implementation of IEnumerable, ICollection and IList

		/// <summary>
		/// Gets a value indicating whether the collection is synchronized (always false).
		/// </summary>
		bool ICollection.IsSynchronized => false;

		/// <summary>
		/// Gets an object that can be used to synchronize the collection.
		/// </summary>
		object ICollection.SyncRoot => this;

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		/// <exception cref="LogFileTooLargeException">The log message contains too many messages to be handled as a regular collection..</exception>
		int ICollection<LogMessage>.Count
		{
			get
			{
				long count = LogFile.MessageCount;
				if (count > int.MaxValue) ThrowLogFileTooLargeException();
				return (int)count;
			}
		}

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		/// <exception cref="LoggingException">The log message contains too many messages to be handled as a regular collection.</exception>
		int ICollection.Count
		{
			get
			{
				long count = LogFile.MessageCount;
				if (count > int.MaxValue) ThrowLogFileTooLargeException();
				return (int)count;
			}
		}

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="NotSupportedException">Setting a log message is not supported.</exception>
		object IList.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException("Setting a log message is not supported.");
		}

		/// <summary>
		/// Gets a value indicating whether the collection is read-only (always false).
		/// </summary>
		public bool IsReadOnly => false;

		/// <summary>
		/// Gets a value indicating whether the collection is of fixed size (always false).
		/// </summary>
		bool IList.IsFixedSize => false;

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return new Enumerator(this);
		}

		/// <summary>
		/// Checks whether the collection contains the specified log message.
		/// </summary>
		/// <param name="item">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		public bool Contains(object item)
		{
			return Contains((LogMessage)item);
		}

		/// <summary>
		/// Gets the index of the specified log message.
		/// </summary>
		/// <param name="item">Log message to locate in the collection.</param>
		/// <returns>Index of the log message; -1, if the specified message is not in the collection.</returns>
		int IList.IndexOf(object item)
		{
			return IndexOf((LogMessage)item);
		}

		/// <summary>
		/// Adds a log message to the collection.
		/// </summary>
		/// <param name="item">Log message to add.</param>
		int IList.Add(object item)
		{
			long index = LogFile.MessageCount;
			if (index >= int.MaxValue) ThrowLogFileTooLargeException();
			Add((LogMessage)item);
			return (int)index;
		}

		/// <summary>
		/// Inserts a log message at the specified position (not supported).
		/// </summary>
		/// <param name="index">The zero-based index at which the log message should be inserted.</param>
		/// <param name="item">The log message to insert into the collection.</param>
		/// <exception cref="NotSupportedException">Inserting is not supported.</exception>
		void IList.Insert(int index, object item)
		{
			Insert(index, (LogMessage)item);
		}

		/// <summary>
		/// Removes the specified log message from the collection (not supported).
		/// </summary>
		/// <param name="item">Log message to remove from the collection.</param>
		/// <returns>true, if the log message was removed; otherwise false.</returns>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		void IList.Remove(object item)
		{
			Remove((LogMessage)item);
		}

		/// <summary>
		/// Removes the log message at the specified index (not supported).
		/// </summary>
		/// <param name="index">Index of the log message to remove.</param>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		public void RemoveAt(int index)
		{
			// removing log messages is not supported, since this would mess up log message counting and ordering
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		/// <summary>
		/// Removes all log messages from the collection.
		/// </summary>
		public void Clear()
		{
			if (LogFile.MessageCount > 0)
			{
				LogFile.Clear();
				mChangeCounter++;
				mCachePages.Clear();

				var handler = CollectionChanged;
				handler?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

				OnPropertyChanged("Count");
				OnPropertyChanged("Item[]");
			}
		}

		/// <summary>
		/// Copies all log messages into the specified array.
		/// </summary>
		/// <param name="array">Array to copy the log messages into.</param>
		/// <param name="arrayIndex">Index in the array to start copying to.</param>
		void ICollection.CopyTo(Array array, int arrayIndex)
		{
			CopyTo((LogMessage[])array, arrayIndex);
		}

		#endregion

		#region Implementation of IEnumerable<T>, ICollection<T> and IList<T>

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="NotSupportedException">Setting a log message is not supported.</exception>
		public LogMessage this[int index]
		{
			get
			{
				long messageId = LogFile.OldestMessageId + index;
				return GetMessage(messageId);
			}

			set => throw new NotSupportedException("Setting a log message is not supported.");
		}

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		/// <returns>Enumerator for iterating over the collection.</returns>
		IEnumerator<LogMessage> IEnumerable<LogMessage>.GetEnumerator()
		{
			return new Enumerator(this);
		}

		/// <summary>
		/// Checks whether the collection contains the specified log message
		/// (the specified message must be associated with the collection as the check works with message ids).
		/// </summary>
		/// <param name="item">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		public bool Contains(LogMessage item)
		{
			if (item == null) return false;
			long oldestMessageId = LogFile.OldestMessageId;
			long newestMessageId = LogFile.NewestMessageId;
			if (item.Id >= oldestMessageId && item.Id <= newestMessageId)
				return true;

			return false;
		}

		/// <summary>
		/// Gets the index of the specified log message
		/// (the specified message must be associated with the collection as the check works with message ids).
		/// </summary>
		/// <param name="item">Log message to locate in the collection (may be null).</param>
		/// <returns>
		/// Index of the log message;
		/// -1, if the specified message is not in the collection.
		/// </returns>
		public int IndexOf(LogMessage item)
		{
			if (item == null) return -1;
			long index = item.Id - LogFile.OldestMessageId;
			if (index >= int.MaxValue) ThrowLogFileTooLargeException();
			return (int)index;
		}

		/// <summary>
		/// Adds a log message to the collection
		/// (does not add the message itself, it simply writes the message into the underlying file; a following read will return a new instance)
		/// </summary>
		/// <param name="item">Log message to add.</param>
		public void Add(LogMessage item)
		{
			LogFile.Write(item);
		}

		/// <summary>
		/// Inserts a log message at the specified position (not supported).
		/// </summary>
		/// <param name="index">The zero-based index at which value should be inserted.</param>
		/// <param name="item">The log message to insert into the collection.</param>
		/// <exception cref="NotSupportedException">Inserting is not supported.</exception>
		public void Insert(int index, LogMessage item)
		{
			// inserting log messages is not supported as this would mess up message counting and ordering
			throw new NotSupportedException("Inserting log messages is not supported.");
		}

		/// <summary>
		/// Removes the specified log message from the collection (not supported).
		/// </summary>
		/// <param name="item">Log message to remove from the collection.</param>
		/// <returns>true, if the log message was removed; otherwise false.</returns>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		public bool Remove(LogMessage item)
		{
			// removing log messages is not supported as this would mess up message counting and ordering
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		/// <summary>
		/// Copies all log messages into the specified array.
		/// </summary>
		/// <param name="array">Array to copy the log messages into.</param>
		/// <param name="arrayIndex">Index in the array to start copying to.</param>
		public void CopyTo(LogMessage[] array, int arrayIndex)
		{
			if (Count > array.Length - arrayIndex)
				throw new ArgumentException("The specified array is too small to receive all log messages.");

			int currentArrayIndex = arrayIndex;
			long firstId = LogFile.OldestMessageId;
			long lastId = LogFile.NewestMessageId;

			for (long id = firstId; id <= lastId; id++)
			{
				array[currentArrayIndex++] = GetMessage(id);
			}
		}

		#endregion

		#region Bulk Operations

		/// <summary>
		/// Adds multiple log messages to the collection at once
		/// (does not influence cached log messages to avoid evicting cache pages that are still needed).
		/// </summary>
		/// <param name="items">Log messages to add.</param>
		public void AddRange(IEnumerable<LogMessage> items)
		{
			if (items == null) throw new ArgumentNullException(nameof(items));
			LogFile.Write(items);
		}

		/// <summary>
		/// Adds the specified range of messages to the specified collection (does not influence cached log messages).
		/// </summary>
		/// <param name="destination">Collection to copy log messages into.</param>
		/// <param name="firstIndex">Index of the first message to copy.</param>
		/// <param name="count">Number of messages to copy.</param>
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

		#region Event Raiser

		/// <summary>
		/// Raises the <see cref="PropertyChanged" /> event.
		/// </summary>
		/// <param name="name">Name of the property that has changed.</param>
		protected virtual void OnPropertyChanged(string name)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		#endregion

		#region Interaction with the LogFile Class

		/// <summary>
		/// Is called when the underlying file removes messages.
		/// </summary>
		internal void ProcessMessagesRemoved()
		{
			mChangeCounter++;

			if (LogFile.MessageCount == 0)
			{
				// the collection has been cleared entirely
				// => flush the cache to discard buffered messages
				mCachePages.Clear();
				mCacheStartMessageId = 0; // inserting the next message will start at message id 0
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
			}

			// notify clients about the change
			var handler = CollectionChanged;
			handler?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
		}

		/// <summary>
		/// Is called when the underlying log file adds messages.
		/// </summary>
		internal void ProcessMessageAdded(int count)
		{
			mChangeCounter++;

			// notify clients about the change
			var handler = CollectionChanged;
			if (handler != null)
			{
				var messages = LogFile.Read(LogFile.NewestMessageId - count + 1, count);

				// do not keep these messages in the cache
				// (a continuous update would drop the performance since frequently requested messages are kicked out the cache)

				// many WPF controls do not support multi-item adds, so adding messages one by one is necessary...
				foreach (var message in messages)
				{
					handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message));
				}
			}

			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
		}

		#endregion

		#region Caching Messages

		/// <summary>
		/// Loads the page containing the message with the specified id into the cache.
		/// </summary>
		/// <param name="id">Id of the message to load.</param>
		private LogMessage GetMessage(long id)
		{
			// Debug.WriteLine("Fetching message: {0}", id);

			long firstMessageId;
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
						var messages = LogFile.Read(firstMessageId + node.Value.Messages.Count, mCachePageCapacity - node.Value.Messages.Count);
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
			firstMessageId = LogFile.OldestMessageId + mCachePageCapacity * ((id - LogFile.OldestMessageId) / mCachePageCapacity);
			if (mCachePages.Count >= mMaxCachePageCount)
			{
				// cache is full
				// => remove least requested page, keep specified page instead.
				node = mCachePages.Last;
				node.Value.FirstMessageId = firstMessageId;
				node.Value.Messages.Clear();
				node.Value.Messages.AddRange(LogFile.Read(firstMessageId, mCachePageCapacity));
				mCachePages.RemoveLast();
				mCachePages.AddFirst(node);
				return node.Value.Messages[(int)(id - firstMessageId)];
			}

			// cache is not full, yet
			// => add page...
			var page = new CachePage(firstMessageId, mCachePageCapacity);
			page.Messages.AddRange(LogFile.Read(firstMessageId, mCachePageCapacity));
			mCachePages.AddFirst(page);
			return page.Messages[(int)(id - firstMessageId)];
		}

		#endregion

		#region Helpers

		/// <summary>
		/// Throws a <see cref="LogFileTooLargeException" /> indicating that the backing log file contains too many messages
		/// to be handled as a regular .NET collection that supports up to <seealso cref="int.MaxValue" /> items only.
		/// </summary>
		private void ThrowLogFileTooLargeException()
		{
			throw new LogFileTooLargeException("The backing log file contains too many messages to be handled as a regular collection.");
		}

		#endregion
	}

}
