///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Net.Sockets;

namespace GriffinPlus.Lib.Logging.LogService
{

	partial class LogServiceChannel
	{
		/// <summary>
		/// Represents a receive operation in the channel.
		/// </summary>
		[DebuggerDisplay("Length = {Buffer.Length}, ProcessingPending = {ProcessingPending}")]
		private class ReceiveOperation
		{
			/// <summary>
			/// Buffer to fill with the receive operation.
			/// </summary>
			public readonly byte[] Buffer;

			/// <summary>
			/// Socket event arguments associated with the receive operation.
			/// </summary>
			public readonly SocketAsyncEventArgs EventArgs;

			/// <summary>
			/// Get or sets a value indicating whether the received buffer needs to be processed
			/// (is set in the callback invoked on completion).
			/// </summary>
			public bool ProcessingPending;

			/// <summary>
			/// Initializes a new instance of the <see cref="ReceiveOperation"/> class.
			/// </summary>
			/// <param name="bufferSize">Size of a receive buffer (in bytes).</param>
			/// <param name="handler">Handler to call when the receive operation completes.</param>
			public ReceiveOperation(int bufferSize, EventHandler<SocketAsyncEventArgs> handler)
			{
				Buffer = new byte[bufferSize];
				EventArgs = new SocketAsyncEventArgs { UserToken = this };
				EventArgs.SetBuffer(Buffer, 0, Buffer.Length);
				EventArgs.Completed += handler;
				ProcessingPending = false;
			}
		}
	}

}
