///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Unit tests targeting the <see cref="SelectableLogMessageFilter{TMessage}"/> class.
/// </summary>
public class SelectableLogMessageFilterTests :
	SelectableLogMessageFilterBaseTests<SelectableLogMessageFilter<LogMessage>, LogMessageCollection<LogMessage>>
{
	/// <summary>
	/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
	/// </summary>
	/// <param name="count">Number of random log messages the collection should contain.</param>
	/// <param name="messages">Receives messages that have been put into the collection.</param>
	/// <returns>A new instance of the collection class to test.</returns>
	protected override LogMessageCollection<LogMessage> CreateCollection(int count, out LogMessage[] messages)
	{
		messages = LoggingTestHelpers.GetTestMessages<LogMessage>(count);
		LogMessageCollection<LogMessage> collection = count != 0 ? new LogMessageCollection<LogMessage>(messages) : [];

		// the test assumes that the collection uses single-item notifications
		collection.UseMultiItemNotifications = false;

		return collection;
	}
}
