///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// A log message filter for the <see cref="LogMessageCollection{TMessage}"/> class that provides collections of fields
	/// that can be selected to be filtered. The filter supports data-binding and is therefore a perfect fit for user interfaces
	/// that present a list of selectable items for log writers, levels, processes etc.
	/// </summary>
	public class SelectableLogMessageFilter<TMessage> :
		SelectableLogMessageFilterBase<TMessage, LogMessageCollection<TMessage>>,
		ILogMessageCollectionFilter<TMessage>
		where TMessage : class, ILogMessage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SelectableLogMessageFilter{TMessage}"/> class.
		/// </summary>
		public SelectableLogMessageFilter() { }
	}

}
