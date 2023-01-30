///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Collections
{

	partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection>
	{
		/// <summary>
		/// A filter selecting log messages with timestamps in a specific interval.
		/// </summary>
		public sealed class IntervalFilter : FilterBase, ISelectableLogMessageFilter_IntervalFilter
		{
			private DateTimeOffset mMinTimestamp;
			private DateTimeOffset mMaxTimestamp;
			private DateTimeOffset mFrom;
			private DateTimeOffset mTo;
			private bool           mInitialized;
			private bool           mIsFilterIntervalUpdatedOnNewMessages;

			/// <summary>
			/// Initializes a new instance of the <see cref="IntervalFilter"/> class.
			/// </summary>
			/// <param name="parent">The <see cref="SelectableLogMessageFilterBase{TMessage,TUnfilteredCollection}"/> the filter belongs to.</param>
			internal IntervalFilter(SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection> parent) : base(parent)
			{
				Reset();
			}

			/// <summary>
			/// Gets the timestamp of the oldest message in the collection.
			/// </summary>
			public DateTimeOffset MinTimestamp
			{
				get => mMinTimestamp;
				set
				{
					if (mMinTimestamp != value)
					{
						mMinTimestamp = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets the timestamp of the newest message in the collection.
			/// </summary>
			public DateTimeOffset MaxTimestamp
			{
				get => mMaxTimestamp;
				set
				{
					if (mMaxTimestamp != value)
					{
						mMaxTimestamp = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets or sets the lower limit of the timestamp interval to select.
			/// </summary>
			public DateTimeOffset From
			{
				get => mFrom;
				set
				{
					if (mFrom != value)
					{
						mFrom = value;
						mIsFilterIntervalUpdatedOnNewMessages = false;
						OnPropertyChanged();
						Parent.OnFilterChanged(Enabled);
					}
				}
			}

			/// <summary>
			/// Gets or sets the upper limit of the timestamp interval to select.
			/// </summary>
			public DateTimeOffset To
			{
				get => mTo;
				set
				{
					if (mTo != value)
					{
						mTo = value;
						mIsFilterIntervalUpdatedOnNewMessages = false;
						OnPropertyChanged();
						Parent.OnFilterChanged(Enabled);
					}
				}
			}

			/// <summary>
			/// Determines whether the specified timestamp passes the filter criteria.
			/// </summary>
			/// <param name="timestamp">Item to check.</param>
			/// <returns>
			/// <c>true</c> if the item passes the filter;<br/>
			/// otherwise <c>false</c>.
			/// </returns>
			internal bool Matches(DateTimeOffset timestamp)
			{
				return timestamp >= mFrom && timestamp <= mTo;
			}

			/// <summary>
			/// Resets the filter.
			/// </summary>
			protected internal override void Reset()
			{
				SuspendPropertyChanged();
				try
				{
					base.Reset();

					TUnfilteredCollection collection = Parent.Collection;
					if (collection != null && collection.Count > 0)
					{
						mMinTimestamp = collection[0].Timestamp;
						mMaxTimestamp = collection[collection.Count - 1].Timestamp;
					}
					else
					{
						mMinTimestamp = mMaxTimestamp = new DateTimeOffset(0, TimeSpan.Zero);
					}

					mFrom = mMinTimestamp;
					mTo = mMaxTimestamp;
					mInitialized = false;
					mIsFilterIntervalUpdatedOnNewMessages = true;

					OnPropertyChanged(null); // unspecific change
				}
				finally
				{
					ResumePropertyChanged();
				}
			}

			/// <summary>
			/// Sets the minimum/maximum timestamp of log messages in the collection.
			/// </summary>
			/// <param name="min">The least timestamp in the collection.</param>
			/// <param name="max">The greatest timestamp in the collection.</param>
			internal void SetMinMax(DateTimeOffset min, DateTimeOffset max)
			{
				SuspendPropertyChanged();
				try
				{
					MinTimestamp = min;
					MaxTimestamp = max;
				}
				finally
				{
					ResumePropertyChanged();
				}
			}

			/// <summary>
			/// Updates <see cref="MinTimestamp"/> and <see cref="MaxTimestamp"/> and adjusts <see cref="From"/> and <see cref="To"/>,
			/// if these properties have not been set explicitly.
			/// </summary>
			/// <param name="timestamp">Timestamp of the added log message.</param>
			internal void UpdateOnAdd(DateTimeOffset timestamp)
			{
				if (mInitialized)
				{
					// there is already at least one message in the collection
					// => update filter data

					if (timestamp < mMinTimestamp) MinTimestamp = timestamp;
					if (timestamp > mMaxTimestamp) MaxTimestamp = timestamp;

					if (mIsFilterIntervalUpdatedOnNewMessages)
					{
						From = mMinTimestamp;
						To = mMaxTimestamp;
					}

					return;
				}

				// this is the first message in the collection
				MinTimestamp = MaxTimestamp = timestamp;
				if (mIsFilterIntervalUpdatedOnNewMessages)
				{
					From = mMinTimestamp;
					To = mMaxTimestamp;
				}

				mInitialized = true;
			}
		}
	}

}
