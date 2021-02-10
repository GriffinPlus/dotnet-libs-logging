///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;

namespace GriffinPlus.Lib.Logging.Collections
{

	partial class SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection>
	{
		/// <summary>
		/// A filter selecting log messages containing a specific string in the message text.
		/// </summary>
		public sealed class FulltextFilter : FilterBase, ISelectableLogMessageFilter_FulltextFilter
		{
			// ReSharper disable once StaticMemberInGenericType
			private static readonly CompareInfo sCompareInfo = CultureInfo.InvariantCulture.CompareInfo;

			private string mSearchText = string.Empty;
			private bool   mIsCaseSensitive;

			/// <summary>
			/// Initializes a new instance of the <see cref="FulltextFilter"/> class.
			/// </summary>
			/// <param name="parent">The <see cref="SelectableLogMessageFilterBase{TMessage,TUnfilteredCollection}"/> the filter belongs to.</param>
			internal FulltextFilter(SelectableLogMessageFilterBase<TMessage, TUnfilteredCollection> parent) : base(parent)
			{
			}

			/// <summary>
			/// Gets or sets the text a log message must contain to be matched.
			/// </summary>
			public string SearchText
			{
				get => mSearchText;
				set
				{
					if (value == null) throw new ArgumentNullException(nameof(value));
					if (mSearchText != value)
					{
						mSearchText = value;
						OnPropertyChanged();
						Parent.OnFilterChanged(Enabled);
					}
				}
			}

			/// <summary>
			/// Gets or sets a value indicating whether the search is done case sensitive (<c>true</c>)
			/// or case insensitive (<c>false</c>).
			/// </summary>
			public bool IsCaseSensitive
			{
				get => mIsCaseSensitive;
				set
				{
					if (mIsCaseSensitive != value)
					{
						mIsCaseSensitive = value;
						OnPropertyChanged();
						Parent.OnFilterChanged(Enabled);
					}
				}
			}

			/// <summary>
			/// Determines whether the specified text passes the filter criteria.
			/// </summary>
			/// <param name="text">Text to check.</param>
			/// <returns>
			/// <c>true</c> if the item passes the filter;
			/// otherwise <c>false</c>.
			/// </returns>
			internal bool Matches(string text)
			{
				if (text == null) return false;
				if (mIsCaseSensitive) return text.Contains(mSearchText);
				return sCompareInfo.IndexOf(text, mSearchText, CompareOptions.IgnoreCase) >= 0;
			}

			/// <summary>
			/// Resets the filter to its initial state.
			/// </summary>
			protected internal override void Reset()
			{
				base.Reset();
				mSearchText = string.Empty;
			}
		}
	}

}
