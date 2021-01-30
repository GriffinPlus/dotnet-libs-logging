///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GriffinPlus.Lib.Logging.Collections
{

	partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection>
	{
		/// <summary>
		/// Base class for more specific filters filtering for names.
		/// </summary>
		partial class ItemFilter<T>
		{
			/// <summary>
			/// An item in the <see cref="SelectableLogMessageFilter{TMessage}"/> with support for data-binding.
			/// </summary>
			public class Item : ISelectableLogMessageFilter_Item<T>
			{
				private bool mSelected;

				/// <summary>
				/// Initializes a new instance of the <see cref="Item"/> class.
				/// </summary>
				/// <param name="group">Name of the group the value belongs to.</param>
				/// <param name="value">Value of the item in the filter.</param>
				/// <param name="isStatic">
				/// true, if the item stays in the list of selectable items;
				/// false, if the item can be removed.
				/// </param>
				internal Item(string group, T value, bool isStatic)
				{
					Group = group;
					Value = value;
					IsStatic = isStatic;
				}

				#region PropertyChanged

				/// <summary>
				/// Occurs when one of the properties has changed.
				/// </summary>
				public event PropertyChangedEventHandler PropertyChanged;

				/// <summary>
				/// Raises the <see cref="PropertyChanged"/> event.
				/// </summary>
				/// <param name="propertyName">Name of the property that has changed.</param>
				protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
				{
					var handler = PropertyChanged;
					handler?.Invoke(this, new PropertyChangedEventArgs(propertyName));
				}

				#endregion

				#region Selected

				/// <summary>
				/// Gets or sets a value indicating whether the item is selected.
				/// </summary>
				public bool Selected
				{
					get => mSelected;
					set
					{
						if (mSelected != value)
						{
							mSelected = value;
							OnPropertyChanged();
						}
					}
				}

				#endregion

				#region Group

				/// <summary>
				/// Gets the name of the group the item belongs to.
				/// </summary>
				public string Group { get; }

				#endregion

				#region Value

				/// <summary>
				/// Gets the value of the item.
				/// </summary>
				public T Value { get; }

				#endregion

				#region IsStatic

				/// <summary>
				/// Gets a value indicating whether the item stays in the collection of selectable items (<c>true</c>)
				/// or whether the item can be removed (<c>false</c>).
				/// </summary>
				internal bool IsStatic { get; }

				#endregion
			}
		}
	}

}
