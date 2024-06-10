///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A log configuration with ini-style file backing (thread-safe).
/// </summary>
public class FileBackedLogConfiguration : LogConfiguration<FileBackedLogConfiguration>
{
	/// <summary>
	/// The default path of the log configuration file.
	/// </summary>
	private static readonly string sDefaultConfigFilePath;

	private static readonly LogWriter sLog = LogWriter.Get("Logging");

	private readonly FileBackedProcessingPipelineConfiguration mProcessingPipelineConfiguration;
	private          LogConfigurationFile                      mFile;
	private          FileSystemWatcher                         mFileSystemWatcher;
	private          Timer                                     mReloadingTimer;
	private          string                                    mPath;
	private          string                                    mFullPath;
	private          string                                    mFileName;

	/// <summary>
	/// Initializes the <see cref="FileBackedLogConfiguration"/> class.
	/// </summary>
	static FileBackedLogConfiguration()
	{
		// initialize the path of the configuration file
		string fileName = Process.GetCurrentProcess().ProcessName + ".gplogconf";
		sDefaultConfigFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="FileBackedLogConfiguration"/> class (the configuration file is
	/// located in the application's base directory and named as the entry assembly plus extension '.gplogconf').
	/// The path of the file can be changed via <see cref="Path"/>.
	/// </summary>
	public FileBackedLogConfiguration()
	{
		// initialize the pipeline configuration part
		mProcessingPipelineConfiguration = new FileBackedProcessingPipelineConfiguration(this);

		// try to open the configuration file with the default name
		Path = sDefaultConfigFilePath;
	}

	/// <summary>
	/// Disposes the object cleaning up unmanaged resources
	/// </summary>
	/// <param name="disposing">
	/// <c>true</c> if called explicitly;<br/>
	/// <c>false</c> if called due to finalization.
	/// </param>
	protected override void Dispose(bool disposing)
	{
		CloseConfigurationFile();
	}

	/// <summary>
	/// Gets or sets the path of the configuration file.
	/// </summary>
	public string Path
	{
		get
		{
			lock (Sync) return mPath;
		}

		set
		{
			lock (Sync)
			{
				if (mPath != value)
				{
					mPath = value;
					mFullPath = System.IO.Path.GetFullPath(mPath);
					mFileName = System.IO.Path.GetFileName(mFullPath);
					OpenConfigurationFile();
				}
			}
		}
	}

	/// <summary>
	/// Gets the full path of the configuration file (please use <see cref="Path"/> to set the path).
	/// </summary>
	public string FullPath
	{
		get
		{
			lock (Sync) return mFullPath;
		}
	}

	/// <summary>
	/// Gets the wrapped log configuration file.
	/// </summary>
	internal LogConfigurationFile File
	{
		get
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			return mFile;
		}

		private set
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			if (mFile != value)
			{
				mFile = value;
				OnChanged();
			}
		}
	}

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
				if (File.ApplicationName != value)
				{
					File.ApplicationName = value;
					OnChanged();
				}
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
			OnChanged();
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
			LogWriterConfiguration settings = File
				.LogWriterSettings
				.Where(
					configuration => configuration
						.NamePatterns
						.Any(x => x.Regex.IsMatch(writer.Name)))
				.FirstOrDefault(
					configuration => !configuration.TagPatterns.Any() || configuration
						                 .TagPatterns
						                 .Any(x => writer.Tags.Any<string>(y => x.Regex.IsMatch(y))));

			if (settings != null)
			{
				LogLevelBitMask mask;

				// enable all log levels that are covered by the base level
				LogLevel level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
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
	/// <c>true</c> to include the default value of settings that have not been explicitly set;<br/>
	/// <c>false</c> to save only settings that have not been explicitly set.
	/// </param>
	public override void Save(bool includeDefaults = false)
	{
		lock (Sync)
		{
			// save the configuration file before making modifications
			LogConfigurationFile oldFile = File;

			try
			{
				if (includeDefaults)
				{
					// create a temporary configuration file and add the default value of the settings that don't
					// have an explicitly set value
					File = new LogConfigurationFile(oldFile);
					foreach (IProcessingPipelineStageConfiguration stageSettings in mProcessingPipelineConfiguration.Stages)
					{
						foreach (IUntypedProcessingPipelineStageSetting setting in stageSettings.Values)
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
							LogLevel.Error,
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
	/// (the comparison is case-insensitive).
	/// </summary>
	/// <param name="fileName">File name to check.</param>
	/// <returns>
	/// <c>true</c> if the file name is the name of the configuration file;<br/>
	/// otherwise <c>false</c>.
	/// </returns>
	private bool IsConfigurationFile(string fileName)
	{
		return StringComparer.InvariantCultureIgnoreCase.Compare(fileName, mFileName) == 0;
	}

	/// <summary>
	/// Tries to open the configured configuration file.
	/// </summary>
	private void OpenConfigurationFile()
	{
		lock (Sync)
		{
			// dispose resources associated with the loaded configuration file
			CloseConfigurationFile();

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
						LogLevel.Error,
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
				Path = System.IO.Path.GetDirectoryName(FullPath),
				Filter = "*" + System.IO.Path.GetExtension(mFileName)
			};
			mFileSystemWatcher.Changed += EH_FileSystemWatcher_Changed;
			mFileSystemWatcher.Deleted += EH_FileSystemWatcher_Removed;
			mFileSystemWatcher.Renamed += EH_FileSystemWatcher_Renamed;
			mFileSystemWatcher.EnableRaisingEvents = true;

			// set up timer that will handle reloading the configuration file
			mReloadingTimer = new Timer(TimerProc, null, -1, -1); // do not start immediately
		}
	}

	/// <summary>
	/// Closes the loaded configuration file.
	/// </summary>
	private void CloseConfigurationFile()
	{
		lock (Sync)
		{
			// stop reloading timer
			mReloadingTimer?.Dispose();
			mReloadingTimer = null;

			// stop watcher monitoring changes to the loaded configuration file
			mFileSystemWatcher?.Dispose();
			mFileSystemWatcher = null;
		}
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
				File = new LogConfigurationFile();
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
				File = new LogConfigurationFile();
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
