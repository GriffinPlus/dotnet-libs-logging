///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.ComponentModel;

namespace GriffinPlus.Lib.Logging.Collections;

/// <summary>
/// Interface for selectable items provided via the <see cref="ISelectableLogMessageFilter_ItemFilter{T}"/> interface.
/// </summary>
/// <typeparam name="T">Type of the item value.</typeparam>
public interface ISelectableLogMessageFilter_Item<out T> : INotifyPropertyChanged
{
	/// <summary>
	/// Gets or sets a value indicating whether the item is selected.
	/// </summary>
	bool Selected { get; set; }

	/// <summary>
	/// Gets the name of the group the item belongs to.
	/// </summary>
	string Group { get; }

	/// <summary>
	/// Gets the value of the item.
	/// </summary>
	T Value { get; }

	/// <summary>
	/// Gets a value indicating whether the item value is used in at least one message in the unfiltered message set.
	/// If <see cref="ISelectableLogMessageFilter_ItemFilter{T}.AccumulateItems"/> is <c>true</c>, this property remains
	/// <c>true</c> once it is <c>true</c> for static items.
	/// </summary>
	bool ValueUsed { get; }
}
