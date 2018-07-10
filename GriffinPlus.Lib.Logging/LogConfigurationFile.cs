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
using System.Text;
using System.Text.RegularExpressions;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// An ini-style configuration file that keeps the log configuration.
	/// </summary>
	public partial class LogConfigurationFile
	{
		private const string SECTION_NAME_SETTINGS = "Settings";
		private const string SECTION_NAME_PROCESSING_PIPELINE_STAGE = "ProcessingPipelineStage";
		private const string SECTION_NAME_LOGWRITER = "LogWriter";
		private const string PROPERTY_NAME_APPLICATION_NAME = "ApplicationName";
		private const string PROPERTY_NAME_WILDCARD_PATTERN = "WildcardPattern";
		private const string PROPERTY_NAME_REGEX_PATTERN = "RegexPattern";
		private const string PROPERTY_NAME_LEVEL = "Level";
		private const string PROPERTY_NAME_INCLUDE = "Include";
		private const string PROPERTY_NAME_EXCLUDE = "Exclude";

		private readonly static string[] sHeaderComment =
		{
			"; ------------------------------------------------------------------------------",
			"; Configuration of the Logging Subsystem",
			"; ------------------------------------------------------------------------------",
			"; This file configures the logging subsystem that is encorporated in the",
			"; application concerned. Each and every executable that makes use of the logging",
			"; subsystem has its own configuration file (extension: .logconf) that is located",
			"; beside the application's executable. The configuration is structured like an",
			"; ini-file, i.e. it consists of sections and properties. A section defines a",
			"; configuration scope while properties contain the actual settings within a",
			"; section.",
			"; ------------------------------------------------------------------------------",
		};

		private readonly static string[] sGlobalSettingsComment =
		{
			"; ------------------------------------------------------------------------------",
			"; Global Settings",
			"; ------------------------------------------------------------------------------"
		};

		private readonly static string[] sProcessingPipelineStageSettingsComment =
		{
			"; ------------------------------------------------------------------------------",
			"; Processing Pipeline Stage Settings",
			"; ------------------------------------------------------------------------------"
		};

		private readonly static string[] sLogWriterConfigurationComment =
		{
			"; ------------------------------------------------------------------------------",
			"; Log Writer Settings",
			"; ------------------------------------------------------------------------------",
			"; The log writer configuration may consist of multiple [LogWriter] sections",
			"; defining active log levels for log writers with a name matching the specified",
			"; pattern. The pattern can be expressed as a wildcard pattern ('WildcardPattern'",
			"; property) or as a regular expression ('RegexPattern' property). Multiple",
			"; [LogWriter] sections are evaluated top-down. The first matching section",
			"; defines the behavior of the log writer. Therefore a default settings section",
			"; matching all log writers should be specified last.",
			";",
			"; The logging module comes with a couple of predefined log levels expressing a",
			"; wide range of severities:",
			"; - Failure          (most severe)",
			"; - Error                  .",
			"; - Warning                .",
			"; - Note                   .",
			"; - Developer              .",
			"; - Trace0                 .",
			"; - Trace[1..18]           .",
			"; - Trace19          (least severe)",
			";",
			"; Furthermore aspect log levels can be used to keep log messages belonging to",
			"; a certain subject together. This is especially useful when multiple log",
			"; writers contribute log messages to the subject. Aspect log levels enlarge the",
			"; list of log levels shown above and can be used just as the predefined log",
			"; levels.",
			";",
			"; The 'Level' property defines a base log level for matching log writers, i.e.",
			"; setting 'Level' to 'Note' tells the log writer to write log messages with",
			"; at least log level 'Note', e.g. 'Note', 'Warning', 'Error' and 'Failure'.",
			"; Do not use aspect log levels here, since the order of aspect log levels is",
			"; not deterministic, especially in multi-threaded environments.",
			";",
			"; The 'Include' property allows including certain log levels that are not",
			"; covered by the 'Level' property. Multiple log levels can be separated by",
			"; commas. Alternatively multiple 'Include' properties can be used.",
			";",
			"; The 'Exclude' property has the opposite effect. It tells the log writer to",
			"; keep log messages with a certain log level out of the log. Multiple log",
			"; levels can be separated by commas. Alternatively multiple 'Exclude' properties",
			"; can be used.",
			"; ------------------------------------------------------------------------------",
		};

		private static Regex sCommentRegex = new Regex(
			@"^\s*;(.*)",
			RegexOptions.Compiled);

		private static Regex sSectionRegex = new Regex(
			@"^\s*\[([-_:\. \w]+)\](.*)",
			RegexOptions.Compiled);

		private static Regex sKeyValueRegex = new Regex(
			@"^\s*(\w[-_\. \w]+?)\s*=\s*([^;]*?)\s*(;.*)?$",
			RegexOptions.Compiled);

		private static Regex sProcessingPipelineStageSectionRegex = new Regex(
			"^" + Regex.Escape(SECTION_NAME_PROCESSING_PIPELINE_STAGE + ":") + @"\s*([-_\. \w]+)\s*$",
			RegexOptions.Compiled);

		private Dictionary<string, string> mGlobalSettings;
		private Dictionary<string, Dictionary<string, string>> mProcessingPipelineStageSettings;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogConfigurationFile"/> class.
		/// </summary>
		public LogConfigurationFile()
		{
			mGlobalSettings = new Dictionary<string, string>();
			mProcessingPipelineStageSettings = new Dictionary<string, Dictionary<string, string>>();

			// init default settings
			ApplicationName = AppDomain.CurrentDomain.FriendlyName;
			LogWriterSettings.Add(new LogConfiguration.LogWriter()); // LogWriter comes with defaults...
		}

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public string ApplicationName
		{
			get
			{
				return mGlobalSettings[PROPERTY_NAME_APPLICATION_NAME];
			}

			set
			{
				if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Invalid application name.");
				mGlobalSettings[PROPERTY_NAME_APPLICATION_NAME] = value;
			}
		}

		/// <summary>
		/// Gets the list of log writer settings.
		/// </summary>
		public List<LogConfiguration.LogWriter> LogWriterSettings { get; } = new List<LogConfiguration.LogWriter>();

		/// <summary>
		/// Gets the settings dictionary for the processing pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the processing pipeline stage to get the settings for.</param>
		/// <returns>
		/// The requested settings dictionary;
		/// null, if the settings dictionary does not exist, yet.
		/// </returns>
		public IDictionary<string, string> GetProcessingPipelineStageSettings(string name)
		{
			Dictionary<string, string> settings;
			if (mProcessingPipelineStageSettings.TryGetValue(name, out settings)) {
				return new Dictionary<string, string>(settings);
			}

			return null;
		}

		/// <summary>
		/// Sets the settings dictionary for the processing pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the processing pipeline stage to set the settings for.</param>
		/// <param name="settings">Processing pipeline settings to set.</param>
		public void SetProcessingPipelineStageSettings(string name, IDictionary<string, string> settings)
		{
			mProcessingPipelineStageSettings[name] = new Dictionary<string, string>(settings);
		}

		/// <summary>
		/// Clears the configuration file.
		/// </summary>
		public void Clear()
		{
			LogWriterSettings.Clear();
			mProcessingPipelineStageSettings.Clear();
		}

		/// <summary>
		/// Loads the configuration file at the specified path.
		/// </summary>
		/// <param name="path">Path of the configuration file to load.</param>
		public static LogConfigurationFile LoadFrom(string path)
		{
			LogConfigurationFile file = new LogConfigurationFile();
			file.Clear();

			using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (StreamReader reader = new StreamReader(fs, true))
			{
				file.Read(reader);
			}

			return file;
		}

		/// <summary>
		/// Reads the configuration using the specified text reader.
		/// </summary>
		/// <param name="reader">Text reader to use.</param>
		private void Read(TextReader reader)
		{
			int lineNumber = 0;

			string currentSectionName = null;
			LogConfiguration.LogWriter logWriter = null;
			Dictionary<string, string> currentSettings = null;

			while (true)
			{
				string line = reader.ReadLine();
				if (line == null) break;
				lineNumber++;

				// skip empty line
				line = line.Trim();
				if (line.Length == 0) continue;

				// a section?
				var match = sSectionRegex.Match(line);
				if (match.Success)
				{
					string section = match.Groups[1].Value;
					string remaining = match.Groups[2].Value;

					// only a comment may be following
					if (remaining.Length > 0 && !sCommentRegex.IsMatch(remaining))
					{
						throw new LoggingException("Syntax error in line {0}.", lineNumber);
					}

					// close started section
					logWriter = null;
					currentSettings = null;

					if (section == SECTION_NAME_SETTINGS)
					{
						// the [Settings] section
						currentSettings = mGlobalSettings;
						continue;
					}
					else if (section == SECTION_NAME_LOGWRITER)
					{
						// a [LogWriter] section
						logWriter = new LogConfiguration.LogWriter();
						LogWriterSettings.Add(logWriter);
						continue;
					}

					// settings for a pipeline stage?
					match = sProcessingPipelineStageSectionRegex.Match(section);
					if (match.Success)
					{
						string stageName = match.Groups[1].Value;
						currentSettings = new Dictionary<string, string>();
						mProcessingPipelineStageSettings[stageName] = currentSettings;
						continue;
					}

					throw new LoggingException("Syntax error in line {0}", lineNumber);
				}

				// a key/value pair?
				match = sKeyValueRegex.Match(line);
				if (match.Success)
				{
					string key = match.Groups[1].Value;
					string value = match.Groups[2].Value;
					string remaining = match.Groups[3].Value;

					// only a comment may be following
					if (remaining.Length > 0 && !sCommentRegex.IsMatch(remaining))
					{
						throw new LoggingException("Syntax error in line {0}.", lineNumber);
					}

					if (logWriter != null)
					{
						// setting belongs to a log writer configuration
						// (WildcardPattern, RegexPattern, Level, Include, Exclude)
						if (key == PROPERTY_NAME_WILDCARD_PATTERN)
						{
							logWriter.Pattern = new LogConfiguration.WildcardLogWriterPattern(value);
							continue;
						}
						else if (key == PROPERTY_NAME_REGEX_PATTERN)
						{
							logWriter.Pattern = new LogConfiguration.RegexLogWriterPattern(value);
							continue;
						}
						else if (key == PROPERTY_NAME_LEVEL)
						{
							logWriter.BaseLevel = value;
							continue;
						}
						else if (key == PROPERTY_NAME_INCLUDE)
						{
							string[] levels = value.Split(',').Select(x => x.Trim()).ToArray();
							logWriter.Includes.AddRange(levels);
							continue;
						}
						else if (key == PROPERTY_NAME_EXCLUDE)
						{
							string[] levels = value.Split(',').Select(x => x.Trim()).ToArray();
							logWriter.Excludes.AddRange(levels);
							continue;
						}
						else
						{
							throw new LoggingException("Unexpected property name in section '{0}' (line: {1}).", currentSectionName, lineNumber);
						}
					}
					else if (currentSettings != null)
					{
						// dictionary backed settings
						currentSettings[key] = value;
						continue;
					}

					throw new LoggingException("Syntax error in line {0}", lineNumber);
				}

				// a comment line?
				if (sCommentRegex.IsMatch(line))
				{
					continue;
				}

				// syntax error
				throw new LoggingException("Syntax error in line {0}.", lineNumber);
			}
		}

		/// <summary>
		/// Saves the configuration file.
		/// </summary>
		/// <param name="path">Path of the configuration file to save.</param>
		public void Save(string path)
		{
			using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
			using (StreamWriter writer = new StreamWriter(fs, new UTF8Encoding(true)))
			{
				Write(writer);
			}
		}

		/// <summary>
		/// Writes the configuration using the specified text writer.
		/// </summary>
		/// <param name="writer">Text writer to use.</param>
		private void Write(TextWriter writer)
		{
			// overview
			foreach (string line in sHeaderComment) {
				writer.WriteLine(line);
			}

			// global settings
			if (mGlobalSettings.Count > 0)
			{
				writer.WriteLine();
				foreach (string line in sGlobalSettingsComment) writer.WriteLine(line);
				writer.WriteLine();
				writer.WriteLine("[{0}]", SECTION_NAME_SETTINGS);
				foreach (var kvp in mGlobalSettings.OrderBy(x => x.Key))
				{
					writer.WriteLine("{0} = {1}", kvp.Key.Trim(), kvp.Value.Trim());
				}
			}

			// processing pipeline stage settings
			if (mProcessingPipelineStageSettings.Count > 0)
			{
				writer.WriteLine();
				foreach (string line in sProcessingPipelineStageSettingsComment) writer.WriteLine(line);
				writer.WriteLine();
				foreach (var stage in mProcessingPipelineStageSettings)
				{
					if (stage.Value.Count > 0)
					{
						writer.WriteLine();
						writer.WriteLine("[{0}:{1}]", SECTION_NAME_PROCESSING_PIPELINE_STAGE, stage.Key);

						foreach (var kvp in stage.Value.OrderBy(x => x.Key))
						{
							writer.WriteLine("{0} = {1}", kvp.Key, kvp.Value);
						}
					}
				}
			}

			// log writer settings
			if (LogWriterSettings.Count > 0)
			{
				writer.WriteLine();
				foreach (string line in sLogWriterConfigurationComment) writer.WriteLine(line);
				foreach (LogConfiguration.LogWriter logWriter in LogWriterSettings)
				{
					writer.WriteLine();
					writer.WriteLine("[{0}]", SECTION_NAME_LOGWRITER);

					if (logWriter.Pattern is LogConfiguration.WildcardLogWriterPattern)
					{
						writer.WriteLine("{0} = {1}", PROPERTY_NAME_WILDCARD_PATTERN, logWriter.Pattern.Pattern);
					}
					else if (logWriter.Pattern is LogConfiguration.RegexLogWriterPattern)
					{
						writer.WriteLine("{0} = {1}", PROPERTY_NAME_REGEX_PATTERN, logWriter.Pattern.Pattern);
					}

					writer.WriteLine("{0} = {1}", PROPERTY_NAME_LEVEL, logWriter.BaseLevel);

					var includes = logWriter.Includes.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
					if (includes.Length > 0)
					{
						writer.WriteLine("{0} = {1}", PROPERTY_NAME_INCLUDE, string.Join(", ", includes));
					}

					var excludes = logWriter.Excludes.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
					if (excludes.Length > 0)
					{
						writer.WriteLine("{0} = {1}", PROPERTY_NAME_EXCLUDE, string.Join(", ", excludes));
					}
				}
			}
		}
	}
}
