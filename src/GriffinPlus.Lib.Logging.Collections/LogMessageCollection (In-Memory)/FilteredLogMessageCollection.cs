///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

// ReSharper disable ExplicitCallerInfoArgument

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// A collection of log messages
/// (supports data binding via <see cref="INotifyCollectionChanged"/> and <see cref="INotifyPropertyChanged"/>).
/// </summary>
/// <typeparam name="TMessage">The log message type.</typeparam>
public class FilteredLogMessageCollection<TMessage> :
	FilteredLogMessageCollectionBase<TMessage, LogMessageCollection<TMessage>, ILogMessageCollectionFilter<TMessage>>
	where TMessage : class, ILogMessage
{
	private readonly List<TMessage> mMessages = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="LogMessageCollection{TMessage}"/> class.
	/// </summary>
	/// <param name="unfiltered">Collection containing the unfiltered message set.</param>
	/// <param name="filter">Filter to apply to the unfiltered message set.</param>
	internal FilteredLogMessageCollection(
		LogMessageCollection<TMessage>        unfiltered,
		ILogMessageCollectionFilter<TMessage> filter) : base(unfiltered)
	{
		Filter = filter ?? throw new ArgumentNullException(nameof(filter));
	}

	/// <summary>
	/// Disposes the collection (actually does nothing, just to satisfy the interface).
	/// </summary>
	/// <param name="disposing">
	/// <c>true</c> if the object is being disposed;<br/>
	/// <c>false</c> if it is being finalized.
	/// </param>
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			Unfiltered.UnregisterFilteredCollection(this);
		}
	}

	#region Count

	/// <summary>
	/// Gets the number of log messages that have passed the filter.
	/// </summary>
	public override long Count => mMessages.Count;

	#endregion

	#region Indexer

	/// <summary>
	/// Gets the log message at the specified index.
	/// </summary>
	/// <param name="index">Index of the log message to get.</param>
	/// <returns>The log message at the specified index.</returns>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
	public override TMessage this[long index] => mMessages[(int)index]; // can throw ArgumentOutOfRangeException

	#endregion

	#region GetEnumerator()

	/// <summary>
	/// Gets an enumerator iterating over the collection.
	/// </summary>
	/// <returns>Enumerator for iterating over the collection.</returns>
	public override IEnumerator<TMessage> GetEnumerator()
	{
		return mMessages.GetEnumerator();
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
		return mMessages.Contains(item);
	}

	#endregion

	#region IndexOf()

	/// <summary>
	/// Gets the index of the specified log message.
	/// </summary>
	/// <param name="item">Log message to locate in the collection.</param>
	/// <returns>
	/// Index of the log message;<br/>
	/// -1, if the specified message is not in the collection.
	/// </returns>
	public override long IndexOf(TMessage item)
	{
		return mMessages.IndexOf(item);
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

		mMessages.CopyTo(array, arrayIndex);
	}

	#endregion

	#region Tracking Changes in Unfiltered Collection

	/// <summary>
	/// Is called when the unfiltered log message collection adds new messages.
	/// </summary>
	/// <param name="startIndex">The index of the first new message in the unfiltered collection.</param>
	/// <param name="count">The number of new messages.</param>
	internal void ProcessUnfilteredCollectionChange_AfterAdd(int startIndex, int count)
	{
		List<TMessage> newMessages = IsCollectionChangedRegistered && UseMultiItemNotifications
			                             ? new List<TMessage>()
			                             : null;

		int newMessagesIndex = mMessages.Count;

		for (int i = startIndex; i < startIndex + count; i++)
		{
			TMessage message = Unfiltered[i];

			if (Filter.Matches(message))
			{
				mMessages.Add(message);
				newMessages?.Add(message);

				// update the overview collection to reflect the change
				UpdateOverviewCollectionsOnAdd(message);

				// raise CollectionChanged event, if using single-item notifications
				if (IsCollectionChangedRegistered && !UseMultiItemNotifications)
				{
					OnCollectionChanged(
						new NotifyCollectionChangedEventArgs(
							NotifyCollectionChangedAction.Add,
							message,
							mMessages.Count - 1));
				}
			}
		}

		// raise CollectionChanged event, if using multi-item notifications
		if (IsCollectionChangedRegistered && UseMultiItemNotifications)
		{
			OnCollectionChanged(
				new NotifyCollectionChangedEventArgs(
					NotifyCollectionChangedAction.Add,
					newMessages,
					newMessagesIndex));
		}

		OnPropertyChanged("Count");
		OnPropertyChanged("Item[]");
	}

	/// <summary>
	/// Is called just before log messages are are removed from the unfiltered collection.
	/// </summary>
	/// <param name="startIndex">The index of the first message in the unfiltered collection that is about to be removed.</param>
	/// <param name="count">The number of messages that are about to be removed.</param>
	internal void ProcessUnfilteredCollectionChange_BeforeRemoving(int startIndex, int count)
	{
		// abort, if the filtered collection is empty
		if (mMessages.Count == 0)
			return;

		// find the first message to remove from the filtered collection
		int firstMatchingMessageIndex_filtered = -1;
		for (int i = startIndex; i < startIndex + count; i++)
		{
			TMessage message = Unfiltered[i];
			if (Filter.Matches(message))
			{
				// found first message in specified range that matches the filter
				// => this message should be in the filtered collection, find it!
				TMessage firstMatchingMessage = message;
				int firstMatchingMessageIndex_unfiltered = i;
				for (int j = Math.Min(firstMatchingMessageIndex_unfiltered, mMessages.Count - 1); j >= 0; j--)
				{
					if (ReferenceEquals(mMessages[j], message))
					{
						firstMatchingMessageIndex_filtered = j;
						break;
					}
				}

				Debug.Assert(firstMatchingMessage != null);
				Debug.Assert(firstMatchingMessageIndex_unfiltered >= 0);
				Debug.Assert(firstMatchingMessageIndex_filtered >= 0);
				break;
			}
		}

		if (firstMatchingMessageIndex_filtered >= 0)
		{
			// find the last message to remove from the filtered collection
			int lastMatchingMessageIndex_filtered = -1;
			for (int i = startIndex + count - 1; i >= startIndex; i--)
			{
				TMessage message = Unfiltered[i];
				if (Filter.Matches(message))
				{
					// found last message in specified range that matches the filter
					// => this message should be in the filtered collection, find it!
					TMessage lastMatchingMessage = message;
					int lastMatchingMessageIndex_unfiltered = i;
					for (int j = firstMatchingMessageIndex_filtered; j <= Math.Min(lastMatchingMessageIndex_unfiltered, mMessages.Count - 1); j++)
					{
						if (ReferenceEquals(mMessages[j], message))
						{
							lastMatchingMessageIndex_filtered = j;
							break;
						}
					}

					Debug.Assert(lastMatchingMessage != null);
					Debug.Assert(lastMatchingMessageIndex_unfiltered >= 0);
					Debug.Assert(lastMatchingMessageIndex_filtered >= 0);
					break;
				}
			}

			// update overview collections to reflect the change
			for (int i = firstMatchingMessageIndex_filtered; i <= lastMatchingMessageIndex_filtered; i++)
			{
				UpdateOverviewCollectionsOnRemove(mMessages[i]);
			}

			int numberOfMessagesToRemove = lastMatchingMessageIndex_filtered - firstMatchingMessageIndex_filtered + 1;
			if (IsCollectionChangedRegistered)
			{
				// save messages to remove for the event
				var removedMessages = new TMessage[numberOfMessagesToRemove];
				mMessages.CopyTo(firstMatchingMessageIndex_filtered, removedMessages, 0, numberOfMessagesToRemove);

				// remove messages
				mMessages.RemoveRange(firstMatchingMessageIndex_filtered, numberOfMessagesToRemove);

				// notify clients that the collection has changed
				OnCollectionChanged(
					new NotifyCollectionChangedEventArgs(
						NotifyCollectionChangedAction.Remove,
						removedMessages,
						0));
			}
			else
			{
				// remove messages
				mMessages.RemoveRange(firstMatchingMessageIndex_filtered, numberOfMessagesToRemove);
			}

			// notify clients that collection properties have changed
			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
		}
	}

	/// <summary>
	/// Notifies filtered collections that the collection was cleared.
	/// </summary>
	internal void ProcessUnfilteredCollectionChange_Clear()
	{
		Rebuild();
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

	#region Rebuild()

	/// <summary>
	/// Invalidates the filtered message set and triggers re-filtering the unfiltered message set using the current filter settings.
	/// It is usually called from an attached filter to rebuild the message set according to new filter settings.
	/// </summary>
	protected internal override void Rebuild()
	{
		mMessages.Clear();

		if (Filter != null)
		{
			foreach (TMessage message in Unfiltered)
			{
				if (Filter.Matches(message))
				{
					mMessages.Add(message);
				}
			}
		}
		else
		{
			mMessages.AddRange(Unfiltered);
		}

		// raise CollectionChanged event
		if (IsCollectionChangedRegistered)
			OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

		OnPropertyChanged("Count");
		OnPropertyChanged("Item[]");
	}

	#endregion
}
