///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// ReSharper disable UnusedMemberInSuper.Global

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface of a log configuration (must be implemented thread-safe).
	/// </summary>
	public interface ILogConfiguration
	{
		/// <summary>
		/// Gets a value indicating whether the configuration is the default configuration that was created
		/// by the logging subsystem at start.
		/// </summary>
		bool IsDefaultConfiguration { get; }

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		string ApplicationName { get; set; }

		/// <summary>
		/// Gets the configuration of the processing pipeline.
		/// </summary>
		IProcessingPipelineConfiguration ProcessingPipeline { get; }

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		LogLevelBitMask GetActiveLogLevelMask(LogWriter writer);

		/// <summary>
		/// Saves the configuration.
		/// </summary>
		/// <param name="includeDefaults">
		/// true to include the default value of settings that have not been explicitly set;
		/// false to save only settings that have not been explicitly set.
		/// </param>
		void Save(bool includeDefaults = false);
	}

}
