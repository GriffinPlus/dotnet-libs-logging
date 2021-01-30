///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging.Collections
{

	/// <summary>
	/// Helper class that assists with creating temporary files and cleaning them up.
	/// </summary>
	static class TemporaryFileManager
	{
		/// <summary>
		/// Regex matching the name of a temporary log file that should be deleted automatically when not used any more.
		/// </summary>
		private static readonly Regex sAutoDeleteFileRegex = new Regex(
			@"^\[LOG-BUFFER\] (?<guid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}) \[AUTO DELETE\]$",
			RegexOptions.Compiled);

		/// <summary>
		/// Creates a new instance of the <see cref="FileBackedLogMessageCollection"/> with a file in the temporary directory
		/// optionally marking the file for auto-deletion.
		/// </summary>
		/// <param name="deleteAutomatically">
		/// true to delete the file automatically when the collection is disposed (or the next time, a temporary collection is created in the same directory);
		/// false to keep it after the collection is disposed.
		/// </param>
		/// <param name="temporaryDirectoryPath">
		/// Path of the temporary directory to use;
		/// null to use the default temporary directory (default).
		/// </param>
		/// <returns>The full path of a non-existent file in the temporary directory.</returns>
		public static string GetTemporaryFileName(bool deleteAutomatically, string temporaryDirectoryPath = null)
		{
			// init temporary directory path, if not specified explicitly
			if (temporaryDirectoryPath == null) temporaryDirectoryPath = Path.GetTempPath();

			// delete temporary files that are not needed any more
			CleanupTemporaryDirectory(temporaryDirectoryPath);

			// create a collection with a temporary database backing the collection
			string path = Path.Combine(temporaryDirectoryPath, "[LOG-BUFFER] " + Guid.NewGuid().ToString("D").ToUpper());
			if (deleteAutomatically) path += " [AUTO DELETE]";
			return path;
		}

		/// <summary>
		/// Scans the specified directory for orphaned temporary files that are marked for auto-deletion, but have not been deleted, yet.
		/// </summary>
		/// <param name="directoryPath">Path of the directory to scan.</param>
		private static void CleanupTemporaryDirectory(string directoryPath)
		{
			try
			{
				foreach (string filePath in Directory.GetFiles(directoryPath))
				{
					string fileName = Path.GetFileName(filePath);
					var match = sAutoDeleteFileRegex.Match(fileName);
					if (match.Success)
					{
						try { File.Delete(filePath); }
						catch
						{
							/* swallow */
						}
					}
				}
			}
			catch
			{
				// some error regarding the directory itself occurred
				// => swallow
			}
		}
	}

}
