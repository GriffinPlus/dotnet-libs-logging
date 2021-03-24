///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Status of the log service server.
	/// </summary>
	public enum LogServiceServerStatus
	{
		/// <summary>
		/// The server is starting up.
		/// This is an intermediate status and should only be visible until the processing thread in online.
		/// </summary>
		Starting,

		/// <summary>
		/// The server is running.
		/// This is the regular status the server should have when operating.
		/// </summary>
		Running,

		/// <summary>
		/// The server is shutting down.
		/// This is an intermediate status and should only be visible when the server shuts down open connections.
		/// </summary>
		Stopping,

		/// <summary>
		/// The server has stopped.
		/// This is the regular status the server should have, if offline.
		/// </summary>
		Stopped,

		/// <summary>
		/// The server in an error condition.
		/// In this state the server cannot operate properly.
		/// </summary>
		Error
	}

}
