///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

// ReSharper disable PossibleInterfaceMemberAmbiguity

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// The common interface of log message collections.
	/// This interface should not be implemented directly.
	/// Please use <see cref="ILogMessageCollection{TMessage}"/> for unfiltered message collections and
	/// <see cref="IFilteredLogMessageCollection{TMessage}"/> for filtered log message collections.
	/// </summary>
	/// <typeparam name="TMessage">The log message type.</typeparam>
	public interface ILogMessageCollectionCommon<out TMessage> :
		IReadOnlyList<TMessage>,
		IList,
		INotifyCollectionChanged,
		INotifyPropertyChanged,
		IDisposable where TMessage : ILogMessage
	{
		/// <summary>
		/// Gets the log message at the specified index in the collection.
		/// </summary>
		/// <param name="index">Index of the log message to get.</param>
		/// <returns>Log message at the specified index.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is out of bounds.</exception>
		TMessage this[long index] { get; }

		/// <summary>
		/// Gets the number of log messages in the collection.
		/// </summary>
		new long Count { get; }

		/// <summary>
		/// Gets a number of messages starting at the specified index.
		/// </summary>
		/// <param name="index">Index of the message to get.</param>
		/// <param name="count">Number of messages to get.</param>
		/// <returns>The requested log messages.</returns>
		/// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> and/or <paramref name="count"/> is out of bounds.</exception>
		IEnumerable<TMessage> GetRange(long index, long count);

		/// <summary>
		/// Gets all log writer names that are used by messages in the collection.
		/// </summary>
		ReadOnlyObservableCollection<string> UsedLogWriters { get; }

		/// <summary>
		/// Gets all log level names that are used by messages in the collection.
		/// </summary>
		ReadOnlyObservableCollection<string> UsedLogLevels { get; }

		/// <summary>
		/// Gets all tags that are used by messages in the collection.
		/// </summary>
		ReadOnlyObservableCollection<string> UsedTags { get; }

		/// <summary>
		/// Gets all application names that are used by messages in the collection.
		/// </summary>
		ReadOnlyObservableCollection<string> UsedApplicationNames { get; }

		/// <summary>
		/// Gets all process names that are used by messages in the collection.
		/// </summary>
		ReadOnlyObservableCollection<string> UsedProcessNames { get; }

		/// <summary>
		/// Gets all process ids that are used by messages in the collection.
		/// </summary>
		ReadOnlyObservableCollection<int> UsedProcessIds { get; }
	}

}
