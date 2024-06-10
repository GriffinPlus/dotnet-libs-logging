///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Factory creating system loggers that provide access to the system's logging facility.
/// </summary>
public class SystemLoggerFactory
{
	/// <summary>
	/// Creates an instance of the operating system specific system logger.
	/// </summary>
	/// <returns>An operating specific system logger.</returns>
	public static ISystemLogger Create()
	{
		PlatformID platform = Environment.OSVersion.Platform;
		switch (platform)
		{
			case PlatformID.Win32NT:
				return new WindowsSystemLogger();

			case PlatformID.Unix:
				return new UnixSystemLogger();

			case PlatformID.MacOSX:
			case PlatformID.Win32S:
			case PlatformID.Win32Windows:
			case PlatformID.WinCE:
			case PlatformID.Xbox:
			default:
				return new NoopSystemLogger();
		}
	}
}
