///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// An accessor that provides direct access to a filtered file-backed log message set.
	/// These accessors always work in cooperation with <see cref="FileBackedLogMessageCollection"/>.
	/// </summary>
	public class FileBackedLogMessageCollectionFilteringAccessor : ILogMessageCollectionFilteringAccessor<LogMessage>
	{
		private readonly FileBackedLogMessageCollection        mCollection;
		private readonly IFileBackedLogMessageCollectionFilter mFilter;
		private readonly SearchByMessageIdComparer             mSearchByMessageIdComparer = new SearchByMessageIdComparer();
		private          bool                                  mIsDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogMessageCollectionFilteringAccessor"/> class.
		/// </summary>
		/// <param name="collection">The unfiltered collection the accessor should work on.</param>
		/// <param name="filter">The filter the accessor should apply.</param>
		internal FileBackedLogMessageCollectionFilteringAccessor(
			FileBackedLogMessageCollection        collection,
			IFileBackedLogMessageCollectionFilter filter)
		{
			mCollection = collection ?? throw new ArgumentNullException(nameof(collection));
			mFilter = filter ?? throw new ArgumentNullException(nameof(filter));
			mCollection.CollectionChanged += OnCollectionChanged;
			mFilter.FilterChanged += OnFilterChanged;
			UpdateFirstMatchingMessageId();
			UpdateLastMatchingMessageId();
		}

		/// <summary>
		/// Disposes the accessor.
		/// </summary>
		public void Dispose()
		{
			if (!mIsDisposed)
			{
				mCollection.CollectionChanged -= OnCollectionChanged;
				mFilter.FilterChanged -= OnFilterChanged;
				mIsDisposed = true;
			}
		}

		/// <summary>
		/// Gets the unfiltered collection the accessor works on.
		/// </summary>
		public FileBackedLogMessageCollection Collection => mCollection;

		/// <summary>
		/// Gets the unfiltered collection the accessor works on.
		/// </summary>
		ILogMessageCollection<LogMessage> ILogMessageCollectionFilteringAccessor<LogMessage>.Collection => mCollection;

		/// <summary>
		/// Gets the filter the accessor works with.
		/// </summary>
		public IFileBackedLogMessageCollectionFilter Filter => mFilter;

		/// <summary>
		/// Gets the filter the accessor works with.
		/// </summary>
		ILogMessageCollectionFilterBase<LogMessage> ILogMessageCollectionFilteringAccessor<LogMessage>.Filter => mFilter;

		/// <summary>
		/// Gets or sets a the maximum number of message to cache before least recently used messages are removed (must be at least 1).
		/// </summary>
		public int CacheCapacity
		{
			get => mCacheCapacity;
			set
			{
				if (value < 1)
					throw new ArgumentOutOfRangeException(nameof(value), "The cache capacity must be greater than 0.");

				if (mCacheCapacity != value)
				{
					mCacheCapacity = value;

					// reduce the number of cached messages, if necessary
					while (mCachedMessagesSortedById.Count > mCacheCapacity)
					{
						RemoveLeastRecentlyUsedMessageFromCache();
					}
				}
			}
		}

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified index in the unfiltered collection going backwards.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="matchIndex">Receives the index of the first log message matching the filter.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		public LogMessage GetPreviousMessage(
			long     fromIndex,
			out long matchIndex)
		{
			if (mIsDisposed)
				throw new ObjectDisposedException(nameof(FileBackedLogMessageCollectionFilteringAccessor));

			if (fromIndex < 0 || fromIndex >= mCollection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			// determine the offset of the collection index to message ids in the log file
			long offset = mCollection.LogFile.OldestMessageId;
			long fromMessageId = fromIndex + offset;

			// abort, of the requested message is outside the filtered set
			if (mFirstMatchingMessageId < 0 || fromMessageId < mFirstMatchingMessageId)
			{
				matchIndex = -1;
				return null;
			}

			// check whether the requested message is in the cache
			int index = GetMessageFromCacheById_AdjustBackwards(fromMessageId, out var message);
			if (index >= 0)
			{
				matchIndex = message.Id - offset;
				return message;
			}

			// cache does not contain the requested message
			// => fetch it and add it to the cache
			message = FetchAndAddMessageToCache(fromMessageId, false);
			matchIndex = message?.Id - offset ?? -1;
			return message;
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the unfiltered collection going backwards.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
		/// <param name="reverse">
		/// <c>true</c> to reverse the list of returned messages, so the order of the messages is the same as in the collection;
		/// <c>false</c> to return the list of messages in the opposite order.
		/// </param>
		/// <returns>Log messages matching filter.</returns>
		/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		public LogMessage[] GetPreviousMessages(
			long       fromIndex,
			int        count,
			out long[] matchIndices,
			bool       reverse)
		{
			if (mIsDisposed)
				throw new ObjectDisposedException(nameof(FileBackedLogMessageCollectionFilteringAccessor));

			if (fromIndex < 0 || fromIndex >= mCollection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(count),
					"The count must be positive.");
			}

			// abort, if no messages were requested or the filtered set is empty
			if (count == 0 || mFirstMatchingMessageId < 0)
			{
				matchIndices = Array.Empty<long>();
				return Array.Empty<LogMessage>();
			}

			// determine the offset of the collection index to message ids in the log file
			long offset = mCollection.LogFile.OldestMessageId;

			// determine the message id corresponding to the index in the collection
			long fromMessageId = fromIndex + offset;

			// check whether the first requested message is in the cache
			long expectedSuccessorId = -1;
			var messages = new List<LogFileMessage>();
			int index = GetMessageFromCacheById_AdjustBackwards(fromMessageId, out var message);
			if (index >= 0)
			{
				messages.Add(message);
				expectedSuccessorId = message.Id;
			}

			if (messages.Count > 0)
			{
				// the first message was found, try to serve the remaining messages from the cache as well
				for (int i = 1; i < count; i++)
				{
					if (index - i < 0) break;
					var cacheItem = mCachedMessagesSortedById[index - i];
					message = cacheItem.Message;
					if (cacheItem.NextMessageId != expectedSuccessorId) break; // cache contains gaps between adjacent matching messages
					messages.Add(message);
					expectedSuccessorId = message.Id;
					mCacheLruList.Remove(cacheItem.LruNode);
					mCacheLruList.AddLast(cacheItem.LruNode);
				}

				// the request was served completely from the cache, if the desired number of messages has been collected
				// or the start of the filtered set is reached
				if (messages.Count == count || messages[messages.Count - 1].Id == mFirstMatchingMessageId)
				{
					matchIndices = messages.Select(x => x.Id - offset).ToArray();
					return messages.Cast<LogMessage>().ToArray();
				}
			}

			// the cache could not suffice the request
			// => fetch requested interval and update the cache
			messages = FetchAndAddMessagesToCache(
				fromMessageId,
				count,
				false,
				false);

			if (reverse) messages.Reverse();
			matchIndices = messages.Select(x => x.Id - offset).ToArray();
			return messages.Cast<LogMessage>().ToArray();
		}

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified index in the unfiltered collection going forward.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="matchIndex">Receives the index of the first log message matching the filter.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		public LogMessage GetNextMessage(
			long     fromIndex,
			out long matchIndex)
		{
			if (mIsDisposed)
				throw new ObjectDisposedException(nameof(FileBackedLogMessageCollectionFilteringAccessor));

			if (fromIndex < 0 || fromIndex >= mCollection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			// determine the offset of the collection index to message ids in the log file
			long offset = mCollection.LogFile.OldestMessageId;
			long fromMessageId = fromIndex + offset;

			// abort, of the requested message is outside the filtered set
			if (mLastMatchingMessageId < 0 || fromMessageId > mLastMatchingMessageId)
			{
				matchIndex = -1;
				return null;
			}

			// check whether the requested message is in the cache
			int index = GetMessageFromCacheById_AdjustForward(fromMessageId, out var message);
			if (index >= 0)
			{
				matchIndex = message.Id - offset;
				return message;
			}

			// cache does not contain the requested message
			// => fetch it and add it to the cache
			message = FetchAndAddMessageToCache(fromMessageId, true);
			matchIndex = message?.Id - offset ?? -1;
			return message;
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the unfiltered collection going forward.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
		/// <returns>Log messages matching filter.</returns>
		/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		public LogMessage[] GetNextMessages(
			long       fromIndex,
			int        count,
			out long[] matchIndices)
		{
			if (mIsDisposed)
				throw new ObjectDisposedException(nameof(FileBackedLogMessageCollectionFilteringAccessor));

			if (fromIndex < 0 || fromIndex >= mCollection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(count),
					"The count must be positive.");
			}

			// abort, if no messages were requested or the filtered set is empty
			if (count == 0 || mFirstMatchingMessageId < 0)
			{
				matchIndices = Array.Empty<long>();
				return Array.Empty<LogMessage>();
			}

			// determine the offset of the collection index to message ids in the log file
			long offset = mCollection.LogFile.OldestMessageId;

			// determine the message id corresponding to the index in the collection
			long fromMessageId = fromIndex + offset;

			// check whether the first requested message is in the cache
			long expectedPredecessorId = -1;
			var messages = new List<LogFileMessage>();
			int index = GetMessageFromCacheById_AdjustForward(fromMessageId, out var message);
			if (index >= 0)
			{
				messages.Add(message);
				expectedPredecessorId = message.Id;
			}

			if (messages.Count > 0)
			{
				// the first message was found, try to serve the remaining messages from the cache as well
				for (int i = 1; i < count; i++)
				{
					if (index + i >= mCachedMessagesSortedById.Count) break;
					var cacheItem = mCachedMessagesSortedById[index + i];
					message = cacheItem.Message;
					if (cacheItem.PreviousMessageId != expectedPredecessorId) break; // cache contains gaps between adjacent matching messages
					messages.Add(message);
					expectedPredecessorId = message.Id;
					mCacheLruList.Remove(cacheItem.LruNode);
					mCacheLruList.AddLast(cacheItem.LruNode);
				}

				// the request was served completely from the cache, if the desired number of messages has been collected
				// or the end of the filtered set is reached
				if (messages.Count == count || messages[messages.Count - 1].Id == mLastMatchingMessageId)
				{
					matchIndices = messages.Select(x => x.Id - offset).ToArray();
					return messages.Cast<LogMessage>().ToArray();
				}
			}

			// the cache could not suffice the request
			// => fetch requested interval and update the cache
			messages = FetchAndAddMessagesToCache(
				fromMessageId,
				count,
				true,
				true);

			matchIndices = messages.Select(x => x.Id - offset).ToArray();
			return messages.Cast<LogMessage>().ToArray();
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the unfiltered collection.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="toIndex">Index of the log message in the unfiltered collection to stop at.</param>
		/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
		/// <returns>Log messages matching filter.</returns>
		/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="fromIndex"/> or <paramref name="toIndex"/> exceeds the bounds of the unfiltered
		/// collection.
		/// </exception>
		public LogMessage[] GetMessageRange(
			long       fromIndex,
			long       toIndex,
			out long[] matchIndices)
		{
			if (mIsDisposed)
				throw new ObjectDisposedException(nameof(FileBackedLogMessageCollectionFilteringAccessor));

			if (fromIndex < 0 || fromIndex >= mCollection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			if (toIndex < 0 || toIndex >= mCollection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(toIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			// abort, if the filtered set is empty
			if (mFirstMatchingMessageId < 0)
			{
				matchIndices = Array.Empty<long>();
				return Array.Empty<LogMessage>();
			}

			// determine the offset of the collection index to message ids in the log file
			long offset = mCollection.LogFile.OldestMessageId;

			// determine the message id corresponding to the specified indices in the collection
			long fromMessageId = fromIndex + offset;
			long toMessageId = toIndex + offset;

			// check whether the last requested message is in the cache (adjust backwards if necessary)
			// (needed to determine whether the retrieved sequence is complete)
			int lastMessageCacheIndex = GetMessageFromCacheById_AdjustBackwards(toMessageId, out var lastMessage);
			if (lastMessageCacheIndex >= 0)
			{
				// the last message is in the cache

				// check whether the first requested message is in the cache (adjust forward if necessary)
				long expectedPredecessorId = -1;
				var messages = new List<LogFileMessage>();
				int firstMessageCacheIndex = GetMessageFromCacheById_AdjustForward(fromMessageId, out var message);
				if (firstMessageCacheIndex >= 0)
				{
					// abort, if the the first matching message is behind the end of the requested range
					if (message.Id > lastMessage.Id)
					{
						matchIndices = Array.Empty<long>();
						return Array.Empty<LogMessage>();
					}

					// found first in the range message 
					messages.Add(message);
					expectedPredecessorId = message.Id;
				}

				if (messages.Count > 0)
				{
					// the first message was found
					// => try to serve the remaining messages from the cache as well
					int index = firstMessageCacheIndex;
					while (true)
					{
						if (++index >= mCachedMessagesSortedById.Count) break;
						var cacheItem = mCachedMessagesSortedById[index];
						message = cacheItem.Message;
						if (cacheItem.PreviousMessageId != expectedPredecessorId) break; // cache contains gaps between adjacent matching messages
						messages.Add(message);
						expectedPredecessorId = message.Id;
						mCacheLruList.Remove(cacheItem.LruNode);
						mCacheLruList.AddLast(cacheItem.LruNode);
					}

					// the request was served completely from the cache, if the end of the filtered set is reached
					if (messages[messages.Count - 1].Id == lastMessage.Id)
					{
						matchIndices = messages.Select(x => x.Id - offset).ToArray();
						return messages.Cast<LogMessage>().ToArray();
					}
				}
			}

			// the cache could not suffice the request
			// => fetch requested interval and update the cache
			var messages2 = FetchAndAddMessageRangeToCache(fromMessageId, toMessageId);
			matchIndices = messages2.Select(x => x.Id - offset).ToArray();
			return messages2.Cast<LogMessage>().ToArray();
		}

		#region Caching

		private class CacheItem
		{
			private         LogFileMessage            mMessage;
			public          long                      MessageId;
			public          long                      PreviousMessageId;
			public          long                      NextMessageId;
			public          int                       MessageCacheIndex;
			public readonly LinkedListNode<CacheItem> LruNode;

			/// <summary>
			/// Initializes a new instance of the <see cref="CacheItem"/> class.
			/// </summary>
			public CacheItem()
			{
				LruNode = new LinkedListNode<CacheItem>(this);
				Reset();
			}

			/// <summary>
			/// Resets the cache item to defaults.
			/// </summary>
			public void Reset()
			{
				Message = null;
				MessageId = -1;
				MessageCacheIndex = -1;
				PreviousMessageId = -1;
				NextMessageId = -1;
			}

			/// <summary>
			/// Gets or sets the cached message.
			/// </summary>
			public LogFileMessage Message
			{
				get => mMessage;
				set
				{
					if (value != null)
					{
						mMessage = value;
						MessageId = value.Id;
					}
					else
					{
						mMessage = null;
						MessageId = -1;
					}
				}
			}
		}

		/// <summary>
		/// A comparer for <see cref="CacheItem"/> that takes only <see cref="CacheItem.MessageId"/> into account.
		/// </summary>
		private class SearchByMessageIdComparer : IComparer<CacheItem>
		{
			/// <summary>
			/// Compares the specified cache items by the id of the cached message.
			/// </summary>
			/// <param name="x">Cache item to compare.</param>
			/// <param name="y">Cache item to compare with.</param>
			/// <returns>
			/// -1, if the message id of <paramref name="x"/> is less than the message id of <paramref name="y"/>;
			/// 1, if the message id of <paramref name="x"/> is greater than the message id of <paramref name="y"/>;
			/// 0, if both message ids are equal.
			/// </returns>
			public int Compare(CacheItem x, CacheItem y)
			{
				Debug.Assert(x != null, nameof(x) + " != null");
				Debug.Assert(y != null, nameof(y) + " != null");
				if (x.MessageId < y.MessageId) return -1;
				if (x.MessageId > y.MessageId) return 1;
				return 0;
			}
		}

		private readonly List<CacheItem>                       mCachedMessagesSortedById = new List<CacheItem>();
		private readonly LinkedList<LinkedListNode<CacheItem>> mCacheLruList             = new LinkedList<LinkedListNode<CacheItem>>(); // front = oldest message, back = newest message
		private readonly Queue<CacheItem>                      mEmptyCacheItems          = new Queue<CacheItem>();
		private          int                                   mCacheCapacity            = 10000;
		private          long                                  mFirstMatchingMessageId   = -1;
		private          long                                  mLastMatchingMessageId    = -1;

		/// <summary>
		/// Gets an empty cache item.
		/// </summary>
		/// <returns>An empty cache item.</returns>
		private CacheItem GetCacheItem()
		{
			if (mEmptyCacheItems.Count > 0) return mEmptyCacheItems.Dequeue();
			return new CacheItem();
		}

		/// <summary>
		/// Returns a cache item to the empty list ready to be re-used.
		/// </summary>
		/// <param name="item"></param>
		private void ReturnCacheItem(CacheItem item)
		{
			item.Reset();
			mEmptyCacheItems.Enqueue(item);
		}

		/// <summary>
		/// Updates the id of the first matching message.
		/// </summary>
		private void UpdateFirstMatchingMessageId()
		{
			if (mCollection.LogFile.OldestMessageId < 0)
			{
				mFirstMatchingMessageId = -1;
				return;
			}

			var message = mFilter.GetNextMessage(mCollection.LogFile.OldestMessageId);
			mFirstMatchingMessageId = message?.Id ?? -1;
		}

		/// <summary>
		/// Updates the id of the last matching message.
		/// </summary>
		private void UpdateLastMatchingMessageId()
		{
			if (mCollection.LogFile.NewestMessageId < 0)
			{
				mLastMatchingMessageId = -1;
				return;
			}

			var message = mFilter.GetPreviousMessage(mCollection.LogFile.NewestMessageId);
			mLastMatchingMessageId = message?.Id ?? -1;
		}

		/// <summary>
		/// Gets the message with the specified id from the cache, if it is in the filtered set.
		/// Adjusts the specified message id backwards, if the id does not belong to a message in the filtered set.
		/// </summary>
		/// <param name="fromMessageId">Message id to start at.</param>
		/// <param name="message">Receives the found message.</param>
		/// <returns>Index of the message in the cache.</returns>
		private int GetMessageFromCacheById_AdjustBackwards(long fromMessageId, out LogFileMessage message)
		{
			// check whether the message at the specified location is in the cache (direct hit)
			var searchItem = GetCacheItem();
			searchItem.MessageId = fromMessageId;
			int index = mCachedMessagesSortedById.BinarySearch(searchItem, mSearchByMessageIdComparer);
			ReturnCacheItem(searchItem);
			if (index >= 0)
			{
				// found the requested messages
				var cacheItem = mCachedMessagesSortedById[index];
				mCacheLruList.Remove(cacheItem.LruNode);
				mCacheLruList.AddLast(cacheItem.LruNode);
				message = cacheItem.Message;
				return index;
			}

			// ~index is the index of the cached message (possibly) following the desired message
			index = ~index;
			if (index > 0)
			{
				index--;
				var cacheItem = mCachedMessagesSortedById[index];
				if (cacheItem.NextMessageId >= 0 && cacheItem.NextMessageId > fromMessageId)
				{
					// the message has a successor and the successor is behind the requested location
					// => we've started at a location between two adjacent filtered messages
					// => found the requested message
					mCacheLruList.Remove(cacheItem.LruNode);
					mCacheLruList.AddLast(cacheItem.LruNode);
					message = cacheItem.Message;
					return index;
				}
			}

			message = null;
			return -1;
		}

		/// <summary>
		/// Gets the message with the specified id from the cache, if it is in the filtered set.
		/// Adjusts the specified message id forward, if the id does not belong to a message in the filtered set.
		/// </summary>
		/// <param name="fromMessageId">Message id to start at.</param>
		/// <param name="message">Receives the found message.</param>
		/// <returns>Index of the message in the cache.</returns>
		private int GetMessageFromCacheById_AdjustForward(long fromMessageId, out LogFileMessage message)
		{
			// check whether the message at the specified location is in the cache (direct hit)
			var searchItem = GetCacheItem();
			searchItem.MessageId = fromMessageId;
			int index = mCachedMessagesSortedById.BinarySearch(searchItem, mSearchByMessageIdComparer);
			ReturnCacheItem(searchItem);
			if (index >= 0)
			{
				// direct hit
				var cacheItem = mCachedMessagesSortedById[index];
				mCacheLruList.Remove(cacheItem.LruNode);
				mCacheLruList.AddLast(cacheItem.LruNode);
				message = cacheItem.Message;
				return index;
			}

			// ~index is the index of the next cached message (if not at the end)
			index = ~index;
			if (index > 0 && index < mCachedMessagesSortedById.Count)
			{
				var cacheItem = mCachedMessagesSortedById[index];
				if (cacheItem.PreviousMessageId >= 0 && cacheItem.PreviousMessageId < fromMessageId)
				{
					// the message has a predecessor and the predecessor is before the requested location
					// => we've started at a location between two adjacent filtered messages
					// => found the requested message
					mCacheLruList.Remove(cacheItem.LruNode);
					mCacheLruList.AddLast(cacheItem.LruNode);
					message = cacheItem.Message;
					return index;
				}
			}

			message = null;
			return -1;
		}

		/// <summary>
		/// Fetches the log message with the specified message id.
		/// Adjusts the specified id, if it does not belong to a message matching the filter.
		/// </summary>
		/// <param name="messageId">Id of the message to fetch.</param>
		/// <param name="adjustForward">
		/// <c>true</c> to adjust forward, if the specified id does not belong to a message matching the filter;
		/// <c>false</c> to adjust backwards, if the specified id does not belong to a message matching the filter.
		/// </param>
		/// <returns>The found message.</returns>
		private LogFileMessage FetchAndAddMessageToCache(
			long messageId,
			bool adjustForward)
		{
			// try to fetch requested message
			var message = adjustForward
				              ? mFilter.GetNextMessage(messageId)
				              : mFilter.GetPreviousMessage(messageId);

			if (message != null)
			{
				// fetch the predecessor of the message
				long previousMatchMessageId = -1;
				if (message.Id > Collection.LogFile.OldestMessageId)
				{
					var previousMessage = mFilter.GetPreviousMessage(message.Id - 1);
					previousMatchMessageId = previousMessage?.Id ?? -1;
				}

				// fetch the successor of the message
				long nextMatchMessageId = -1;
				if (message.Id < Collection.LogFile.NewestMessageId)
				{
					var nextMessage = mFilter.GetNextMessage(message.Id + 1);
					nextMatchMessageId = nextMessage?.Id ?? -1;
				}

				// update cache item, if the message is already cached
				CacheItem cacheItem;
				var searchItem = GetCacheItem();
				searchItem.MessageId = message.Id;
				int index = mCachedMessagesSortedById.BinarySearch(searchItem, mSearchByMessageIdComparer);
				ReturnCacheItem(searchItem);
				if (index >= 0)
				{
					// message is already in the cache
					// => update and refresh it
					cacheItem = mCachedMessagesSortedById[index];
					cacheItem.Message = message;
					cacheItem.PreviousMessageId = previousMatchMessageId;
					cacheItem.NextMessageId = nextMatchMessageId;
					mCacheLruList.Remove(cacheItem.LruNode);
					mCacheLruList.AddLast(cacheItem.LruNode);
					return message;
				}

				// the message is not in the cache, yet
				// => add it
				cacheItem = GetCacheItem();
				cacheItem.Message = message;
				cacheItem.MessageCacheIndex = ~index;
				cacheItem.PreviousMessageId = previousMatchMessageId;
				cacheItem.NextMessageId = nextMatchMessageId;
				mCacheLruList.AddLast(cacheItem.LruNode);
				mCachedMessagesSortedById.Insert(~index, cacheItem);
				for (int i = ~index + 1; i < mCachedMessagesSortedById.Count; i++)
				{
					mCachedMessagesSortedById[i].MessageCacheIndex++;
				}

				// remove excessive cache item, if the cache has reached its capacity
				if (mCachedMessagesSortedById.Count > mCacheCapacity)
					RemoveLeastRecentlyUsedMessageFromCache();
			}

			return message;
		}

		/// <summary>
		/// Fetches multiple log messages starting with the specified message id.
		/// Adjusts the specified id, if it does not belong to a message matching the filter.
		/// </summary>
		/// <param name="messageId">Id of the message to fetch.</param>
		/// <param name="count">Maximum number of messages to fetch.</param>
		/// <param name="adjustForward">
		/// <c>true</c> to adjust forward, if the specified id does not belong to a message matching the filter;
		/// <c>false</c> to adjust backwards, if the specified id does not belong to a message matching the filter.
		/// </param>
		/// <param name="getForward">
		/// <c>true</c> to get messages following the specified message id;
		/// <c>false</c> to get messages preceding the specified message id.
		/// </param>
		/// <returns>The retrieved messages.</returns>
		private List<LogFileMessage> FetchAndAddMessagesToCache(
			long messageId,
			int  count,
			bool adjustForward,
			bool getForward)
		{
			// try to fetch requested messages
			var firstMessage = adjustForward
				                   ? mFilter.GetNextMessage(messageId)
				                   : mFilter.GetPreviousMessage(messageId);

			// abort, if the start message was not found
			if (firstMessage == null)
				return new List<LogFileMessage>();

			var messages = new List<LogFileMessage> { firstMessage };
			long previousMatchMessageId = -1;
			long nextMatchMessageId = -1;

			if (getForward)
			{
				// reading filtered message set forward
				// => get messages following the first one
				if (firstMessage.Id < mCollection.LogFile.NewestMessageId)
				{
					var followingMessages = mFilter.GetNextMessages(firstMessage.Id + 1, count);
					if (followingMessages.Length == count)
					{
						// got message following the last requested one which delivers the id of the successor of the last requested message
						messages.AddRange(followingMessages.Take(count - 1));
						nextMatchMessageId = followingMessages[count - 1].Id;
					}
					else
					{
						// read up to the end of the filtered set
						messages.AddRange(followingMessages);
					}
				}

				// fetch the predecessor of the first message
				if (firstMessage.Id > Collection.LogFile.OldestMessageId)
				{
					var previousMessage = mFilter.GetPreviousMessage(firstMessage.Id - 1);
					previousMatchMessageId = previousMessage?.Id ?? -1;
				}
			}
			else
			{
				// reading filtered message set backwards
				// => get messages preceding the first one
				if (firstMessage.Id > mCollection.LogFile.OldestMessageId)
				{
					var precedingMessages = mFilter.GetPreviousMessages(firstMessage.Id - 1, count, false);
					if (precedingMessages.Length == count)
					{
						// got message following the last requested one which delivers id of the successor of the last requested message
						messages.AddRange(precedingMessages.Take(count - 1));
						previousMatchMessageId = precedingMessages[count - 1].Id;
					}
					else
					{
						// read up to the end of the filtered set
						messages.AddRange(precedingMessages);
					}
				}

				// fetch the successor of the message
				if (firstMessage.Id < Collection.LogFile.NewestMessageId)
				{
					var nextMessage = mFilter.GetNextMessage(firstMessage.Id + 1);
					nextMatchMessageId = nextMessage?.Id ?? -1;
				}
			}

			// put messages into the cache
			AddMessagesToCache(
				messages,
				previousMatchMessageId,
				nextMatchMessageId);

			return messages;
		}

		/// <summary>
		/// Fetches all matching log messages in the specified message id interval.
		/// </summary>
		/// <param name="fromMessageId">Id of the message to start at (incl. the message).</param>
		/// <param name="toMessageId">Id of the message to stop at (incl. the message).</param>
		/// <returns>The retrieved messages.</returns>
		private List<LogFileMessage> FetchAndAddMessageRangeToCache(long fromMessageId, long toMessageId)
		{
			Debug.Assert(fromMessageId <= toMessageId);

			// try to fetch the first matching messages in the range
			var firstMessage = mFilter.GetNextMessage(fromMessageId);

			// abort, if the start message was not found
			if (firstMessage == null)
				return new List<LogFileMessage>();

			// the range contains at least the first message
			var messages = new List<LogFileMessage> { firstMessage };

			// reading filtered message set forward
			// => get messages following the first one
			if (firstMessage.Id < mCollection.LogFile.NewestMessageId)
			{
				var followingMessages = mFilter.GetMessageRange(firstMessage.Id + 1, toMessageId);
				messages.AddRange(followingMessages);
			}

			// fetch the predecessor of the first message
			long previousMatchMessageId = -1;
			if (firstMessage.Id > Collection.LogFile.OldestMessageId)
			{
				var previousMessage = mFilter.GetPreviousMessage(firstMessage.Id - 1);
				previousMatchMessageId = previousMessage?.Id ?? -1;
			}

			// fetch the successor of the last message
			long nextMatchMessageId = -1;
			var lastMessage = messages[messages.Count - 1];
			if (lastMessage.Id < Collection.LogFile.NewestMessageId)
			{
				var nextMessage = mFilter.GetNextMessage(lastMessage.Id + 1);
				nextMatchMessageId = nextMessage?.Id ?? -1;
			}

			// put messages into the cache
			AddMessagesToCache(
				messages,
				previousMatchMessageId,
				nextMatchMessageId);

			return messages;
		}

		/// <summary>
		/// Adds the specified messages to the cache.
		/// </summary>
		/// <param name="messages">Messages to add to the cache (must not contain any gaps!).</param>
		/// <param name="predecessorMessageId">Id of the message preceding the first message in the sequence.</param>
		/// <param name="successorMessageId">Id of the message following the last message in the sequence.</param>
		private void AddMessagesToCache(
			List<LogFileMessage> messages,
			long                 predecessorMessageId,
			long                 successorMessageId)
		{
			// get cache item to use for searching in the sorted list
			var searchItem = GetCacheItem();

			for (int i = 0; i < messages.Count; i++)
			{
				var message = messages[i];

				// update cache item, if the message is already cached
				CacheItem cacheItem;
				searchItem.MessageId = message.Id;
				int index = mCachedMessagesSortedById.BinarySearch(searchItem, mSearchByMessageIdComparer);
				if (index >= 0)
				{
					// message is already in the cache
					// => update and refresh it
					cacheItem = mCachedMessagesSortedById[index];
					cacheItem.Message = message;
					cacheItem.PreviousMessageId = i > 0 ? messages[i - 1].Id : predecessorMessageId;
					cacheItem.NextMessageId = i + 1 < messages.Count ? messages[i + 1].Id : successorMessageId;
					mCacheLruList.Remove(cacheItem.LruNode);
					mCacheLruList.AddLast(cacheItem.LruNode);
					continue;
				}

				// the message is not in the cache, yet
				// => add it
				cacheItem = GetCacheItem();
				cacheItem.Message = message;
				cacheItem.MessageCacheIndex = ~index;
				cacheItem.PreviousMessageId = i > 0 ? messages[i - 1].Id : predecessorMessageId;
				cacheItem.NextMessageId = i + 1 < messages.Count ? messages[i + 1].Id : successorMessageId;
				mCacheLruList.AddLast(cacheItem.LruNode);
				mCachedMessagesSortedById.Insert(~index, cacheItem);
				for (int j = ~index + 1; j < mCachedMessagesSortedById.Count; j++)
				{
					mCachedMessagesSortedById[j].MessageCacheIndex++;
				}
			}

			// return search item to the cache item pool
			ReturnCacheItem(searchItem);

			// remove excessive cache items, if the cache has reached its capacity
			while (mCachedMessagesSortedById.Count > mCacheCapacity)
			{
				RemoveLeastRecentlyUsedMessageFromCache();
			}
		}

		/// <summary>
		/// Removes the least requested message from the cache.
		/// </summary>
		private void RemoveLeastRecentlyUsedMessageFromCache()
		{
			// get cache item referring to the least recently used message (first node in the LRU list)
			var lruNode = mCacheLruList.First;
			var messageNodeToRemove = lruNode.Value;
			var cacheItemToRemove = messageNodeToRemove.Value;

			// remove message from the list of cached messages and the LRU list
			mCacheLruList.Remove(lruNode);
			mCachedMessagesSortedById.RemoveAt(cacheItemToRemove.MessageCacheIndex);
			mEmptyCacheItems.Enqueue(cacheItemToRemove);
			for (int i = cacheItemToRemove.MessageCacheIndex; i < mCachedMessagesSortedById.Count; i++)
			{
				mCachedMessagesSortedById[i].MessageCacheIndex--;
			}
		}

		/// <summary>
		/// Consolidates the cache by removing messages that are not in the log file any more.
		/// </summary>
		private void Consolidate()
		{
			// update the id of the first and the last matching message
			UpdateFirstMatchingMessageId();
			UpdateLastMatchingMessageId();

			if (mFirstMatchingMessageId < 0)
			{
				// the filter does not match any messages
				// => invalidate the entire cache
				mCachedMessagesSortedById.ForEach(ReturnCacheItem);
				mCachedMessagesSortedById.Clear();
				mCacheLruList.Clear();
			}
			else
			{
				// at least one message matches the filter

				// find index of the first cached message after the last matching message
				var searchItem = GetCacheItem();
				searchItem.MessageId = mLastMatchingMessageId;
				int index = mCachedMessagesSortedById.BinarySearch(searchItem, mSearchByMessageIdComparer);
				ReturnCacheItem(searchItem);
				int indexOfFirstCacheItemBeforeLastMatchingMessage = -1;
				if (index >= 0)
				{
					// found cache item of the last matching message
					// => everything behind it should be removed
					if (index + 1 < mCachedMessagesSortedById.Count)
						indexOfFirstCacheItemBeforeLastMatchingMessage = index + 1;
				}
				else
				{
					// the last matching message is not in the cache
					if (~index < mCachedMessagesSortedById.Count)
						indexOfFirstCacheItemBeforeLastMatchingMessage = ~index;
				}

				// remove these messages from the cache
				if (indexOfFirstCacheItemBeforeLastMatchingMessage >= 0)
				{
					for (int i = indexOfFirstCacheItemBeforeLastMatchingMessage; i < mCachedMessagesSortedById.Count; i++)
					{
						var cacheItem = mCachedMessagesSortedById[i];
						mCacheLruList.Remove(cacheItem.LruNode);
						ReturnCacheItem(cacheItem);
					}

					mCachedMessagesSortedById.RemoveRange(
						indexOfFirstCacheItemBeforeLastMatchingMessage,
						mCachedMessagesSortedById.Count - indexOfFirstCacheItemBeforeLastMatchingMessage);
				}

				// find index of the last cached message before the first matching message
				searchItem = GetCacheItem();
				searchItem.MessageId = mFirstMatchingMessageId;
				index = mCachedMessagesSortedById.BinarySearch(searchItem, mSearchByMessageIdComparer);
				ReturnCacheItem(searchItem);
				int indexOfLastCacheItemBeforeFirstMatchingMessage = -1;
				if (index >= 0)
				{
					// found cache item of the first matching message
					// => everything before it should be removed
					if (index > 0)
						indexOfLastCacheItemBeforeFirstMatchingMessage = index - 1;
				}
				else
				{
					// the first matching message is not in the cache
					indexOfLastCacheItemBeforeFirstMatchingMessage = ~index - 1;
				}

				// remove these messages from the cache
				if (indexOfLastCacheItemBeforeFirstMatchingMessage >= 0)
				{
					for (int i = 0; i <= indexOfLastCacheItemBeforeFirstMatchingMessage; i++)
					{
						var cacheItem = mCachedMessagesSortedById[i];
						mCacheLruList.Remove(cacheItem.LruNode);
						ReturnCacheItem(cacheItem);
					}

					mCachedMessagesSortedById.RemoveRange(0, indexOfLastCacheItemBeforeFirstMatchingMessage + 1);
				}
			}
		}

		/// <summary>
		/// Invalidates the cache.
		/// </summary>
		private void InvalidateCache()
		{
			// clear the cache
			mCachedMessagesSortedById.ForEach(ReturnCacheItem);
			mCachedMessagesSortedById.Clear();
			mCacheLruList.Clear();

			// update the id of the first and the last matching message
			UpdateFirstMatchingMessageId();
			UpdateLastMatchingMessageId();
		}

		#endregion

		#region Processing Changes

		/// <summary>
		/// Is called when the unfiltered collection has changed
		/// </summary>
		/// <param name="sender">The unfiltered collection.</param>
		/// <param name="e">Event arguments.</param>
		private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (mFirstMatchingMessageId < 0) UpdateFirstMatchingMessageId();
					UpdateLastMatchingMessageId();
					break;

				case NotifyCollectionChangedAction.Remove: // for pruning
				case NotifyCollectionChangedAction.Reset:  // for clearing
					Consolidate();
					break;
			}
		}

		/// <summary>
		/// Is called when the filter used to access the filtered message set has changed.
		/// </summary>
		/// <param name="sender">The filter.</param>
		/// <param name="e">Event arguments.</param>
		private void OnFilterChanged(object sender, FilterChangedEventArgs e)
		{
			// invalidate the cache, if the filter has changed in a way that effects the filtered message set
			if (e.ChangeEffectsFilterResult)
				InvalidateCache();
		}

		#endregion
	}

}
