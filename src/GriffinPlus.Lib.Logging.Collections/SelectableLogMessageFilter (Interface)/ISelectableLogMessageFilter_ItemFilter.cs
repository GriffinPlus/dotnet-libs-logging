///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.ObjectModel;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Interface of a filter matching a certain field of a log messages using a list of selectable items.
	/// </summary>
	public interface ISelectableLogMessageFilter_ItemFilter<T> : ISelectableLogMessageFilter_FilterBase
	{
		/// <summary>
		/// Gets items of the filtered field that occur in the log message collection.
		/// </summary>
		ReadOnlyObservableCollection<ISelectableLogMessageFilter_Item<T>> Items { get; }

		/// <summary>
		/// Gets or sets a value indicating whether <see cref="Items"/> contains items only that belong to at
		/// least one message in the collection (<c>false</c>) or whether all items are kept even when log
		/// messages are removed from the collection (<c>true</c>). Default is <c>false</c>.
		/// </summary>
		bool AccumulateItems { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the filter is disabled when it is reset.
		/// Default is <c>false</c>.
		/// </summary>
		bool DisableFilterOnReset { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether items are unselected when the filter is reset.
		/// Default is <c>false</c>.
		/// </summary>
		bool UnselectItemsOnReset { get; set; }
	}

}
