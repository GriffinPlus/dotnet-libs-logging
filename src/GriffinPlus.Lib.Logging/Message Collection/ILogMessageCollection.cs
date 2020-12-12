///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Interface log message collections must implement.
	/// </summary>

	// ReSharper disable once PossibleInterfaceMemberAmbiguity
	public interface ILogMessageCollection<TMessage> :
		IList<TMessage>,
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
		TMessage this[long index] { get; }

		/// <summary>
		/// Gets the number of log messages in the collection.
		/// </summary>
		new long Count { get; }

		/// <summary>
		/// Adds multiple log messages to the collection at once.
		/// </summary>
		/// <param name="messages">Log messages to add.</param>
		void AddRange(IEnumerable<TMessage> messages);
	}

}
