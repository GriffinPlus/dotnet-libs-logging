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
using System.Diagnostics;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Delegate of the log source initialization callback that configures how the logging subsystem is set up.
	/// </summary>
	/// <param name="settings">Settings to configure.</param>
	public delegate void LogSourceInitializer(LogSourceSettings settings);

	/// <summary>
	/// The access point to the logging subsystem.
	/// </summary>
	public class LogSource
	{
		private static readonly ReaderWriterLockSlim sLock;
		private static LogSourceSettings sSettings;
		private static Dictionary<string, LogWriter> sLogWritersByName;

		/// <summary>
		/// Intializes the <see cref="LogSource"/> class.
		/// </summary>
		static LogSource()
		{
			sLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
			sLogWritersByName = new Dictionary<string, LogWriter>();
		}

		/// <summary>
		/// Gets the reader-writer-lock protecting access to the logging subsystem.
		/// </summary>
		internal static ReaderWriterLockSlim Lock
		{
			get { return sLock; }
		}

		/// <summary>
		/// Initializes the log source.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		/// <exception cref="InvalidOperationException">The log source is already initialized.</exception>
		public static void Init(LogSourceSettings settings)
		{
			if (sSettings != null)
			{
				throw new InvalidOperationException("The log source is already initialized.");
			}

			settings.Freeze();
			sSettings = settings;
		}

		/// <summary>
		/// Initializes the log source.
		/// </summary>
		/// <param name="with">Callback modifying the settings.</param>
		/// <exception cref="InvalidOperationException">The log source is already initialized.</exception>
		public static void Init(LogSourceInitializer with)
		{
			if (sSettings != null)
			{
				throw new InvalidOperationException("The log source is already initialized.");
			}

			LogSourceSettings settings = new LogSourceSettings();
			sSettings = settings;
			with(sSettings);
			sSettings.Freeze();
		}

		/// <summary>
		/// Writes the specified log message using the specified log writer at the specified level.
		/// </summary>
		/// <param name="writer">Log writer to use.</param>
		/// <param name="level">Log level to use.</param>
		/// <param name="text">Text of the log message.</param>
		internal static void WriteMessage(LogWriter writer, LogLevel level, string text)
		{
			LogMessage message = new LogMessage(
				DateTimeOffset.Now,
				Stopwatch.GetTimestamp(),
				writer,
				level,
				text);

			// TODO: push message into the processing pipeline here...
		}

		/// <summary>
		/// Gets a log writer with the specified name that can be used to write to the log.
		/// </summary>
		/// <param name="name">Name of the log writer to get.</param>
		/// <returns>The requested log writer.</returns>
		public static LogWriter GetWriter(string name)
		{
			LogWriter writer;

			try { }
			finally  // prevents ThreadAbortException from disrupting the following block
			{
				// try to get an existing log writer
				LogSource.Lock.EnterReadLock();
				try
				{
					sLogWritersByName.TryGetValue(name, out writer);
				}
				finally
				{
					LogSource.Lock.ExitReadLock();
				}

				// ask unmanaged log source for the log writer, if it does not exist in the managed world, yet
				if (writer == null)
				{
					LogSource.Lock.EnterWriteLock();
					try
					{
						if (!sLogWritersByName.TryGetValue(name, out writer))
						{
							writer = new LogWriter(name);
							sLogWritersByName.Add(writer.Name, writer);
						}
					}
					finally
					{
						LogSource.Lock.ExitWriteLock();
					}
				}
			}

			return writer;
		}

		/// <summary>
		/// Gets a log writer for the specified type that can be used to write to the log
		/// (the full name of the type becomes the name of the log writer).
		/// </summary>
		/// <param name="type">The type whose full name is to use as the log writer name.</param>
		/// <returns>The requested log writer.</returns>
		public static LogWriter GetWriter(Type type)
		{
			return GetWriter(type.FullName);
		}

		/// <summary>
		/// Gets a log writer for the specified type that can be used to write to the log
		/// (the full name of the type becomes the name of the log writer).
		/// </summary>
		/// <typeparam name="T">The type whose full name is to use as the log writer name.</typeparam>
		/// <returns>The requested log writer.</returns>
		public static LogWriter GetWriter<T>()
		{
			return GetWriter(typeof(T).FullName);
		}

	}
}
