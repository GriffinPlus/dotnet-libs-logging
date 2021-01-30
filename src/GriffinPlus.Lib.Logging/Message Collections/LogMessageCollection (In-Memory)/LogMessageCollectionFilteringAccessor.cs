///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// An accessor that provides direct access to a filtered log message set without creating a filtered collection.
	/// These accessors always work in cooperation with <see cref="LogMessageCollection{TMessage}"/>.
	/// </summary>
	/// <typeparam name="TMessage">The log message type.</typeparam>
	public class LogMessageCollectionFilteringAccessor<TMessage> : ILogMessageCollectionFilteringAccessor<TMessage>
		where TMessage : class, ILogMessage
	{
		private readonly LogMessageCollection<TMessage>        mCollection;
		private readonly ILogMessageCollectionFilter<TMessage> mFilter;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageCollectionFilteringAccessor{TMessage}"/> class.
		/// </summary>
		/// <param name="collection">The unfiltered collection the accessor should work on.</param>
		/// <param name="filter">
		/// The filter the accessor should apply (<c>null</c> to create an accessor without filtering capabilities).
		/// </param>
		internal LogMessageCollectionFilteringAccessor(
			LogMessageCollection<TMessage>        collection,
			ILogMessageCollectionFilter<TMessage> filter)
		{
			mCollection = collection ?? throw new ArgumentNullException(nameof(collection));
			mFilter = filter;
		}

		/// <summary>
		/// Disposes the accessor.
		/// </summary>
		public void Dispose()
		{
		}

		/// <summary>
		/// Gets the unfiltered collection the accessor works on.
		/// </summary>
		public LogMessageCollection<TMessage> Collection => mCollection;

		/// <summary>
		/// Gets the unfiltered collection the accessor works on.
		/// </summary>
		ILogMessageCollection<TMessage> ILogMessageCollectionFilteringAccessor<TMessage>.Collection => mCollection;

		/// <summary>
		/// Gets the filter the accessor works with.
		/// </summary>
		public ILogMessageCollectionFilter<TMessage> Filter => mFilter;

		/// <summary>
		/// Gets the filter the accessor works with.
		/// </summary>
		ILogMessageCollectionFilterBase<TMessage> ILogMessageCollectionFilteringAccessor<TMessage>.Filter => mFilter;

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified index in the unfiltered collection going backwards.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="matchIndex">Receives the index of the first log message matching the filter.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		public TMessage GetPreviousMessage(
			long     fromIndex,
			out long matchIndex)
		{
			var collection = mCollection.Messages;

			if (fromIndex < 0 || fromIndex >= collection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			for (int i = (int)fromIndex; i >= 0; i--)
			{
				var message = collection[i];
				if (mFilter == null || mFilter.Matches(message))
				{
					matchIndex = i;
					return message;
				}
			}

			matchIndex = -1;
			return null;
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the unfiltered collection going backwards.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
		/// <param name="reverse">
		/// <c>true</c> to reverse the list of returned messages, so the order of the messages is the same as in the collection;
		/// <c>false</c> to return the list of messages in the opposite order.
		/// </param>
		/// <returns>Log messages matching filter.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		public TMessage[] GetPreviousMessages(
			long       fromIndex,
			int        count,
			out long[] matchIndices,
			bool       reverse)
		{
			var collection = mCollection.Messages;

			if (fromIndex < 0 || fromIndex >= collection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(count),
					"The count must be positive.");
			}

			var matches = new List<TMessage>();
			var matchIndexList = new List<long>();

			for (int i = (int)fromIndex; i >= 0 && matches.Count < count; i--)
			{
				var message = collection[i];
				if (mFilter == null || mFilter.Matches(message))
				{
					matches.Add(message);
					matchIndexList.Add(i);
				}
			}

			// reverse lists, if requested
			if (reverse)
			{
				matches.Reverse();
				matchIndexList.Reverse();
			}

			matchIndices = matchIndexList.ToArray();
			return matches.ToArray();
		}

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified index in the unfiltered collection going forward.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="matchIndex">Receives the index of the first log message matching the filter.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		public TMessage GetNextMessage(
			long     fromIndex,
			out long matchIndex)
		{
			var collection = mCollection.Messages;

			if (fromIndex < 0 || fromIndex >= collection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			for (int i = (int)fromIndex; i < collection.Count; i++)
			{
				var message = collection[i];
				if (mFilter == null || mFilter.Matches(message))
				{
					matchIndex = i;
					return message;
				}
			}

			matchIndex = -1;
			return null;
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the unfiltered collection going forward.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
		/// <returns>Log messages matching filter.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		public TMessage[] GetNextMessages(
			long       fromIndex,
			int        count,
			out long[] matchIndices)
		{
			var collection = mCollection.Messages;

			if (fromIndex < 0 || fromIndex >= collection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(count),
					"The count must be positive.");
			}

			var matches = new List<TMessage>();
			var matchIndexList = new List<long>();

			for (int i = (int)fromIndex; i < collection.Count && matches.Count < count; i++)
			{
				var message = collection[i];
				if (mFilter == null || mFilter.Matches(message))
				{
					matches.Add(message);
					matchIndexList.Add(i);
				}
			}

			matchIndices = matchIndexList.ToArray();
			return matches.ToArray();
		}

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the unfiltered collection.
		/// </summary>
		/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
		/// <param name="toIndex">Index of the log message in the unfiltered collection to stop at.</param>
		/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
		/// <returns>Log messages matching filter.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="fromIndex"/> or <paramref name="toIndex"/> exceeds the bounds of the unfiltered
		/// collection.
		/// </exception>
		public TMessage[] GetMessageRange(
			long       fromIndex,
			long       toIndex,
			out long[] matchIndices)
		{
			var collection = mCollection.Messages;

			if (fromIndex < 0 || fromIndex >= collection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(fromIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			if (toIndex < 0 || toIndex >= collection.Count)
			{
				throw new ArgumentOutOfRangeException(
					nameof(toIndex),
					"The start index exceeds the bounds of the unfiltered collection.");
			}

			var matches = new List<TMessage>();
			var matchIndexList = new List<long>();

			for (int i = (int)fromIndex; i <= toIndex; i++)
			{
				var message = collection[i];
				if (mFilter == null || mFilter.Matches(message))
				{
					matches.Add(message);
					matchIndexList.Add(i);
				}
			}

			matchIndices = matchIndexList.ToArray();
			return matches.ToArray();
		}
	}

}
