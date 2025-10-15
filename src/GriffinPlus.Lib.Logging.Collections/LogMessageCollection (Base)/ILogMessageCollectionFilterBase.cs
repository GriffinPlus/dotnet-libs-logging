///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Interface for log message filters that plug into a collection inheriting from the <see cref="LogMessageCollectionBase{TMessage}"/>
/// class to filter messages provided by the collection.
/// </summary>
/// <typeparam name="TMessage">The type of the log message.</typeparam>
public interface ILogMessageCollectionFilterBase<TMessage>
	where TMessage : class, ILogMessage
{
	/// <summary>
	/// Occurs when the filter changes.
	/// </summary>
	event EventHandler<FilterChangedEventArgs> FilterChanged;

	/// <summary>
	/// Attaches the filter to the specified collection.
	/// </summary>
	/// <param name="collection">Collection to attach.</param>
	/// <exception cref="ArgumentNullException">The specified collection is <see langword="null"/>.</exception>
	void AttachToCollection(ILogMessageCollection<TMessage> collection);

	/// <summary>
	/// Detaches the filter from its collection.
	/// </summary>
	void DetachFromCollection();

	/// <summary>
	/// Determines whether the specified log message matches the filter criteria.
	/// </summary>
	/// <param name="message">Message to check.</param>
	/// <returns>
	/// <see langword="true"/> if the specified message matches the filter criteria;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	/// <exception cref="ArgumentNullException">The specified message is <see langword="null"/>.</exception>
	bool Matches(TMessage message);
}
