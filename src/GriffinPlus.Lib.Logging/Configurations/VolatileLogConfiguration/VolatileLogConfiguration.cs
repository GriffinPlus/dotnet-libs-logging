///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// The log configuration without persistence (purely in memory, thread-safe).
	/// </summary>
	public class VolatileLogConfiguration : LogConfiguration<VolatileLogConfiguration>
	{
		private          string                                  mApplicationName;
		private readonly VolatileProcessingPipelineConfiguration mProcessingPipelineConfiguration;
		private          List<LogWriterConfiguration>            mLogWriterSettings;

		/// <summary>
		/// Initializes a new instance of the <see cref="VolatileLogConfiguration"/> class.
		/// </summary>
		public VolatileLogConfiguration()
		{
			mProcessingPipelineConfiguration = new VolatileProcessingPipelineConfiguration(this);
			mLogWriterSettings = new List<LogWriterConfiguration>();
			var writer = LogWriterConfiguration.Default;
			writer.IsDefault = true;
			mLogWriterSettings.Add(writer);
			mApplicationName = AppDomain.CurrentDomain.FriendlyName;
		}

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public override string ApplicationName
		{
			get => mApplicationName;
			set
			{
				lock (Sync)
				{
					if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Invalid application name.");
					mApplicationName = value;
				}
			}
		}

		/// <summary>
		/// Gets the configuration of the processing pipeline.
		/// </summary>
		public override IProcessingPipelineConfiguration ProcessingPipeline => mProcessingPipelineConfiguration;

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public override LogLevelBitMask GetActiveLogLevelMask(LogWriter writer)
		{
			lock (Sync)
			{
				// get the first matching log writer settings
				LogWriterConfiguration settings = null;
				foreach (var configuration in mLogWriterSettings)
				{
					if (configuration.NamePatterns.Any(x => x.Regex.IsMatch(writer.Name)))
					{
						// found match by log writer name, check tags now
						// - if no tags are configured => match always
						// - if tags are configured => match, if at least one tag matches
						if (!configuration.TagPatterns.Any() || configuration.TagPatterns.Any(x => writer.Tags.Any<string>(y => x.Regex.IsMatch(y))))
						{
							settings = configuration;
							break;
						}
					}
				}

				if (settings != null)
				{
					LogLevelBitMask mask;

					// enable all log levels that are covered by the base level
					var level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
					if (level == LogLevel.All)
					{
						mask = new LogLevelBitMask(LogLevel.MaxId + 1, true, false);
					}
					else
					{
						mask = new LogLevelBitMask(LogLevel.MaxId + 1, false, false);
						mask.SetBits(0, level.Id + 1);
					}

					// add log levels explicitly included
					foreach (string include in settings.Includes)
					{
						level = LogLevel.GetAspect(include);
						mask.SetBit(level.Id);
					}

					// disable log levels explicitly excluded
					foreach (string exclude in settings.Excludes)
					{
						level = LogLevel.GetAspect(exclude);
						mask.ClearBit(level.Id);
					}

					return mask;
				}

				// no matching settings found
				// => disable all log levels...
				return new LogLevelBitMask(0, false, false);
			}
		}

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public override IEnumerable<LogWriterConfiguration> GetLogWriterSettings()
		{
			lock (Sync)
			{
				// mLogWriterSettings is immutable after it has been set
				// => copying is not necessary
				return mLogWriterSettings;
			}
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public override void SetLogWriterSettings(IEnumerable<LogWriterConfiguration> settings)
		{
			lock (Sync)
			{
				// log writer settings are immutable after creation, so copying the collection is sufficient
				mLogWriterSettings = new List<LogWriterConfiguration>(settings);
			}
		}

		/// <summary>
		/// Saves the configuration
		/// (not supported as the volatile configuration does not support persistence).
		/// </summary>
		/// <param name="includeDefaults">
		/// true to include the default value of settings that have not been explicitly set;
		/// false to save only settings that have not been explicitly set.
		/// </param>
		public override void Save(bool includeDefaults = false)
		{
		}
	}

}
