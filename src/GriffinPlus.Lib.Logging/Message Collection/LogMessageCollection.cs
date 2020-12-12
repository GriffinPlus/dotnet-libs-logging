///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A collection of log messages
	/// (supports data binding via <see cref="INotifyCollectionChanged" /> and <see cref="INotifyPropertyChanged" />).
	/// </summary>
	public class LogMessageCollection<TMessage> : ILogMessageCollection<TMessage> where TMessage : ILogMessage
	{
		private readonly List<TMessage> mMessages;

		/// <summary>
		/// Occurs when the collection has changed.
		/// </summary>
		public event NotifyCollectionChangedEventHandler CollectionChanged;

		/// <summary>
		/// Occurs when a property has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageCollection{TMessage}" /> class.
		/// </summary>
		public LogMessageCollection()
		{
			mMessages = new List<TMessage>();
		}

		/// <summary>
		/// Disposes the collection (actually does nothing, just to satisfy the interface).
		/// </summary>
		public void Dispose()
		{
		}

		/// <summary>
		/// Gets the number of log messages that have passed the filter.
		/// </summary>
		public long Count => mMessages.Count;

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="NotSupportedException">Setting a log message is not supported.</exception>
		public TMessage this[long index]
		{
			get
			{
				if (index < 0 || index > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(index));
				return mMessages[(int)index];
			}

			set => throw new NotSupportedException("Setting a log message is not supported.");
		}

		/// <summary>
		/// Gets a value indicating whether the collection is read-only (always false).
		/// </summary>
		public bool IsReadOnly => false;

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		/// <returns>Enumerator for iterating over the collection.</returns>
		public IEnumerator<TMessage> GetEnumerator()
		{
			return mMessages.GetEnumerator();
		}

		/// <summary>
		/// Checks whether the collection contains the specified log message.
		/// </summary>
		/// <param name="item">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		public bool Contains(TMessage item)
		{
			return mMessages.Contains(item);
		}

		/// <summary>
		/// Gets the index of the specified log message.
		/// </summary>
		/// <param name="item">Log message to locate in the collection.</param>
		/// <returns>Index of the log message; -1, if the specified message is not in the collection.</returns>
		public int IndexOf(TMessage item)
		{
			return mMessages.IndexOf(item);
		}

		/// <summary>
		/// Adds a log message to the collection.
		/// </summary>
		/// <param name="item">Log message to add.</param>
		public void Add(TMessage item)
		{
			mMessages.Add(item);

			var handler = CollectionChanged;
			handler?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));

			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
		}

		/// <summary>
		/// Adds multiple log messages to the collection at once.
		/// </summary>
		/// <param name="messages">Log messages to add.</param>
		public void AddRange(IEnumerable<TMessage> messages)
		{
			var handler = CollectionChanged;
			if (handler != null)
			{
				// many WPF controls do not support multi-item adds, so adding messages one by one is necessary...
				foreach (var message in messages)
				{
					mMessages.Add(message);
					handler(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, message));
				}
			}
			else
			{
				mMessages.AddRange(messages);
			}

			OnPropertyChanged("Count");
			OnPropertyChanged("Item[]");
		}

		/// <summary>
		/// Inserts a log message at the specified position (not supported).
		/// </summary>
		/// <param name="index">The zero-based index at which value should be inserted.</param>
		/// <param name="item">The log message to insert into the collection.</param>
		/// <exception cref="NotSupportedException">Inserting is not supported.</exception>
		public void Insert(int index, TMessage item)
		{
			throw new NotSupportedException("Inserting log messages is not supported.");
		}

		/// <summary>
		/// Removes the specified log message from the collection (not supported).
		/// </summary>
		/// <param name="item">Log message to remove from the collection.</param>
		/// <returns>true, if the log message was removed; otherwise false.</returns>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		public bool Remove(TMessage item)
		{
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		/// <summary>
		/// Copies all log messages into the specified array.
		/// </summary>
		/// <param name="array">Array to copy the log messages into.</param>
		/// <param name="arrayIndex">Index in the array to start copying to.</param>
		public void CopyTo(TMessage[] array, int arrayIndex)
		{
			mMessages.CopyTo(array, arrayIndex);
		}

		/// <summary>
		/// Removes all log messages from the collection.
		/// </summary>
		public void Clear()
		{
			var handler = CollectionChanged;
			if (mMessages.Count > 0)
			{
				mMessages.Clear();
				handler?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
				OnPropertyChanged("Count");
				OnPropertyChanged("Item[]");
			}
		}

		#region Implementation of IEnumerable, ICollection and IList

		/// <summary>
		/// Gets a value indicating whether the collection is synchronized (always false).
		/// </summary>
		bool ICollection.IsSynchronized => false;

		/// <summary>
		/// Gets a value indicating whether the collection is of fixed size (always false).
		/// </summary>
		bool IList.IsFixedSize => false;

		/// <summary>
		/// Gets an object that can be used to synchronize the collection.
		/// </summary>
		object ICollection.SyncRoot => this;

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="NotSupportedException">Setting a log message is not supported.</exception>
		object IList.this[int index]
		{
			get => mMessages[index];
			set => throw new NotSupportedException("Setting a log message is not supported.");
		}

		/// <summary>
		/// Gets the total number of log messages in the collection.
		/// </summary>
		int ICollection.Count => mMessages.Count;

		/// <summary>
		/// Gets an enumerator iterating over the collection.
		/// </summary>
		/// <returns>Enumerator for iterating over the collection.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			return mMessages.GetEnumerator();
		}

		/// <summary>
		/// Checks whether the collection contains the specified log message.
		/// </summary>
		/// <param name="item">Log message to check for.</param>
		/// <returns>true, if the collection contains the log message; otherwise false.</returns>
		bool IList.Contains(object item)
		{
			return Contains((TMessage)item);
		}

		/// <summary>
		/// Gets the index of the specified log message.
		/// </summary>
		/// <param name="item">Log message to locate in the collection.</param>
		/// <returns>
		/// Index of the log message;
		/// -1, if the specified message is not in the collection.
		/// </returns>
		int IList.IndexOf(object item)
		{
			return IndexOf((TMessage)item);
		}

		/// <summary>
		/// Adds a log message to the collection.
		/// </summary>
		/// <param name="item">Log message to add.</param>
		/// <returns>Index of the added item.</returns>
		int IList.Add(object item)
		{
			Add((TMessage)item);
			return mMessages.Count - 1;
		}

		/// <summary>
		/// Inserts a log message at the specified position (not supported).
		/// </summary>
		/// <param name="index">The zero-based index at which the log message should be inserted.</param>
		/// <param name="item">The log message to insert into the collection.</param>
		/// <exception cref="NotSupportedException">Inserting is not supported.</exception>
		void IList.Insert(int index, object item)
		{
			Insert(index, (TMessage)item);
		}

		/// <summary>
		/// Removes the specified log message from the collection (not supported).
		/// </summary>
		/// <param name="item">Log message to remove from the collection.</param>
		/// <returns>true, if the log message was removed; otherwise false.</returns>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		void IList.Remove(object item)
		{
			Remove((TMessage)item);
		}

		/// <summary>
		/// Removes the log message at the specified index
		/// (not supported, removing log messages would mess up log message counting).
		/// </summary>
		/// <param name="index">Index of the log message to remove.</param>
		/// <exception cref="NotSupportedException">Removing log messages is not supported.</exception>
		public void RemoveAt(int index)
		{
			throw new NotSupportedException("Removing log messages is not supported.");
		}

		/// <summary>
		/// Copies all log messages into the specified array.
		/// </summary>
		/// <param name="array">Array to copy the log messages into.</param>
		/// <param name="arrayIndex">Index in the array to start copying to.</param>
		void ICollection.CopyTo(Array array, int arrayIndex)
		{
			mMessages.CopyTo((TMessage[])array, arrayIndex);
		}

		#endregion

		#region Explicit Implementation of IEnumerable<T>, ICollection<T> and IList<T>

		/// <summary>
		/// Gets the log message at the specified index.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>The log message at the specified index.</returns>
		/// <exception cref="NotSupportedException">Setting a log message is not supported.</exception>
		TMessage IList<TMessage>.this[int index]
		{
			get => mMessages[index];
			set => throw new NotSupportedException("Setting a log message is not supported.");
		}

		/// <summary>
		/// Gets the total number of log messages in the collection
		/// </summary>
		int ICollection<TMessage>.Count => mMessages.Count;

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
	}

}
