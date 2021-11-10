///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Interface for selectable items provided via the <see cref="ISelectableLogMessageFilter_ItemFilter{T}"/> interface.
	/// </summary>
	/// <typeparam name="T">Type of the item value.</typeparam>
	interface ISelectableLogMessageFilter_ItemInternal<out T> : ISelectableLogMessageFilter_Item<T>
	{
		/// <summary>
		/// Gets a value indicating whether the item stays in the collection of selectable items (<c>true</c>)
		/// or whether the item can be removed (<c>false</c>).
		/// </summary>
		bool IsStatic { get; }

		/// <summary>
		/// Gets or sets a value indicating whether the item value is used in at least one message in the unfiltered message set.
		/// If <see cref="ISelectableLogMessageFilter_ItemFilter{T}.AccumulateItems"/> is <c>true</c>, this property remains
		/// <c>true</c> once it is <c>true</c> for static items.
		/// </summary>
		new bool ValueUsed { get; set; }
	}

}
