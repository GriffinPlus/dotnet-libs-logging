///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

// ReSharper disable PossibleInterfaceMemberAmbiguity

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Interface of unfiltered log message collections.
/// </summary>
/// <typeparam name="TMessage">The log message type.</typeparam>
public interface ILogMessageCollection<TMessage> : ILogMessageCollectionCommon<TMessage>, IList<TMessage>
	where TMessage : class, ILogMessage
{
	/// <summary>
	/// Gets the number of log messages in the collection.
	/// </summary>
	new long Count { get; } // needed to resolve ambiguities when using a ILogMessageCollection<T> reference

	/// <summary>
	/// Removes all log messages from the collection.
	/// </summary>
	/// <exception cref="NotSupportedException">The collection is read-only.</exception>
	new void Clear(); // needed to resolve ambiguities when using a ILogMessageCollection<T> reference

	/// <summary>
	/// Adds multiple log messages to the collection at once.
	/// </summary>
	/// <param name="messages">Log messages to add.</param>
	/// <exception cref="NotSupportedException">The collection is read-only.</exception>
	void AddRange(IEnumerable<TMessage> messages);

	/// <summary>
	/// Removes the oldest log messages that are above the specified message limit -or- log messages that are older
	/// than the specified age.
	/// </summary>
	/// <param name="maximumMessageCount">
	/// Maximum number of messages to keep;
	/// -1 to disable removing messages by maximum message count.
	/// </param>
	/// <param name="minimumMessageTimestamp">
	/// Point in time (UTC) to keep messages after (includes the exact point in time);
	/// <seealso cref="DateTime.MinValue"/> to disable removing messages by age.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// The message limit must be > 0 to limit the number of message or -1 to disable the limit.
	/// </exception>
	/// <exception cref="NotSupportedException">The collection is read-only.</exception>
	void Prune(long maximumMessageCount, DateTime minimumMessageTimestamp);
}
