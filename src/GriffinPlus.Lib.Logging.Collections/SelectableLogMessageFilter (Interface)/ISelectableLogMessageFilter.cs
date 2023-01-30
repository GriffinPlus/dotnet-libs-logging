///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Interface of a log message filter that provides collections of fields that can be selected to be filtered.
	/// This filter supports data-binding and is therefore a perfect fit for user interfaces that present a list
	/// of selectable items for log writers, levels, processes etc.
	/// </summary>
	/// <typeparam name="TMessage">Type of the log message.</typeparam>
	public interface ISelectableLogMessageFilter<TMessage> :
		ILogMessageCollectionFilterBase<TMessage>,
		INotifyPropertyChanged,
		IDisposable
		where TMessage : class, ILogMessage
	{
		/// <summary>
		/// Gets or sets a value indicating whether the filter is enabled.
		/// </summary>
		/// <Value>
		/// <c>true</c> applies the filter when matching log messages (default).<br/>
		/// <c>false</c> lets all log messages pass the filter when matching.
		/// </Value>
		bool Enabled { get; set; }

		/// <summary>
		/// Gets the timestamp filter.
		/// </summary>
		ISelectableLogMessageFilter_IntervalFilter TimestampFilter { get; }

		/// <summary>
		/// Gets the log writer filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> LogWriterFilter { get; }

		/// <summary>
		/// Gets the log level filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> LogLevelFilter { get; }

		/// <summary>
		/// Gets the tag filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> TagFilter { get; }

		/// <summary>
		/// Gets the application name filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ApplicationNameFilter { get; }

		/// <summary>
		/// Gets the process name filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<string> ProcessNameFilter { get; }

		/// <summary>
		/// Gets the process id filter.
		/// </summary>
		ISelectableLogMessageFilter_ItemFilter<int> ProcessIdFilter { get; }

		/// <summary>
		/// Gets the message text filter.
		/// </summary>
		ISelectableLogMessageFilter_FulltextFilter TextFilter { get; }
	}

}
