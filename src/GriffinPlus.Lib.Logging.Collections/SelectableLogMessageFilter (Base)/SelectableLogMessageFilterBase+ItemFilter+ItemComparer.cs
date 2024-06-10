///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections;
using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging.Collections;

partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection>
{
	/// <summary>
	/// Base class for more specific filters that are filtering for names.
	/// </summary>
	partial class ItemFilter<T>
	{
		/// <summary>
		/// A comparer for items of an <see cref="ItemFilter{T}"/>.
		/// </summary>
		internal class ItemValueComparer(IComparer<T> valueComparer) : IComparer<ISelectableLogMessageFilter_Item<T>>, IComparer
		{
			public readonly IComparer<T> ValueComparer = valueComparer;

			public int Compare(ISelectableLogMessageFilter_Item<T> x, ISelectableLogMessageFilter_Item<T> y)
			{
				if (x == y) return 0;
				if (x == null) return -1;
				if (y == null) return 1;
				return ValueComparer.Compare(x.Value, y.Value);
			}

			public int Compare(object x, object y)
			{
				return Compare((ISelectableLogMessageFilter_Item<T>)x, (ISelectableLogMessageFilter_Item<T>)y);
			}
		}
	}
}
