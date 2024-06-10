///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

partial class ProcessIntegration
{
	/// <summary>
	/// Event arguments for the <see cref="OutputStreamReceivedText"/> and the <see cref="ErrorStreamReceivedText"/> events.
	/// </summary>
	public class LineReceivedEventArgs : EventArgs
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LineReceivedEventArgs"/> class.
		/// </summary>
		/// <param name="line">The received line.</param>
		public LineReceivedEventArgs(string line)
		{
			Line = line;
		}

		/// <summary>
		/// Gets the received line.
		/// </summary>
		public string Line { get; }
	}
}
