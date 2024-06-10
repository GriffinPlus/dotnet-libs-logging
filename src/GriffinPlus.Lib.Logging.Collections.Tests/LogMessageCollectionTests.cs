///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Unit tests targeting the <see cref="LogMessageCollection{TMessage}"/> class.
/// </summary>
public class LogMessageCollectionTests : LogMessageCollectionBaseTests<ILogMessageCollection<LogMessage>>
{
	/// <summary>
	/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
	/// </summary>
	/// <param name="count">Number of random log messages the collection should contain.</param>
	/// <param name="messages">Receives messages that have been put into the collection.</param>
	/// <returns>A new instance of the collection class to test.</returns>
	protected override ILogMessageCollection<LogMessage> CreateCollection(int count, out LogMessage[] messages)
	{
		messages = LoggingTestHelpers.GetTestMessages<LogMessage>(count);
		LogMessageCollection<LogMessage> collection = count == 0
			                                              ? new LogMessageCollection<LogMessage>()
			                                              : new LogMessageCollection<LogMessage>(messages);

		// the test assumes that the collection uses single-item notifications
		collection.UseMultiItemNotifications = false;

		return collection;
	}

	#region Construction

	/// <summary>
	/// Tests creating an empty collection.
	/// </summary>
	[Fact]
	private void Create_Empty()
	{
		var collection = new LogMessageCollection<LogMessage>();
		TestCollectionPropertyDefaults(collection, 0);
	}

	/// <summary>
	/// Tests creating a collection with some log messages.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(1000)]
	private void Create_WithMessages(int count)
	{
		LogMessage[] messages = LoggingTestHelpers.GetTestMessages<LogMessage>(count, 1);
		var collection = new LogMessageCollection<LogMessage>(messages);
		TestCollectionPropertyDefaults(collection, count);
		Assert.Equal(messages, collection.ToArray());
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Checks whether properties of the specified collection have the expected default values and
	/// whether the collection contains the expected amount of log messages.
	/// </summary>
	/// <param name="collection">Collection to check.</param>
	/// <param name="expectedCount">Expected number of log messages in the collection.</param>
	private void TestCollectionPropertyDefaults(ILogMessageCollectionCommon<LogMessage> collection, long expectedCount)
	{
		using (LogMessageCollectionEventWatcher eventWatcher = collection.AttachEventWatcher())
		{
			// check collection specific properties
			Assert.Equal(expectedCount, collection.Count);

			// check properties exposed by IList implementation
			{
				var list = collection as IList;
				Assert.Equal(expectedCount, list.Count);
				Assert.Equal(CollectionIsReadOnly, list.IsReadOnly);
				Assert.Equal(CollectionIsFixedSize, list.IsFixedSize);
				Assert.Equal(CollectionIsSynchronized, list.IsSynchronized);
				Assert.NotSame(collection, list.SyncRoot); // sync root must not be the same as the collection to avoid deadlocks
			}

			// check properties exposed by IList<T> implementation
			{
				var list = (IList<LogMessage>)collection;
				Assert.False(list.IsReadOnly);
				Assert.Equal(expectedCount, list.Count);
			}

			// no events should have been raised
			eventWatcher.CheckInvocations();
		}
	}

	#endregion

	#region Copying Message (For Test Purposes)

	/// <summary>
	/// Creates a copy of the specified log message.
	/// </summary>
	/// <param name="message">Log message to copy.</param>
	/// <returns>A copy of the specified log message.</returns>
	protected override LogMessage CopyMessage(LogMessage message)
	{
		return new LogMessage(message);
	}

	#endregion
}
