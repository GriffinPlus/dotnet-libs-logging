///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Base class for log message collections that implements common functionality.
	/// This collection is read/write by default.
	/// </summary>
	/// <typeparam name="TMessage">Type of the log message stored in the collection.</typeparam>
	public abstract class LogMessageCollectionBase<TMessage> : ILogMessageCollection<TMessage>
		where TMessage : class, ILogMessage
	{
		#region Construction and Disposal

		private bool mDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageCollectionBase{TMessage}"/> class.
		/// </summary>
		protected LogMessageCollectionBase()
		{
			UsedLogWritersWritable = new ObservableCollection<string>();
			UsedLogWriters = new ReadOnlyObservableCollection<string>(UsedLogWritersWritable);
			UsedLogLevelsWritable = new ObservableCollection<string>();
			UsedLogLevels = new ReadOnlyObservableCollection<string>(UsedLogLevelsWritable);
			UsedTagsWritable = new ObservableCollection<string>();
			UsedTags = new ReadOnlyObservableCollection<string>(UsedTagsWritable);
			UsedApplicationNamesWritable = new ObservableCollection<string>();
			UsedApplicationNames = new ReadOnlyObservableCollection<string>(UsedApplicationNamesWritable);
			UsedProcessNamesWritable = new ObservableCollection<string>();
			UsedProcessNames = new ReadOnlyObservableCollection<string>(UsedProcessNamesWritable);
			UsedProcessIdsWritable = new ObservableCollection<int>();
			UsedProcessIds = new ReadOnlyObservableCollection<int>(UsedProcessIdsWritable);
		}

		/// <summary>
		/// Disposes the collection.
		/// </summary>
		public void Dispose()
		{
			if (!mDisposed)
			{
				Dispose(true);
				mDisposed = true;
			}
		}

		/// <summary>
		/// Disposes the collection.
		/// </summary>
		/// <param name="disposing">
		/// true if the object is being disposed;
		/// false, if it is being finalized.
		/// </param>
		protected abstract void Dispose(bool disposing);

		#endregion

		#region CollectionChanged

		/// <summary>
		/// Occurs when the collection has changed.
		/// </summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary>
		/// Gets a value indicating whether the <see cref="CollectionChanged"/> event has been registered,
		/// so raising the event is necessary.
		/// </summary>
		protected bool IsCollectionChangedRegistered => CollectionChanged != null;

		/// <summary>
		/// Raises the <see cref="CollectionChanged"/> event.
		/// </summary>
		/// <param name="e">Event arguments to pass to event handlers.</param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			var handler = CollectionChanged;
			handler?.Invoke(this, e);
		}

		#endregion

		#region PropertyChanged

		/// <summary>
		/// Occurs when a property has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="name">Name of the property that has changed.</param>
		protected virtual void OnPropertyChanged([CallerMemberName] string name = null)
		{
			var handler = PropertyChanged;
			handler?.Invoke(this, new PropertyChangedEventArgs(name));
		}

		#endregion

		#region Count

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		public abstract long Count { get; }

		/// <summary>
		/// Gets the total number of log messages in the collection
		/// </summary>
		/// <exception cref="NotSupportedException">The collection is too large to be accessed via the ICollection interface.</exception>
		int ICollection<TMessage>.Count
		{
			get
			{
				long count = Count;
				if (count > int.MaxValue) throw new NotSupportedException("The collection is too large to be accessed via the ICollection interface.");
				return (int)count;
			}
		}

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		/// <exception cref="NotSupportedException">The collection is too large to be accessed via the ICollection interface.</exception>
		int IReadOnlyCollection<TMessage>.Count
		{
			get
			{
				long count = Count;
				if (count > int.MaxValue) throw new NotSupportedException("The collection is too large to be accessed via the ICollection interface.");
				return (int)count;
			}
		}

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		/// <exception cref="NotSupportedException">The collection is too large to be accessed via the ICollection interface.</exception>
		int ICollection.Count
		{
			get
			{
				long count = Count;
				if (count > int.MaxValue) throw new NotSupportedException("The collection is too large to be accessed via the ICollection interface.");
				return (int)count;
			}
		}

		#endregion

		#region Indexer

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
		public abstract TMessage this[long index] { get; }

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
		/// <exception cref="NotSupportedException">Setting log messages is not supported.</exception>
		TMessage IList<TMessage>.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException("Setting log messages is not supported.");
		}

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
		TMessage IReadOnlyList<TMessage>.this[int index] => this[index];

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
		/// <exception cref="NotSupportedException">Setting log messages is not supported.</exception>
		object IList.this[int index]
		{
			get => this[index];
			set => throw new NotSupportedException("Setting log messages is not supported.");
		}

		#endregion

		#region IsReadOnly

		/// <summary>
		/// Gets a value indicating whether the collection is read-only.
		/// </summary>
		public virtual bool IsReadOnly => false;

		#endregion

		#region IsSynchronized

		/// <summary>
		/// Gets a value indicating whether the collection is synchronized.
		/// </summary>
		public virtual bool IsSynchronized => false;

		#endregion

		#region IsFixedSize

		/// <summary>
		/// Gets a value indicating whether the collection is of fixed size (always false).
		/// </summary>
		public virtual bool IsFixedSize => false;

		#endregion

		#region SyncRoot

		/// <summary>
		/// Gets an object that can be used to synchronize the collection.
		/// </summary>
		public object SyncRoot { get; } = new object();

		#endregion

		#region UseMultiItemNotifications

		/// <summary>
		/// Gets or sets a value indicating whether the <see cref="INotifyCollectionChanged.CollectionChanged"/> event
		/// fires for single messages that are added to or removed from the collection (<c>false</c>) or whether it
		/// bundles them in a single notification (<c>true</c>, default). Some controls do not support multi-item
		/// notifications, so it might be necessary to disable them.
		/// </summary>
		public bool UseMultiItemNotifications { get; set; } = true;

		#endregion

		#region UsedLogWriters

		/// <summary>
		/// The writable collection backing <see cref="UsedLogWriters"/>.
		/// This collection must be updated by the derived class to comply with the provided messages set.
		/// </summary>
		protected ObservableCollection<string> UsedLogWritersWritable;

		/// <summary>
		/// Gets all log writer names that are used by messages in the collection.
		/// </summary>
		public ReadOnlyObservableCollection<string> UsedLogWriters { get; }

		#endregion

		#region UsedLogLevels

		/// <summary>
		/// The writable collection backing <see cref="UsedLogLevels"/>.
		/// This collection must be updated by the derived class to comply with the provided messages set.
		/// </summary>
		protected ObservableCollection<string> UsedLogLevelsWritable;

		/// <summary>
		/// Gets all log level names that are used by messages in the collection.
		/// </summary>
		public ReadOnlyObservableCollection<string> UsedLogLevels { get; }

		#endregion

		#region UsedTags

		/// <summary>
		/// The writable collection backing <see cref="UsedTags"/>.
		/// This collection must be updated by the derived class to comply with the provided messages set.
		/// </summary>
		protected ObservableCollection<string> UsedTagsWritable;

		/// <summary>
		/// Gets all tags that are used by messages in the collection.
		/// </summary>
		public ReadOnlyObservableCollection<string> UsedTags { get; }

		#endregion

		#region UsedApplicationNames

		/// <summary>
		/// The writable collection backing <see cref="UsedApplicationNames"/>.
		/// This collection must be updated by the derived class to comply with the provided messages set.
		/// </summary>
		protected ObservableCollection<string> UsedApplicationNamesWritable;

		/// <summary>
		/// Gets all application names that are used by messages in the collection.
		/// </summary>
		public ReadOnlyObservableCollection<string> UsedApplicationNames { get; }

		#endregion

		#region UsedProcessNames

		/// <summary>
		/// The writable collection backing <see cref="UsedProcessNames"/>.
		/// This collection must be updated by the derived class to comply with the provided messages set.
		/// </summary>
		protected ObservableCollection<string> UsedProcessNamesWritable;

		/// <summary>
		/// Gets all process names that are used by messages in the collection.
		/// </summary>
		public ReadOnlyObservableCollection<string> UsedProcessNames { get; }

		#endregion

		#region UsedProcessIds

		/// <summary>
		/// The writable collection backing <see cref="UsedProcessIds"/>.
		/// This collection must be updated by the derived class to comply with the provided messages set.
		/// </summary>
		protected ObservableCollection<int> UsedProcessIdsWritable;

		/// <summary>
		/// Gets all process ids that are used by messages in the collection.
		/// </summary>
		public ReadOnlyObservableCollection<int> UsedProcessIds { get; }

		#endregion

		#region GetEnumerator()

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		/// <returns>Enumerator for iterating over the collection.</returns>
		public abstract IEnumerator<TMessage> GetEnumerator();

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		/// <returns>Enumerator for iterating over the collection.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion

		#region GetRange()

		/// <summary>
		/// Gets a number of messages starting at the specified index.
		/// </summary>
		/// <param name="index">Index of the message to get.</param>
		/// <param name="count">Number of messages to get.</param>
		/// <returns>The requested log messages.</returns>
		/// <exception cref="NotSupportedException">This implementation does not support returning more than int.MaxValue messages at once.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> and/or <paramref name="count"/> is out of bounds.</exception>
		public virtual IEnumerable<TMessage> GetRange(long index, long count)
		{
			if (count > int.MaxValue) throw new NotSupportedException("The default implementation does not support returning more than int.MaxValue messages at once.");
			if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index), "The index is out of bounds.");
			if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "The count is negative.");
			if (index + count > Count) throw new ArgumentOutOfRangeException(nameof(index), "The requested range exceeds the bounds of the collection.");
			var messages = new TMessage[count];
			for (int i = 0; i < count; i++) messages[i] = this[index + i];
			return messages;
		}

		#endregion

		#region Contains()

		/// <summary>
		/// Checks whether the collection contains the specified log message.
		/// </summary>
		/// <param name="message">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		public abstract bool Contains(TMessage message);

		/// <summary>
		/// Checks whether the collection contains the specified log message.
		/// </summary>
		/// <param name="item">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		bool IList.Contains(object item)
		{
			return Contains((TMessage)item);
		}

		#endregion

		#region IndexOf()

		/// <summary>
		/// Gets the index of the specified log message.
		/// </summary>
		/// <param name="message">Log message to locate in the collection.</param>
		/// <returns>
		/// Index of the log message;
		/// -1, if the specified message is not in the collection.
		/// </returns>
		public abstract long IndexOf(TMessage message);

		/// <summary>
		/// Gets the index of the specified log message.
		/// </summary>
		/// <param name="item">Log message to locate in the collection.</param>
		/// <returns>
		/// Index of the log message;
		/// -1, if the specified message is not in the collection.
		/// </returns>
		/// <exception cref="NotSupportedException">The collection is too large to be accessed via the ICollection interface.</exception>
		int IList<TMessage>.IndexOf(TMessage item)
		{
			if (Count > int.MaxValue) throw new NotSupportedException("The collection is too large to be accessed via the ICollection interface.");
			return (int)IndexOf(item);
		}

		/// <summary>
		/// Gets the index of the specified log message.
		/// </summary>
		/// <param name="item">Log message to locate in the collection.</param>
		/// <returns>
		/// Index of the log message;
		/// -1, if the specified message is not in the collection.
		/// </returns>
		/// <exception cref="NotSupportedException">The collection is too large to be accessed via the ICollection interface.</exception>
		int IList.IndexOf(object item)
		{
			if (Count > int.MaxValue) throw new NotSupportedException("The collection is too large to be accessed via the ICollection interface.");
			return (int)IndexOf((TMessage)item);
		}

		#endregion

		#region Add()

		/// <summary>
		/// Adds a log message to the collection.
		/// </summary>
		/// <param name="message">Log message to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="message"/> is <c>null</c>.</exception>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public abstract void Add(TMessage message);

		/// <summary>
		/// Adds a log message to the collection.
		/// </summary>
		/// <param name="item">Log message to add.</param>
		/// <returns>Index of the added item.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="item"/> is <c>null</c>.</exception>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		/// <exception cref="NotSupportedException">The collection is too large to be accessed via the ICollection interface.</exception>
		int IList.Add(object item)
		{
			long count = Count;
			if (count >= int.MaxValue) throw new NotSupportedException("The collection is too large to be accessed via the IList interface.");
			Add((TMessage)item);
			return (int)count - 1;
		}

		#endregion

		#region AddRange()

		/// <summary>
		/// Adds multiple log messages to the collection at once.
		/// </summary>
		/// <param name="messages">Log messages to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="messages"/> is <c>null</c>.</exception>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public abstract void AddRange(IEnumerable<TMessage> messages);

		#endregion

		#region Insert()

		/// <summary>
		/// Inserts a log message at the specified position (not supported).
		/// </summary>
		/// <param name="index">The zero-based index at which value should be inserted.</param>
		/// <param name="item">The log message to insert into the collection.</param>
		/// <exception cref="NotSupportedException">Inserting log messages is not supported.</exception>
		void IList<TMessage>.Insert(int index, TMessage item)
		{
			throw new NotSupportedException("Inserting log messages is not supported.");
		}

		/// <summary>
		/// Inserts a log message at the specified position (not supported).
		/// </summary>
		/// <param name="index">The zero-based index at which the log message should be inserted.</param>
		/// <param name="item">The log message to insert into the collection.</param>
		/// <exception cref="NotSupportedException">Inserting log messages is not supported.</exception>
		void IList.Insert(int index, object item)
		{
			throw new NotSupportedException("Inserting log messages is not supported.");
		}

		#endregion

		#region Remove()

		/// <summary>
		/// Removes the specified log message from the collection (not supported).
		/// </summary>
		/// <param name="item">Log message to remove from the collection.</param>
		/// <returns>true, if the log message was removed; otherwise false.</returns>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		bool ICollection<TMessage>.Remove(TMessage item)
		{
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		/// <summary>
		/// Removes the specified log message from the collection (not supported).
		/// </summary>
		/// <param name="item">Log message to remove from the collection.</param>
		/// <returns>true, if the log message was removed; otherwise false.</returns>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		void IList.Remove(object item)
		{
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		#endregion

		#region RemoveAt()

		/// <summary>
		/// Removes the log message at the specified index (not supported).
		/// </summary>
		/// <param name="index">Index of the log message to remove.</param>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		void IList<TMessage>.RemoveAt(int index)
		{
			// not supported, removing log messages would mess up log message counting
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		/// <summary>
		/// Removes the log message at the specified index (not supported).
		/// </summary>
		/// <param name="index">Index of the log message to remove.</param>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		void IList.RemoveAt(int index)
		{
			// not supported, removing log messages would mess up log message counting
			throw new NotSupportedException("Removing log messages is not supported.");
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
		public virtual void CopyTo(TMessage[] array, int arrayIndex)
		{
			if (array == null) throw new ArgumentNullException(nameof(array));
			if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "The array index is negative.");
			if (array.Rank != 1) throw new ArgumentException("The specified array is multi-dimensional.");
			if (arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex), "The array index is outside the specified array.");
			if (Count > array.Length - arrayIndex) throw new ArgumentException("The specified array is too small to receive all log messages.");

			using (var enumerator = GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					array[arrayIndex++] = enumerator.Current;
				}
			}
		}

		/// <summary>
		/// Copies all log messages into the specified array.
		/// </summary>
		/// <param name="array">Array to copy the log messages into.</param>
		/// <param name="arrayIndex">Index in the array to start copying to.</param>
		/// <exception cref="ArgumentNullException"><paramref name="array"/> is <c>null</c>.</exception>
		/// <exception cref="ArgumentException"><paramref name="array"/> is no a one-dimensional array or the array is too small to store all messages.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is out of bounds.</exception>
		void ICollection.CopyTo(Array array, int arrayIndex)
		{
			CopyTo((TMessage[])array, arrayIndex);
		}

		#endregion

		#region Clear()

		/// <summary>
		/// Removes all log messages from the collection.
		/// </summary>
		/// <exception cref="NotSupportedException">The collection is read-only.</exception>
		public abstract void Clear();

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
		public abstract void Prune(long maximumMessageCount, DateTime minimumMessageTimestamp);

		#endregion
	}

}
