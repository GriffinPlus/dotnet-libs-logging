///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Interface of an accessor that provides direct access to a filtered log message set without creating a filtered collection.
/// The use of these accessors offers a much better performance, since the overhead for the realization of collection-typical structures can be omitted.
/// </summary>
/// <typeparam name="TMessage">The log message type.</typeparam>
public interface ILogMessageCollectionFilteringAccessor<TMessage> : IDisposable
	where TMessage : class, ILogMessage
{
	/// <summary>
	/// Gets the unfiltered collection the accessor works on.
	/// </summary>
	ILogMessageCollection<TMessage> Collection { get; }

	/// <summary>
	/// Gets the filter the accessor works with.
	/// </summary>
	ILogMessageCollectionFilterBase<TMessage> Filter { get; }

	/// <summary>
	/// Gets the first log message matching the filter criteria starting at the specified index in the unfiltered collection going backwards.
	/// </summary>
	/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
	/// <param name="matchIndex">Receives the index of the first log message matching the filter.</param>
	/// <returns>
	/// The first log message matching the filter;<br/>
	/// <see langword="null"/> if no message matching the filter was found.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
	TMessage GetPreviousMessage(
		long     fromIndex,
		out long matchIndex);

	/// <summary>
	/// Gets a range of log messages matching the filter criteria from the unfiltered collection going backwards.
	/// </summary>
	/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
	/// <param name="count">Maximum number of matching log messages to get.</param>
	/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
	/// <param name="reverse">
	/// <see langword="true"/> to reverse the list of returned messages, so the order of the messages is the same as in the collection;<br/>
	/// <see langword="false"/> to return the list of messages in the opposite order.
	/// </param>
	/// <returns>Log messages matching filter.</returns>
	/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
	TMessage[] GetPreviousMessages(
		long       fromIndex,
		int        count,
		out long[] matchIndices,
		bool       reverse);

	/// <summary>
	/// Gets the first log message matching the filter criteria starting at the specified index in the unfiltered collection going forward.
	/// </summary>
	/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
	/// <param name="matchIndex">Receives the index of the first log message matching the filter.</param>
	/// <returns>
	/// The first log message matching the filter;<br/>
	/// <see langword="null"/> if no message matching the filter was found.
	/// </returns>
	/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
	TMessage GetNextMessage(
		long     fromIndex,
		out long matchIndex);

	/// <summary>
	/// Gets a range of log messages matching the filter criteria from the unfiltered collection going forward.
	/// </summary>
	/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
	/// <param name="count">Maximum number of matching log messages to get.</param>
	/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
	/// <returns>Log messages matching filter.</returns>
	/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromIndex"/> exceeds the bounds of the unfiltered collection.</exception>
	/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
	TMessage[] GetNextMessages(
		long       fromIndex,
		int        count,
		out long[] matchIndices);

	/// <summary>
	/// Gets a range of log messages matching the filter criteria from the unfiltered collection.
	/// </summary>
	/// <param name="fromIndex">Index of the log message in the unfiltered collection to start at.</param>
	/// <param name="toIndex">Index of the log message in the unfiltered collection to stop at.</param>
	/// <param name="matchIndices">Receives the indices of the log messages matching the filter.</param>
	/// <returns>Log messages matching filter.</returns>
	/// <exception cref="ObjectDisposedException">The accessor has been disposed.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="fromIndex"/> or <paramref name="toIndex"/> exceeds the bounds of the unfiltered
	/// collection.
	/// </exception>
	TMessage[] GetMessageRange(
		long       fromIndex,
		long       toIndex,
		out long[] matchIndices);
}
