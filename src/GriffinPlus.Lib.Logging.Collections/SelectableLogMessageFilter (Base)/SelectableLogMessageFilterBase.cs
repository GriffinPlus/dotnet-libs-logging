///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Base class for log message filters that provide collections of fields that can be selected to be filtered.
	/// This type of filter supports data-binding and is therefore a perfect fit for user interfaces that present a list
	/// of selectable items for log writers, levels, processes etc.
	/// </summary>
	public abstract partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection> :
		ISelectableLogMessageFilter<TMessage>,
		IDisposable
		where TMessage : class, ILogMessage
		where TUnfilteredCollection : LogMessageCollectionBase<TMessage>
	{
		/// <summary>
		/// The filtered collection.
		/// </summary>
		protected TUnfilteredCollection Collection;

		/// <summary>
		/// Initializes a new instance of the <see cref="SelectableLogMessageFilter{TMessage}"/> class.
		/// </summary>
		protected SelectableLogMessageFilterBase()
		{
			TimestampFilter = new IntervalFilter(this);
			LogWriterFilter = new ItemFilter<string>(this, "", StringComparer.OrdinalIgnoreCase);
			LogLevelFilter = new ItemFilter<string>(this, AspectLogLevelGroup, StringComparer.OrdinalIgnoreCase);
			TagFilter = new ItemFilter<string>(this, "", StringComparer.OrdinalIgnoreCase);
			ApplicationNameFilter = new ItemFilter<string>(this, "", StringComparer.OrdinalIgnoreCase);
			ProcessNameFilter = new ItemFilter<string>(this, "", StringComparer.OrdinalIgnoreCase);
			ProcessIdFilter = new ItemFilter<int>(this, "", Comparer<int>.Default);
			TextFilter = new FulltextFilter(this);

			// populate the log level filter with predefined log levels that always remain in the filter (static items)
			LogLevelFilter.AddStaticItems(LogLevel.PredefinedLogLevels.Select(x => x.Name), PredefinedLogLevelGroup);
		}

		/// <summary>
		/// Disposes the filter detaching it from the collection and releasing unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		/// <summary>
		/// Disposes the filter detaching it from the collection and releasing unmanaged resources.
		/// </summary>
		/// <param name="disposing">
		/// <c>true</c>, if Dispose() was called intentionally;
		/// <c>false</c> if running as part of the finalization.
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			DetachFromCollection();
		}

		#region PropertyChanged Event

		private readonly HashSet<string> mChangedProperties = new HashSet<string>();
		private          int             mPropertyChangedSuspendedCounter;

		/// <summary>
		/// Occurs when one of the properties has changed.
		/// </summary>
		public event PropertyChangedEventHandler PropertyChanged;

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> event.
		/// </summary>
		/// <param name="propertyName">Name of the property that has changed.</param>
		protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			if (mPropertyChangedSuspendedCounter == 0)
			{
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
			}
			else
			{
				mChangedProperties.Add(propertyName);
			}
		}

		/// <summary>
		/// Suspends raising the <see cref="PropertyChanged"/> event.
		/// </summary>
		protected internal void SuspendPropertyChanged()
		{
			if (mPropertyChangedSuspendedCounter++ == 0)
			{
				Contract.Assert(
					mChangedProperties.Count == 0,
					"The list of suspended property changed notifications should be empty when starting suspension.");
			}
		}

		/// <summary>
		/// Resumes raising the <see cref="PropertyChanged"/> event firing the event for properties
		/// that have changed meanwhile.
		/// </summary>
		protected internal void ResumePropertyChanged()
		{
			Contract.Assert(
				mPropertyChangedSuspendedCounter > 0,
				"The suspension counter should always be greater than 0 when resuming.");

			if (--mPropertyChangedSuspendedCounter == 0)
			{
				try
				{
					if (mChangedProperties.Contains(null) || mChangedProperties.Contains(string.Empty))
					{
						// some operation invalidated all properties
						// => it is sufficient to notify this one only (more specific property changes are covered by it)
						PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
					}
					else
					{
						// operations have invalidated single properties only
						// => notify for each of them
						foreach (string propertyName in mChangedProperties)
						{
							try
							{
								PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
							}
							catch (Exception ex)
							{
								Debug.Fail("PropertyChanged handler threw an unhandled exception.", ex.ToString());
								throw;
							}
						}
					}
				}
				finally
				{
					mChangedProperties.Clear();
				}
			}
		}

		#endregion

		#region FilterChanged Event

		/// <summary>
		/// Occurs when the filter has changed.
		/// </summary>
		public event EventHandler<FilterChangedEventArgs> FilterChanged;

		/// <summary>
		/// Raises the <see cref="FilterChanged"/> event.
		/// </summary>
		/// <param name="changeEffectsFilterResult">
		/// <c>true</c> if the change to the filter may change the set of filtered messages;
		/// otherwise <c>false</c>.
		/// </param>
		protected virtual void OnFilterChanged(bool changeEffectsFilterResult)
		{
			FilterChanged?.Invoke(this, new FilterChangedEventArgs(changeEffectsFilterResult));
		}

		#endregion

		#region Enabled

		private bool mEnabled = true;

		/// <summary>
		/// Gets or sets a value indicating whether the filter is enabled.
		/// <c>true</c> applies the filter when matching log messages (default).
		/// <c>false</c> lets all log messages pass the filter when matching.
		/// </summary>
		public bool Enabled
		{
			get => mEnabled;
			set
			{
				if (mEnabled != value)
				{
					mEnabled = value;
					OnPropertyChanged();
					OnFilterChanged(true);
				}
			}
		}

		#endregion

		#region TimestampFilter

		/// <summary>
		/// Gets the timestamp filter.
		/// </summary>
		public IntervalFilter TimestampFilter { get; }

		/// <summary>
		/// Gets the timestamp filter.
		/// </summary>
		ISelectableLogMessageFilter_IntervalFilter ISelectableLogMessageFilter<TMessage>.TimestampFilter => TimestampFilter;

		#endregion

		#region LogWriterFilter

		/// <summary>
		/// Gets the log writer filter.
		/// </summary>
		public ItemFilter<string> LogWriterFilter { get; }

		/// <summary>
		/// Gets the log writer filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ISelectableLogMessageFilter<TMessage>.LogWriterFilter => LogWriterFilter;

		#endregion

		#region LogLevelFilter

		/// <summary>
		/// Gets the log level filter.
		/// </summary>
		public ItemFilter<string> LogLevelFilter { get; }

		/// <summary>
		/// Gets the log level filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ISelectableLogMessageFilter<TMessage>.LogLevelFilter => LogLevelFilter;

		#endregion

		#region TagFilter

		/// <summary>
		/// Gets the tag filter.
		/// </summary>
		public ItemFilter<string> TagFilter { get; }

		/// <summary>
		/// Gets the tag filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ISelectableLogMessageFilter<TMessage>.TagFilter => TagFilter;

		#endregion

		#region ApplicationNameFilter

		/// <summary>
		/// Gets the application name filter.
		/// </summary>
		public ItemFilter<string> ApplicationNameFilter { get; }

		/// <summary>
		/// Gets the application name filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ISelectableLogMessageFilter<TMessage>.ApplicationNameFilter => ApplicationNameFilter;

		#endregion

		#region ProcessNameFilter

		/// <summary>
		/// Gets the process name filter.
		/// </summary>
		public ItemFilter<string> ProcessNameFilter { get; }

		/// <summary>
		/// Gets the process name filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ISelectableLogMessageFilter<TMessage>.ProcessNameFilter => ProcessNameFilter;

		#endregion

		#region ProcessIdFilter

		/// <summary>
		/// Gets the process id filter.
		/// </summary>
		public ItemFilter<int> ProcessIdFilter { get; }

		/// <summary>
		/// Gets the process id filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<int> ISelectableLogMessageFilter<TMessage>.ProcessIdFilter => ProcessIdFilter;

		#endregion

		#region TextFilter

		/// <summary>
		/// Gets the message text filter.
		/// </summary>
		public FulltextFilter TextFilter { get; }

		/// <summary>
		/// Gets the message text filter.
		/// </summary>
		ISelectableLogMessageFilter_FulltextFilter ISelectableLogMessageFilter<TMessage>.TextFilter => TextFilter;

		#endregion

		#region Implementation of ILogMessageCollectionFilter<TMessage>

		private const string PredefinedLogLevelGroup = "Predefined";
		private const string AspectLogLevelGroup     = "Aspects";

		/// <summary>
		/// Attaches the filter to the specified collection.
		/// </summary>
		/// <param name="collection">Collection to attach.</param>
		/// <exception cref="ArgumentNullException">The specified collection is <c>null</c>.</exception>
		public void AttachToCollection(ILogMessageCollection<TMessage> collection)
		{
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));

			if (!(collection is TUnfilteredCollection))
				throw new ArgumentException($"The collection must be of type '{typeof(TUnfilteredCollection)}'");

			// detach old collection, if necessary
			if (Collection != null)
				DetachFromCollection();

			// reset all filters to defaults
			Reset();

			// set new collection
			Collection = (TUnfilteredCollection)collection;

			// register for changes to the unfiltered collection
			Collection.CollectionChanged += OnUnfilteredCollectionChanged;

			// bind specific filters to overview collections
			LogWriterFilter.BindToOverviewCollection(Collection.UsedLogWriters);
			LogLevelFilter.BindToOverviewCollection(Collection.UsedLogLevels);
			TagFilter.BindToOverviewCollection(Collection.UsedTags);
			ApplicationNameFilter.BindToOverviewCollection(Collection.UsedApplicationNames);
			ProcessNameFilter.BindToOverviewCollection(Collection.UsedProcessNames);
			ProcessIdFilter.BindToOverviewCollection(Collection.UsedProcessIds);

			// initialize the timestamp filter, if there are messages in the collection
			// (if no messages are in the collection, the filter will update itself as soon as messages are added)
			if (Collection.Count > 0)
				TimestampFilter.SetMinMax(Collection[0].Timestamp, Collection[Collection.Count - 1].Timestamp);

			// populate remaining filters
			LogWriterFilter.AddItems(Collection.UsedLogWriters, null);
			LogLevelFilter.AddItems(Collection.UsedLogLevels, AspectLogLevelGroup);
			TagFilter.AddItems(Collection.UsedTags, null);
			ApplicationNameFilter.AddItems(Collection.UsedApplicationNames, null);
			ProcessNameFilter.AddItems(Collection.UsedProcessNames, null);
			ProcessIdFilter.AddItems(Collection.UsedProcessIds, null);

			// let derived class do its work
			OnAttachToCollection();
		}

		/// <summary>
		/// Is called after the filter has been attached to the collection.
		/// </summary>
		protected virtual void OnAttachToCollection()
		{
		}

		/// <summary>
		/// Detaches the filter from its collection.
		/// </summary>
		public void DetachFromCollection()
		{
			if (Collection != null)
			{
				// let base class do its work
				OnDetachFromCollection();

				// register from changes to the unfiltered collection
				Collection.CollectionChanged -= OnUnfilteredCollectionChanged;

				// unbind specific filters from overview collections
				LogWriterFilter.BindToOverviewCollection(null);
				LogLevelFilter.BindToOverviewCollection(null);
				TagFilter.BindToOverviewCollection(null);
				ApplicationNameFilter.BindToOverviewCollection(null);
				ProcessNameFilter.BindToOverviewCollection(null);
				ProcessIdFilter.BindToOverviewCollection(null);

				// remove collection association
				Collection = null;
			}
		}

		/// <summary>
		/// Is called before the filter is detached from the collection.
		/// </summary>
		protected virtual void OnDetachFromCollection()
		{
		}

		/// <summary>
		/// Determines whether the specified log message matches the filter criteria.
		/// </summary>
		/// <param name="message">Message to check.</param>
		/// <returns>
		/// <c>true</c> if the specified message matches the filter criteria;
		/// otherwise <c>false</c>.
		/// </returns>
		/// <exception cref="ArgumentNullException">The specified message is <c>null</c>.</exception>
		public bool Matches(TMessage message)
		{
			if (message == null) throw new ArgumentNullException(nameof(message));
			if (!mEnabled) return true;
			if (TimestampFilter.Enabled && !TimestampFilter.Matches(message.Timestamp)) return false;
			if (LogWriterFilter.Enabled && !LogWriterFilter.Matches(message.LogWriterName)) return false;
			if (LogLevelFilter.Enabled && !LogLevelFilter.Matches(message.LogLevelName)) return false;
			if (ApplicationNameFilter.Enabled && !ApplicationNameFilter.Matches(message.ApplicationName)) return false;
			if (ProcessNameFilter.Enabled && !ProcessNameFilter.Matches(message.ProcessName)) return false;
			if (ProcessIdFilter.Enabled && !ProcessIdFilter.Matches(message.ProcessId)) return false;

			// match tags (at least one tag must be enabled)
			if (TagFilter.Enabled)
			{
				// abort, if no tag has matched
				if (!message.Tags.Any(tag => TagFilter.Matches(tag)))
					return false;
			}

			// match message text (most expensive operation)
			if (TextFilter.Enabled && !TextFilter.Matches(message.Text))
				return false;

			// message passed all filters
			return true;
		}

		/// <summary>
		/// Resets all filters to defaults and disables them.
		/// </summary>
		protected virtual void Reset()
		{
			TimestampFilter.Reset();
			LogWriterFilter.Reset();
			LogLevelFilter.Reset();
			TagFilter.Reset();
			ApplicationNameFilter.Reset();
			ProcessNameFilter.Reset();
			ProcessIdFilter.Reset();
			TextFilter.Reset();
		}

		/// <summary>
		/// Is called when the unfiltered log message collection changes.
		/// </summary>
		/// <param name="sender">The unfiltered collection.</param>
		/// <param name="e">Event arguments indicating what has changed.</param>
		private void OnUnfilteredCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action == NotifyCollectionChangedAction.Add)
			{
				// messages in the collection are always added to the end, inserting is not supported
				// => timestamps should be increase monotonically
				foreach (LogMessage message in e.NewItems)
				{
					TimestampFilter.UpdateOnAdd(message.Timestamp);
				}
			}
			else
			{
				// any other change to the collection
				// => update the minimum/maximum timestamps
				if (Collection.Count > 0)
				{
					TimestampFilter.SetMinMax(
						Collection[0].Timestamp,
						Collection[Collection.Count - 1].Timestamp);
				}
				else
				{
					var timestamp = new DateTimeOffset(0, TimeSpan.Zero);
					TimestampFilter.SetMinMax(timestamp, timestamp);
				}
			}
		}

		#endregion
	}

}
