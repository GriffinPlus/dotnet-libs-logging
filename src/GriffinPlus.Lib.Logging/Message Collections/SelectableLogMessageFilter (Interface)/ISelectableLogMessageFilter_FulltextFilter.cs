///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Interface of a filter selecting log messages containing a specific string in the message text.
	/// </summary>
	public interface ISelectableLogMessageFilter_FulltextFilter : ISelectableLogMessageFilter_FilterBase
	{
		/// <summary>
		/// Gets or sets the text a log message must contain to be matched.
		/// </summary>
		string SearchText { get; set; }
	}

}
