///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Events arguments that belong to events notifying that a log message filter has changed.
	/// </summary>
	public class FilterChangedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FilterChangedEventArgs"/> class.
		/// </summary>
		/// <param name="changeEffectsFilterResult">
		/// <c>true</c> if the change to the filter may change the filtered message set;
		/// otherwise <c>false</c>.
		/// </param>
		public FilterChangedEventArgs(bool changeEffectsFilterResult)
		{
			ChangeEffectsFilterResult = changeEffectsFilterResult;
		}

		/// <summary>
		/// Gets a value indicating whether the change to the filter may change the filtered message set.
		/// </summary>
		public bool ChangeEffectsFilterResult { get; }
	}

}
