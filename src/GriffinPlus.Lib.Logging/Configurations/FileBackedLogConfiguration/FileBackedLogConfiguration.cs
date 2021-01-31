///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log configuration with ini-style file backing (thread-safe).
	/// </summary>
	public class FileBackedLogConfiguration : LogConfiguration<FileBackedLogConfiguration>, IDisposable
	{
		/// <summary>
		/// The default path of the log configuration file.
		/// </summary>
		private static readonly string sDefaultConfigFilePath;

		private static readonly LogWriter sLog = Log.GetWriter("Logging");

		private readonly FileBackedProcessingPipelineConfiguration mProcessingPipelineConfiguration;
		private          FileSystemWatcher                         mFileSystemWatcher;
		private          Timer                                     mReloadingTimer;
		private readonly string                                    mFileName;

		/// <summary>
		/// Initializes the <see cref="FileBackedLogConfiguration"/> class.
		/// </summary>
		static FileBackedLogConfiguration()
		{
			// initialize the path of the configuration file
			var assembly = Assembly.GetEntryAssembly();
			if (assembly != null)
			{
				// regular case
				// => use name of the entry assembly (application)
				string fileName = Path.GetFileNameWithoutExtension(assembly.Location) + ".gplogconf";
				sDefaultConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
			}
			else
			{
				// no entry assembly (most probably a unit test runner)
				// => use friendly name of the application domain
				string fileName = AppDomain.CurrentDomain.FriendlyName + ".gplogconf";
				sDefaultConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogConfiguration"/> class (the configuration file is
		/// located in the application's base directory and named as the entry assembly plus extension '.logconf').
		/// </summary>
		public FileBackedLogConfiguration() : this(sDefaultConfigFilePath)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogConfiguration"/> class.
		/// </summary>
		/// <param name="path">Path of the configuration file to use.</param>
		public FileBackedLogConfiguration(string path)
		{
			lock (Sync)
			{
				FullPath = Path.GetFullPath(path);
				mFileName = Path.GetFileName(path);

				// load configuration file
				const int maxRetryCount = 5;
				for (int retry = 0; retry < maxRetryCount; retry++)
				{
					try
					{
						File = LogConfigurationFile.LoadFrom(FullPath);
						break;
					}
					catch (FileNotFoundException)
					{
						// file does not exist
						// => that's ok, use a default configuration file...
						File = new LogConfigurationFile();
						break;
					}
					catch (IOException)
					{
						// there is something wrong at a lower level, most probably a sharing violation
						// => just try again...
						if (retry + 1 >= maxRetryCount) throw;
						Thread.Sleep(10);
					}
					catch (Exception ex)
					{
						// a severe error that cannot be fixed here
						// => abort
						sLog.ForceWrite(
							LogLevel.Failure,
							"Loading log configuration file ({0}) failed. Exception: {1}",
							FullPath,
							ex);

						throw;
					}
				}

				// set up the file system watcher to get notified of changes to the file
				// (notifications about the creation of files with zero-length do not contain
				// valuable information, so renaming/deleting is sufficient)
				mFileSystemWatcher = new FileSystemWatcher
				{
					Path = Path.GetDirectoryName(FullPath),
					Filter = "*" + Path.GetExtension(mFileName)
				};
				mFileSystemWatcher.Changed += EH_FileSystemWatcher_Changed;
				mFileSystemWatcher.Deleted += EH_FileSystemWatcher_Removed;
				mFileSystemWatcher.Renamed += EH_FileSystemWatcher_Renamed;
				mFileSystemWatcher.EnableRaisingEvents = true;

				// set up timer that will handle reloading the configuration file
				mReloadingTimer = new Timer(TimerProc, null, -1, -1); // do not start immediately

				// initialize the pipeline configuration part
				mProcessingPipelineConfiguration = new FileBackedProcessingPipelineConfiguration(this);
			}
		}

		/// <summary>
		/// Disposes the object cleaning up unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		/// <summary>
		/// Disposes the object cleaning up unmanaged resources
		/// </summary>
		/// <param name="disposing">
		/// true, if called explicitly;
		/// false, if called due to finalization.
		/// </param>
		protected virtual void Dispose(bool disposing)
		{
			lock (Sync)
			{
				if (mReloadingTimer != null)
				{
					mReloadingTimer.Dispose();
					mReloadingTimer = null;
				}

				if (mFileSystemWatcher != null)
				{
					mFileSystemWatcher.Dispose();
					mFileSystemWatcher = null;
				}
			}
		}

		/// <summary>
		/// Gets the full path of the configuration file.
		/// </summary>
		public string FullPath { get; }

		/// <summary>
		/// Gets the wrapped log configuration file.
		/// </summary>
		internal LogConfigurationFile File { get; private set; }

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public override string ApplicationName
		{
			get
			{
				lock (Sync)
				{
					return File.ApplicationName;
				}
			}

			set
			{
				lock (Sync)
				{
					File.ApplicationName = value;
				}
			}
		}

		/// <summary>
		/// Gets the configuration of the processing pipeline.
		/// </summary>
		public override IProcessingPipelineConfiguration ProcessingPipeline => mProcessingPipelineConfiguration;

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public override IEnumerable<LogWriterConfiguration> GetLogWriterSettings()
		{
			lock (Sync)
			{
				return new List<LogWriterConfiguration>(File.LogWriterSettings.Select(x => new LogWriterConfiguration(x)));
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
				File.LogWriterSettings.Clear();
				File.LogWriterSettings.AddRange(settings);
			}
		}

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
				foreach (var configuration in File.LogWriterSettings)
				{
					if (configuration.NamePatterns.Any(x => x.Regex.IsMatch(writer.Name)))
					{
						// found match by log writer name, check tags now
						// - if no tags are configured => match always
						// - if tags are configured => match, if at least one tag matches
						if (!configuration.TagPatterns.Any() || configuration.TagPatterns.Any(x => writer.Tags.Any(y => x.Regex.IsMatch(y))))
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
		/// Saves the configuration.
		/// </summary>
		/// <param name="includeDefaults">
		/// true to include the default value of settings that have not been explicitly set;
		/// false to save only settings that have not been explicitly set.
		/// </param>
		public override void Save(bool includeDefaults = false)
		{
			lock (Sync)
			{
				// save the configuration file before making modifications
				var oldFile = File;

				try
				{
					if (includeDefaults)
					{
						// create a temporary configuration file and add the default value of the settings that don't
						// have an explicitly set value
						File = new LogConfigurationFile(oldFile);
						foreach (var stageSettings in mProcessingPipelineConfiguration.Stages)
						{
							foreach (var setting in stageSettings.Values)
							{
								if (!setting.HasValue)
								{
									setting.Value = setting.DefaultValue;
								}
							}
						}
					}

					const int maxRetryCount = 5;
					for (int retry = 0; retry < maxRetryCount; retry++)
					{
						try
						{
							File.Save(FullPath);
						}
						catch (IOException)
						{
							// there is something wrong at a lower level, most probably a sharing violation
							// => just try again...
							if (retry + 1 >= maxRetryCount) throw;
							Thread.Sleep(10);
						}
						catch (Exception ex)
						{
							// a severe error that cannot be fixed here
							// => abort
							sLog.ForceWrite(
								LogLevel.Failure,
								"Loading log configuration file ({0}) failed. Exception: {1}",
								FullPath,
								ex);

							throw;
						}
					}
				}
				finally
				{
					// revert to using the old configuration file
					File = oldFile;
				}
			}
		}

		/// <summary>
		/// Checks whether the specified file name is the name of the configuration file of the log
		/// (the comparison is case insensitive).
		/// </summary>
		/// <param name="fileName">File name to check.</param>
		/// <returns>
		/// true, if the file name is the name of the configuration file;
		/// otherwise false.
		/// </returns>
		private bool IsConfigurationFile(string fileName)
		{
			return StringComparer.InvariantCultureIgnoreCase.Compare(fileName, mFileName) == 0;
		}

		/// <summary>
		/// Is called by the file system watcher when a file changes in the watched directory.
		/// </summary>
		/// <param name="sender">The file system watcher.</param>
		/// <param name="e">Event arguments.</param>
		private void EH_FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			// called by worker thread...
			lock (Sync)
			{
				if (mReloadingTimer == null) return; // object has been disposed

				if (IsConfigurationFile(e.Name))
				{
					// configuration file is present now
					// => schedule loading the file...
					mReloadingTimer.Change(500, -1);
				}
			}
		}

		/// <summary>
		/// Is called by the file system watcher when a file is deleted in the watched directory.
		/// </summary>
		/// <param name="sender">The file system watcher.</param>
		/// <param name="e">Event arguments.</param>
		private void EH_FileSystemWatcher_Removed(object sender, FileSystemEventArgs e)
		{
			// called by worker thread...
			lock (Sync)
			{
				if (mReloadingTimer == null) return; // object has been disposed

				if (IsConfigurationFile(e.Name))
				{
					// configuration file was removed
					// => create a default configuration...
					var file = new LogConfigurationFile();
					File = file;
				}
			}
		}

		/// <summary>
		/// Is called by the file system watcher when a file is renamed in the watched directory.
		/// </summary>
		/// <param name="sender">The file system watcher.</param>
		/// <param name="e">Event arguments.</param>
		private void EH_FileSystemWatcher_Renamed(object sender, RenamedEventArgs e)
		{
			// called by worker thread...
			lock (Sync)
			{
				if (mReloadingTimer == null) return; // object has been disposed

				bool vanished = IsConfigurationFile(e.OldName);

				if (IsConfigurationFile(e.Name))
				{
					// configuration file is present now
					// => schedule loading the file...
					mReloadingTimer.Change(500, -1);
				}
				else if (vanished)
				{
					// configuration file was removed
					// => create a default configuration...
					var file = new LogConfigurationFile();
					File = file;
				}
			}
		}

		/// <summary>
		/// Entry point for the timer reloading the configuration file.
		/// </summary>
		/// <param name="state">Some state object (not used).</param>
		private void TimerProc(object state)
		{
			lock (Sync)
			{
				if (mReloadingTimer == null) return; // object has been disposed

				try
				{
					// load file (always replace mFile, do not modify existing instance for threading reasons)
					File = LogConfigurationFile.LoadFrom(FullPath);
				}
				catch (FileNotFoundException)
				{
					// file does not exist, should be handled using file system notifications
					// => don't do anything here...
				}
				catch (Exception ex)
				{
					sLog.ForceWrite(LogLevel.Error, "Reloading configuration file failed. Exception: {0}", ex);
					mReloadingTimer.Change(500, -1); // try again later...
				}
			}
		}
	}

}
