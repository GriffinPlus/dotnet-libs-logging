///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Settings of a <see cref="LogServiceServer"/> instance.
	/// </summary>
	public class LogServiceServerSettings
	{
		/// <summary>
		/// Gets or sets the greeting the server sends to a connecting client.
		/// Default: "Griffin+ Log Service"
		/// </summary>
		public string GreetingText { get; set; } = "Griffin+ Log Service";

		/// <summary>
		/// Gets or sets a value indicating whether the server sends its version number as part of the greeting procedure.
		/// Default: <c>true</c>.
		/// </summary>
		public bool SendServerVersion { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether the server sends the version of the log service library as part of the greeting procedure.
		/// Default: <c>true</c>.
		/// </summary>
		public bool SendLibraryVersion { get; set; } = true;
	}

}
