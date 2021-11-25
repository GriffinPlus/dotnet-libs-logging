///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;

// ReSharper disable PossibleMultipleEnumeration

namespace GriffinPlus.Lib.Logging.Collections
{

	partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection>
	{
		/// <summary>
		/// Base class for more specific filters filtering for names.
		/// </summary>
		public partial class ItemFilter<T> : FilterBase, ISelectableLogMessageFilter_ItemFilter<T>
			where T : IEquatable<T>
		{
			private readonly List<ISelectableLogMessageFilter_ItemInternal<T>>          mStaticItems;
			private readonly List<ISelectableLogMessageFilter_ItemInternal<T>>          mSortedItems;
			private readonly ObservableCollection<ISelectableLogMessageFilter_Item<T>>  mCombinedItems;
			private readonly Dictionary<T, ISelectableLogMessageFilter_ItemInternal<T>> mAllItemsByValue;
			private readonly HashSet<T>                                                 mEnabledValues;
			private readonly string                                                     mDefaultGroup;
			private readonly ItemValueComparer                                          mComparer;
			private          ReadOnlyObservableCollection<T>                            mOverviewCollection;
			private          bool                                                       mAccumulateItems;
			private          bool                                                       mDisableFilterOnReset;
			private          bool                                                       mUnselectItemsOnReset;

			/// <summary>
			/// Initializes a new instance of the <see cref="ItemFilter{T}"/> class.
			/// </summary>
			/// <param name="parent">The <see cref="SelectableLogMessageFilterBase{TMessage,TUnfilteredCollection}"/> the filter belongs to.</param>
			/// <param name="defaultGroup">Default group to use when no specific group is specified.</param>
			/// <param name="comparer">Comparer to use for sorting items.</param>
			internal ItemFilter(SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection> parent, string defaultGroup, IComparer<T> comparer) : base(parent)
			{
				mStaticItems = new List<ISelectableLogMessageFilter_ItemInternal<T>>();
				mSortedItems = new List<ISelectableLogMessageFilter_ItemInternal<T>>();
				mCombinedItems = new ObservableCollection<ISelectableLogMessageFilter_Item<T>>();
				mAllItemsByValue = new Dictionary<T, ISelectableLogMessageFilter_ItemInternal<T>>();
				Items = new ReadOnlyObservableCollection<ISelectableLogMessageFilter_Item<T>>(mCombinedItems);
				mEnabledValues = new HashSet<T>();
				mDefaultGroup = defaultGroup;
				mComparer = new ItemValueComparer(comparer);
			}

			/// <summary>
			/// Gets items of the filtered field that occur in the log message collection.
			/// </summary>
			public ReadOnlyObservableCollection<ISelectableLogMessageFilter_Item<T>> Items { get; }

			/// <summary>
			/// Gets or sets a value indicating whether <see cref="Items"/> contains items only that belong to at
			/// least one message in the collection (<c>false</c>false) or whether all items are kept even when log
			/// messages are removed from the collection (<c>true</c>). Default is <c>false</c>.
			/// </summary>
			public bool AccumulateItems
			{
				get => mAccumulateItems;
				set
				{
					if (mAccumulateItems != value)
					{
						mAccumulateItems = value;
						OnPropertyChanged();

						// if accumulating was disabled, rebuild filter item collection to remove orphaned entries
						if (!mAccumulateItems)
							RebuildItems();
					}
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether the filter is disabled when it is reset.
			/// Default is <c>false</c>.
			/// </summary>
			public bool DisableFilterOnReset
			{
				get => mDisableFilterOnReset;
				set
				{
					if (mDisableFilterOnReset != value)
					{
						mDisableFilterOnReset = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether items are unselected when the filter is reset.
			/// Default is <c>false</c>.
			/// </summary>
			public bool UnselectItemsOnReset
			{
				get => mUnselectItemsOnReset;
				set
				{
					if (mUnselectItemsOnReset != value)
					{
						mUnselectItemsOnReset = value;
						OnPropertyChanged();
					}
				}
			}

			/// <summary>
			/// Binds the filter to the specified overview collection of the unfiltered message collection.
			/// </summary>
			internal void BindToOverviewCollection(ReadOnlyObservableCollection<T> overview)
			{
				if (mOverviewCollection != null)
				{
					(mOverviewCollection as INotifyCollectionChanged).CollectionChanged -= OnOverviewCollectionChanged;
				}

				mOverviewCollection = overview;

				if (mOverviewCollection != null)
				{
					(mOverviewCollection as INotifyCollectionChanged).CollectionChanged += OnOverviewCollectionChanged;
				}
			}

			/// <summary>
			/// Adds the specified value to the list of selectable items (see <see cref="Items"/>).
			/// The item is added to the end, if it is not in the filter, yet.
			/// </summary>
			/// <param name="value">Value to add.</param>
			/// <param name="group">Name of the group, the item belongs to.</param>
			internal void AddItem(T value, string group)
			{
				if (!mAllItemsByValue.TryGetValue(value, out var item))
				{
					item = new Item(group, value, false);
					item.ValueUsed = true;
					int index = mSortedItems.BinarySearch(item, mComparer);
					Debug.Assert(index < 0);
					item.PropertyChanged += ItemPropertyChanged;
					mSortedItems.Insert(~index, item);
					mCombinedItems.Insert(mStaticItems.Count + ~index, item);
					mAllItemsByValue.Add(value, item);
					// if (item.Selected) mEnabledValues.Add(item.Value); // new items are always disabled
				}
				else
				{
					item.ValueUsed = true;
				}
			}

			/// <summary>
			/// Adds the specified values to the list of selectable items (see <see cref="Items"/>).
			/// Items that are not in the filter set yet are added to the end.
			/// </summary>
			/// <param name="values">Values to add.</param>
			/// <param name="group">Name of the group, the items belong to.</param>
			internal void AddItems(IEnumerable<T> values, string group)
			{
				foreach (var value in values)
				{
					if (!mAllItemsByValue.TryGetValue(value, out var item))
					{
						item = new Item(group, value, false);
						item.ValueUsed = true;
						int index = mSortedItems.BinarySearch(item, mComparer);
						Debug.Assert(index < 0);
						item.PropertyChanged += ItemPropertyChanged;
						mSortedItems.Insert(~index, item);
						mCombinedItems.Insert(mStaticItems.Count + ~index, item);
						mAllItemsByValue.Add(value, item);
						// if (item.Selected) mEnabledValues.Add(item.Value); // new items are always disabled
					}
					else
					{
						item.ValueUsed = true;
					}
				}
			}

			/// <summary>
			/// Adds the specified values to the static list of selectable items (see <see cref="Items"/>).
			/// Must be done at start when there are no sorted items, yet.
			/// </summary>
			/// <param name="values">Values to add.</param>
			/// <param name="group">Name of the group, the items belong to.</param>
			internal void AddStaticItems(IEnumerable<T> values, string group)
			{
				Debug.Assert(mSortedItems.Count == 0);

				foreach (var value in values)
				{
					var item = new Item(group, value, true);
					item.PropertyChanged += ItemPropertyChanged;
					mStaticItems.Add(item);
					mCombinedItems.Add(item);
					mAllItemsByValue.Add(value, item);
					// if (item.Selected) mEnabledValues.Add(item.Value); // new items are always disabled
				}
			}

			/// <summary>
			/// Removes the specified value from the list of selectable items (see <see cref="Items"/>).
			/// Static items cannot be removed.
			/// </summary>
			/// <param name="value">Item to remove.</param>
			internal void RemoveItem(T value)
			{
				if (mAllItemsByValue.TryGetValue(value, out var item))
				{
					if (!mAccumulateItems)
					{
						item.ValueUsed = false;
						item = new Item(null, value, false);
						int index = mSortedItems.BinarySearch(item, mComparer);
						if (index >= 0)
						{
							item = (Item)mSortedItems[index];
							mSortedItems[index].PropertyChanged -= ItemPropertyChanged;
							mSortedItems.RemoveAt(index);
							mCombinedItems.RemoveAt(mStaticItems.Count + index);
							Debug.Assert(!item.IsStatic);
							mAllItemsByValue.Remove(item.Value);
							mEnabledValues.Remove(item.Value);
						}
					}
				}
			}

			/// <summary>
			/// Determines whether the specified item passes the filter criteria.
			/// </summary>
			/// <param name="value">Item to check.</param>
			/// <returns>
			/// <c>true</c> if the item passes the filter;
			/// otherwise <c>false</c>.
			/// </returns>
			internal bool Matches(T value)
			{
				return mEnabledValues.Contains(value);
			}

			/// <summary>
			/// Resets the filter (keeps items, if <see cref="AccumulateItems"/> is <c>true</c>).
			/// </summary>
			protected internal override void Reset()
			{
				SuspendPropertyChanged();
				try
				{
					if (mDisableFilterOnReset)
						Enabled = false;

					if (mAccumulateItems)
					{
						// filter should accumulate items
						// => unselect all items, if requested, but do not remove anything
						foreach (var item in mAllItemsByValue.Values)
						{
							if (mUnselectItemsOnReset) item.Selected = false;
						}
					}
					else
					{
						// filter should not accumulate items
						// => unselect static items only...
						foreach (var item in mStaticItems)
						{
							if (mUnselectItemsOnReset) item.Selected = false;
							item.ValueUsed = false;
						}

						// ... and remove other items
						for (int i = mSortedItems.Count - 1; i >= 0; i--)
						{
							var item = mSortedItems[i];
							item.PropertyChanged -= ItemPropertyChanged;
							mCombinedItems.RemoveAt(mStaticItems.Count + i);
							mSortedItems.RemoveAt(i);
							mAllItemsByValue.Remove(item.Value);
						}
					}

					// update enabled state
					mEnabledValues.Clear();
					foreach (var item in mAllItemsByValue.Values)
					{
						if (item.Selected)
							mEnabledValues.Add(item.Value);
					}
				}
				finally
				{
					ResumePropertyChanged();
				}
			}

			/// <summary>
			/// Is called when the overview collection changes.
			/// </summary>
			/// <param name="sender">The overview collection that has changed.</param>
			/// <param name="e">Event arguments indicating what has changed.</param>
			private void OnOverviewCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
			{
				if (e.Action == NotifyCollectionChangedAction.Add)
				{
					foreach (T item in e.NewItems)
					{
						AddItem(item, mDefaultGroup);
					}
				}
				else
				{
					// remove all items that are not in the new set
					var set = new HashSet<T>(mOverviewCollection);
					foreach (var item in mCombinedItems.ToArray())
					{
						var value = item.Value;
						if (!set.Contains(value))
							RemoveItem(value);
					}

					// add items that are not in the current set, yet
					foreach (var value in mOverviewCollection)
					{
						AddItem(value, mDefaultGroup);
					}
				}
			}

			/// <summary>
			/// Is called when one of the <see cref="Item"/> properties changes.
			/// </summary>
			/// <param name="sender">The <see cref="Item"/> itself.</param>
			/// <param name="e">Event arguments.</param>
			private void ItemPropertyChanged(object sender, PropertyChangedEventArgs e)
			{
				var item = (Item)sender;

				if (e.PropertyName == nameof(Item.Selected))
				{
					// update the set of enabled item values
					if (item.Selected) mEnabledValues.Add(item.Value);
					else mEnabledValues.Remove(item.Value);
					Parent.OnFilterChanged(Enabled);
				}
			}

			/// <summary>
			/// Rebuilds the collection backing the <see cref="Items"/> property.
			/// </summary>
			private void RebuildItems()
			{
				// capture current filter item set
				var itemByValue = new Dictionary<T, ISelectableLogMessageFilter_ItemInternal<T>>();
				foreach (var item in mCombinedItems) itemByValue[item.Value] = (ISelectableLogMessageFilter_ItemInternal<T>)item;

				// clear item set
				mSortedItems.ForEach(x => x.PropertyChanged -= ItemPropertyChanged);
				mCombinedItems.Clear();
				mSortedItems.Clear();
				mEnabledValues.Clear();
				mAllItemsByValue.Clear();

				// add static items
				foreach (var item in mStaticItems)
				{
					mCombinedItems.Add(item);
					if (item.Selected) mEnabledValues.Add(item.Value);
					item.ValueUsed = false;
					mAllItemsByValue.Add(item.Value, item);
				}

				// add sorted items
				if (mOverviewCollection != null)
				{
					foreach (var value in mOverviewCollection)
					{
						if (!mAllItemsByValue.TryGetValue(value, out var item))
						{
							item = itemByValue.TryGetValue(value, out var oldItem) ? oldItem : new Item(mDefaultGroup, value, false);
							int index = mSortedItems.BinarySearch(item, mComparer);
							Debug.Assert(index < 0);
							item.PropertyChanged += ItemPropertyChanged;
							mSortedItems.Insert(~index, item);
							mCombinedItems.Insert(mStaticItems.Count + ~index, item);
							if (item.Selected) mEnabledValues.Add(item.Value);
							mAllItemsByValue.Add(item.Value, item);
						}

						item.ValueUsed = true;
					}
				}
			}
		}
	}

}
