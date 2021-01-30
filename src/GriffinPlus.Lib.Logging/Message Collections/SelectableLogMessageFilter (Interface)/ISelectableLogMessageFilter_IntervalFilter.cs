///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// A filter selecting log messages with timestamps in a specific interval.
	/// </summary>
	public interface ISelectableLogMessageFilter_IntervalFilter : ISelectableLogMessageFilter_FilterBase
	{
		/// <summary>
		/// Gets the timestamp of the oldest message in the collection.
		/// </summary>
		DateTimeOffset MinTimestamp { get; }

		/// <summary>
		/// Gets the timestamp of the newest message in the collection.
		/// </summary>
		DateTimeOffset MaxTimestamp { get; }

		/// <summary>
		/// Gets or sets the lower limit of the timestamp interval to select.
		/// </summary>
		DateTimeOffset From { get; set; }

		/// <summary>
		/// Gets or sets the upper limit of the timestamp interval to select.
		/// </summary>
		DateTimeOffset To { get; set; }
	}

}
