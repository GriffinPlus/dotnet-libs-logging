///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

using Xunit;


// ReSharper disable InvalidXmlDocComment
// ReSharper disable RedundantCast

#pragma warning disable 1574
#pragma warning disable 1584 // XML comment has syntactically incorrect cref attribute

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="ILogMessageCollection{TMessage}" /> interface of collection implementations.
	/// This class tests extended interfaces separately to cover explicit interface implementations, if existing.
	/// </summary>
	public abstract class LogMessageCollectionBaseTests
	{
		#region Adjustments of Derived Test Classes

		/// <summary>
		/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
		/// </summary>
		/// <param name="count">Number of random log messages the collection should contain.</param>
		/// <param name="messages">Receives messages that have been put into the collection.</param>
		/// <returns>A new instance of the collection class to test.</returns>
		protected abstract ILogMessageCollection<LogMessage> CreateCollection(int count, out LogMessage[] messages);

		/// <summary>
		/// Gets or sets a value indicating whether the collection is expected to be read-only.
		/// </summary>
		protected bool CollectionIsReadOnly { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether the collection provides protected messages that cannot be modified.
		/// </summary>
		protected bool CollectionProvidesProtectedMessages { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether the collection has a fixed size.
		/// </summary>
		protected bool CollectionIsFixedSize { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether the collection is synchronized.
		/// </summary>
		protected bool CollectionIsSynchronized { get; set; } = false;

		#endregion

		#region CollectionChanged

		/// <summary>
		/// Tests registering and unregistering the <see cref="INotifyCollectionChanged.CollectionChanged" /> event.
		/// </summary>
		[Fact]
		protected virtual void INotifyCollectionChanged_CollectionChanged()
		{
			void Handler(object sender, NotifyCollectionChangedEventArgs args)
			{
			}

			using (var collection = CreateCollection(0, out _))
			{
				// register and unregister the event
				// (firing event is tested as part of the methods modifying the collection)
				collection.CollectionChanged += Handler;
				collection.CollectionChanged -= Handler;
			}
		}

		#endregion

		#region PropertyChanged

		/// <summary>
		/// Tests registering and unregistering the <see cref="INotifyPropertyChanged.PropertyChanged" /> event.
		/// </summary>
		[Fact]
		protected virtual void INotifyPropertyChanged_PropertyChanged()
		{
			void Handler(object sender, PropertyChangedEventArgs args)
			{
			}

			using (var collection = CreateCollection(0, out _))
			{
				// register and unregister the event
				// (firing event is tested as part of the methods modifying the collection)
				collection.PropertyChanged += Handler;
				collection.PropertyChanged -= Handler;
			}
		}

		#endregion

		#region Count

		/// <summary>
		/// Tests getting the number of log messages in the collection using <see cref="ICollection.Count" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollection_Count(int initialCollectionSize)
		{
			Common_Count(
				initialCollectionSize,
				collection => ((ICollection)collection).Count);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests getting the number of log messages in the collection using <see cref="ICollection{T}.Count" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollectionT_Count(int initialCollectionSize)
		{
			Common_Count(
				initialCollectionSize,
				collection => ((ICollection<LogMessage>)collection).Count);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests getting the number of log messages in the collection using <see cref="ILogMessageCollection{T}.Count" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ILogMessageCollectionT_Count(int initialCollectionSize)
		{
			Common_Count(
				initialCollectionSize,
				collection => ((ILogMessageCollection<LogMessage>)collection).Count);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for getting the number of log messages in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Count(
			int                                           initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, long> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and compare with the expected count
				long count = operation(collection);
				Assert.Equal(initialCollectionSize, count);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region IsReadOnly

		/// <summary>
		/// Tests whether the collection is read-only using <see cref="IList.IsReadOnly" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		protected virtual void IList_IsReadOnly(int initialCollectionSize)
		{
			Common_IsReadOnly(
				initialCollectionSize,
				collection => ((IList)collection).IsReadOnly);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests whether the collection is read-only using <see cref="ICollection{T}.IsReadOnly" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		protected virtual void ICollectionT_IsReadOnly(int initialCollectionSize)
		{
			Common_IsReadOnly(
				initialCollectionSize,
				collection => ((ICollection<LogMessage>)collection).IsReadOnly);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for getting a value that indicates whether the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_IsReadOnly(
			int                                           initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, bool> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and compare with the expected state
				bool isReadOnly = operation(collection);
				Assert.Equal(CollectionIsReadOnly, isReadOnly);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region IsFixedSize

		/// <summary>
		/// Tests whether the collection has a fixed size using <see cref="IList.IsFixedSize" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		protected virtual void IList_IsFixedSize(int initialCollectionSize)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// check whether the actual state of the property matches the expected state
				Assert.Equal(CollectionIsFixedSize, ((IList)collection).IsFixedSize);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region SyncRoot

		/// <summary>
		/// Tests whether the collection has a sync root using <see cref="ICollection.SyncRoot" /> and that the object is not the same as the collection itself
		/// (can cause deadlocks, if the collection itself is used for synchronization as well).
		/// </summary>
		[Fact]
		protected virtual void ICollection_SyncRoot()
		{
			using (var collection = CreateCollection(0, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// check whether the sync root is not a null reference and whether it is not the same as the collection itself
				// (can cause deadlocks, if the collection itself is used for synchronization as well)
				var syncRoot = ((ICollection)collection).SyncRoot;
				Assert.NotNull(syncRoot);
				Assert.NotSame(collection, syncRoot);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region IsSynchronized

		/// <summary>
		/// Tests whether the collection is synchronized using <see cref="ICollection.IsSynchronized" />.
		/// </summary>
		[Fact]
		protected virtual void ICollection_IsSynchronized()
		{
			using (var collection = CreateCollection(0, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// check whether the actual state of the property matches the expected state
				Assert.Equal(CollectionIsSynchronized, ((ICollection)collection).IsSynchronized);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Indexer (Getter)

		/// <summary>
		/// Tests getting the log message at a specific index using <see cref="IList.this" /> that is inside the allowed range.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to try to get.</param>
		[Theory]
		[InlineData(1000, 0)]
		[InlineData(1000, 999)]
		protected virtual void IList_Indexer_Get_Success(int initialCollectionSize, int indexOfMessageToGet)
		{
			Common_Indexer_Get_Success(
				initialCollectionSize,
				indexOfMessageToGet,
				(collection, index) => ((IList)collection)[index]);
		}

		/// <summary>
		/// Tests getting the log message at a specific index using <see cref="IList.this" /> that is outside the allowed range.
		/// Should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to try to get.</param>
		[Theory]
		[InlineData(10, -1)]
		[InlineData(10, 10)]
		protected virtual void IList_Indexer_Get_IndexOutOfBounds(int initialCollectionSize, int indexOfMessageToGet)
		{
			Common_Indexer_Get_IndexOutOfBounds(
				initialCollectionSize,
				indexOfMessageToGet,
				(collection, index) => ((IList)collection)[index]);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests getting the log message at a specific index using <see cref="IList{T}.this" /> that is inside the allowed range.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to try to get.</param>
		[Theory]
		[InlineData(1000, 0)]
		[InlineData(1000, 999)]
		protected virtual void IListT_Indexer_Get_Success(int initialCollectionSize, int indexOfMessageToGet)
		{
			Common_Indexer_Get_Success(
				initialCollectionSize,
				indexOfMessageToGet,
				(collection, index) => ((IList<LogMessage>)collection)[index]);
		}

		/// <summary>
		/// Tests getting the log message at a specific index using <see cref="IList{T}.this" /> that is outside the allowed range.
		/// Should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to try to get.</param>
		[Theory]
		[InlineData(10, -1)]
		[InlineData(10, 10)]
		protected virtual void IListT_Indexer_Get_IndexOutOfBounds(int initialCollectionSize, int indexOfMessageToGet)
		{
			Common_Indexer_Get_IndexOutOfBounds(
				initialCollectionSize,
				indexOfMessageToGet,
				(collection, index) => ((IList<LogMessage>)collection)[index]);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests getting the log message at a specific index using <see cref="ILogMessageCollection{TMessage}.this(long)" /> that is inside the allowed range.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to try to get.</param>
		[Theory]
		[InlineData(1000, 0)]
		[InlineData(1000, 999)]
		protected virtual void ILogMessageCollectionT_Indexer_Get_Success(int initialCollectionSize, int indexOfMessageToGet)
		{
			Common_Indexer_Get_Success(
				initialCollectionSize,
				indexOfMessageToGet,
				(collection, index) => ((ILogMessageCollection<LogMessage>)collection)[index]);
		}

		/// <summary>
		/// Tests getting the log message at a specific index using <see cref="ILogMessageCollection{TMessage}.this(long)" /> that is outside the allowed range.
		/// Should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to try to get.</param>
		[Theory]
		[InlineData(10, -1)]
		[InlineData(10, 10)]
		protected virtual void ILogMessageCollectionT_Indexer_Get_IndexOutOfBounds(int initialCollectionSize, int indexOfMessageToGet)
		{
			Common_Indexer_Get_IndexOutOfBounds(
				initialCollectionSize,
				indexOfMessageToGet,
				(collection, index) => ((ILogMessageCollection<LogMessage>)collection)[index]);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for getting the log message at a specific index in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Indexer_Get_Success(
			int                                                  initialCollectionSize,
			int                                                  indexOfMessageToGet,
			Func<ILogMessageCollection<LogMessage>, int, object> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run the operation and check whether the returned message equals the expected one
				var message = (LogMessage)operation(collection, indexOfMessageToGet);
				Assert.Equal(messages[indexOfMessageToGet], message); // does not take IsReadOnly into account
				Assert.Equal(CollectionProvidesProtectedMessages, message.IsReadOnly);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for getting the log message at a specific index in the collection with an index that is out of bounds.
		/// The operation should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToGet">Index of the message to get.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Indexer_Get_IndexOutOfBounds(
			int                                                  initialCollectionSize,
			int                                                  indexOfMessageToGet,
			Func<ILogMessageCollection<LogMessage>, int, object> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run the operation and ensure that the appropriate exception is thrown
				Assert.Throws<ArgumentOutOfRangeException>(() => operation(collection, indexOfMessageToGet));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Indexer (Setter)

		/// <summary>
		/// Tests setting the log message at a specific index using <see cref="IList.this" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToSet">Index of the message to try to set.</param>
		[Theory]
		[InlineData(10, 0)]  // first log message
		[InlineData(10, 9)]  // last log message
		[InlineData(10, -1)] // out of bounds, but should also throw exception
		[InlineData(10, 10)] // out of bounds, but should also throw exception
		protected virtual void IList_Indexer_Set_NotSupportedException(int initialCollectionSize, int indexOfMessageToSet)
		{
			Common_Indexer_Set_IndexOutOfBounds(
				initialCollectionSize,
				indexOfMessageToSet,
				(collection, index, message) => ((IList)collection)[index] = message);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests setting the log message at a specific index using <see cref="IList{T}.this" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToSet">Index of the message to try to set.</param>
		[Theory]
		[InlineData(10, 0)]  // first log message
		[InlineData(10, 9)]  // last log message
		[InlineData(10, -1)] // out of bounds, but should also throw exception
		[InlineData(10, 10)] // out of bounds, but should also throw exception
		protected virtual void IListT_Indexer_Set_NotSupportedException(int initialCollectionSize, int indexOfMessageToSet)
		{
			Common_Indexer_Set_IndexOutOfBounds(
				initialCollectionSize,
				indexOfMessageToSet,
				(collection, index, message) => ((IList<LogMessage>)collection)[index] = message);
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for setting the log message at a specific index in the collection with an index that is out of bounds.
		/// The operation should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToSet">Index of the message to set.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Indexer_Set_IndexOutOfBounds(
			int                                                        initialCollectionSize,
			int                                                        indexOfMessageToSet,
			Action<ILogMessageCollection<LogMessage>, int, LogMessage> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				var newMessage = LoggingTestHelpers.GetTestMessages(1, 1)[0];
				Assert.Throws<NotSupportedException>(() => operation(collection, indexOfMessageToSet, newMessage));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region GetEnumerator()

		/// <summary>
		/// Tests getting an enumerator using <see cref="IEnumerable.GetEnumerator" /> and iterating over
		/// the collection using this enumerator.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IEnumerable_GetEnumerator(int initialCollectionSize)
		{
			Common_GetEnumerator_Success(
				initialCollectionSize,
				collection => ((IEnumerable)collection).GetEnumerator());
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests getting an enumerator using <see cref="IEnumerable{T}.GetEnumerator" /> and iterating over
		/// the collection using this enumerator.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IEnumerableT_GetEnumerator(int initialCollectionSize)
		{
			Common_GetEnumerator_Success(
				initialCollectionSize,
				collection => ((IEnumerable<LogMessage>)collection).GetEnumerator());
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for getting an enumerator iterating over the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_GetEnumerator_Success(
			int                                                  initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, IEnumerator> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out var expectedMessages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// iterate over all messages in the collection and collect them in a list
				var messagesInCollection = new List<LogMessage>();
				var enumerator = operation(collection);
				while (enumerator.MoveNext()) messagesInCollection.Add((LogMessage)enumerator.Current);

				// dispose the enumerator, if it is IEnumerator<T>
				// (the IEnumerator interface dos not support disposing)
				if (enumerator is IEnumerator<LogMessage> genericEnumerator) genericEnumerator.Dispose();

				// the enumerator should have iterated over all messages
				Assert.Equal(expectedMessages, messagesInCollection.ToArray()); // does not take IsReadOnly into account
				Assert.All(messagesInCollection, message => Assert.Equal(CollectionProvidesProtectedMessages, message.IsReadOnly));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Add()

		/// <summary>
		/// Tests adding a log message the the collection using <see cref="IList.Add" />.
		/// Should throw <see cref="NotSupportedException" />, if the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_Add(int initialCollectionSize)
		{
			Common_Add(
				initialCollectionSize,
				(collection, message) => ((IList)collection).Add(message));
		}

		/// <summary>
		/// Tests adding a log message the the collection using <see cref="IList.Add" />.
		/// Should throw <see cref="ArgumentNullException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_Add_ArgumentNull(int initialCollectionSize)
		{
			Common_Add_ArgumentNull(
				initialCollectionSize,
				collection => ((IList)collection).Add(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests adding a log message the the collection using <see cref="ICollection{T}.Add" />.
		/// Should throw <see cref="NotSupportedException" />, if the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollectionT_Add(int initialCollectionSize)
		{
			Common_Add(
				initialCollectionSize,
				(collection, message) => ((ICollection<LogMessage>)collection).Add(message));
		}

		/// <summary>
		/// Tests adding a log message the the collection using <see cref="ICollection{T}.Add" />.
		/// Should throw <see cref="ArgumentNullException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollectionT_Add_ArgumentNull(int initialCollectionSize)
		{
			Common_Add_ArgumentNull(
				initialCollectionSize,
				collection => ((ICollection<LogMessage>)collection).Add(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for adding a log message to the collection.
		/// The operation should throw <see cref="NotSupportedException" />, if the collection is readonly.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Add(
			int                                                   initialCollectionSize,
			Action<ILogMessageCollection<LogMessage>, LogMessage> operation)
		{
			// create collection to test
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// create new message that is not in the collection, yet
				// (use seed 1 to generate a message that differs from the messages generated by default)
				var newMessage = LoggingTestHelpers.GetTestMessages(1, 1)[0];

				if (CollectionIsReadOnly)
				{
					// collection is read-only and should throw exception
					Assert.Throws<NotSupportedException>(() => operation(collection, newMessage));
					Assert.Equal(initialCollectionSize, collection.Count);
				}
				else
				{
					// add message to the collection
					operation(collection, newMessage);

					// the collection may change the id of the message => adjust it before comparing
					// (message id starts at 0, so this should be the correct message id)
					newMessage.Id = initialCollectionSize;

					// check whether the collection contains the new message now
					Assert.Equal(initialCollectionSize + 1, collection.Count);
					var expectedMessages = new List<LogMessage>(messages) { newMessage };
					Assert.Equal(expectedMessages.ToArray(), collection.ToArray()); // does not take IsReadOnly into account
					Assert.All(collection, message => Assert.Equal(CollectionProvidesProtectedMessages, message.IsReadOnly));

					// add expected events that should have been raised
					eventWatcher.ExpectCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, new[] { newMessage }, initialCollectionSize));
					eventWatcher.ExpectPropertyChanged(new PropertyChangedEventArgs("Count"));
					eventWatcher.ExpectPropertyChanged(new PropertyChangedEventArgs("Item[]"));
				}

				// check events that should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for adding a log message to the collection with the log message to add being a null reference.
		/// The operation should throw <see cref="ArgumentNullException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Add_ArgumentNull(
			int                                       initialCollectionSize,
			Action<ILogMessageCollection<LogMessage>> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				Assert.Throws<ArgumentNullException>(() => operation(collection));
				Assert.Equal(initialCollectionSize, collection.Count);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region AddRange()

		/// <summary>
		/// Tests adding multiple log messages the the collection using <see cref="ILogMessageCollection{TMessage}.AddRange" />.
		/// Should throw <see cref="NotSupportedException" />, if the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="numberOfMessagesToAdd">Number of messages to add at once.</param>
		[Theory]
		[InlineData(0, 1)]
		[InlineData(0, 10)]
		[InlineData(1000, 1)]
		[InlineData(1000, 10)]
		protected virtual void ILogMessageCollectionT_AddRange(int initialCollectionSize, int numberOfMessagesToAdd)
		{
			Common_AddRange(
				initialCollectionSize,
				numberOfMessagesToAdd,
				(collection, messages) => collection.AddRange(messages));
		}

		/// <summary>
		/// Tests adding multiple log messages the the collection using <see cref="ILogMessageCollection{TMessage}.AddRange" />.
		/// Should throw <see cref="ArgumentNullException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ILogMessageCollectionT_AddRange_ArgumentNull(int initialCollectionSize)
		{
			Common_AddRange_ArgumentNull(
				initialCollectionSize,
				collection => collection.AddRange(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for adding a log message to the collection.
		/// The operation should throw <see cref="NotSupportedException" />, if the collection is readonly.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="numberOfMessagesToAdd">Number of messages to add at once.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_AddRange(
			int                                                                initialCollectionSize,
			int                                                                numberOfMessagesToAdd,
			Action<ILogMessageCollection<LogMessage>, IEnumerable<LogMessage>> operation)
		{
			// create collection to test
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// create new messages that is not in the collection, yet
				// (use seed 1 to generate a message that differs from the messages generated by default)
				var newMessages = LoggingTestHelpers.GetTestMessages(numberOfMessagesToAdd, 1);

				if (CollectionIsReadOnly)
				{
					// collection is read-only and should throw exception
					Assert.Throws<NotSupportedException>(() => operation(collection, newMessages));
					Assert.Equal(initialCollectionSize, collection.Count);
				}
				else
				{
					// add message to the collection
					operation(collection, newMessages);

					// the collection may change the id of the message => adjust it before comparing
					// (message id starts at 0, so this should be the correct message id)
					for (int i = 0; i < numberOfMessagesToAdd; i++) newMessages[i].Id = initialCollectionSize + i;

					// check whether the collection contains the new message now
					Assert.Equal(initialCollectionSize + numberOfMessagesToAdd, collection.Count);
					var expectedMessages = new List<LogMessage>(messages);
					expectedMessages.AddRange(newMessages);
					Assert.Equal(expectedMessages.ToArray(), collection.ToArray()); // does not take IsReadOnly into account
					Assert.All(collection, message => Assert.Equal(CollectionProvidesProtectedMessages, message.IsReadOnly));

					// add events that should have been raised
					for (int i = 0; i < numberOfMessagesToAdd; i++)
					{
						eventWatcher.ExpectCollectionChanged(
							new NotifyCollectionChangedEventArgs(
								NotifyCollectionChangedAction.Add,
								new[] { newMessages[i] },
								initialCollectionSize + i));
					}

					eventWatcher.ExpectPropertyChanged(new PropertyChangedEventArgs("Count"));
					eventWatcher.ExpectPropertyChanged(new PropertyChangedEventArgs("Item[]"));
				}

				// check events that should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for adding multiple log messages to the collection with the log messages to add being a null reference.
		/// The operation should throw <see cref="ArgumentNullException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_AddRange_ArgumentNull(
			int                                       initialCollectionSize,
			Action<ILogMessageCollection<LogMessage>> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				Assert.Throws<ArgumentNullException>(() => operation(collection));
				Assert.Equal(initialCollectionSize, collection.Count);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Insert()

		/// <summary>
		/// Tests inserting a log message into the collection using <see cref="IList.Insert" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexToInsertAt">Index of the position in the collection to insert the log message into.</param>
		[Theory]
		[InlineData(10, 0)]  // first log message
		[InlineData(10, 9)]  // last log message
		[InlineData(10, -1)] // out of bounds, but should also throw exception
		[InlineData(10, 10)] // out of bounds, but should also throw exception
		protected virtual void IList_Insert_NotSupportedException(int initialCollectionSize, int indexToInsertAt)
		{
			Common_Insert_NotSupported(
				initialCollectionSize,
				(collection, message) => ((IList)collection).Insert(indexToInsertAt, message));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests inserting a log message into the collection using <see cref="IList{T}.Insert" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexToInsertAt">Index of the position in the collection to insert the log message into.</param>
		[Theory]
		[InlineData(10, 0)]  // first log message
		[InlineData(10, 9)]  // last log message
		[InlineData(10, -1)] // out of bounds, but should also throw exception
		[InlineData(10, 10)] // out of bounds, but should also throw exception
		protected virtual void IListT_Insert_NotSupportedException(int initialCollectionSize, int indexToInsertAt)
		{
			Common_Insert_NotSupported(
				initialCollectionSize,
				(collection, message) => ((IList<LogMessage>)collection).Insert(indexToInsertAt, message));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for inserting a log message into the collection.
		/// The operation should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Insert_NotSupported(
			int                                                   initialCollectionSize,
			Action<ILogMessageCollection<LogMessage>, LogMessage> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				var newMessage = LoggingTestHelpers.GetTestMessages(1, 1)[0];
				Assert.Throws<NotSupportedException>(() => operation(collection, newMessage));
				Assert.Equal(initialCollectionSize, collection.Count);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Clear()

		/// <summary>
		/// Tests clearing the collection using <see cref="IList.Clear" />.
		/// Should throw <see cref="NotSupportedException" />, if the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_Clear(int initialCollectionSize)
		{
			Common_Clear(
				initialCollectionSize,
				collection => ((IList)collection).Clear());
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests clearing the collection using <see cref="ICollection{T}.Clear" />.
		/// Should throw <see cref="NotSupportedException" />, if the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollectionT_Clear(int initialCollectionSize)
		{
			Common_Clear(
				initialCollectionSize,
				collection => ((ICollection<LogMessage>)collection).Clear());
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for clearing a collection.
		/// The operation should throw <see cref="NotSupportedException" />, if the collection is read-only.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Clear(
			int                                       initialCollectionSize,
			Action<ILogMessageCollection<LogMessage>> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				Assert.Equal(initialCollectionSize, collection.Count);

				if (CollectionIsReadOnly)
				{
					// run operation and ensure that the appropriate exception is thrown
					Assert.Throws<NotSupportedException>(() => operation(collection));
					Assert.Equal(initialCollectionSize, collection.Count);
				}
				else
				{
					// run operation and ensure that the collection is empty afterwards
					operation(collection);
					Assert.Equal(0, collection.Count);
					Assert.Empty(collection);

					// add events that should have been raised
					if (initialCollectionSize > 0)
					{
						eventWatcher.ExpectCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
						eventWatcher.ExpectPropertyChanged(new PropertyChangedEventArgs("Count"));
						eventWatcher.ExpectPropertyChanged(new PropertyChangedEventArgs("Item[]"));
					}
				}

				// check events that should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Contains()

		/// <summary>
		/// Tests checking whether the collection contains a specific log message using <see cref="IList.Contains" />.
		/// The test message is in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToCheck">Index of the message in the test set to check for.</param>
		[Theory]
		[InlineData(1, 0)]
		[InlineData(1000, 0)]
		[InlineData(1000, 499)]
		[InlineData(1000, 999)]
		protected virtual void IList_Contains_MessageInCollection(int initialCollectionSize, int indexOfMessageToCheck)
		{
			Common_Contains_MessageInCollection(
				initialCollectionSize,
				indexOfMessageToCheck,
				(collection, message) => ((IList)collection).Contains(message));
		}

		/// <summary>
		/// Tests checking whether the collection contains a specific log message using <see cref="IList.Contains" />.
		/// The log message to check for is not in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_Contains_MessageNotInCollection(int initialCollectionSize)
		{
			Common_Contains_MessageNotInCollection(
				initialCollectionSize,
				(collection, message) => ((IList)collection).Contains(message));
		}

		/// <summary>
		/// Tests checking whether the collection contains a specific log message using <see cref="IList.Contains" /> with the log
		/// message being a null reference. Should always return <c>false</c>.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_Contains_ArgumentNull(int initialCollectionSize)
		{
			Common_Contains_ArgumentNull(
				initialCollectionSize,
				collection => ((IList)collection).Contains(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests checking whether the collection contains a specific log message using <see cref="ICollection{T}.Contains" />.
		/// The test message is in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToCheck">Index of the message in the test set to check for.</param>
		[Theory]
		[InlineData(1, 0)]
		[InlineData(1000, 0)]
		[InlineData(1000, 499)]
		[InlineData(1000, 999)]
		protected virtual void ICollectionT_Contains_MessageInCollection(int initialCollectionSize, int indexOfMessageToCheck)
		{
			Common_Contains_MessageInCollection(
				initialCollectionSize,
				indexOfMessageToCheck,
				(collection, message) => ((ICollection<LogMessage>)collection).Contains(message));
		}

		/// <summary>
		/// Tests checking whether the collection contains a specific log message using <see cref="ICollection{T}.Contains" />.
		/// The log message to check for is not in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollectionT_Contains_MessageNotInCollection(int initialCollectionSize)
		{
			Common_Contains_MessageNotInCollection(
				initialCollectionSize,
				(collection, message) => ((ICollection<LogMessage>)collection).Contains(message));
		}

		/// <summary>
		/// Tests checking whether the collection contains a specific log message using <see cref="ICollection{T}.Contains" /> with the log
		/// message being a null reference. Should always return <c>false</c>.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void ICollectionT_Contains_ArgumentNull(int initialCollectionSize)
		{
			Common_Contains_ArgumentNull(
				initialCollectionSize,
				collection => ((ICollection<LogMessage>)collection).Contains(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for checking whether the collection contains a specific log message.
		/// The log message to check for is in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToCheck">Index of the message in the test set to check for.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Contains_MessageInCollection(
			int                                                       initialCollectionSize,
			int                                                       indexOfMessageToCheck,
			Func<ILogMessageCollection<LogMessage>, LogMessage, bool> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the message was found
				var messageCopy = new LogMessage(messages[indexOfMessageToCheck]); // use copy of log message to check whether equality check works
				Assert.True(operation(collection, messageCopy));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for checking whether the collection contains a specific log message.
		/// The log message to check for is not in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Contains_MessageNotInCollection(
			int                                                       initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, LogMessage, bool> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the message was not found
				var newMessage = LoggingTestHelpers.GetTestMessages(1, 1)[0]; // use seed 1 to generate messages different from the default set
				Assert.False(operation(collection, newMessage));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for checking whether the collection contains a specific log message with the log message being a null reference.
		/// The operation should always return <c>false</c>.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Contains_ArgumentNull(
			int                                           initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, bool> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that false is returned
				bool result = operation(collection);
				Assert.False(result);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region IndexOf()

		/// <summary>
		/// Tests getting the index of a log message in the collection using <see cref="IList.IndexOf" />.
		/// The message is in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToCheck">Index of the message to check for.</param>
		[Theory]
		[InlineData(1, 0)]
		[InlineData(1000, 0)]
		[InlineData(1000, 499)]
		[InlineData(1000, 999)]
		protected virtual void IList_IndexOf_MessageInCollection(int initialCollectionSize, int indexOfMessageToCheck)
		{
			Common_IndexOf_MessageInCollection(
				initialCollectionSize,
				indexOfMessageToCheck,
				(collection, message) => ((IList)collection).IndexOf(message));
		}

		/// <summary>
		/// Tests getting the index of a log message in the collection using <see cref="IList.IndexOf" />.
		/// The message is not in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_IndexOf_MessageNotInCollection(int initialCollectionSize)
		{
			Common_IndexOf_MessageNotInCollection(
				initialCollectionSize,
				(collection, message) => ((IList)collection).IndexOf(message));
		}

		/// <summary>
		/// Tests getting the index of a log message in the collection using <see cref="IList.IndexOf" /> with the log
		/// message being a null reference. Should always return -1.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IList_IndexOf_ArgumentNull(int initialCollectionSize)
		{
			Common_IndexOf_ArgumentNull(
				initialCollectionSize,
				collection => ((IList)collection).IndexOf(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests getting the index of a log message in the collection using <see cref="IList{T}.IndexOf" />.
		/// The message is in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToCheck">Index of the message to check for.</param>
		[Theory]
		[InlineData(1, 0)]
		[InlineData(1000, 0)]
		[InlineData(1000, 499)]
		[InlineData(1000, 999)]
		protected virtual void IListT_IndexOf_MessageInCollection(int initialCollectionSize, int indexOfMessageToCheck)
		{
			Common_IndexOf_MessageInCollection(
				initialCollectionSize,
				indexOfMessageToCheck,
				(collection, message) => ((IList<LogMessage>)collection).IndexOf(message));
		}

		/// <summary>
		/// Tests getting the index of a log message in the collection using <see cref="IList{T}.IndexOf" />.
		/// The message is not in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IListT_IndexOf_MessageNotInCollection(int initialCollectionSize)
		{
			Common_IndexOf_MessageNotInCollection(
				initialCollectionSize,
				(collection, message) => ((IList<LogMessage>)collection).IndexOf(message));
		}

		/// <summary>
		/// Tests getting the index of a log message in the collection using <see cref="IList{T}.IndexOf" /> with the log
		/// message being a null reference. Should always return -1.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		[Theory]
		[InlineData(0)]
		[InlineData(1000)]
		protected virtual void IListT_IndexOf_ArgumentNull(int initialCollectionSize)
		{
			Common_IndexOf_ArgumentNull(
				initialCollectionSize,
				collection => ((IList<LogMessage>)collection).IndexOf(null));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for determining at which index a specific log message is located in the collection.
		/// The log message to check for is in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToCheck">Index of the message in the test set to check for.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_IndexOf_MessageInCollection(
			int                                                      initialCollectionSize,
			int                                                      indexOfMessageToCheck,
			Func<ILogMessageCollection<LogMessage>, LogMessage, int> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the message was found
				var messageCopy = new LogMessage(messages[indexOfMessageToCheck]); // use copy of log message to check whether equality check works
				Assert.Equal(indexOfMessageToCheck, operation(collection, messageCopy));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for determining at which index a specific log message is located in the collection.
		/// The log message to check for is not in the collection.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_IndexOf_MessageNotInCollection(
			int                                                      initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, LogMessage, int> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the message was not found
				var newMessage = LoggingTestHelpers.GetTestMessages(1, 1)[0]; // use seed 1 to generate messages different from the default set
				Assert.Equal(-1, operation(collection, newMessage));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for determining at which index a specific log message is located in the collection with the log message being a null reference.
		/// The operation should always return -1.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_IndexOf_ArgumentNull(
			int                                          initialCollectionSize,
			Func<ILogMessageCollection<LogMessage>, int> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that -1 is returned
				int result = operation(collection);
				Assert.Equal(-1, result);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region CopyTo()

		/// <summary>
		/// Tests copying the log messages from the collection into an array using <see cref="ICollection.CopyTo" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		[Theory]
		[InlineData(0, 0, 0)]       // empty collection => empty destination array
		[InlineData(1000, 1000, 0)] // populated collection => destination array with enough space for the messages starting at the beginning
		[InlineData(1000, 1001, 1)] // populated collection => destination array with enough space for the messages starting at the index 1
		protected virtual void ICollection_CopyTo_Success(int initialCollectionSize, int destinationBufferSize, int startIndex)
		{
			Common_CopyTo_Success(
				initialCollectionSize,
				destinationBufferSize,
				startIndex,
				(collection, buffer) => ((ICollection)collection).CopyTo(buffer, startIndex));
		}

		/// <summary>
		/// Tests copying the log messages from the collection into an array using <see cref="ICollection.CopyTo" /> with a start index that is outside
		/// the destination array. Should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		[Theory]
		[InlineData(0, 0, -1)]       // start index is negative and therefore out of bounds
		[InlineData(0, 0, 1)]        // start index is outside the destination array
		[InlineData(1000, 1000, -1)] // start index is negative and therefore out of bounds
		protected virtual void ICollection_CopyTo_StartIndexOutOfRange(int initialCollectionSize, int destinationBufferSize, int startIndex)
		{
			Common_CopyTo_StartIndexOutOfRange(
				initialCollectionSize,
				destinationBufferSize,
				startIndex,
				(collection, buffer, index) => ((ICollection)collection).CopyTo(buffer, index));
		}

		/// <summary>
		/// Tests copying the log messages from the collection into an array using <see cref="ICollection{T}.CopyTo" /> with a destination array
		/// that is too small (taking the start index into account). Should throw <see cref="ArgumentException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		[Theory]
		[InlineData(1000, 999, 0)]  // messages do not fit into the destination array (array too small)
		[InlineData(1000, 1000, 1)] // messages do not fit into the destination array due to the start index
		protected virtual void ICollection_CopyTo_ArrayTooSmall(int initialCollectionSize, int destinationBufferSize, int startIndex)
		{
			Common_CopyTo_ArrayTooSmall(
				initialCollectionSize,
				destinationBufferSize,
				startIndex,
				(collection, buffer, index) => ((ICollection)collection).CopyTo(buffer, index));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests copying the log messages from the collection into an array using <see cref="ICollection{T}.CopyTo" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		[Theory]
		[InlineData(0, 0, 0)]       // empty collection => empty destination array
		[InlineData(1000, 1000, 0)] // populated collection => destination array with enough space for the messages starting at the beginning
		[InlineData(1000, 1001, 1)] // populated collection => destination array with enough space for the messages starting at the index 1
		protected virtual void ICollectionT_CopyTo_Success(int initialCollectionSize, int destinationBufferSize, int startIndex)
		{
			Common_CopyTo_Success(
				initialCollectionSize,
				destinationBufferSize,
				startIndex,
				(collection, buffer) => ((ICollection<LogMessage>)collection).CopyTo(buffer, startIndex));
		}

		/// <summary>
		/// Tests copying the log messages from the collection into an array using <see cref="ICollection{T}.CopyTo" /> with a start index that is outside
		/// the destination array. Should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		[Theory]
		[InlineData(0, 0, -1)]       // start index is negative and therefore out of bounds
		[InlineData(0, 0, 1)]        // start index is outside the destination array
		[InlineData(1000, 1000, -1)] // start index is negative and therefore out of bounds
		protected virtual void ICollectionT_CopyTo_StartIndexOutOfRange(int initialCollectionSize, int destinationBufferSize, int startIndex)
		{
			Common_CopyTo_StartIndexOutOfRange(
				initialCollectionSize,
				destinationBufferSize,
				startIndex,
				(collection, buffer, index) => ((ICollection<LogMessage>)collection).CopyTo(buffer, index));
		}

		/// <summary>
		/// Tests copying the log messages from the collection into an array using <see cref="ICollection{T}.CopyTo" /> with a destination array
		/// that is too small (taking the start index into account). Should throw <see cref="ArgumentException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		[Theory]
		[InlineData(1000, 999, 0)]  // messages do not fit into the destination array (array too small)
		[InlineData(1000, 1000, 1)] // messages do not fit into the destination array due to the start index
		protected virtual void ICollectionT_CopyTo_ArrayTooSmall(int initialCollectionSize, int destinationBufferSize, int startIndex)
		{
			Common_CopyTo_ArrayTooSmall(
				initialCollectionSize,
				destinationBufferSize,
				startIndex,
				(collection, buffer, index) => ((ICollection<LogMessage>)collection).CopyTo(buffer, index));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for copying the log messages from the collection to an array starting at the specified index.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_CopyTo_Success(
			int                                                     initialCollectionSize,
			int                                                     destinationBufferSize,
			int                                                     startIndex,
			Action<ILogMessageCollection<LogMessage>, LogMessage[]> operation)
		{
			// create collection with the specified number of messages
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// create destination array and copy messages into it
				var destination = new LogMessage[destinationBufferSize];
				operation(collection, destination);

				// check whether the destination array contains the expected messages
				var expectedMessages = new List<LogMessage>();
				for (int i = 0; i < startIndex; i++) expectedMessages.Add(null);
				expectedMessages.AddRange(messages);
				Assert.Equal(expectedMessages, destination); // does not take IsReadOnly into account
				Assert.All(destination.Skip(startIndex), message => Assert.Equal(CollectionProvidesProtectedMessages, message.IsReadOnly));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for copying the log messages from the collection to an array starting at an index that is out of bounds.
		/// The operation should throw <see cref="ArgumentOutOfRangeException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_CopyTo_StartIndexOutOfRange(
			int                                                          initialCollectionSize,
			int                                                          destinationBufferSize,
			int                                                          startIndex,
			Action<ILogMessageCollection<LogMessage>, LogMessage[], int> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				var destination = new LogMessage[destinationBufferSize];
				Assert.Throws<ArgumentOutOfRangeException>(() => operation(collection, destination, startIndex));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		/// <summary>
		/// Common test code for copying the log messages from the collection to an array that is too small to store all messages
		/// taking the start index into account. The operation should throw <see cref="ArgumentException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="destinationBufferSize">Size of the destination array to copy the messages into.</param>
		/// <param name="startIndex">Index of the position in the destination array to start copying to.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_CopyTo_ArrayTooSmall(
			int                                                          initialCollectionSize,
			int                                                          destinationBufferSize,
			int                                                          startIndex,
			Action<ILogMessageCollection<LogMessage>, LogMessage[], int> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				var destination = new LogMessage[destinationBufferSize];
				Assert.Throws<ArgumentException>(() => operation(collection, destination, startIndex));

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region Remove()

		/// <summary>
		/// Tests removing a specific log message from the collection using <see cref="IList.Remove" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToRemove">Index of the log message to try to remove.</param>
		[Theory]
		[InlineData(10, 0)]
		[InlineData(10, 9)]
		protected virtual void IList_Remove(int initialCollectionSize, int indexOfMessageToRemove)
		{
			Common_Remove_NotSupported(
				initialCollectionSize,
				indexOfMessageToRemove,
				(collection, message) => ((IList)collection).Remove(message));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests removing a specific log message from the collection using <see cref="ICollection{T}.Remove" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToRemove">Index of the log message to try to remove.</param>
		[Theory]
		[InlineData(10, 0)]
		[InlineData(10, 9)]
		protected virtual void ICollectionT_Remove(int initialCollectionSize, int indexOfMessageToRemove)
		{
			Common_Remove_NotSupported(
				initialCollectionSize,
				indexOfMessageToRemove,
				(collection, message) => ((ICollection<LogMessage>)collection).Remove(message));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for removing a specific log message from the collection.
		/// The operation should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToRemove">Index of the log message in the test set to remove.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_Remove_NotSupported(
			int                                                   initialCollectionSize,
			int                                                   indexOfMessageToRemove,
			Action<ILogMessageCollection<LogMessage>, LogMessage> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out var messages))
			{
				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				var messageToRemove = new LogMessage(messages[indexOfMessageToRemove]);
				Assert.Equal(initialCollectionSize, collection.Count);
				Assert.Throws<NotSupportedException>(() => operation(collection, messageToRemove));
				Assert.Equal(initialCollectionSize, collection.Count);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion

		#region RemoveAt()

		/// <summary>
		/// Tests removing the log message at a specific index from the collection using <see cref="IList.RemoveAt" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToRemove">Index of the log message in the test set to try to remove.</param>
		[Theory]
		[InlineData(10, 0)]  // first log message
		[InlineData(10, 9)]  // last log message
		[InlineData(10, -1)] // out of bounds, but should also throw exception
		[InlineData(10, 10)] // out of bounds, but should also throw exception
		protected virtual void IList_RemoveAt(int initialCollectionSize, int indexOfMessageToRemove)
		{
			Common_RemoveAt_NotSupported(
				initialCollectionSize,
				indexOfMessageToRemove,
				(collection, index) => ((IList)collection).RemoveAt(index));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Tests removing the log message at a specific index from the collection using <see cref="IList{T}.RemoveAt" />.
		/// Should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToRemove">Index of the log message in the test set to try to remove.</param>
		[Theory]
		[InlineData(10, 0)]  // first log message
		[InlineData(10, 9)]  // last log message
		[InlineData(10, -1)] // out of bounds, but should also throw exception
		[InlineData(10, 10)] // out of bounds, but should also throw exception
		protected virtual void IListT_RemoveAt(int initialCollectionSize, int indexOfMessageToRemove)
		{
			Common_RemoveAt_NotSupported(
				initialCollectionSize,
				indexOfMessageToRemove,
				(collection, index) => ((IList<LogMessage>)collection).RemoveAt(index));
		}

		// ------------------------------------------------------------------------------------------------------------------

		/// <summary>
		/// Common test code for removing a specific log message from the collection.
		/// The operation should throw <see cref="NotSupportedException" />.
		/// </summary>
		/// <param name="initialCollectionSize">Number of messages to put into the collection before running the test.</param>
		/// <param name="indexOfMessageToRemove">Index of the log message in the test set to try to remove.</param>
		/// <param name="operation">Operation to invoke on the collection.</param>
		private void Common_RemoveAt_NotSupported(
			int                                            initialCollectionSize,
			int                                            indexOfMessageToRemove,
			Action<ILogMessageCollection<LogMessage>, int> operation)
		{
			using (var collection = CreateCollection(initialCollectionSize, out _))
			{

				var eventWatcher = collection.AttachEventWatcher();

				// run operation and ensure that the appropriate exception is thrown
				Assert.Equal(initialCollectionSize, collection.Count);
				Assert.Throws<NotSupportedException>(() => operation(collection, indexOfMessageToRemove));
				Assert.Equal(initialCollectionSize, collection.Count);

				// no events should have been raised
				eventWatcher.CheckInvocations();
			}
		}

		#endregion
	}

}

#pragma warning restore CS1584 // XML comment has syntactically incorrect cref attribute
