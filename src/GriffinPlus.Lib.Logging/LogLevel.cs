///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log level (or aspect) that indicates the severity of a log message (immutable).
	/// </summary>
	public class LogLevel
	{
		private static Dictionary<string, LogLevel> sLogLevelsByName;
		private static LogLevel[]                   sLogLevelsById;
		private static int                          sNextId;

		/// <summary>
		/// Failure:
		/// The log message is about a severe error condition that threatens the system's stability.
		/// </summary>
		public static readonly LogLevel Failure = new LogLevel("Failure");

		/// <summary>
		/// Error:
		/// The log message is about a "normal" error condition.
		/// </summary>
		public static readonly LogLevel Error = new LogLevel("Error");

		/// <summary>
		/// Warning:
		/// The log message is not an error condition, but something a user should keep an eye on.
		/// </summary>
		public static readonly LogLevel Warning = new LogLevel("Warning");

		/// <summary>
		/// Note:
		/// The log message is a note a regular user should see.
		/// </summary>
		public static readonly LogLevel Note = new LogLevel("Note");

		/// <summary>
		/// Developer:
		/// A log message only developers should see.
		/// </summary>
		public static readonly LogLevel Developer = new LogLevel("Developer");

		/// <summary>
		/// Trace0:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace0 = new LogLevel("Trace0");

		/// <summary>
		/// Trace1:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace1 = new LogLevel("Trace1");

		/// <summary>
		/// Trace2:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace2 = new LogLevel("Trace2");

		/// <summary>
		/// Trace3:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace3 = new LogLevel("Trace3");

		/// <summary>
		/// Trace4:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace4 = new LogLevel("Trace4");

		/// <summary>
		/// Trace5:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace5 = new LogLevel("Trace5");

		/// <summary>
		/// Trace6:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace6 = new LogLevel("Trace6");

		/// <summary>
		/// Trace7:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace7 = new LogLevel("Trace7");

		/// <summary>
		/// Trace8:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace8 = new LogLevel("Trace8");

		/// <summary>
		/// Trace9:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace9 = new LogLevel("Trace9");

		/// <summary>
		/// Trace10:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace10 = new LogLevel("Trace10");

		/// <summary>
		/// Trace11:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace11 = new LogLevel("Trace11");

		/// <summary>
		/// Trace12:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace12 = new LogLevel("Trace12");

		/// <summary>
		/// Trace13:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace13 = new LogLevel("Trace13");

		/// <summary>
		/// Trace14:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace14 = new LogLevel("Trace14");

		/// <summary>
		/// Trace15:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace15 = new LogLevel("Trace15");

		/// <summary>
		/// Trace16:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace16 = new LogLevel("Trace16");

		/// <summary>
		/// Trace17:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace17 = new LogLevel("Trace17");

		/// <summary>
		/// Trace18:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace18 = new LogLevel("Trace18");

		/// <summary>
		/// Trace19:
		/// A log message the implementer of the code might be interested in.
		/// </summary>
		public static readonly LogLevel Trace19 = new LogLevel("Trace19");

		/// <summary>
		/// Timing:
		/// An aspect that is used when timing is concerned.
		/// </summary>
		public static readonly LogLevel Timing = new LogLevel("Timing");

		/// <summary>
		/// None: Special log level expressing the lowest possible threshold for filtering purposes
		/// (no log level passes the filter).
		/// </summary>
		public static readonly LogLevel None = new LogLevel("None", -1);

		/// <summary>
		/// All: Special log level expressing the highest possible threshold for filtering purposes
		/// (all log levels pass the filter).
		/// </summary>
		public static readonly LogLevel All = new LogLevel("All", int.MaxValue);

		/// <summary>
		/// Gets the maximum id assigned to a log level.
		/// </summary>
		public static int MaxId => sNextId - 1;

		/// <summary>
		/// All predefined log levels (the index corresponds to the id of the log level).
		/// </summary>
		private static readonly LogLevel[] sPredefinedLogLevels =
		{
			Failure, Error, Warning, Note, Developer,
			Trace0, Trace1, Trace2, Trace3, Trace4, Trace5, Trace6, Trace7, Trace8, Trace9,
			Trace10, Trace11, Trace12, Trace13, Trace14, Trace15, Trace16, Trace17, Trace18, Trace19
		};

		/// <summary>
		/// Initializes the <see cref="LogLevel"/> class.
		/// </summary>
		static LogLevel()
		{
			// populate log level collections with predefined log levels
			sLogLevelsByName = new Dictionary<string, LogLevel>
			{
				{ None.Name, None },
				{ All.Name, All },
				{ Timing.Name, Timing }
			};

			foreach (var level in sPredefinedLogLevels)
			{
				sLogLevelsByName.Add(level.Name, level);
			}

			sLogLevelsById = sLogLevelsByName
				.Where(x => x.Value.Id >= 0 && x.Value.Id < sNextId)
				.OrderBy(x => x.Value.Id)
				.Select(x => x.Value)
				.ToArray();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogLevel"/> class.
		/// </summary>
		/// <param name="name">Name of the log level.</param>
		private LogLevel(string name)
		{
			// global logging lock is acquired here...
			Name = name;
			Id = sNextId++;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogLevel"/> class (assigns a specific log level id).
		/// </summary>
		/// <param name="name">Name of the log level.</param>
		/// <param name="id">Id of the log level.</param>
		private LogLevel(string name, int id)
		{
			Name = name;
			Id = id;
		}

		/// <summary>
		/// Gets the name of the log level.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Gets the id of the log level.
		/// </summary>
		public int Id { get; }

		/// <summary>
		/// Gets predefined log levels (all log levels that are not an aspect).
		/// </summary>
		public static IReadOnlyList<LogLevel> PredefinedLogLevels => sPredefinedLogLevels;

		/// <summary>
		/// Gets all log levels that are currently known (except log level 'None' and 'All').
		/// The index of the log level in the list corresponds to <see cref="LogLevel.Id"/>.
		/// </summary>
		public static IReadOnlyList<LogLevel> KnownLevels => sLogLevelsById;

		/// <summary>
		/// Gets a dictionary containing all known log levels by name.
		/// </summary>
		public static IReadOnlyDictionary<string, LogLevel> KnownLevelsByName => sLogLevelsByName;

		/// <summary>
		/// Gets the aspect log level with the specified name (or creates a new one, if it does not exist, yet).
		/// </summary>
		/// <param name="name">Name of the aspect log level to get.</param>
		/// <returns>The requested aspect log level.</returns>
		public static LogLevel GetAspect(string name)
		{
			sLogLevelsByName.TryGetValue(name, out var level);
			if (level == null)
			{
				lock (Log.Sync)
				{
					if (!sLogLevelsByName.TryGetValue(name, out level))
					{
						// log level does not exist, yet
						// => add a new one...
						level = new LogLevel(name);
						var newLogLevelsByName = new Dictionary<string, LogLevel>(sLogLevelsByName) { { level.Name, level } };
						var newLogLevelById = new LogLevel[sLogLevelsById.Length + 1];
						Array.Copy(sLogLevelsById, newLogLevelById, sLogLevelsById.Length);
						newLogLevelById[sLogLevelsById.Length] = level;
						Thread.MemoryBarrier(); // ensures everything has been actually written to memory at this point
						sLogLevelsByName = newLogLevelsByName;
						sLogLevelsById = newLogLevelById;

						// notify about the new log level
						Log.ProcessLogLevelAdded(level);
					}
				}
			}

			return level;
		}

		/// <summary>
		/// Converts a <see cref="LogLevel"/> to its name.
		/// </summary>
		/// <param name="level">Log level to convert.</param>
		public static implicit operator string(LogLevel level)
		{
			return level.Name;
		}

		/// <summary>
		/// Gets the string representation of the current log level.
		/// </summary>
		/// <returns>String representation of the current log level.</returns>
		public override string ToString()
		{
			return $"{Name} ({Id})";
		}
	}

}
