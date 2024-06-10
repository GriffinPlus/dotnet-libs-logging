///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A method that parameterizes a log configuration when initializing the logging subsystem.
/// </summary>
/// <typeparam name="TConfiguration">Type of the configuration to parameterize.</typeparam>
/// <param name="configuration">The configuration to parameterize.</param>
public delegate void LogConfigurationInitializer<in TConfiguration>(TConfiguration configuration)
	where TConfiguration : ILogConfiguration, new();
