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
		/// Represents a send operation in the channel.
		/// </summary>
		[DebuggerDisplay("SocketError = {EventArgs.SocketError}, BufferCapacity = {Buffer.Capacity}, ValidBufferLength = {EventArgs.Count}")]
		private class SendOperation
		{
			private ChainableMemoryBlock mBuffer;

			/// <summary>
			/// Buffer containing data to send.
			/// </summary>
			public ChainableMemoryBlock Buffer
			{
				get => mBuffer;
				set
				{
					mBuffer = value;
					if (mBuffer != null) EventArgs.SetBuffer(mBuffer.Buffer, 0, mBuffer.Length);
					else EventArgs.SetBuffer(null, 0, 0);
				}
			}

			/// <summary>
			/// Get or sets a value indicating whether the send operation is available.
			/// </summary>
			public volatile bool Available;

			/// <summary>
			/// Socket event arguments associated with the send operation.
			/// </summary>
			public readonly SocketAsyncEventArgs EventArgs;

			/// <summary>
			/// Initializes a new instance of the <see cref="SendOperation"/> class.
			/// </summary>
			/// <param name="handler">Handler to call when the send operation completes.</param>
			public SendOperation(EventHandler<SocketAsyncEventArgs> handler)
			{
				EventArgs = new SocketAsyncEventArgs { UserToken = this };
				EventArgs.Completed += handler;
				Available = true;
			}
		}
	}

}
