﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

// ReSharper disable ExplicitCallerInfoArgument

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// A collection of log messages
/// (supports data binding via <see cref="INotifyCollectionChanged"/> and <see cref="INotifyPropertyChanged"/>).
/// </summary>
/// <typeparam name="TMessage">The log message type.</typeparam>
public class LogMessageCollection<TMessage> : LogMessageCollectionBase<TMessage>
	where TMessage : class, ILogMessage
{
	internal readonly List<TMessage> Messages;

	/// <summary>
	/// Initializes a new instance of the <see cref="LogMessageCollection{TMessage}"/> class.
	/// </summary>
	public LogMessageCollection()
	{
		Messages = [];
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LogMessageCollection{TMessage}"/> class containing the specified log messages.
	/// </summary>
	/// <param name="messages">Log messages to keep in the collection.</param>
	public LogMessageCollection(IEnumerable<TMessage> messages)
	{
		if (messages == null) throw new ArgumentNullException(nameof(messages));
		Messages = [..messages];
		foreach (TMessage message in Messages) UpdateOverviewCollectionsOnAdd(message);
	}

	/// <summary>
	/// Disposes the collection (actually does nothing, just to satisfy the interface).
	/// </summary>
	/// <param name="disposing">
	/// <c>true</c> if the object is being disposed;<br/>
	/// <c>false</c> if it is being finalized.
	/// </param>
	protected override void Dispose(bool disposing) { }

	#region Count

	/// <summary>
	/// Gets the total number of log messages in the collection.
	/// </summary>
	public override long Count => Messages.Count;

	#endregion

	#region Indexer

	/// <summary>
	/// Gets the log message at the specified index.
	/// </summary>
	/// <param name="index">Index of the log message to get.</param>
	/// <returns>The log message at the specified index.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
	public override TMessage this[long index] => Messages[(int)index]; // can throw ArgumentOutOfRangeException

	#endregion

	#region GetEnumerator()

	/// <summary>
	/// Gets an enumerator iterating over the collection.
	/// </summary>
	/// <returns>Enumerator for iterating over the collection.</returns>
	public override IEnumerator<TMessage> GetEnumerator()
	{
		return Messages.GetEnumerator();
	}

	#endregion

	#region Contains()

	/// <summary>
	/// Checks whether the collection contains the specified log message.
	/// </summary>
	/// <param name="item">Log message to check for.</param>
	/// <returns>
	/// <c>true</c> if the collection contains the log message;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	public override bool Contains(TMessage item)
	{
		return Messages.Contains(item);
	}

	#endregion

	#region IndexOf()

	/// <summary>
	/// Gets the index of the specified log message.
	/// </summary>
	/// <param name="item">Log message to locate in the collection.</param>
	/// <returns>Index of the log message; -1, if the specified message is not in the collection.</returns>
	public override long IndexOf(TMessage item)
	{
		return Messages.IndexOf(item);
	}

	#endregion

	#region Add()

	/// <summary>
	/// Adds a log message to the collection.
	/// </summary>
	/// <param name="message">Log message to add.</param>
	/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
	/// <exception cref="NotSupportedException">The collection is read-only.</exception>
	public override void Add(TMessage message)
	{
		if (message == null) throw new ArgumentNullException(nameof(message));
		if (IsReadOnly) throw new NotSupportedException("The collection is read-only.");

		// add message to the collection and update overview collections
		Messages.Add(message);
		UpdateOverviewCollectionsOnAdd(message);

		// raise CollectionChanged event
		if (IsCollectionChangedRegistered)
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message, Messages.Count - 1));

		OnPropertyChanged("Count");
		OnPropertyChanged("Item[]");

		// notify filtered collection that depend on the collection
		NotifyFilteredCollections_AfterAdd(Messages.Count - 1, 1);
	}

	#endregion

	#region AddRange()

	/// <summary>
	/// Adds multiple log messages to the collection at once.
	/// </summary>
	/// <param name="messages">Log messages to add.</param>
	/// <exception cref="ArgumentNullException"><paramref name="messages"/> is <c>null</c>.</exception>
	/// <exception cref="NotSupportedException">The collection is read-only.</exception>
	public override void AddRange(IEnumerable<TMessage> messages)
	{
		if (messages == null) throw new ArgumentNullException(nameof(messages));
		if (IsReadOnly) throw new NotSupportedException("The collection is read-only.");

		int count = 0;
		if (IsCollectionChangedRegistered)
		{
			if (UseMultiItemNotifications)
			{
				int newMessagesStartIndex = Messages.Count;
				var newMessages = new List<TMessage>();
				foreach (TMessage message in messages)
				{
					Messages.Add(message);
					UpdateOverviewCollectionsOnAdd(message);
					newMessages.Add(message);
					count++;
				}

				OnCollectionChanged(
					new NotifyCollectionChangedEventArgs(
						NotifyCollectionChangedAction.Add,
						newMessages,
						newMessagesStartIndex));
			}
			else
			{
				foreach (TMessage message in messages)
				{
					Messages.Add(message);
					UpdateOverviewCollectionsOnAdd(message);
					OnCollectionChanged(
						new NotifyCollectionChangedEventArgs(
							NotifyCollectionChangedAction.Add,
							message,
							Messages.Count - 1));
					count++;
				}
			}
		}
		else
		{
			foreach (TMessage message in messages)
			{
				Messages.Add(message);
				UpdateOverviewCollectionsOnAdd(message);
				count++;
			}
		}

		OnPropertyChanged("Count");
		OnPropertyChanged("Item[]");

		// notify filtered collections
		NotifyFilteredCollections_AfterAdd(Messages.Count - count, count);
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
	public override void CopyTo(TMessage[] array, int arrayIndex)
	{
		if (array == null) throw new ArgumentNullException(nameof(array));
		if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "The array index is negative.");
		if (array.Rank != 1) throw new ArgumentException("The specified array is multi-dimensional.");
		if (arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "The array index is outside the specified array.");
		if (Count > array.Length - arrayIndex) throw new ArgumentException("The specified array is too small to receive all log messages.");

		Messages.CopyTo(array, arrayIndex);
	}

	#endregion

	#region Clear()

	/// <summary>
	/// Removes all log messages from the collection.
	/// </summary>
	public override void Clear()
	{
		if (Messages.Count > 0)
		{
			// clear the collection itself
			Messages.Clear();

			// clear overview collections
			UpdateOverviewCollectionsOnRemove(null);

			// notify clients that the collection itself has changed
			if (IsCollectionChangedRegistered)
				OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

			// notify clients that collection properties have changed
			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");

			// notify filtered collections
			NotifyFilteredCollections_Clear();
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
		if (maximumMessageCount < 0 && maximumMessageCount != -1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maximumMessageCount),
				"The maximum number of messages must be positive to enable or -1 to disable pruning by count.");
		}

		long minimumTimestampUtcTicks = minimumMessageTimestamp.ToUniversalTime().Ticks;

		// determine how many messages to remove by maximum count
		int numberOfMessagesToRemove = 0;
		if (maximumMessageCount > 0) numberOfMessagesToRemove = (int)Math.Max(numberOfMessagesToRemove, Messages.Count - maximumMessageCount);

		// determine how many messages to remove by timestamp
		if (minimumMessageTimestamp > DateTime.MinValue)
		{
			// find the first message with a timestamp that is greater or equal to the specified timestamp
			int indexOfFirstMessageToKeep = -1;
			for (int i = 0; i < Messages.Count; i++)
			{
				if (Messages[i].Timestamp.UtcTicks >= minimumTimestampUtcTicks)
				{
					indexOfFirstMessageToKeep = i;
					break;
				}
			}

			numberOfMessagesToRemove = Math.Max(numberOfMessagesToRemove, indexOfFirstMessageToKeep);
		}

		if (numberOfMessagesToRemove > 0)
		{
			// notify filtered collections
			NotifyFilteredCollections_BeforeRemoving(0, numberOfMessagesToRemove);

			// update overview collections
			for (int i = 0; i < numberOfMessagesToRemove; i++)
			{
				UpdateOverviewCollectionsOnRemove(Messages[i]);
			}

			if (IsCollectionChangedRegistered)
			{
				// save messages to remove for the event
				var removedMessages = new TMessage[numberOfMessagesToRemove];
				Messages.CopyTo(0, removedMessages, 0, numberOfMessagesToRemove);

				// remove the messages from the collection
				Messages.RemoveRange(0, numberOfMessagesToRemove);

				// notify clients that the collection has changed
				OnCollectionChanged(
					new NotifyCollectionChangedEventArgs(
						NotifyCollectionChangedAction.Remove,
						removedMessages,
						0));
			}
			else
			{
				// remove the messages from the collection
				Messages.RemoveRange(0, numberOfMessagesToRemove);
			}

			// notify clients that collection properties have changed
			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
		}
	}

	#endregion

	#region GetFilteredCollection() + Interaction with Filtered Collections

	private readonly List<WeakReference<FilteredLogMessageCollection<TMessage>>> mFilteredCollections = [];

	/// <summary>
	/// Gets a collection providing the message set that pass the specified filter.
	/// </summary>
	/// <param name="filter">Filter to apply.</param>
	/// <returns>The filtered message collection.</returns>
	public FilteredLogMessageCollection<TMessage> GetFilteredCollection(ILogMessageCollectionFilter<TMessage> filter)
	{
		var collection = new FilteredLogMessageCollection<TMessage>(this, filter);
		mFilteredCollections.Add(new WeakReference<FilteredLogMessageCollection<TMessage>>(collection));
		return collection;
	}

	/// <summary>
	/// Unregisters the specified filtered collection, so it does not receive any further notifications
	/// when the unfiltered collection changes.
	/// </summary>
	/// <param name="collection">The filtered collection to unregister.</param>
	internal void UnregisterFilteredCollection(FilteredLogMessageCollection<TMessage> collection)
	{
		for (int i = 0; i < mFilteredCollections.Count; i++)
		{
			if (mFilteredCollections[i].TryGetTarget(out FilteredLogMessageCollection<TMessage> other))
			{
				if (ReferenceEquals(collection, other))
				{
					mFilteredCollections.RemoveAt(i--);
				}
			}
			else
			{
				// the filtered collection was collected
				// => clean up
				mFilteredCollections.RemoveAt(i--);
			}
		}
	}

	/// <summary>
	/// Notifies filtered collections that log messages were added to the collection.
	/// </summary>
	/// <param name="startIndex">The index of the first new message in the unfiltered collection.</param>
	/// <param name="count">The number of new messages.</param>
	private void NotifyFilteredCollections_AfterAdd(int startIndex, int count)
	{
		for (int i = 0; i < mFilteredCollections.Count; i++)
		{
			if (mFilteredCollections[i].TryGetTarget(out FilteredLogMessageCollection<TMessage> collection))
			{
				collection.ProcessUnfilteredCollectionChange_AfterAdd(startIndex, count);
			}
			else
			{
				// the filtered collection was collected
				// => clean up
				mFilteredCollections.RemoveAt(i--);
			}
		}
	}

	/// <summary>
	/// Notifies filtered collections that log messages are about to be removed from the collection.
	/// </summary>
	/// <param name="startIndex">The index of the first message in the unfiltered collection that is about to be removed.</param>
	/// <param name="count">The number of messages that are about to be removed.</param>
	private void NotifyFilteredCollections_BeforeRemoving(int startIndex, int count)
	{
		for (int i = 0; i < mFilteredCollections.Count; i++)
		{
			if (mFilteredCollections[i].TryGetTarget(out FilteredLogMessageCollection<TMessage> collection))
			{
				collection.ProcessUnfilteredCollectionChange_BeforeRemoving(startIndex, count);
			}
			else
			{
				// the filtered collection was collected
				// => clean up
				mFilteredCollections.RemoveAt(i--);
			}
		}
	}

	/// <summary>
	/// Notifies filtered collections that the collection was cleared.
	/// </summary>
	private void NotifyFilteredCollections_Clear()
	{
		for (int i = 0; i < mFilteredCollections.Count; i++)
		{
			if (mFilteredCollections[i].TryGetTarget(out FilteredLogMessageCollection<TMessage> collection))
			{
				collection.ProcessUnfilteredCollectionChange_Clear();
			}
			else
			{
				// the filtered collection was collected
				// => clean up
				mFilteredCollections.RemoveAt(i--);
			}
		}
	}

	#endregion

	#region GetFilteringAccessor()

	/// <summary>
	/// Gets a filtering accessor that matches log messages using the specified filter.
	/// </summary>
	/// <param name="filter">Filter to use.</param>
	/// <returns>The filtering accessor.</returns>
	public LogMessageCollectionFilteringAccessor<TMessage> GetFilteringAccessor(ILogMessageCollectionFilter<TMessage> filter)
	{
		return new LogMessageCollectionFilteringAccessor<TMessage>(this, filter);
	}

	#endregion

	#region Managing Overview Collections

	private readonly Dictionary<string, int> mUsedLogWriterCounts       = new();
	private readonly Dictionary<string, int> mUsedLogLevelCounts        = new();
	private readonly Dictionary<string, int> mUsedTagsCounts            = new();
	private readonly Dictionary<string, int> mUsedApplicationNameCounts = new();
	private readonly Dictionary<string, int> mUsedProcessNameCounts     = new();
	private readonly Dictionary<int, int>    mUsedProcessIdCounts       = new();

	/// <summary>
	/// Updates the overview collections as the specified message is added to the collection.
	/// </summary>
	/// <param name="message"></param>
	private void UpdateOverviewCollectionsOnAdd(TMessage message)
	{
		RegisterForOverviewCollection(message.LogWriterName, mUsedLogWriterCounts, UsedLogWritersWritable);
		RegisterForOverviewCollection(message.LogLevelName, mUsedLogLevelCounts, UsedLogLevelsWritable);
		RegisterForOverviewCollection(message.Tags, mUsedTagsCounts, UsedTagsWritable);
		RegisterForOverviewCollection(message.ApplicationName, mUsedApplicationNameCounts, UsedApplicationNamesWritable);
		RegisterForOverviewCollection(message.ProcessName, mUsedProcessNameCounts, UsedProcessNamesWritable);
		RegisterForOverviewCollection(message.ProcessId, mUsedProcessIdCounts, UsedProcessIdsWritable);
	}

	/// <summary>
	/// Updates the tracking counter for the specified item and adds it to the overview collection, if necessary.
	/// </summary>
	/// <typeparam name="T">Type of the item to register.</typeparam>
	/// <param name="item">Item to register.</param>
	/// <param name="itemCountMap">Dictionary used to track the number of occurrences of the item in the message set.</param>
	/// <param name="overviewCollection">Overview collection to add the item to.</param>
	private static void RegisterForOverviewCollection<T>(T item, IDictionary<T, int> itemCountMap, ICollection<T> overviewCollection)
	{
		if (!itemCountMap.TryGetValue(item, out int count)) itemCountMap.Add(item, 0);
		itemCountMap[item] = count + 1;
		if (count == 0) overviewCollection.Add(item);
	}

	/// <summary>
	/// Updates the tracking counter for the specified items and adds them to the overview collection, if necessary.
	/// </summary>
	/// <typeparam name="T">Type of the items to register.</typeparam>
	/// <param name="items">Items to register.</param>
	/// <param name="itemCountMap">Dictionary used to track the number of occurrences of the item in the message set.</param>
	/// <param name="overviewCollection">Overview collection to add the items to.</param>
	private static void RegisterForOverviewCollection<T>(IEnumerable<T> items, IDictionary<T, int> itemCountMap, ICollection<T> overviewCollection)
	{
		foreach (T item in items)
		{
			if (!itemCountMap.TryGetValue(item, out int count)) itemCountMap.Add(item, 0);
			itemCountMap[item] = count + 1;
			if (count == 0) overviewCollection.Add(item);
		}
	}

	/// <summary>
	/// Updates the overview collections as the specified message is removed from the collection.
	/// </summary>
	/// <param name="message">Message that is removed from the collection (<c>null</c> if all messages are removed).</param>
	private void UpdateOverviewCollectionsOnRemove(TMessage message)
	{
		if (message != null)
		{
			// unregister a single message

			UnregisterForOverviewCollection(message.LogWriterName, mUsedLogWriterCounts, UsedLogWritersWritable);
			UnregisterForOverviewCollection(message.LogLevelName, mUsedLogLevelCounts, UsedLogLevelsWritable);
			UnregisterForOverviewCollection(message.Tags, mUsedTagsCounts, UsedTagsWritable);
			UnregisterForOverviewCollection(message.ApplicationName, mUsedApplicationNameCounts, UsedApplicationNamesWritable);
			UnregisterForOverviewCollection(message.ProcessName, mUsedProcessNameCounts, UsedProcessNamesWritable);
			UnregisterForOverviewCollection(message.ProcessId, mUsedProcessIdCounts, UsedProcessIdsWritable);
		}
		else
		{
			// unregister all messages

			mUsedLogWriterCounts.Clear();
			mUsedLogLevelCounts.Clear();
			mUsedTagsCounts.Clear();
			mUsedApplicationNameCounts.Clear();
			mUsedProcessNameCounts.Clear();
			mUsedProcessIdCounts.Clear();

			UsedLogWritersWritable.Clear();
			UsedLogLevelsWritable.Clear();
			UsedTagsWritable.Clear();
			UsedApplicationNamesWritable.Clear();
			UsedProcessNamesWritable.Clear();
			UsedProcessIdsWritable.Clear();
		}
	}

	/// <summary>
	/// Updates the tracking counter for the specified item and removes it from the overview collection, if necessary.
	/// </summary>
	/// <typeparam name="T">Type of the item to unregister.</typeparam>
	/// <param name="item">Item to unregister.</param>
	/// <param name="itemCountMap">Dictionary used to track the number of occurrences of the item in the message set.</param>
	/// <param name="overviewCollection">Overview collection to remove the item from.</param>
	private static void UnregisterForOverviewCollection<T>(T item, IDictionary<T, int> itemCountMap, ICollection<T> overviewCollection)
	{
		if (itemCountMap.TryGetValue(item, out int count))
		{
			if (--count > 0)
			{
				itemCountMap[item] = count;
			}
			else
			{
				itemCountMap.Remove(item);
				overviewCollection.Remove(item);
			}
		}
	}

	/// <summary>
	/// Updates the tracking counter for the specified items and removes them from the overview collection, if necessary.
	/// </summary>
	/// <typeparam name="T">Type of the items to unregister.</typeparam>
	/// <param name="items">Items to unregister.</param>
	/// <param name="itemCountMap">Dictionary used to track the number of occurrences of items in the message set.</param>
	/// <param name="overviewCollection">Overview collection to remove the item from.</param>
	private static void UnregisterForOverviewCollection<T>(IEnumerable<T> items, IDictionary<T, int> itemCountMap, ICollection<T> overviewCollection)
	{
		foreach (T item in items)
		{
			if (itemCountMap.TryGetValue(item, out int count))
			{
				if (--count > 0)
				{
					itemCountMap[item] = count;
				}
				else
				{
					itemCountMap.Remove(item);
					overviewCollection.Remove(item);
				}
			}
		}
	}

	#endregion
}
