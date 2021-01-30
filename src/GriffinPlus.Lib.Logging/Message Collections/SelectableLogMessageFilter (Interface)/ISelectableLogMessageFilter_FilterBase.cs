///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.ComponentModel;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Common interface for all filters in a <see cref="ISelectableLogMessageFilter{TMessage}"/>.
	/// </summary>
	public interface ISelectableLogMessageFilter_FilterBase : INotifyPropertyChanged
	{
		/// <summary>
		/// Gets or sets a value indicating whether the filter is enabled.
		/// </summary>
		bool Enabled { get; set; }
	}

}
