///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	partial class ProcessIntegration
	{
		/// <summary>
		/// Event arguments for the <see cref="OutputStreamReceivedMessage" /> and the <see cref="ErrorStreamReceivedMessage" /> events.
		/// </summary>
		public class MessageReceivedEventArgs : EventArgs
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="MessageReceivedEventArgs" /> class.
			/// </summary>
			/// <param name="message">The received log message.</param>
			public MessageReceivedEventArgs(ILogMessage message)
			{
				Message = message;
			}

			/// <summary>
			/// Gets the received log message.
			/// </summary>
			public ILogMessage Message { get; }
		}
	}

}
