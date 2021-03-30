///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Status of a log service channel.
	/// </summary>
	public enum LogServiceChannelStatus
	{
		/// <summary>
		/// The channel has been created.
		/// </summary>
		Created,

		/// <summary>
		/// The channel is connecting.
		/// </summary>
		Connecting,

		/// <summary>
		/// The channel is up and running.
		/// </summary>
		Operational,

		/// <summary>
		/// The channel is shutting down.
		/// </summary>
		ShuttingDown,

		/// <summary>
		/// The channel has completed shutting down.
		/// </summary>
		ShutdownCompleted,

		/// <summary>
		/// The channel is malfunctional and cannot be used.
		/// </summary>
		Malfunctional
	}

}
