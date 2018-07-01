﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log level (or aspect) that indicates the severity of a log message.
	/// </summary>
	public class LogLevel
	{
		/// <summary>
		/// Failure:
		/// The log message is about a severe error condition that threatens the system's stability.
		/// </summary>
		static public readonly LogLevel Failure = new LogLevel("Failure");

		/// <summary>
		/// Error:
		/// The log message is about a "normal" error condition.
		/// </summary>
		static public readonly LogLevel Error = new LogLevel("Error");

		/// <summary>
		/// Warning:
		/// The log message is not an error condition, but something a user should keep an eye on.
		/// </summary>
		static public readonly LogLevel Warning = new LogLevel("Warning");

		/// <summary>
		/// Note:
		/// The log message is a note a regular user should see.
		/// </summary>
		static public readonly LogLevel Note = new LogLevel("Note");

		/// <summary>
		/// Developer:
		/// A log message only developers should see.
		/// </summary>
		static public readonly LogLevel Developer = new LogLevel("Developer");

		/// <summary>
		/// Trace0:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace0 = new LogLevel("Trace0");

		/// <summary>
		/// Trace1:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace1 = new LogLevel("Trace1");

		/// <summary>
		/// Trace2:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace2 = new LogLevel("Trace2");

		/// <summary>
		/// Trace3:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace3 = new LogLevel("Trace3");
		
		/// <summary>
		/// Trace4:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace4 = new LogLevel("Trace4");

		/// <summary>
		/// Trace5:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace5 = new LogLevel("Trace5");

		/// <summary>
		/// Trace6:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace6  = new LogLevel("Trace6");

		/// <summary>
		/// Trace7:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace7 = new LogLevel("Trace7");

		/// <summary>
		/// Trace8:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace8 = new LogLevel("Trace8");

		/// <summary>
		/// Trace9:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace9 = new LogLevel("Trace9");

		/// <summary>
		/// Trace10:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace10 = new LogLevel("Trace10");

		/// <summary>
		/// Trace11:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace11 = new LogLevel("Trace11");

		/// <summary>
		/// Trace12:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace12 = new LogLevel("Trace12");

		/// <summary>
		/// Trace13:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace13 = new LogLevel("Trace13");

		/// <summary>
		/// Trace14:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace14 = new LogLevel("Trace14");

		/// <summary>
		/// Trace15:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace15 = new LogLevel("Trace15");

		/// <summary>
		/// Trace16:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace16 = new LogLevel("Trace16");

		/// <summary>
		/// Trace17:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace17 = new LogLevel("Trace17");

		/// <summary>
		/// Trace18:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace18 = new LogLevel("Trace18");

		/// <summary>
		/// Trace19:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		static public readonly LogLevel Trace19 = new LogLevel("Trace19");

		/// <summary>
		/// Timing:
		/// An aspect that is used when timing is concerned.
		/// </summary>
		static public readonly LogLevel Timing = new LogLevel("Timing");

		/// <summary>
		/// All:
		/// Special log level expressing the highest possible threshold for filtering purposes (all log levels pass the filter).
		/// </summary>
		static public readonly LogLevel All = new LogLevel("All", int.MaxValue);

		private static LogLevel[] sPredefinedLogLevels = new LogLevel[] {
			Failure, Error, Warning, Note, Developer,
			Trace0, Trace1, Trace2, Trace3, Trace4, Trace5, Trace6, Trace7, Trace8, Trace9, Trace10, Trace11, Trace12, Trace13, Trace14, Trace15, Trace16, Trace17, Trace18, Trace19,
			Timing
		};

		private static Dictionary<int,LogLevel> sLogLevelsById;
		private static SortedDictionary<string,LogLevel> sLogLevelsByName;
		private static int sNextId = 0;

		internal readonly int mId;
		internal readonly string mName;

		/// <summary>
		/// Initializes the <see cref="LogLevel"/> class.
		/// </summary>
		static LogLevel()
		{
			// populate log level collections with predefined log levels
			sLogLevelsById = new Dictionary<int,LogLevel>();
			sLogLevelsByName = new SortedDictionary<string,LogLevel>();
			sLogLevelsByName.Add(All.Name, All);
			sLogLevelsById.Add(All.Id, All);
			foreach (LogLevel level in sPredefinedLogLevels) {
				sLogLevelsByName.Add(level.Name, level);
				sLogLevelsById.Add(level.Id, level);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogLevel"/> class (assigns a log level id ascendingly).
		/// </summary>
		/// <param name="name">Name of the log level.</param>
		private LogLevel(string name) 
		{
			// global logging lock is acquired here...
			mName = name;
			mId = sNextId++;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogLevel"/> class (assigns a specific log level id).
		/// </summary>
		/// <param name="name">Name of the log level.</param>
		/// <param name="id">Id of the log level.</param>
		private LogLevel(string name, int id) 
		{
			mName = name;
			mId = id;
		}

		/// <summary>
		/// Gets predefined log levels (all log levels that are not an aspect).
		/// </summary>
		public static IEnumerable<LogLevel> PredefinedLogLevels
		{
			get {
				return sPredefinedLogLevels;
			}
		}

		/// <summary>
		/// Gets the name of the log level.
		/// </summary>
		public string Name
		{
			get {
				return mName;
			}
		}

		/// <summary>
		/// Gets the id of the log level.
		/// </summary>
		public int Id
		{
			get {
				return mId;
			}
		}

		/// <summary>
		/// Gets the aspect log level with the specified name (or creates a new one, if it does not exist, yet).
		/// </summary>
		/// <param name="name">Name of the aspect log level to get.</param>
		/// <returns>The requested aspect log level.</returns>
		public static LogLevel GetAspect(string name)
		{
			LogLevel level;

			try {} finally  // prevents ThreadAbortException from disrupting the following block
			{
				LogSource.Lock.EnterReadLock();
				try {
					sLogLevelsByName.TryGetValue(name, out level);
				} finally {
					LogSource.Lock.ExitReadLock();
				}

				if (level == null)
				{
					LogSource.Lock.EnterWriteLock();
					try {
						if (!sLogLevelsByName.TryGetValue(name, out level)) {
							level = new LogLevel(name);
							sLogLevelsByName.Add(level.Name, level);
							sLogLevelsById.Add(level.Id, level);
						}
					} finally {
						LogSource.Lock.ExitWriteLock();
					}
				}
			}

			return level;
		}

		/// <summary>
		/// Gets the string representation of the current log level.
		/// </summary>
		/// <returns>String representation of the current log level.</returns>
		public override string ToString()
		{
			return string.Format("{0} ({1})", mName, mId);
		}

	}
}