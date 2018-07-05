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
using System.IO;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// The default log source configuration (an ini-file like configuration file).
	/// </summary>
	public class DefaultLogSourceConfiguration : ILogSourceConfiguration, IDisposable
	{
		private LogWriter sLog = LogSource.GetWriter("Logging");
		private FileSystemWatcher mFileSystemWatcher;
		private LogSourceConfigurationFile mFile;
		private string mFilePath;
		private string mFileName;

		/// <summary>
		/// Initializes a new instance of the <see cref="DefaultLogSourceConfiguration"/> class.
		/// </summary>
		/// <param name="path">Path of the configuration file to use.</param>
		public DefaultLogSourceConfiguration(string path)
		{
			mFilePath = Path.GetFullPath(path);
			mFileName = Path.GetFileName(path);

			// load configuration file
			try
			{
				mFile = LogSourceConfigurationFile.LoadFrom(mFilePath);
			}
			catch (FileNotFoundException)
			{
				// file does not exist
				// => that's ok, use a default configuration file...
				mFile = new LogSourceConfigurationFile();
			}
			catch (Exception ex)
			{
				// loading file failed
				sLog.ForceWrite(
					LogLevel.Failure,
					"Loading log source configuration file ({0}) failed. Exception: {1}",
					mFilePath, ex);
			}

			// set up the file system watcher to get notified of changes to the file
			// (notifications about the creation of files with zero-length do not contain
			// valuable information, so renaming/deleting is sufficient)
			mFileSystemWatcher = new FileSystemWatcher();
			mFileSystemWatcher.Path = Path.GetDirectoryName(path);
			mFileSystemWatcher.Renamed += EH_FileSystemWatcher_Renamed;
			mFileSystemWatcher.Deleted += EH_FileSystemWatcher_Deleted;
			mFileSystemWatcher.EnableRaisingEvents = true;
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
			if (mFileSystemWatcher != null)
			{
				mFileSystemWatcher.Dispose();
				mFileSystemWatcher = null;
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
				// => load it...
				try
				{
					mFile = LogSourceConfigurationFile.LoadFrom(e.FullPath);
				}
				catch (Exception ex)
				{
					sLog.ForceWrite(LogLevel.Error, "Reloading configuration file failed. Exception: {0}", ex);
				}
			}
			else if (vanished)
			{
				// configuration file was removed
				// => create a default configuration...
				LogSourceConfigurationFile file = new LogSourceConfigurationFile();
				mFile = file;
			}
		}

		/// <summary>
		/// Is called by the file system watcher when a file is deleted in the watched directory.
		/// </summary>
		/// <param name="sender">The file system watcher.</param>
		/// <param name="e">Event arguments.</param>
		private void EH_FileSystemWatcher_Deleted(object sender, FileSystemEventArgs e)
		{
			// called by worker thread...
			if (IsConfigurationFile(e.Name))
			{
				// configuration file was removed
				// => create a default configuration...
				LogSourceConfigurationFile file = new LogSourceConfigurationFile();
				mFile = file;
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
			return new LogLevelBitMask(0, true, true);
		}

		/// <summary>
		/// Saves the configuration.
		/// </summary>
		public void Save()
		{
			mFile.Save(mFilePath);
		}

		/// <summary>
		/// Checks whether the specified file name is the name of the configuration file of the log source.
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
