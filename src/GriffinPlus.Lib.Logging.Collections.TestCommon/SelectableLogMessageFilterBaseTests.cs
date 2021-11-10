///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

using Xunit;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Unit tests targeting the <see cref="SelectableLogMessageFilterBase{TMessage,TUnfilteredCollection}"/> base class of collection filter
	/// implementations.
	/// </summary>
	public abstract class SelectableLogMessageFilterBaseTests<TSelectableLogMessageFilter, TLogMessageCollection>
		where TSelectableLogMessageFilter : class, ISelectableLogMessageFilter<LogMessage>, new()
		where TLogMessageCollection : class, ILogMessageCollection<LogMessage>
	{
		/// <summary>
		/// Number of test messages a test message set should contain (if not otherwise required for a test).
		/// </summary>
		private const int DefaultTestMessageCount = 500;

		#region Adjustments of Derived Test Classes

		/// <summary>
		/// Creates a new instance of the collection class to test, pre-populated with the specified number of random log messages.
		/// </summary>
		/// <param name="count">Number of random log messages the collection should contain.</param>
		/// <param name="messages">Receives messages that have been put into the collection.</param>
		/// <returns>A new instance of the collection class to test.</returns>
		protected abstract TLogMessageCollection CreateCollection(int count, out LogMessage[] messages);

		/// <summary>
		/// Gets or sets a value indicating whether the collection is expected to be read-only.
		/// </summary>
		protected bool CollectionIsReadOnly { get; set; } = false;

		#endregion

		#region Construction

		/// <summary>
		/// Tests creating a log message filter. The filter is not attached to a collection.
		/// </summary>
		[Fact]
		public void Create_Unbound()
		{
			var filter = new TSelectableLogMessageFilter();
			TestInitialFilterSettings(filter, Array.Empty<LogMessage>(), false);
		}

		#endregion

		#region TimestampFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.TimestampFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TimestampFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.TimestampFilter.Enabled);

				Assert.PropertyChanged(
					filter.TimestampFilter,
					nameof(filter.TimestampFilter.Enabled),
					() => filter.TimestampFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.TimestampFilter,
					nameof(filter.TimestampFilter.Enabled),
					() => filter.TimestampFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'From' of the <see cref="ISelectableLogMessageFilter{TMessage}.TimestampFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TimestampFilter_From(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.Equal(new DateTimeOffset(0, TimeSpan.Zero), filter.TimestampFilter.From);

				Assert.PropertyChanged(
					filter.TimestampFilter,
					nameof(filter.TimestampFilter.From),
					() => filter.TimestampFilter.From = DateTimeOffset.Now);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'To' of the <see cref="ISelectableLogMessageFilter{TMessage}.TimestampFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TimestampFilter_To(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.Equal(new DateTimeOffset(0, TimeSpan.Zero), filter.TimestampFilter.To);

				Assert.PropertyChanged(
					filter.TimestampFilter,
					nameof(filter.TimestampFilter.To),
					() => filter.TimestampFilter.To = DateTimeOffset.Now);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		#endregion

		#region ApplicationNameFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.ApplicationNameFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ApplicationNameFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.ApplicationNameFilter.Enabled);

				Assert.PropertyChanged(
					filter.ApplicationNameFilter,
					nameof(filter.ApplicationNameFilter.Enabled),
					() => filter.ApplicationNameFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.ApplicationNameFilter,
					nameof(filter.ApplicationNameFilter.Enabled),
					() => filter.ApplicationNameFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'AccumulateItems' of the <see cref="ISelectableLogMessageFilter{TMessage}.ApplicationNameFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ApplicationNameFilter_AccumulateItems(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.ApplicationNameFilter.AccumulateItems);

				Assert.PropertyChanged(
					filter.ApplicationNameFilter,
					nameof(filter.ApplicationNameFilter.AccumulateItems),
					() => filter.ApplicationNameFilter.AccumulateItems = true);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior

				Assert.PropertyChanged(
					filter.ApplicationNameFilter,
					nameof(filter.ApplicationNameFilter.AccumulateItems),
					() => filter.ApplicationNameFilter.AccumulateItems = false);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior
			}
		}

		#endregion

		#region ProcessNameFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.ProcessNameFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ProcessNameFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.ProcessNameFilter.Enabled);

				Assert.PropertyChanged(
					filter.ProcessNameFilter,
					nameof(filter.ProcessNameFilter.Enabled),
					() => filter.ProcessNameFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.ProcessNameFilter,
					nameof(filter.ProcessNameFilter.Enabled),
					() => filter.ProcessNameFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'AccumulateItems' of the <see cref="ISelectableLogMessageFilter{TMessage}.ProcessNameFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ProcessNameFilter_AccumulateItems(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.ProcessNameFilter.AccumulateItems);

				Assert.PropertyChanged(
					filter.ProcessNameFilter,
					nameof(filter.ProcessNameFilter.AccumulateItems),
					() => filter.ProcessNameFilter.AccumulateItems = true);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior

				Assert.PropertyChanged(
					filter.ProcessNameFilter,
					nameof(filter.ProcessNameFilter.AccumulateItems),
					() => filter.ProcessNameFilter.AccumulateItems = false);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior
			}
		}

		#endregion

		#region ProcessIdFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.ProcessIdFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ProcessIdFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.ProcessIdFilter.Enabled);

				Assert.PropertyChanged(
					filter.ProcessIdFilter,
					nameof(filter.ProcessIdFilter.Enabled),
					() => filter.ProcessIdFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.ProcessIdFilter,
					nameof(filter.ProcessIdFilter.Enabled),
					() => filter.ProcessIdFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'AccumulateItems' of the <see cref="ISelectableLogMessageFilter{TMessage}.ProcessIdFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ProcessIdFilter_AccumulateItems(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.ProcessIdFilter.AccumulateItems);

				Assert.PropertyChanged(
					filter.ProcessIdFilter,
					nameof(filter.ProcessIdFilter.AccumulateItems),
					() => filter.ProcessIdFilter.AccumulateItems = true);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior

				Assert.PropertyChanged(
					filter.ProcessIdFilter,
					nameof(filter.ProcessIdFilter.AccumulateItems),
					() => filter.ProcessIdFilter.AccumulateItems = false);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior
			}
		}

		#endregion

		#region LogLevelFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.LogLevelFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void LogLevelFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.LogLevelFilter.Enabled);

				Assert.PropertyChanged(
					filter.LogLevelFilter,
					nameof(filter.LogLevelFilter.Enabled),
					() => filter.LogLevelFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.LogLevelFilter,
					nameof(filter.LogLevelFilter.Enabled),
					() => filter.LogLevelFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'AccumulateItems' of the <see cref="ISelectableLogMessageFilter{TMessage}.LogLevelFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void LogLevelFilter_AccumulateItems(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.LogLevelFilter.AccumulateItems);

				Assert.PropertyChanged(
					filter.LogLevelFilter,
					nameof(filter.LogLevelFilter.AccumulateItems),
					() => filter.LogLevelFilter.AccumulateItems = true);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior

				Assert.PropertyChanged(
					filter.LogLevelFilter,
					nameof(filter.LogLevelFilter.AccumulateItems),
					() => filter.LogLevelFilter.AccumulateItems = false);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior
			}
		}

		#endregion

		#region LogWriterFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.LogWriterFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void LogWriterFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.LogWriterFilter.Enabled);

				Assert.PropertyChanged(
					filter.LogWriterFilter,
					nameof(filter.LogWriterFilter.Enabled),
					() => filter.LogWriterFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.LogWriterFilter,
					nameof(filter.LogWriterFilter.Enabled),
					() => filter.LogWriterFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'AccumulateItems' of the <see cref="ISelectableLogMessageFilter{TMessage}.LogWriterFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void LogWriterFilter_AccumulateItems(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.LogWriterFilter.AccumulateItems);

				Assert.PropertyChanged(
					filter.LogWriterFilter,
					nameof(filter.LogWriterFilter.AccumulateItems),
					() => filter.LogWriterFilter.AccumulateItems = true);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior

				Assert.PropertyChanged(
					filter.LogWriterFilter,
					nameof(filter.LogWriterFilter.AccumulateItems),
					() => filter.LogWriterFilter.AccumulateItems = false);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior
			}
		}

		#endregion

		#region TagFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.TagFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TagFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.TagFilter.Enabled);

				Assert.PropertyChanged(
					filter.TagFilter,
					nameof(filter.TagFilter.Enabled),
					() => filter.TagFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.TagFilter,
					nameof(filter.TagFilter.Enabled),
					() => filter.TagFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'AccumulateItems' of the <see cref="ISelectableLogMessageFilter{TMessage}.TagFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TagFilter_AccumulateItems(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.TagFilter.AccumulateItems);

				Assert.PropertyChanged(
					filter.TagFilter,
					nameof(filter.TagFilter.AccumulateItems),
					() => filter.TagFilter.AccumulateItems = true);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior

				Assert.PropertyChanged(
					filter.TagFilter,
					nameof(filter.TagFilter.AccumulateItems),
					() => filter.TagFilter.AccumulateItems = false);
				Assert.False(filterChanged); // the event is not called as it does not influence the filtering behavior
			}
		}

		#endregion

		#region TextFilter

		/// <summary>
		/// Tests setting 'Enable' of the <see cref="ISelectableLogMessageFilter{TMessage}.TextFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TextFilter_Enabled(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.TextFilter.Enabled);

				Assert.PropertyChanged(
					filter.TextFilter,
					nameof(filter.TextFilter.Enabled),
					() => filter.TextFilter.Enabled = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.TextFilter,
					nameof(filter.TextFilter.Enabled),
					() => filter.TextFilter.Enabled = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'IsCaseSensitive' of the <see cref="ISelectableLogMessageFilter{TMessage}.TextFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TextFilter_IsCaseSensitive(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.False(filter.TextFilter.IsCaseSensitive);

				Assert.PropertyChanged(
					filter.TextFilter,
					nameof(filter.TextFilter.IsCaseSensitive),
					() => filter.TextFilter.IsCaseSensitive = true);
				Assert.True(filterChanged);
				filterChanged = false;

				Assert.PropertyChanged(
					filter.TextFilter,
					nameof(filter.TextFilter.IsCaseSensitive),
					() => filter.TextFilter.IsCaseSensitive = false);
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		/// <summary>
		/// Tests setting 'SearchText' of the <see cref="ISelectableLogMessageFilter{TMessage}.TextFilter"/>.
		/// </summary>
		/// <param name="attach">
		/// <c>true</c> to attach the filter to a collection before testing;
		/// <c>false</c> to test the filter without being attached to a collection.
		/// </param>
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void TextFilter_SearchText(bool attach)
		{
			using (var collection = CreateCollection(DefaultTestMessageCount, out _))
			using (var filter = new TSelectableLogMessageFilter())
			{
				if (attach) filter.AttachToCollection(collection);
				bool filterChanged = false;
				filter.FilterChanged += (sender, args) => filterChanged = true;

				Assert.Equal("", filter.TextFilter.SearchText);

				Assert.PropertyChanged(
					filter.TextFilter,
					nameof(filter.TextFilter.SearchText),
					() => filter.TextFilter.SearchText = "XXX");
				Assert.True(filterChanged);
				filterChanged = false;
			}
		}

		#endregion

		#region AttachToCollection()

		/// <summary>
		/// Tests creating a log message filter and attaching it to a populated collection.
		/// </summary>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(1000)]
		public void AttachToCollection(int count)
		{
			using (var collection = CreateCollection(count, out var messages))
			{
				// create a new filter
				var filter = new TSelectableLogMessageFilter();

				// attach filter to the collection and check whether the filter presents the settings as expected
				filter.AttachToCollection(collection);
				TestInitialFilterSettings(filter, messages);
			}
		}

		#endregion

		#region DetachFromCollection()

		/// <summary>
		/// Tests creating a log message filter, attaching it to a populated collection and detaching it afterwards.
		/// </summary>
		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(1000)]
		public void DetachFromCollection(int count)
		{
			using (var collection = CreateCollection(count, out var messages))
			{
				// create a new filter
				var filter = new TSelectableLogMessageFilter();

				// attach filter to the collection and check whether the filter presents the settings as expected
				filter.AttachToCollection(collection);
				TestInitialFilterSettings(filter, messages);

				// detach filter from the collection, the filter should remain in the same state as it is not reset
				// as part of the detaching procedure
				filter.DetachFromCollection();
				TestInitialFilterSettings(filter, messages);
			}
		}

		#endregion

		#region Matches() (for 'Timestamp')

		/// <summary>
		/// Test data for the <see cref="Matches_TimestampIntervalFilter"/> test method.
		/// </summary>
		public static IEnumerable<object[]> MatchesTestData_TimestampIntervalFilter
		{
			get
			{
				const int count = DefaultTestMessageCount;

				foreach (bool enableGlobalFilter in new[] { false, true })
				foreach (bool enableSpecificFilter in new[] { false, true })
				{
					// empty collection
					yield return new object[] { 0, enableGlobalFilter, enableSpecificFilter, -1, -1 };

					// entire interval
					yield return new object[] { count, enableGlobalFilter, enableSpecificFilter, 0, count - 1 };

					// interval in between
					yield return new object[] { count, enableGlobalFilter, enableSpecificFilter, 0, count - 2 };         // all messages, but not the right-most
					yield return new object[] { count, enableGlobalFilter, enableSpecificFilter, 1, count - 1 };         // all messages, but not the left-most
					yield return new object[] { count, enableGlobalFilter, enableSpecificFilter, 1, count - 2 };         // all messages, but not the left-most and the right-most
					yield return new object[] { count, enableGlobalFilter, enableSpecificFilter, count / 2, count / 2 }; // one message in the middle
				}
			}
		}

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.TimestampFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="firstMatchingMessageIndex">Index of the first message that should be in the selected interval.</param>
		/// <param name="lastMatchingMessageIndex">Index of the last message that should be in the selected interval.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_TimestampIntervalFilter))]
		public void Matches_TimestampIntervalFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			int  firstMatchingMessageIndex,
			int  lastMatchingMessageIndex)
		{
			using (var collection = CreateCollection(count, out var messages))
			using (var filter = new TSelectableLogMessageFilter())
			{
				// attach filter to the collection
				filter.AttachToCollection(collection);
				TestInitialFilterSettings(filter, messages);

				// enable/disable the global filter
				filter.Enabled = enableGlobalFilter;

				// configure the filter
				filter.TimestampFilter.Enabled = enableSpecificFilter;
				var from = DateTimeOffset.MinValue;
				var to = DateTimeOffset.MaxValue;
				if (count > 0)
				{
					filter.TimestampFilter.From = from = messages[firstMatchingMessageIndex].Timestamp;
					filter.TimestampFilter.To = to = messages[lastMatchingMessageIndex].Timestamp;
				}

				// determine the log message set that is expected to pass the filter
				var expected = new List<LogMessage>();
				var matching = new List<LogMessage>();
				foreach (var message in messages)
				{
					// pass the message to filter and check whether it matches
					if (filter.Matches(message))
						matching.Add(message);

					// adjust the set of messages that are expected to pass the filter
					if (!enableGlobalFilter || !enableSpecificFilter || message.Timestamp >= from && message.Timestamp <= to)
						expected.Add(message);
				}

				// check whether the expected messages have passed the filter
				Assert.Equal(expected, matching);
			}
		}

		#endregion

		#region Matches() (for Item Filters)

		#region Matches() (for 'ApplicationName')

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.ApplicationNameFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_ItemFilters))]
		public void Matches_ApplicationNameFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool accumulateItems)
		{
			Matches_ItemFilters(
				x => x.ApplicationName,
				x => x.ApplicationNameFilter,
				count,
				enableGlobalFilter,
				enableSpecificFilter,
				accumulateItems);
		}

		#endregion

		#region Matches() (for 'ProcessName')

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.ProcessNameFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_ItemFilters))]
		public void Matches_ProcessNameFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool accumulateItems)
		{
			Matches_ItemFilters(
				x => x.ProcessName,
				x => x.ProcessNameFilter,
				count,
				enableGlobalFilter,
				enableSpecificFilter,
				accumulateItems);
		}

		#endregion

		#region Matches() (for 'ProcessId')

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.ProcessIdFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_ItemFilters))]
		public void Matches_ProcessIdFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool accumulateItems)
		{
			Matches_ItemFilters(
				x => x.ProcessId,
				x => x.ProcessIdFilter,
				count,
				enableGlobalFilter,
				enableSpecificFilter,
				accumulateItems);
		}

		#endregion

		#region Matches() (for 'LogLevel')

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.LogLevelFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_ItemFilters))]
		public void Matches_LogLevelFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool accumulateItems)
		{
			Matches_ItemFilters(
				x => x.LogLevelName,
				x => x.LogLevelFilter,
				count,
				enableGlobalFilter,
				enableSpecificFilter,
				accumulateItems);
		}

		#endregion

		#region Matches() (for 'LogWriter')

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.LogWriterFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_ItemFilters))]
		public void Matches_LogWriterFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool accumulateItems)
		{
			Matches_ItemFilters(
				x => x.LogWriterName,
				x => x.LogWriterFilter,
				count,
				enableGlobalFilter,
				enableSpecificFilter,
				accumulateItems);
		}

		#endregion

		#region Common

		/// <summary>
		/// Test data for test method that test <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> for item filters
		/// (application name, process name, )
		/// </summary>
		public static IEnumerable<object[]> MatchesTestData_ItemFilters
		{
			get
			{
				foreach (int count in new[] { 0, 1, 500 })
				foreach (bool enableGlobally in new[] { false, true })
				foreach (bool enablePerItem in new[] { false, true })
				foreach (bool accumulateItems in new[] { false })
				{
					yield return new object[]
					{
						count,
						enableGlobally,
						enablePerItem,
						accumulateItems
					};
				}
			}
		}

		/// <summary>
		/// Common test logic for tests targeting <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/>.
		/// </summary>
		/// <typeparam name="T">Type of an item value (same as the type of the corresponding property of the log message).</typeparam>
		/// <param name="propertySelectorExpression">Expression that selects the property to filter for from a log message.</param>
		/// <param name="itemFilterSelectorExpression">Expression that selects the item filter corresponding to the selected log message property.</param>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		private void Matches_ItemFilters<T>(
			Expression<Func<LogMessage, T>>                                                                      propertySelectorExpression,
			Expression<Func<ISelectableLogMessageFilter<LogMessage>, ISelectableLogMessageFilter_ItemFilter<T>>> itemFilterSelectorExpression,
			int                                                                                                  count,
			bool                                                                                                 enableGlobalFilter,
			bool                                                                                                 enableSpecificFilter,
			bool                                                                                                 accumulateItems)
		{
			var propertySelector = propertySelectorExpression.Compile();
			var itemFilterSelector = itemFilterSelectorExpression.Compile();

			using (var collection = CreateCollection(count, out var messages))
			{
				// determine the different items one can filter
				var itemsToFilterFor = messages.Select(propertySelector).OrderBy(x => x).Distinct().ToList();

				// try to filter the message set selecting one item at a time
				foreach (var itemToFilterFor in itemsToFilterFor)
				{
					// create a new filter
					using (var filter = new TSelectableLogMessageFilter())
					{
						// attach filter to the collection
						// (it is automatically detached as part of its disposal procedure)
						filter.AttachToCollection(collection);
						TestInitialFilterSettings(filter, messages);

						// enable/disable the global filter
						filter.Enabled = enableGlobalFilter;

						// configure the filter
						var itemFilter = itemFilterSelector(filter);
						itemFilter.Enabled = enableSpecificFilter;
						itemFilter.AccumulateItems = accumulateItems;
						var itemFilterItem = itemFilter.Items.First(x => x.Value.Equals(itemToFilterFor));
						itemFilterItem.Selected = true;

						// determine the log message set that is expected to pass the filter
						var expected = new List<LogMessage>();
						var matching = new List<LogMessage>();
						foreach (var message in messages)
						{
							// pass the message to filter and check whether it matches
							if (filter.Matches(message))
								matching.Add(message);

							if (!enableGlobalFilter || !enableSpecificFilter)
							{
								// global or specific filter is disabled
								// => the message passes the filter
								expected.Add(message);
								continue;
							}

							// add message to the set of messages expected to pass the filter
							if (propertySelector(message).Equals(itemToFilterFor))
								expected.Add(message);
						}

						// check whether the expected messages have passed the filter
						Assert.Equal(expected, matching);
					}
				}
			}
		}

		#endregion

		#endregion

		#region Matches() (for 'Tag')

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.TagFilter"/>.
		/// </summary>
		/// <param name="count">Number of log messages to insert into the collection before starting to filter.</param>
		/// <param name="enableGlobalFilter"><c>true</c> to enable the filter globally; otherwise <c>false</c> (disables the filter entirely).</param>
		/// <param name="enableSpecificFilter"><c>true</c> to enable the specific item filter; otherwise <c>false</c> (disables the item filter).</param>
		/// <param name="accumulateItems"><c>true</c> to let the item filter accumulate items; otherwise <c>false</c>.</param>
		[Theory]
		[MemberData(nameof(MatchesTestData_ItemFilters))]
		public void Matches_TagFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool accumulateItems)
		{
			using (var collection = CreateCollection(count, out var messages))
			{
				// determine the different tag sets one can filter
				var tags = messages.SelectMany(x => x.Tags).OrderBy(x => x).Distinct().ToList();

				// try to filter the message set selecting one item at a time
				foreach (string tag in tags)
				{
					// create a new filter
					using (var filter = new TSelectableLogMessageFilter())
					{
						// attach filter to the collection
						filter.AttachToCollection(collection);
						TestInitialFilterSettings(filter, messages);

						// enable/disable the global filter
						filter.Enabled = enableGlobalFilter;

						// configure the filter
						filter.TagFilter.Enabled = enableSpecificFilter;
						filter.TagFilter.AccumulateItems = accumulateItems;
						var itemFilterItem = filter.TagFilter.Items.First(x => x.Value.Equals(tag));
						itemFilterItem.Selected = true;

						// determine the log message set that is expected to pass the filter
						var expected = new List<LogMessage>();
						var matching = new List<LogMessage>();
						foreach (var message in messages)
						{
							// pass the message to filter and check whether it matches
							if (filter.Matches(message))
								matching.Add(message);

							if (!enableGlobalFilter || !enableSpecificFilter || message.Tags.Contains(tag))
							{
								// global or specific filter is disabled
								// => the message passes the filter
								expected.Add(message);
							}
						}

						// check whether the expected messages have passed the filter
						Assert.Equal(expected, matching);
					}
				}
			}
		}

		#endregion

		#region Matches() (for 'Text')

		/// <summary>
		/// Test data for the <see cref="Matches_TextFilter"/> test method.
		/// </summary>
		public static IEnumerable<object[]> MatchesTestData_TextFilter
		{
			get
			{
				const int count = 500;

				foreach (bool enableGlobalFilter in new[] { false, true })
				foreach (bool enableSpecificFilter in new[] { false, true })
				foreach (bool isFilterCaseSensitive in new[] { false, true })
				foreach (bool isSearchTextExact in new[] { false, true })
				{
					yield return new object[]
					{
						0,
						enableGlobalFilter,
						enableSpecificFilter,
						isFilterCaseSensitive,
						isSearchTextExact
					};

					yield return new object[]
					{
						count,
						enableGlobalFilter,
						enableSpecificFilter,
						isFilterCaseSensitive,
						isSearchTextExact
					};
				}
			}
		}

		/// <summary>
		/// Tests <see cref="ILogMessageCollectionFilterBase{TMessage}.Matches"/> with <see cref="ISelectableLogMessageFilter{TMessage}.TextFilter"/>.
		/// </summary>
		/// <param name="count">
		/// Number of log messages to insert into the collection before starting to filter.
		/// </param>
		/// <param name="enableGlobalFilter">
		/// <c>true</c> to enable the filter globally;
		/// otherwise <c>false</c> (disables the filter entirely).
		/// </param>
		/// <param name="enableSpecificFilter">
		/// <c>true</c> to enable the specific item filter;
		/// otherwise <c>false</c> (disables the item filter).
		/// </param>
		/// <param name="isFilterCaseSensitive">
		/// <c>true</c> to match case-sensitive;
		/// <c>false</c> to match case-insensitive.
		/// </param>
		/// <param name="isSearchTextExact">
		/// <c>true</c> to pass the search text exactly as it occurs in the message;
		/// <c>false</c> to convert it to upper-case expecting that a case-sensitive filter will fail.
		/// </param>
		[Theory]
		[MemberData(nameof(MatchesTestData_TextFilter))]
		public void Matches_TextFilter(
			int  count,
			bool enableGlobalFilter,
			bool enableSpecificFilter,
			bool isFilterCaseSensitive,
			bool isSearchTextExact)
		{
			using (var collection = CreateCollection(count, out var messages))
			{
				// checking that at least one message passes the filter
				// => search for '000000/a' which is always part of the text of the first message in the message set
				string searchText = "000000/a";
				if (!isSearchTextExact) searchText = searchText.ToUpper();

				// create a new filter
				using (var filter = new TSelectableLogMessageFilter())
				{
					// attach filter to the collection
					filter.AttachToCollection(collection);
					TestInitialFilterSettings(filter, messages);

					// enable/disable the global filter
					filter.Enabled = enableGlobalFilter;

					// configure the text filter
					filter.TextFilter.Enabled = enableSpecificFilter;
					filter.TextFilter.IsCaseSensitive = isFilterCaseSensitive;
					filter.TextFilter.SearchText = searchText;

					// determine the log message set that is expected to pass the filter
					var expected = new List<LogMessage>();
					var matching = new List<LogMessage>();
					foreach (var message in messages)
					{
						// pass the message to filter and check whether it matches
						bool matches = filter.Matches(message);
						if (matches) matching.Add(message);

						// if the global filter and the specific filter is enabled,
						// filtering is done case-sensitive and the search text does not match exactly
						// => no message should pass the filter at all
						if (enableGlobalFilter && enableSpecificFilter && isFilterCaseSensitive && !isSearchTextExact)
							Assert.False(matches);

						if (!enableGlobalFilter ||
						    !enableSpecificFilter ||
						    CultureInfo.InvariantCulture.CompareInfo.IndexOf(
							    message.Text,
							    searchText,
							    isFilterCaseSensitive ? CompareOptions.None : CompareOptions.IgnoreCase) >=
						    0)
						{
							// global or specific filter is disabled
							// => the message passes the filter
							expected.Add(message);
						}
					}

					// check whether the expected messages have passed the filter
					Assert.Equal(expected, matching);
				}
			}
		}

		#endregion

		#region TestInitialFilterSettings()

		/// <summary>
		/// Tests whether the filter has the expected settings after attaching it to a collection.
		/// </summary>
		/// <param name="filter">Filter to test.</param>
		/// <param name="messages">The message set injected into the collection the filter works on.</param>
		/// <param name="filterIsAttached">
		/// <c>true</c> if the filter has been attached to a collection;
		/// <c>false</c> if the filter was created, but not attached to a collection.
		/// </param>
		protected static void TestInitialFilterSettings(
			ISelectableLogMessageFilter<LogMessage> filter,
			LogMessage[]                            messages,
			bool                                    filterIsAttached = true)
		{
			// determine the oldest/newest message
			var oldestMessage = messages.Length > 0 ? messages[0] : null;
			var newestMessage = messages.Length > 0 ? messages[messages.Length - 1] : null;

			// Enabled
			Assert.True(filter.Enabled);

			// Timestamp Filter
			Assert.False(filter.TimestampFilter.Enabled);
			var zeroTimestamp = new DateTimeOffset(0, TimeSpan.Zero);
			if (oldestMessage != null)
			{
				Debug.Assert(newestMessage != null, nameof(newestMessage) + " != null");
				Assert.Equal(oldestMessage.Timestamp, filter.TimestampFilter.MinTimestamp);
				Assert.Equal(newestMessage.Timestamp, filter.TimestampFilter.MaxTimestamp);
			}
			else
			{
				// no messages in the collection
				// => the timestamp filter's min/max timestamp defaults to the current date/time
				Assert.Equal(zeroTimestamp, filter.TimestampFilter.MinTimestamp);
				Assert.Equal(zeroTimestamp, filter.TimestampFilter.MaxTimestamp);
			}

			// the timestamp filter defaults to the current date/time
			Assert.Equal(zeroTimestamp, filter.TimestampFilter.From);
			Assert.Equal(zeroTimestamp, filter.TimestampFilter.To);

			// Application Name Filter
			var expectedApplicationNames = messages.Select(x => x.ApplicationName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
			Assert.False(filter.ApplicationNameFilter.Enabled);
			Assert.False(filter.ApplicationNameFilter.AccumulateItems);
			Assert.Equal(expectedApplicationNames, filter.ApplicationNameFilter.Items.Select(x => x.Value).ToList());
			Assert.All(
				filter.ApplicationNameFilter.Items,
				x =>
				{
					Assert.False(x.Selected);
					Assert.Null(x.Group);
				});

			// Log Level Filter
			var logLevelsInFile = messages.Select(x => x.LogLevelName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
			var expectedLogLevels = new List<string>();
			if (filterIsAttached) expectedLogLevels.AddRange(LogLevel.PredefinedLogLevels.Select(x => x.Name));
			expectedLogLevels.AddRange(logLevelsInFile);
			Assert.False(filter.LogLevelFilter.Enabled);
			Assert.False(filter.LogLevelFilter.AccumulateItems);
			Assert.Equal(expectedLogLevels, filter.LogLevelFilter.Items.Select(x => x.Value).ToList());
			Assert.All(
				filter.LogLevelFilter.Items,
				x =>
				{
					Assert.False(x.Selected);
					Assert.Equal(messages.Any(y => y.LogLevelName == x.Value), x.ValueUsed);
					Assert.Equal(LogLevel.PredefinedLogLevels.Select(y => y.Name).Contains(x.Value) ? "Predefined" : "Aspects", x.Group);
				});

			// Log Writer Filter
			var expectedLogWriters = messages.Select(x => x.LogWriterName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
			Assert.False(filter.LogWriterFilter.Enabled);
			Assert.False(filter.LogWriterFilter.AccumulateItems);
			Assert.Equal(expectedLogWriters, filter.LogWriterFilter.Items.Select(x => x.Value).ToList());
			Assert.All(
				filter.LogWriterFilter.Items,
				x =>
				{
					Assert.False(x.Selected);
					Assert.Null(x.Group);
				});

			// Process Name Filter
			var expectedProcessNames = messages.Select(x => x.ProcessName).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
			Assert.False(filter.ProcessNameFilter.Enabled);
			Assert.False(filter.ProcessNameFilter.AccumulateItems);
			Assert.Equal(expectedProcessNames, filter.ProcessNameFilter.Items.Select(x => x.Value).ToList());
			Assert.All(
				filter.ProcessNameFilter.Items,
				x =>
				{
					Assert.False(x.Selected);
					Assert.Null(x.Group);
				});

			// Process Id Filter
			var expectedProcessIds = messages.Select(x => x.ProcessId).OrderBy(x => x).Distinct().ToList();
			Assert.False(filter.ProcessIdFilter.Enabled);
			Assert.False(filter.ProcessIdFilter.AccumulateItems);
			Assert.Equal(expectedProcessIds, filter.ProcessIdFilter.Items.Select(x => x.Value).ToList());
			Assert.All(
				filter.ProcessIdFilter.Items,
				x =>
				{
					Assert.False(x.Selected);
					Assert.Null(x.Group);
				});

			// Tag Filter
			var expectedTagNames = messages.SelectMany(x => x.Tags).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Distinct().ToList();
			Assert.False(filter.TagFilter.Enabled);
			Assert.False(filter.TagFilter.AccumulateItems);
			Assert.Equal(expectedTagNames, filter.TagFilter.Items.Select(x => x.Value).ToList());
			Assert.All(
				filter.TagFilter.Items,
				x =>
				{
					Assert.False(x.Selected);
					Assert.Null(x.Group);
				});

			// Text Filter
			Assert.False(filter.TextFilter.Enabled);
			Assert.False(filter.TextFilter.IsCaseSensitive);
			Assert.Empty(filter.TextFilter.SearchText);
		}

		#endregion
	}

}
