///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.LogService
{

	partial class LogServiceChannel
	{
		/// <summary>
		/// An event handler that can be attached to the <see cref="LogServiceChannel.LineReceived"/> event.
		/// </summary>
		/// <param name="channel">The <see cref="LogServiceChannel"/> that raised the event.</param>
		/// <param name="line">The received line.</param>
		public delegate void LineReceivedEventHandler(LogServiceChannel channel, ReadOnlySpan<char> line);
	}

}
