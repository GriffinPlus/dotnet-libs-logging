///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Xunit;

// ReSharper disable UseIndexFromEndExpression

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Unit tests targeting the <see cref="SelectableFileBackedLogMessageFilter"/> class.
/// </summary>
public abstract class SelectableFileBackedLogMessageFilterTests_Base :
	SelectableLogMessageFilterBaseTests<SelectableFileBackedLogMessageFilter, FileBackedLogMessageCollection>,
	IClassFixture<LogFileTestsFixture>
{
	protected readonly LogFileTestsFixture Fixture;
	protected readonly LogFilePurpose      LogFilePurposeToTest;
	protected readonly LogFileWriteMode    LogFileWriteModeToTest;

	/// <summary>
	/// Initializes an instance of the <see cref="FileBackedLogMessageCollectionTests_Base"/> class.
	/// </summary>
	/// <param name="fixture">Fixture providing static test data.</param>
	/// <param name="useReadOnlyCollection">Indicates whether the collection used for the test is read-only or not.</param>
	/// <param name="purpose">The log file purpose to test.</param>
	/// <param name="writeMode">The log file write mode to test.</param>
	protected SelectableFileBackedLogMessageFilterTests_Base(
		LogFileTestsFixture fixture,
		bool                useReadOnlyCollection,
		LogFilePurpose      purpose,
		LogFileWriteMode    writeMode)
	{
		Fixture = fixture;
		LogFilePurposeToTest = purpose;
		LogFileWriteModeToTest = writeMode;
		CollectionIsReadOnly = useReadOnlyCollection;
	}

	/// <summary>
	/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
	/// The collection should be disposed at the end to avoid generating orphaned log files.
	/// </summary>
	/// <param name="count">Number of random log messages the collection should contain.</param>
	/// <param name="messages">Receives messages that have been put into the collection.</param>
	/// <returns>A new instance of the collection class to test.</returns>
	protected override FileBackedLogMessageCollection CreateCollection(
		int              count,
		out LogMessage[] messages)
	{
		return CreateCollection(
			count,
			LogFilePurposeToTest,
			LogFileWriteModeToTest,
			CollectionIsReadOnly,
			out messages);
	}

	/// <summary>
	/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
	/// The collection should be disposed at the end to avoid generating orphaned log files.
	/// </summary>
	/// <param name="count">Number of random log messages the collection should contain.</param>
	/// <param name="purpose">Purpose of the log file backing the collection.</param>
	/// <param name="writeMode">Write mode of the log file backing the collection.</param>
	/// <param name="isReadOnly">
	/// <see langword="true"/> to create a read-only log file;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <param name="messages">Receives messages that have been put into the collection.</param>
	/// <returns>A new instance of the collection class to test.</returns>
	private static FileBackedLogMessageCollection CreateCollection(
		int              count,
		LogFilePurpose   purpose,
		LogFileWriteMode writeMode,
		bool             isReadOnly,
		out LogMessage[] messages)
	{
		string path = Guid.NewGuid().ToString("D") + ".gplog";

		// create a collection backed by a new file
		using (var file1 = LogFile.OpenOrCreate(path, purpose, writeMode))
		{
			// generate the required number of log message and add them to the collection
			LogFileMessage[] fileLogMessages = LoggingTestHelpers.GetTestMessages<LogFileMessage>(count);
			for (long i = 0; i < fileLogMessages.Length; i++) fileLogMessages[i].Id = i;
			messages = fileLogMessages.Cast<LogMessage>().ToArray();
			if (count > 0) file1.Write(messages);
		}

		// open the created log file again as expected for the test
		LogFile file2 = isReadOnly
			                ? LogFile.OpenReadOnly(path)
			                : LogFile.Open(path, writeMode);

		// let the collection delete the log file on its disposal
		file2.Messages.AutoDelete = true;

		// the collection should now contain the messages written into it
		// (the file-backed collection assigns message ids on its own, but they should be the same as the ids assigned to the test set)
		Assert.Equal(messages, file2.Messages.ToArray());

		// the test assumes that the collection uses single-item notifications
		file2.Messages.UseMultiItemNotifications = false;

		return file2.Messages;
	}

#if NET48_OR_GREATER || NETCOREAPP
	/// <summary>
	/// Verifies that the <see cref="CommonGetMessages_TestData"/> collection does not contain duplicate entries.
	/// </summary>
	[Fact]
	public void CommonGetMessages_HasNoDuplicates()
	{
		var rows = CommonGetMessages_TestData.Select(e => new
			{
				g = (bool)e[0]!,
				en = (LogMessageField)e[1]!,
				ma = (LogMessageField)e[2]!,
				mb = (MatchBehavior)e[3]!
			})
			.ToList();

		string Key(dynamic r) => $"{r.g}|{(int)r.en}|{(int)r.ma}|{(int)r.mb}";

		IEnumerable<IGrouping<string, (int i, string key)>> dupes = rows.Select((r, i) => (i, key: Key(r)))
			.GroupBy(x => x.key)
			.Where(g => g.Count() > 1);

		Assert.False(
			dupes.Any(),
			"Duplicates in CommonGetMessages_TestData:\n" +
			string.Join("\n\n", dupes.Select(g => $"KEY: {g.Key}\nINDICES: {string.Join(", ", g.Select(x => x.i))}")));
	}
#endif

	#region Common Test Data for Get[Previous|Next]Message[s]()

	/// <summary>
	/// Test data for methods returning the filtered message set of a log message.
	/// </summary>

	public static TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior> CommonGetMessages_TestData
	{
		get
		{
			var data = new TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior>();

			LogMessageField[] allFilters =
			[
				LogMessageField.Timestamp,
				LogMessageField.ApplicationName,
				LogMessageField.ProcessName,
				LogMessageField.ProcessId,
				LogMessageField.LogLevelName,
				LogMessageField.LogWriterName,
				LogMessageField.Tags,
				LogMessageField.Text
			];

			const LogMessageField allFilterFlags =
				LogMessageField.Timestamp |
				LogMessageField.ApplicationName |
				LogMessageField.ProcessName |
				LogMessageField.ProcessId |
				LogMessageField.LogLevelName |
				LogMessageField.LogWriterName |
				LogMessageField.Tags |
				LogMessageField.Text;

			MatchBehavior[] matchBehaviors =
			[
				MatchBehavior.MatchFirst,
				MatchBehavior.MatchFirst | MatchBehavior.MatchInBetween,
				MatchBehavior.MatchFirst | MatchBehavior.MatchInBetween | MatchBehavior.MatchLast,
				MatchBehavior.MatchInBetween | MatchBehavior.MatchLast,
				MatchBehavior.MatchLast
			];

			// global filter switch is disabled
			// => all messages pass the filter, even if specific filters do not match
			data.Add(
				false,                // disable global filter switch
				allFilterFlags,       // enable all specific filters
				LogMessageField.None, // configure no specific filters to match
				MatchBehavior.MatchAll);

			// global filter switch is enabled, non-matching specific filters are enabled one at a time
			// => no message passes the filter as the specific filter blocks them
			foreach (LogMessageField enabledFilter in allFilters)
			{
				data.Add(
					true,                    // enable global filter switch
					enabledFilter,           // enable specific filter
					LogMessageField.None,    // configure no specific filters to match
					MatchBehavior.MatchAll); // not in effect as no field is selected
			}

			// global filter switch is enabled, matching specific filters are enabled one at a time
			// => messages pass the filter selectively
			foreach (LogMessageField enabledFilter in allFilters)
			foreach (MatchBehavior matchBehavior in matchBehaviors)
			{
				data.Add(
					true,           // enable global filter switch
					enabledFilter,  // enable specific filter
					enabledFilter,  // configure specific filters to match
					matchBehavior); // determines whether the first/last message or messages in between are matched
			}

			return data;
		}
	}

	/// <summary>
	/// Flags determining whether the first/last message and/or messages in between are matched by the filter.
	/// </summary>
	[Flags]
	public enum MatchBehavior
	{
		/// <summary>
		/// Filter under test should match the first message in the message set.
		/// </summary>
		MatchFirst = 1 << 0,

		/// <summary>
		/// Filter under test should match the last message in the message set.
		/// </summary>
		MatchLast = 1 << 1,

		/// <summary>
		/// Filter under test should match the message between the first and the last message.
		/// </summary>
		MatchInBetween = 1 << 2,

		/// <summary>
		/// Filter under test should match all messages from the first to the last message.
		/// </summary>
		MatchAll = MatchFirst | MatchInBetween | MatchLast
	}

	/// <summary>
	/// Creates a <see cref="FileBackedLogMessageCollection"/>, attaches a <see cref="SelectableFileBackedLogMessageFilter"/>
	/// to it and configures it for various test cases.
	/// </summary>
	/// <param name="isGloballyEnabled">
	/// <see langword="true"/> to enable the filter globally (specific filters are in effect);<br/>
	/// <see langword="false"/> to disable the filter globally (all messages bypass specific filters).
	/// </param>
	/// <param name="enabledSpecificFilters">Enabled specific filters (flags).</param>
	/// <param name="matchingSpecificFilters">Specific filters that should be configured to match (flags).</param>
	/// <param name="matchBehavior">Determines how to select matching messages.</param>
	/// <param name="collection">Receives the created collection the filter works on.</param>
	/// <param name="filter">Receives the created filter.</param>
	/// <param name="unfilteredMessages">Receives messages that have been put into the collection.</param>
	/// <param name="filteredMessages">Receives messages that pass the filter.</param>
	private void CreateFilterAndReferencePredicate(
		bool                                     isGloballyEnabled,
		LogMessageField                          enabledSpecificFilters,
		LogMessageField                          matchingSpecificFilters,
		MatchBehavior                            matchBehavior,
		out FileBackedLogMessageCollection       collection,
		out SelectableFileBackedLogMessageFilter filter,
		out LogFileMessage[]                     unfilteredMessages,
		out LogFileMessage[]                     filteredMessages)
	{
		// create collection and attach a filter to it
		collection = CreateCollection(500, out LogMessage[] generatedMessages);
		unfilteredMessages = generatedMessages.Cast<LogFileMessage>().ToArray();
		filter = new SelectableFileBackedLogMessageFilter { Enabled = isGloballyEnabled };
		filter.AttachToCollection(collection);
		LogFileMessage firstMessage = unfilteredMessages[0];
		LogFileMessage lastMessage = unfilteredMessages[unfilteredMessages.Length - 1];

		// --------------------------------------------------------------------------------------------------------
		// configure timestamp filter
		// --------------------------------------------------------------------------------------------------------
		DateTimeOffset from, to;
		filter.TimestampFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.Timestamp);
		if (matchingSpecificFilters.HasFlag(LogMessageField.Timestamp))
		{
			// filter should match
			if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
			{
				// match starting at the first message
				from = to = collection[0].Timestamp;
			}
			else
			{
				// match starting at the second message
				from = to = collection[1].Timestamp;
			}

			if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
			{
				// match up to the last message
				to = collection[collection.Count - 1].Timestamp;
			}
			else if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
			{
				// match up to the second last message
				to = collection[collection.Count - 2].Timestamp;
			}

			filter.TimestampFilter.From = from;
			filter.TimestampFilter.To = to;
		}
		else
		{
			// filter should not match
			// => choose interval from last message (+ margin) to 'infinity'
			from = collection[collection.Count - 1].Timestamp + TimeSpan.FromMilliseconds(1);
			to = DateTimeOffset.MaxValue;
			filter.TimestampFilter.From = from;
			filter.TimestampFilter.To = to;
		}

		// --------------------------------------------------------------------------------------------------------
		// configure application name filter
		// --------------------------------------------------------------------------------------------------------
		filter.ApplicationNameFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.ApplicationName);
		bool applicationNameSelected = matchingSpecificFilters.HasFlag(LogMessageField.ApplicationName);
		var selectedApplicationNames = new HashSet<string>();

		if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
		{
			// select the application name matching the first message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.ApplicationNameFilter
				.Items
				.First(x => x.Value == firstMessage.ApplicationName);
			specificFilter.Selected = applicationNameSelected;
			if (applicationNameSelected) selectedApplicationNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
		{
			// select the application name matching the last message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.ApplicationNameFilter
				.Items
				.First(x => x.Value == lastMessage.ApplicationName);
			specificFilter.Selected = applicationNameSelected;
			if (applicationNameSelected) selectedApplicationNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
		{
			// select an application name matching messages in between, but surely not the first or the last one
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.ApplicationNameFilter
				.Items
				.First(x => x.Value != firstMessage.ApplicationName && x.Value != lastMessage.ApplicationName);
			specificFilter.Selected = applicationNameSelected;
			if (applicationNameSelected) selectedApplicationNames.Add(specificFilter.Value);
		}

		// --------------------------------------------------------------------------------------------------------
		// configure process name filter
		// --------------------------------------------------------------------------------------------------------
		filter.ProcessNameFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.ProcessName);
		bool processNameSelected = matchingSpecificFilters.HasFlag(LogMessageField.ProcessName);
		var selectedProcessNames = new HashSet<string>();

		if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
		{
			// select the process name matching the first message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.ProcessNameFilter
				.Items
				.First(x => x.Value == firstMessage.ProcessName);
			specificFilter.Selected = processNameSelected;
			if (processNameSelected) selectedProcessNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
		{
			// select the process name matching the last message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.ProcessNameFilter
				.Items
				.First(x => x.Value == lastMessage.ProcessName);
			specificFilter.Selected = processNameSelected;
			if (processNameSelected) selectedProcessNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
		{
			// select a process name matching messages in between, but surely not the first or the last one
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.ProcessNameFilter
				.Items
				.First(x => x.Value != firstMessage.ProcessName && x.Value != lastMessage.ProcessName);
			specificFilter.Selected = processNameSelected;
			if (processNameSelected) selectedProcessNames.Add(specificFilter.Value);
		}

		// --------------------------------------------------------------------------------------------------------
		// configure process id filter
		// --------------------------------------------------------------------------------------------------------
		filter.ProcessIdFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.ProcessId);
		bool processIdSelected = matchingSpecificFilters.HasFlag(LogMessageField.ProcessId);
		var selectedProcessIds = new HashSet<int>();

		if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
		{
			// select the process id matching the first message in the set
			ISelectableLogMessageFilter_Item<int> specificFilter = filter
				.ProcessIdFilter
				.Items
				.First(x => x.Value == firstMessage.ProcessId);
			specificFilter.Selected = processIdSelected;
			if (processIdSelected) selectedProcessIds.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
		{
			// select the process id matching the last message in the set
			ISelectableLogMessageFilter_Item<int> specificFilter = filter
				.ProcessIdFilter
				.Items
				.First(x => x.Value == lastMessage.ProcessId);
			specificFilter.Selected = processIdSelected;
			if (processIdSelected) selectedProcessIds.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
		{
			// select a process id matching messages in between, but surely not the first or the last one
			ISelectableLogMessageFilter_Item<int> specificFilter = filter
				.ProcessIdFilter
				.Items
				.First(x => x.Value != firstMessage.ProcessId && x.Value != lastMessage.ProcessId);
			specificFilter.Selected = processIdSelected;
			if (processIdSelected) selectedProcessIds.Add(specificFilter.Value);
		}

		// --------------------------------------------------------------------------------------------------------
		// configure log level filter
		// --------------------------------------------------------------------------------------------------------
		filter.LogLevelFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.LogLevelName);
		bool logLevelSelected = matchingSpecificFilters.HasFlag(LogMessageField.LogLevelName);
		var selectedLogLevelNames = new HashSet<string>();

		if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
		{
			// select the log level matching the first message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.LogLevelFilter
				.Items
				.First(x => x.Value == firstMessage.LogLevelName);
			specificFilter.Selected = logLevelSelected;
			if (logLevelSelected) selectedLogLevelNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
		{
			// select the log level matching the last message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.LogLevelFilter
				.Items
				.First(x => x.Value == lastMessage.LogLevelName);
			specificFilter.Selected = logLevelSelected;
			if (logLevelSelected) selectedLogLevelNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
		{
			// select a log level matching messages in between, but surely not the first or the last one
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.LogLevelFilter
				.Items
				.First(x => x.Value != firstMessage.LogLevelName && x.Value != lastMessage.LogLevelName);
			specificFilter.Selected = logLevelSelected;
			if (logLevelSelected) selectedLogLevelNames.Add(specificFilter.Value);
		}

		// --------------------------------------------------------------------------------------------------------
		// configure log writer filter
		// --------------------------------------------------------------------------------------------------------
		filter.LogWriterFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.LogWriterName);
		bool logWriterSelected = matchingSpecificFilters.HasFlag(LogMessageField.LogWriterName);
		var selectedLogWriterNames = new HashSet<string>();

		if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
		{
			// select the log writer name matching the first message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.LogWriterFilter
				.Items
				.First(x => x.Value == firstMessage.LogWriterName);
			specificFilter.Selected = logWriterSelected;
			if (logWriterSelected) selectedLogWriterNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
		{
			// select the log writer name matching the last message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.LogWriterFilter
				.Items
				.First(x => x.Value == lastMessage.LogWriterName);
			specificFilter.Selected = logWriterSelected;
			if (logWriterSelected) selectedLogWriterNames.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
		{
			// select a log writer matching messages in between, but surely not the first or the last one
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.LogWriterFilter
				.Items
				.First(x => x.Value != firstMessage.LogWriterName && x.Value != lastMessage.LogWriterName);
			specificFilter.Selected = logWriterSelected;
			if (logWriterSelected) selectedLogWriterNames.Add(specificFilter.Value);
		}

		// --------------------------------------------------------------------------------------------------------
		// configure tag filter
		// --------------------------------------------------------------------------------------------------------
		filter.TagFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.Tags);
		bool tagSelected = matchingSpecificFilters.HasFlag(LogMessageField.Tags);
		var selectedTags = new HashSet<string>();

		if (matchBehavior.HasFlag(MatchBehavior.MatchFirst))
		{
			// select the tag matching the first message in the set
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.TagFilter
				.Items
				.First(x => firstMessage.Tags.Contains(x.Value));
			specificFilter.Selected = tagSelected;
			if (tagSelected) selectedTags.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchLast))
		{
			// select the tag matching the last message in the set
			// (may also match the first as multiple tags can be attached to a message)
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.TagFilter
				.Items
				.First(x => lastMessage.Tags.Contains(x.Value));
			specificFilter.Selected = tagSelected;
			if (tagSelected) selectedTags.Add(specificFilter.Value);
		}

		if (matchBehavior.HasFlag(MatchBehavior.MatchInBetween))
		{
			// select the tag matching messages in between, but surely not the first or the last one
			ISelectableLogMessageFilter_Item<string> specificFilter = filter
				.TagFilter
				.Items
				.First(x => !firstMessage.Tags.Contains(x.Value) && !lastMessage.Tags.Contains(x.Value));
			specificFilter.Selected = tagSelected;
			if (tagSelected) selectedTags.Add(specificFilter.Value);
		}

		// --------------------------------------------------------------------------------------------------------
		// configure text filter
		// (filtering for the first/last message or in-between is very difficult...)
		// --------------------------------------------------------------------------------------------------------
		string searchText = matchingSpecificFilters.HasFlag(LogMessageField.Text) ? "1/a" : "~~~";
		filter.TextFilter.Enabled = enabledSpecificFilters.HasFlag(LogMessageField.Text);
		filter.TextFilter.IsCaseSensitive = true;
		filter.TextFilter.SearchText = searchText;
		const bool isFilterCaseSensitive = true;

		filteredMessages = unfilteredMessages.Where(Predicate).ToArray();

		return;

		// --------------------------------------------------------------------------------------------------------
		// create predicate matching messages
		// --------------------------------------------------------------------------------------------------------
		bool Predicate(LogFileMessage message)
		{
			// bypass specific filters when the global filter switch is disabled
			if (!isGloballyEnabled)
				return true;

			if (enabledSpecificFilters.HasFlag(LogMessageField.Timestamp))
			{
				// the filter selects all messages except the first and the last message in the collection
				if (message.Timestamp < from || message.Timestamp > to)
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.ApplicationName))
			{
				if (!selectedApplicationNames.Contains(message.ApplicationName))
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.ProcessName))
			{
				if (!selectedProcessNames.Contains(message.ProcessName))
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.ProcessId))
			{
				if (!selectedProcessIds.Contains(message.ProcessId))
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.LogLevelName))
			{
				if (!selectedLogLevelNames.Contains(message.LogLevelName))
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.LogWriterName))
			{
				if (!selectedLogWriterNames.Contains(message.LogWriterName))
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.Tags))
			{
				if (!selectedTags.Any(x => message.Tags.Contains(x)))
					return false;
			}

			if (enabledSpecificFilters.HasFlag(LogMessageField.Text))
			{
				// ReSharper disable once HeuristicUnreachableCode
				if (CultureInfo.InvariantCulture.CompareInfo.IndexOf(message.Text, searchText, isFilterCaseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase) < 0)
				{
					return false;
				}
			}

			return true;
		}
	}

	#endregion

	#region GetPreviousMessage()

	/// <summary>
	/// Test data for <see cref="GetPreviousMessage"/>.
	/// </summary>
	public static TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double> GetPreviousMessage_TestData
	{
		get
		{
			var data = new TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double>();
			var seen = new HashSet<string>();

			static string Key(
				bool            g,
				LogMessageField en,
				LogMessageField ma,
				MatchBehavior   mb,
				double          s) => $"{g}|{(int)en}|{(int)ma}|{(int)mb}|{s:0.###}";

			foreach (object[] entry in CommonGetMessages_TestData)
			{
				bool isGloballyEnabled = (bool)entry[0]!;
				var enabledSpecificFilters = (LogMessageField)entry[1]!;
				var matchingSpecificFilters = (LogMessageField)entry[2]!;
				var matchBehavior = (MatchBehavior)entry[3]!;

				foreach (double startIdRatio in new[] { 0.0, 0.5, 1.0 })
				{
					string key = Key(isGloballyEnabled, enabledSpecificFilters, matchingSpecificFilters, matchBehavior, startIdRatio);
					if (!seen.Add(key))
						throw new InvalidOperationException($"Duplicate in GetPreviousMessage_TestData: {key}");

					data.Add(
						isGloballyEnabled,
						enabledSpecificFilters,
						matchingSpecificFilters,
						matchBehavior,
						startIdRatio);
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests accessing the file-backed log message collection using <see cref="SelectableFileBackedLogMessageFilter.GetPreviousMessage"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(GetPreviousMessage_TestData))]
	public void GetPreviousMessage(
		bool            isGloballyEnabled,
		LogMessageField enabledSpecificFilters,
		LogMessageField matchingSpecificFilters,
		MatchBehavior   matchBehavior,
		double          startIdRatio)
	{
		CreateFilterAndReferencePredicate(
			isGloballyEnabled,
			enabledSpecificFilters,
			matchingSpecificFilters,
			matchBehavior,
			out FileBackedLogMessageCollection collection,
			out SelectableFileBackedLogMessageFilter filter,
			out LogFileMessage[] unfilteredMessages,
			out LogFileMessage[] filteredMessages);

		// calculate the id where to start searching
		long startId = (long)(startIdRatio * (unfilteredMessages.Length - 1));

		// determine the message that is expected to be returned
		LogFileMessage expectedMessage = null; // assume no match at start, revise later
		foreach (LogFileMessage message in filteredMessages)
		{
			if (message.Id > startId) break;
			expectedMessage = message;
		}

		using (collection)
		using (filter)
		{
			LogFileMessage found = filter.GetPreviousMessage(startId);
			Assert.Equal(expectedMessage, found);
		}
	}

	#endregion

	#region GetPreviousMessages()

	/// <summary>
	/// Test data for <see cref="GetPreviousMessages"/>.
	/// </summary>
	public static TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double, int, bool> GetPreviousMessages_TestData
	{
		get
		{
			var data = new TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double, int, bool>();
			var seen = new HashSet<string>();

			static string Key(
				bool            g,
				LogMessageField en,
				LogMessageField ma,
				MatchBehavior   mb,
				double          s,
				int             c,
				bool            r) => $"{g}|{(int)en}|{(int)ma}|{(int)mb}|{s:0.###}|{c}|{r}";

			// Reuse data from CommonGetMessages_TestData
			foreach (object[] entry in CommonGetMessages_TestData)
			{
				bool isGloballyEnabled = (bool)entry[0]!;
				var enabledSpecificFilters = (LogMessageField)entry[1]!;
				var matchingSpecificFilters = (LogMessageField)entry[2]!;
				var matchBehavior = (MatchBehavior)entry[3]!;

				foreach (double startIdRatio in new[] { 0.0, 0.5, 1.0 })
				foreach (int count in new[] { 5 })
				foreach (bool reverse in new[] { false, true })
				{
					string key = Key(isGloballyEnabled, enabledSpecificFilters, matchingSpecificFilters, matchBehavior, startIdRatio, count, reverse);
					if (!seen.Add(key))
						throw new InvalidOperationException($"Duplicate in GetPreviousMessages_TestData: {key}");

					data.Add(
						isGloballyEnabled,
						enabledSpecificFilters,
						matchingSpecificFilters,
						matchBehavior,
						startIdRatio,
						count,
						reverse);
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests accessing the file-backed log message collection using <see cref="SelectableFileBackedLogMessageFilter.GetPreviousMessages"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(GetPreviousMessages_TestData))]
	public void GetPreviousMessages(
		bool            isGloballyEnabled,
		LogMessageField enabledSpecificFilters,
		LogMessageField matchingSpecificFilters,
		MatchBehavior   matchBehavior,
		double          startIdRatio,
		int             count,
		bool            reverse)
	{
		CreateFilterAndReferencePredicate(
			isGloballyEnabled,
			enabledSpecificFilters,
			matchingSpecificFilters,
			matchBehavior,
			out FileBackedLogMessageCollection collection,
			out SelectableFileBackedLogMessageFilter filter,
			out LogFileMessage[] unfilteredMessages,
			out LogFileMessage[] filteredMessages);

		// calculate the id where to start searching
		long startId = (long)(startIdRatio * (unfilteredMessages.Length - 1));

		// determine the messages that is expected to be returned
		List<LogFileMessage> expectedMessages = filteredMessages
			.TakeWhile(message => message.Id <= startId)
			.ToList();

		// remove superfluous messages and reverse the result list, if necessary
		expectedMessages.RemoveRange(0, Math.Max(expectedMessages.Count - count, 0));
		if (!reverse) expectedMessages.Reverse();

		using (collection)
		using (filter)
		{
			LogFileMessage[] found = filter.GetPreviousMessages(startId, count, reverse);
			Assert.Equal(expectedMessages, found);
		}
	}

	#endregion

	#region GetNextMessage()

	/// <summary>
	/// Test data for <see cref="GetNextMessage"/>.
	/// </summary>
	public static TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double> GetNextMessage_TestData
	{
		get
		{
			var data = new TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double>();
			var seen = new HashSet<string>();

			static string Key(
				bool            g,
				LogMessageField en,
				LogMessageField ma,
				MatchBehavior   mb,
				double          s) => $"{g}|{(int)en}|{(int)ma}|{(int)mb}|{s:0.###}";

			foreach (object[] entry in CommonGetMessages_TestData)
			{
				bool isGloballyEnabled = (bool)entry[0]!;
				var enabledSpecificFilters = (LogMessageField)entry[1]!;
				var matchingSpecificFilters = (LogMessageField)entry[2]!;
				var matchBehavior = (MatchBehavior)entry[3]!;

				foreach (double startIdRatio in new[] { 0.0, 0.5, 1.0 })
				{
					string key = Key(isGloballyEnabled, enabledSpecificFilters, matchingSpecificFilters, matchBehavior, startIdRatio);
					if (!seen.Add(key))
						throw new InvalidOperationException($"Duplicate in GetNextMessage_TestData: {key}");

					data.Add(
						isGloballyEnabled,
						enabledSpecificFilters,
						matchingSpecificFilters,
						matchBehavior,
						startIdRatio);
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests accessing the file-backed log message collection using <see cref="SelectableFileBackedLogMessageFilter.GetNextMessage"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(GetNextMessage_TestData))]
	public void GetNextMessage(
		bool            isGloballyEnabled,
		LogMessageField enabledSpecificFilters,
		LogMessageField matchingSpecificFilters,
		MatchBehavior   matchBehavior,
		double          startIdRatio)
	{
		CreateFilterAndReferencePredicate(
			isGloballyEnabled,
			enabledSpecificFilters,
			matchingSpecificFilters,
			matchBehavior,
			out FileBackedLogMessageCollection collection,
			out SelectableFileBackedLogMessageFilter filter,
			out LogFileMessage[] unfilteredMessages,
			out LogFileMessage[] filteredMessages);

		// calculate the id where to start searching
		long startId = (long)(startIdRatio * (unfilteredMessages.Length - 1));

		// determine the message that is expected to be returned
		LogFileMessage expectedMessage = null; // assume no match at start, revise later
		for (int i = filteredMessages.Length - 1; i >= 0; i--)
		{
			LogFileMessage message = filteredMessages[i];
			if (message.Id < startId) break;
			expectedMessage = message;
		}

		using (collection)
		using (filter)
		{
			LogFileMessage found = filter.GetNextMessage(startId);
			Assert.Equal(expectedMessage, found);
		}
	}

	#endregion

	#region GetNextMessages()

	/// <summary>
	/// Test data for <see cref="GetNextMessages"/>.
	/// </summary>
	public static TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double, int> GetNextMessages_TestData
	{
		get
		{
			var data = new TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double, int>();
			var seen = new HashSet<string>();

			static string Key(
				bool            g,
				LogMessageField en,
				LogMessageField ma,
				MatchBehavior   mb,
				double          s,
				int             c) => $"{g}|{(int)en}|{(int)ma}|{(int)mb}|{s:0.###}|{c}";

			foreach (object[] entry in CommonGetMessages_TestData)
			{
				bool isGloballyEnabled = (bool)entry[0]!;
				var enabledSpecificFilters = (LogMessageField)entry[1]!;
				var matchingSpecificFilters = (LogMessageField)entry[2]!;
				var matchBehavior = (MatchBehavior)entry[3]!;

				foreach (double startIdRatio in new[] { 0.0, 0.5, 1.0 })
				foreach (int count in new[] { 5 })
				{
					string key = Key(isGloballyEnabled, enabledSpecificFilters, matchingSpecificFilters, matchBehavior, startIdRatio, count);
					if (!seen.Add(key))
						throw new InvalidOperationException($"Duplicate in GetNextMessages_TestData: {key}");

					data.Add(
						isGloballyEnabled,
						enabledSpecificFilters,
						matchingSpecificFilters,
						matchBehavior,
						startIdRatio,
						count);
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests accessing the file-backed log message collection using <see cref="SelectableFileBackedLogMessageFilter.GetNextMessages"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(GetNextMessages_TestData))]
	public void GetNextMessages(
		bool            isGloballyEnabled,
		LogMessageField enabledSpecificFilters,
		LogMessageField matchingSpecificFilters,
		MatchBehavior   matchBehavior,
		double          startIdRatio,
		int             count)
	{
		CreateFilterAndReferencePredicate(
			isGloballyEnabled,
			enabledSpecificFilters,
			matchingSpecificFilters,
			matchBehavior,
			out FileBackedLogMessageCollection collection,
			out SelectableFileBackedLogMessageFilter filter,
			out LogFileMessage[] unfilteredMessages,
			out LogFileMessage[] filteredMessages);

		// calculate the id where to start searching
		long startId = (long)(startIdRatio * (unfilteredMessages.Length - 1));

		// determine the messages that is expected to be returned
		var expectedMessages = new List<LogFileMessage>();
		for (int i = filteredMessages.Length - 1; i >= 0; i--)
		{
			LogFileMessage message = filteredMessages[i];
			if (message.Id < startId) break;
			expectedMessages.Add(message);
		}

		// remove superfluous messages and reverse the result list to comply with the expected order
		expectedMessages.RemoveRange(0, Math.Max(expectedMessages.Count - count, 0));
		expectedMessages.Reverse();

		using (collection)
		using (filter)
		{
			LogFileMessage[] found = filter.GetNextMessages(startId, count);
			Assert.Equal(expectedMessages, found);
		}
	}

	#endregion

	#region GetMessageRange()

	/// <summary>
	/// Test data for <see cref="GetMessageRange"/>.
	/// </summary>
	public static TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double, double> GetMessageRange_TestData
	{
		get
		{
			var data = new TheoryData<bool, LogMessageField, LogMessageField, MatchBehavior, double, double>();
			var seen = new HashSet<string>();

			static string Key(
				bool            g,
				LogMessageField en,
				LogMessageField ma,
				MatchBehavior   mb,
				double          s,
				double          e) => $"{g}|{(int)en}|{(int)ma}|{(int)mb}|{s:0.###}->{e:0.###}";

			foreach (object[] entry in CommonGetMessages_TestData)
			{
				bool isGloballyEnabled = (bool)entry[0]!;
				var enabledSpecificFilters = (LogMessageField)entry[1]!;
				var matchingSpecificFilters = (LogMessageField)entry[2]!;
				var matchBehavior = (MatchBehavior)entry[3]!;

				foreach (double startIdRatio in new[] { 0.0, 0.5, 1.0 })
				foreach (double endIdRatio in new[] { 0.0, 0.5, 1.0 })
				{
					if (startIdRatio <= endIdRatio)
					{
						string key = Key(isGloballyEnabled, enabledSpecificFilters, matchingSpecificFilters, matchBehavior, startIdRatio, endIdRatio);
						if (!seen.Add(key))
							throw new InvalidOperationException($"Duplicate in GetMessageRange_TestData: {key}");

						data.Add(
							isGloballyEnabled,
							enabledSpecificFilters,
							matchingSpecificFilters,
							matchBehavior,
							startIdRatio,
							endIdRatio);
					}
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests accessing the file-backed log message collection using <see cref="SelectableFileBackedLogMessageFilter.GetMessageRange"/>.
	/// </summary>
	[Theory]
	[MemberData(nameof(GetMessageRange_TestData))]
	public void GetMessageRange(
		bool            isGloballyEnabled,
		LogMessageField enabledSpecificFilters,
		LogMessageField matchingSpecificFilters,
		MatchBehavior   matchBehavior,
		double          startIdRatio,
		double          endIdRatio)
	{
		CreateFilterAndReferencePredicate(
			isGloballyEnabled,
			enabledSpecificFilters,
			matchingSpecificFilters,
			matchBehavior,
			out FileBackedLogMessageCollection collection,
			out SelectableFileBackedLogMessageFilter filter,
			out LogFileMessage[] unfilteredMessages,
			out LogFileMessage[] filteredMessages);

		// calculate the id where to start/end searching
		long startId = (long)(startIdRatio * (unfilteredMessages.Length - 1));
		long endId = (long)(endIdRatio * (unfilteredMessages.Length - 1));

		// determine the messages that is expected to be returned
		List<LogFileMessage> expectedMessages = filteredMessages
			.Where(message => message.Id >= startId && message.Id <= endId)
			.ToList();

		using (collection)
		using (filter)
		{
			LogFileMessage[] found = filter.GetMessageRange(startId, endId);
			Assert.Equal(expectedMessages, found);
		}
	}

	#endregion
}
