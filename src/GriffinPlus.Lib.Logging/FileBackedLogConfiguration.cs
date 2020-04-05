///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
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
	/// A log configuration with ini-style file backing (thread-safe).
	/// </summary>
	public class FileBackedLogConfiguration : ILogConfiguration, IDisposable
	{
		/// <summary>
		/// The default path of the log configuration file.
		/// </summary>
		private static readonly string sDefaultConfigFilePath;
		private static readonly LogWriter sLog = Log.GetWriter("Logging");
		private readonly object mSync = new object();
		private FileSystemWatcher mFileSystemWatcher;
		private Timer mReloadingTimer;
		private LogConfigurationFile mFile;
		private readonly string mFilePath;
		private readonly string mFileName;

		/// <summary>
		/// Initializes the <see cref="FileBackedLogConfiguration"/> class.
		/// </summary>
		static FileBackedLogConfiguration()
		{
			// initialize the path of the configuration file
			Assembly assembly = Assembly.GetEntryAssembly();
			if (assembly != null)
			{
				// regular case
				// => use name of the entry assembly (application)
				string fileName = Path.GetFileNameWithoutExtension(assembly.Location) + ".logconf";
				sDefaultConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
			}
			else
			{
				// no entry assembly (most probably a unit test runner)
				// => use friendly name of the application domain
				string fileName = AppDomain.CurrentDomain.FriendlyName + ".logconf";
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
			lock (mSync)
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
		public string FullPath => mFilePath;

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public string ApplicationName
		{
			get
			{
				lock (mSync)
				{
					return mFile.ApplicationName;
				}
			}
			
			set
			{
				lock (mSync)
				{
					mFile.ApplicationName = value;
				}
			}
		}

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public IList<LogConfiguration.LogWriter> GetLogWriterSettings()
		{
			lock (mSync)
			{
				return new List<LogConfiguration.LogWriter>(mFile.LogWriterSettings);
			}
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public void SetLogWriterSettings(IEnumerable<LogConfiguration.LogWriter> settings)
		{
			lock (mSync)
			{
				mFile.LogWriterSettings.Clear();
				mFile.LogWriterSettings.AddRange(settings);
			}
		}

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public void SetLogWriterSettings(params LogConfiguration.LogWriter[] settings)
		{
			lock (mSync)
			{
				mFile.LogWriterSettings.Clear();
				mFile.LogWriterSettings.AddRange(settings);
			}
		}

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public BitMask GetActiveLogLevelMask(LogWriter writer)
		{
			// get the first matching log writer settings
			var settings = mFile.LogWriterSettings.FirstOrDefault(x => x.Pattern.Regex.IsMatch(writer.Name));

			if (settings != null)
			{
				BitMask mask;

				// enable all log levels that are covered by the base level
				LogLevel level = LogLevel.GetAspect(settings.BaseLevel); // returns predefined log levels as well
				if (level == LogLevel.All) {
					mask = new BitMask(LogLevel.MaxId + 1, true, false);
				} else {
					mask = new BitMask(LogLevel.MaxId + 1, false, false);
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
				return new BitMask(0, false, false);
			}
		}

		/// <summary>
		/// Gets the settings for pipeline stages by their name.
		/// </summary>
		/// <returns>The requested settings.</returns>
		public IDictionary<string, IDictionary<string, string>> GetProcessingPipelineStageSettings()
		{
			lock (mSync)
			{
				// return a copy of the settings to avoid uncontrolled modifications
				IDictionary<string, IDictionary<string, string>> copy = new Dictionary<string, IDictionary<string, string>>();
				foreach (var kvp in mFile.ProcessingPipelineStageSettings) {
					copy.Add(kvp.Key, new Dictionary<string, string>(kvp.Value));
				}

				return copy;
			}
		}

		/// <summary>
		/// Gets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to get the settings for.</param>
		/// <returns>
		/// The requested settings;
		/// null, if the settings do not exist.</returns>
		public IDictionary<string, string> GetProcessingPipelineStageSettings(string name)
		{
			lock (mSync)
			{
				// return a copy of the settings to avoid uncontrolled modifications
				if (mFile.ProcessingPipelineStageSettings.TryGetValue(name, out var settings)) {
					return new Dictionary<string, string>(settings);
				}

				return null;
			}
		}

		/// <summary>
		/// Sets the settings for the pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage to set the settings for.</param>
		/// <param name="settings">Settings to set.</param>
		public void SetProcessingPipelineStageSettings(string name, IDictionary<string, string> settings)
		{
			lock (mSync)
			{
				mFile.ProcessingPipelineStageSettings[name] = new Dictionary<string, string>(settings);
			}
		}

		/// <summary>
		/// Saves the configuration.
		/// </summary>
		public void Save()
		{
			lock (mSync)
			{
				mFile.Save(mFilePath);
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
			lock (mSync)
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
			lock (mSync)
			{
				if (mReloadingTimer == null) return; // object has been disposed

				if (IsConfigurationFile(e.Name))
				{
					// configuration file was removed
					// => create a default configuration...
					LogConfigurationFile file = new LogConfigurationFile();
					mFile = file;
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
			lock (mSync)
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
					LogConfigurationFile file = new LogConfigurationFile();
					mFile = file;
				}
			}
		}

		/// <summary>
		/// Entry point for the timer reloading the configuration file.
		/// </summary>
		/// <param name="state">Some state object (not used).</param>
		private void TimerProc(object state)
		{
			lock (mSync)
			{
				if (mReloadingTimer == null) return; // object has been disposed

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
		}

	}
}
