///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Interface for log message filters that plug into <see cref="FileBackedLogMessageCollection"/> filter messages
	/// provided by the collection.
	/// </summary>
	/// <remarks>
	/// This interface exists only to ensure that filters can be used in <see cref="FileBackedLogMessageCollection"/> only.
	/// </remarks>
	public interface IFileBackedLogMessageCollectionFilter : ILogMessageCollectionFilterBase<LogMessage>
	{
		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified position in the log file going backwards.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		LogFileMessage GetPreviousMessage(long fromMessageId);

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the log file going backwards.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <param name="reverse">
		/// <c>true</c> to reverse the list of returned messages, so the order of the messages is the same as in the log file;
		/// <c>false</c> to return the list of messages in the opposite order.
		/// </param>
		/// <returns>Log messages matching the filter.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		LogFileMessage[] GetPreviousMessages(
			long fromMessageId,
			long count,
			bool reverse);

		/// <summary>
		/// Gets the first log message matching the filter criteria starting at the specified position in the log file going forward.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <returns>
		/// The first log message matching the filter;
		/// null, if no message matching the filter was found.
		/// </returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		LogFileMessage GetNextMessage(long fromMessageId);

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the log file going forward.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <param name="count">Maximum number of matching log messages to get.</param>
		/// <returns>Log messages matching the filter.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="fromMessageId"/> exceeds the bounds of the log file.</exception>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
		LogFileMessage[] GetNextMessages(long fromMessageId, long count);

		/// <summary>
		/// Gets a range of log messages matching the filter criteria from the log file.
		/// </summary>
		/// <param name="fromMessageId">Id of the log message in the log file to start at.</param>
		/// <param name="toMessageId">Id of the log message in the log file to stop at.</param>
		/// <returns>Log messages matching the filter.</returns>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="fromMessageId"/> or <paramref name="toMessageId"/> exceeds the bounds of the log file.
		/// </exception>
		LogFileMessage[] GetMessageRange(long fromMessageId, long toMessageId);
	}

}
