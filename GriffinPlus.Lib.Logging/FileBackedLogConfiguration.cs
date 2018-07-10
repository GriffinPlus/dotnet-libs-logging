///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
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
	/// A log configuration with file backing (ini-style configuration file).
	/// </summary>
	/// <remarks>
	/// This class is thread-safe as working data is always replaced atomically.
	/// </remarks>
	public class FileBackedLogConfiguration : ILogConfiguration, IDisposable
	{
		/// <summary>
		/// The default path of the log configuration file.
		/// </summary>
		private static readonly string sDefaultConfigFilePath = Path.Combine(
			Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
			Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location) + ".logconf");

		private static readonly Logging.LogWriter sLog = Log.GetWriter("Logging");
		private FileSystemWatcher mFileSystemWatcher;
		private Timer mReloadingTimer;
		private LogConfigurationFile mFile;
		private string mFilePath;
		private string mFileName;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedLogConfiguration"/> class
		/// (the configuration file is located beside the entry assembly named as the entry assembly plus extension '.logconf').
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
			mFilePath = Path.GetFullPath(path);
			mFileName = Path.GetFileName(path);

			// load configuration file
			try
			{
				mFile = LogConfigurationFile.LoadFrom(mFilePath);
			}
			catch (FileNotFoundException)
			{
				// file does not exist
				// => that's ok, use a default configuration file...
				mFile = new LogConfigurationFile();
			}
			catch (Exception ex)
			{
				// loading file failed
				sLog.ForceWrite(
					LogLevel.Failure,
					"Loading log configuration file ({0}) failed. Exception: {1}",
					mFilePath, ex);
			}

			// set up the file system watcher to get notified of changes to the file
			// (notifications about the creation of files with zero-length do not contain
			// valuable information, so renaming/deleting is sufficient)
			mFileSystemWatcher = new FileSystemWatcher();
			mFileSystemWatcher.Path = Path.GetDirectoryName(path);
			mFileSystemWatcher.Filter = "*" + Path.GetExtension(mFileName);
			mFileSystemWatcher.Changed += EH_FileSystemWatcher_Changed;
			mFileSystemWatcher.Deleted += EH_FileSystemWatcher_Removed;
			mFileSystemWatcher.Renamed += EH_FileSystemWatcher_Renamed;
			mFileSystemWatcher.EnableRaisingEvents = true;

			// set up timer that will handle reloading the configuration file
			mReloadingTimer = new Timer(TimerProc, null, -1, -1); // do not start immediately
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

		/// <summary>
		/// Gets the full path of the configuration file.
		/// </summary>
		public string FullPath
		{
			get { return mFilePath; }
		}

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public string ApplicationName
		{
			get { return mFile.ApplicationName; }
			set { mFile.ApplicationName = value; }
		}

		/// <summary>
		/// Is called by the file system watcher when a file changes in the watched directory.
		/// </summary>
		/// <param name="sender">The file system watcher.</param>
		/// <param name="e">Event arguments.</param>
		private void EH_FileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
		{
			// called by worker thread...
			if (IsConfigurationFile(e.Name))
			{
				// configuration file is present now
				// => schedule loading the file...
				mReloadingTimer.Change(500, -1);
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
			if (IsConfigurationFile(e.Name))
			{
				// configuration file was removed
				// => create a default configuration...
				LogConfigurationFile file = new LogConfigurationFile();
				mFile = file;
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
				LogConfigurationFile file = new LogConfigurationFile();
				mFile = file;
			}
		}

		/// <summary>
		/// Entrypoint for the timer reloading the configuration file.
		/// </summary>
		/// <param name="state">Some state object (not used).</param>
		private void TimerProc(object state)
		{
			try
			{
				// load file (always replace mFile, do not modify existing instance for threading reasons)
				mFile = LogConfigurationFile.LoadFrom(mFilePath);
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

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public LogLevelBitMask GetActiveLogLevelMask(LogWriter writer)
		{
			// get the first matching log writer settings
			var settings = mFile.LogWriterSettings
				.Where(x => x.Pattern.Regex.IsMatch(writer.Name))
				.FirstOrDefault();

			if (settings != null)
			{
				LogLevelBitMask mask;

				// enable all log levels that are covered by the base level
				LogLevel level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
				if (level == LogLevel.All) {
					mask = new LogLevelBitMask(LogLevel.MaxId + 1, true, false);
				} else {
					mask = new LogLevelBitMask(LogLevel.MaxId + 1, false, false);
					mask.SetBits(0, level.Id + 1);
				}

				// add log levels explicitly included
				foreach (var include in settings.Includes)
				{
					level = LogLevel.GetAspect(include);
					mask.SetBit(level.Id);
				}

				// disable log levels explicitly excluded
				foreach (var exclude in settings.Excludes)
				{
					level = LogLevel.GetAspect(exclude);
					mask.ClearBit(level.Id);
				}

				return mask;
			}
			else
			{
				// no matching settings found
				// => disable all log levels...
				return new LogLevelBitMask(0, false, false);
			}
		}

		/// <summary>
		/// Gets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to get the settings for.</param>
		/// <returns>The requested settings.</returns>
		public IDictionary<string, string> GetProcessingPipelineStageSettings(string name)
		{
			return mFile.GetProcessingPipelineStageSettings(name); // returns a copy of the internal settings
		}

		/// <summary>
		/// Sets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to set the settings for.</param>
		/// <param name="settings">Settings to set.</param>
		public void SetProcessingPipelineStageSettings(string name, IDictionary<string, string> settings)
		{
			mFile.SetProcessingPipelineStageSettings(name, settings);
		}

		/// <summary>
		/// Saves the configuration.
		/// </summary>
		public void Save()
		{
			mFile.Save(mFilePath);
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

	}
}
