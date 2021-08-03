///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Reflection;
using System.Security;
using System.Text;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// System logger that uses the Windows Event Log.
	/// </summary>
	public class WindowsSystemLogger : ISystemLogger
	{
		private readonly EventLog mEventLog;

		/// <summary>
		/// Initializes a new instances of the <see cref="WindowsSystemLogger"/> class.
		/// </summary>
		public WindowsSystemLogger()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				throw new PlatformNotSupportedException("The event log is available on windows systems only.");

			string desiredSource = AppDomain.CurrentDomain.FriendlyName;
			string source = desiredSource;
			bool usingFallback = false;

			// create a source with the desired name
			try
			{
				if (!EventLog.SourceExists(source))
					EventLog.CreateEventSource(source, "Application");
			}
			catch (SecurityException)
			{
				// the log source does not exist and it could not be created
				// => use "Application" as fallback
				source = "Application";
				usingFallback = true;
			}

			// create event log
			mEventLog = new EventLog("Application") { Source = source };

			// log hint to fix the source name
			if (usingFallback)
			{
				string executablePath = Assembly.GetEntryAssembly()?.Location ?? Assembly.GetExecutingAssembly().Location;
				var builder = new StringBuilder();
				builder.AppendLine($"Registering source '{desiredSource}' failed due to insufficient privileges.");
				builder.AppendLine("You should register this source to get rid of malformed messages in the event log.");
				builder.AppendLine("This can be done by a) starting the application with administrative rights -or- b) registering the source manually.");
				builder.AppendLine("Once the source is registered administrative rights are not needed any more.");
				builder.AppendLine($"Executable: {executablePath}");
				mEventLog.WriteEntry(builder.ToString(), EventLogEntryType.Warning);
			}
		}

		/// <summary>
		/// Disposes the system logger.
		/// </summary>
		public void Dispose()
		{
			lock (mEventLog)
			{
				mEventLog.Dispose();
			}
		}

		/// <summary>
		/// Writes an informational message to the system log.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public void WriteInfo(string message)
		{
			lock (mEventLog)
			{
				mEventLog.WriteEntry(message, EventLogEntryType.Information);
			}
		}

		/// <summary>
		/// Writes a warning to the system log.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public void WriteWarning(string message)
		{
			lock (mEventLog)
			{
				mEventLog.WriteEntry(message, EventLogEntryType.Warning);
			}
		}

		/// <summary>
		/// Writes an error to the system log.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public void WriteError(string message)
		{
			lock (mEventLog)
			{
				mEventLog.WriteEntry(message, EventLogEntryType.Error);
			}
		}
	}

}
