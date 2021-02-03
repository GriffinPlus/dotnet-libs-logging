///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;

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
			/// An comparer for items of an <see cref="ItemFilter{T}"/>.
			/// </summary>
			internal class ItemValueComparer : IComparer<ISelectableLogMessageFilter_Item<T>>, IComparer
			{
				private readonly IComparer<T> mComparer;

				public ItemValueComparer(IComparer<T> comparer)
				{
					mComparer = comparer;
				}

				public int Compare(ISelectableLogMessageFilter_Item<T> x, ISelectableLogMessageFilter_Item<T> y)
				{
					return mComparer.Compare(x.Value, y.Value);
				}

				public int Compare(object x, object y)
				{
					return Compare((ISelectableLogMessageFilter_Item<T>)x, (ISelectableLogMessageFilter_Item<T>)y);
				}
			}
		}
	}

}
