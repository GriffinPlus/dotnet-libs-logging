///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A test helper class that registers the <see cref="INotifyPropertyChanged.PropertyChanged" /> event and the
	/// <see cref="INotifyCollectionChanged.CollectionChanged" /> event and helps to check whether these events are
	/// raised as expected.
	/// </summary>
	public class LogMessageCollectionEventWatcher : IDisposable
	{
		#region PropertyChangedEventArgsEqualityComparer

		/// <summary>
		/// Equality comparer for the <see cref="PropertyChangedEventArgs" /> class.
		/// </summary>
		private class PropertyChangedEventArgsEqualityComparer : IEqualityComparer<PropertyChangedEventArgs>
		{
			public static PropertyChangedEventArgsEqualityComparer Instance { get; } = new PropertyChangedEventArgsEqualityComparer();

			public bool Equals(PropertyChangedEventArgs x, PropertyChangedEventArgs y)
			{
				if (ReferenceEquals(x, y))
					return true;

				if (ReferenceEquals(x, null))
					return false;

				if (ReferenceEquals(y, null))
					return false;

				if (x.GetType() != y.GetType())
					return false;

				return x.PropertyName == y.PropertyName;
			}

			public int GetHashCode(PropertyChangedEventArgs obj)
			{
				return obj.PropertyName != null ? obj.PropertyName.GetHashCode() : 0;
			}
		}

		#endregion

		#region NotifyCollectionChangedEventArgsEqualityComparer

		/// <summary>
		/// Equality comparer for the <see cref="NotifyCollectionChangedEventArgs" /> class.
		/// </summary>
		private class NotifyCollectionChangedEventArgsEqualityComparer : IEqualityComparer<NotifyCollectionChangedEventArgs>
		{
			public static NotifyCollectionChangedEventArgsEqualityComparer Instance { get; } = new NotifyCollectionChangedEventArgsEqualityComparer();

			public bool Equals(NotifyCollectionChangedEventArgs x, NotifyCollectionChangedEventArgs y)
			{
				if (ReferenceEquals(x, y))
					return true;

				if (ReferenceEquals(x, null))
					return false;

				if (ReferenceEquals(y, null))
					return false;

				if (x.GetType() != y.GetType())
					return false;

				if (x.Action != y.Action || x.NewStartingIndex != y.NewStartingIndex || x.OldStartingIndex != y.OldStartingIndex)
					return false;

				if (x.OldItems == null && y.OldItems != null) return false;
				if (x.OldItems != null && y.OldItems == null) return false;
				if (x.OldItems != null && y.OldItems != null)
				{
					if (x.OldItems.Count != y.OldItems.Count)
						return false;

					foreach (var xElement in x.OldItems)
					foreach (var yElement in y.OldItems)
					{
						if (!xElement.Equals(yElement))
							return false;
					}
				}

				if (x.NewItems == null && y.NewItems != null) return false;
				if (x.NewItems != null && y.NewItems == null) return false;
				if (x.NewItems != null && y.NewItems != null)
				{
					if (x.NewItems.Count != y.NewItems.Count)
						return false;

					foreach (var xElement in x.NewItems)
					foreach (var yElement in y.NewItems)
					{
						if (!xElement.Equals(yElement))
							return false;
					}
				}

				return true;
			}

			public int GetHashCode(NotifyCollectionChangedEventArgs obj)
			{
				unchecked
				{
					int hashCode = (int)obj.Action;
					hashCode = (hashCode * 397) ^ obj.NewStartingIndex;
					hashCode = (hashCode * 397) ^ obj.OldStartingIndex;

					if (obj.OldItems != null)
					{
						foreach (var item in obj.OldItems)
						{
							hashCode = (hashCode * 397) ^ (item != null ? item.GetHashCode() : 0);
						}
					}
					else
					{
						hashCode *= 397;
					}

					if (obj.NewItems != null)
					{
						foreach (var item in obj.NewItems)
						{
							hashCode = (hashCode * 397) ^ (item != null ? item.GetHashCode() : 0);
						}
					}
					else
					{
						hashCode *= 397;
					}

					return hashCode;
				}
			}
		}

		#endregion

		private readonly ILogMessageCollection<LogMessage> mCollection;
		private readonly List<Tuple<string, EventArgs>>    mWatchedEventInvocations  = new List<Tuple<string, EventArgs>>();
		private readonly List<Tuple<string, EventArgs>>    mExpectedEventInvocations = new List<Tuple<string, EventArgs>>();

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageCollectionEventWatcher" /> class.
		/// </summary>
		/// <param name="collection">Collection to watch.</param>
		public LogMessageCollectionEventWatcher(ILogMessageCollection<LogMessage> collection)
		{
			mCollection = collection;
			mCollection.PropertyChanged += HandlePropertyChanged;
			mCollection.CollectionChanged += HandleCollectionChanged;
		}

		/// <summary>
		/// Disposes the watcher unregistering watched events.
		/// </summary>
		public void Dispose()
		{
			mCollection.PropertyChanged -= HandlePropertyChanged;
			mCollection.CollectionChanged -= HandleCollectionChanged;
		}

		/// <summary>
		/// Adds an expected invocation of the <see cref="INotifyPropertyChanged.PropertyChanged" /> event handler.
		/// </summary>
		/// <param name="e">Expected event arguments.</param>
		public void ExpectPropertyChanged(PropertyChangedEventArgs e)
		{
			mExpectedEventInvocations.Add(new Tuple<string, EventArgs>(nameof(INotifyPropertyChanged.PropertyChanged), e));
		}

		/// <summary>
		/// Adds an expected invocation of the <see cref="INotifyCollectionChanged.CollectionChanged" /> event handler.
		/// </summary>
		/// <param name="e">Expected event arguments.</param>
		public void ExpectCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			mExpectedEventInvocations.Add(new Tuple<string, EventArgs>(nameof(INotifyCollectionChanged.CollectionChanged), e));
		}

		/// <summary>
		/// Checks whether the observed event activities match the expected behavior.
		/// Uses XUnit assertions to check.
		/// </summary>
		public void CheckInvocations()
		{
			Assert.Equal(mExpectedEventInvocations.Count, mWatchedEventInvocations.Count);

			using (var expectedEnumerator = mExpectedEventInvocations.GetEnumerator())
			using (var watchedEnumerator = mWatchedEventInvocations.GetEnumerator())
			{
				while (expectedEnumerator.MoveNext() && watchedEnumerator.MoveNext())
				{
					var expected = expectedEnumerator.Current;
					var watched = watchedEnumerator.Current;
					Assert.NotNull(expected);
					Assert.NotNull(watched);

					Assert.Equal(expected.Item1, watched.Item1);

					switch (expected.Item1)
					{
						case nameof(INotifyPropertyChanged.PropertyChanged):
							Assert.Equal(
								(PropertyChangedEventArgs)expected.Item2,
								(PropertyChangedEventArgs)watched.Item2,
								PropertyChangedEventArgsEqualityComparer.Instance);
							break;

						case nameof(INotifyCollectionChanged.CollectionChanged):
							Assert.Equal(
								(NotifyCollectionChangedEventArgs)expected.Item2,
								(NotifyCollectionChangedEventArgs)watched.Item2,
								NotifyCollectionChangedEventArgsEqualityComparer.Instance);
							break;
					}
				}
			}
		}

		private void HandlePropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			Assert.Same(mCollection, sender); // the sender should always be the collection itself
			mWatchedEventInvocations.Add(new Tuple<string, EventArgs>(nameof(INotifyPropertyChanged.PropertyChanged), e));
		}

		private void HandleCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
		{
			Assert.Same(mCollection, sender); // the sender should always be the collection itself
			mWatchedEventInvocations.Add(new Tuple<string, EventArgs>(nameof(INotifyCollectionChanged.CollectionChanged), e));
		}
	}

}
