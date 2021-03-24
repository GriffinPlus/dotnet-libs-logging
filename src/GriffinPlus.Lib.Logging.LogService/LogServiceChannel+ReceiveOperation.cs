///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Net.Sockets;

using GriffinPlus.Lib.Io;

namespace GriffinPlus.Lib.Logging.LogService
{

	partial class LogServiceChannel
	{
		/// <summary>
		/// Represents a receive operation in the channel.
		/// </summary>
		[DebuggerDisplay("Length = {Buffer.Length}, ReceiveCompleted = {ReceiveCompleted}, ProcessingPending = {ProcessingPending}")]
		private class ReceiveOperation
		{
			private ChainableMemoryBlock mBuffer;

			/// <summary>
			/// Initializes a new instance of the <see cref="ReceiveOperation"/> class.
			/// </summary>
			/// <param name="handler">Handler to call when the receive operation completes.</param>
			public ReceiveOperation(EventHandler<SocketAsyncEventArgs> handler)
			{
				EventArgs = new SocketAsyncEventArgs { UserToken = this };
				EventArgs.Completed += handler;
				ProcessingPending = false;
			}

			/// <summary>
			/// Gets the socket event arguments associated with the receive operation.
			/// </summary>
			public SocketAsyncEventArgs EventArgs { get; }

			/// <summary>
			/// Gets the buffer to fill with the receive operation.
			/// </summary>
			public ChainableMemoryBlock Buffer
			{
				get => mBuffer;
				set
				{
					mBuffer = value;
					if (mBuffer != null) EventArgs.SetBuffer(mBuffer.Buffer, 0, mBuffer.Capacity);
					else EventArgs.SetBuffer(null, 0, 0);
				}
			}

			/// <summary>
			/// Get or sets a value indicating whether the received buffer needs to be processed
			/// (is set in the callback invoked on completion).
			/// </summary>
			public bool ProcessingPending; // should be a field due to performance reasons
		}
	}

}
