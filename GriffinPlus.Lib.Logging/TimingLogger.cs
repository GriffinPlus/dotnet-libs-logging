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
using System.Diagnostics;
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A timing logger that can be used to measure the time that elapses between its construction and its disposal.
	/// It is recommended to use it in a using statement to ensure it is really disposed at its end.
	/// The timing logger is built to be very efficient, so it should not influence the measurement that much.
	/// NOTE: Do not use the struct's parameterless default constructor, it will not do anything.
	/// </summary>
	public struct TimingLogger : IDisposable
	{
		private static LogWriter sDefaultLogWriter = Log.GetWriter("Timing");
		private static LogLevel sDefaultLogLevel = LogLevel.Timing;
		private long mTimestamp;
		private LogWriter mLogWriter;
		private LogLevel mLogLevel;
		private string mOperation;
		private string mThreadName;
		private int mManagedThreadId;
		private bool mActive;

		/// <summary>
		/// Initializes a new instance of the <see cref="TimingLogger"/> struct using the specified log writer and log level
		/// to emit timing related log messages.
		/// </summary>
		/// <param name="writer">Log writer to use.</param>
		/// <param name="level">Log level to use.</param>
		/// <param name="operation">Name of the operation that is being measured.</param>
		public TimingLogger(LogWriter writer, LogLevel level, string operation = null)
		{
			mLogWriter = writer;
			mLogLevel = level;
			mOperation = operation;
			mThreadName = Thread.CurrentThread.Name;
			mManagedThreadId = Thread.CurrentThread.ManagedThreadId;
			mActive = true;
			mTimestamp = 0;
			WriteStartMessage();

			// init timestamp at last to ensure most accurate measurements
			mTimestamp = Stopwatch.GetTimestamp();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TimingLogger"/> struct using the specified log writer and the default
		/// aspect log level 'Timing' to emit timing related log messages.
		/// </summary>
		/// <param name="writer">Log writer to use.</param>
		/// <param name="operation">Name of the operation that is being measured.</param>
		public TimingLogger(LogWriter writer, string operation = null)
		{
			mLogWriter = writer;
			mLogLevel = sDefaultLogLevel;
			mOperation = operation;
			mThreadName = Thread.CurrentThread.Name;
			mManagedThreadId = Thread.CurrentThread.ManagedThreadId;
			mActive = true;
			mTimestamp = 0;
			WriteStartMessage();

			// init timestamp at last to ensure most accurate measurements
			mTimestamp = Stopwatch.GetTimestamp();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TimingLogger"/> struct using the specified log level and the default
		/// log writer 'Timing' to emit timing related log messages.
		/// </summary>
		/// <param name="level">Log level to use.</param>
		/// <param name="operation">Name of the operation that is being measured.</param>
		public TimingLogger(LogLevel level, string operation = null)
		{
			mLogWriter = sDefaultLogWriter;
			mLogLevel = level;
			mOperation = operation;
			mThreadName = Thread.CurrentThread.Name;
			if (mThreadName.Length == 0) mThreadName = null;
			mManagedThreadId = Thread.CurrentThread.ManagedThreadId;
			mActive = true;
			mTimestamp = 0;
			WriteStartMessage();

			// init timestamp at last to ensure most accurate measurements
			mTimestamp = Stopwatch.GetTimestamp();
		}

		/// <summary>
		/// Disposes the timing logger emitting a log message that notifys about the time since the timing logger was created.
		/// </summary>
		public void Dispose()
		{
			if (mActive) {
				double elapsed = (double)(Stopwatch.GetTimestamp() - mTimestamp) / Stopwatch.Frequency;
				WriteEndMessage(elapsed);
				mActive = false;
			}
		}

		/// <summary>
		/// Writes a log message indicating that the measured operation is starting.
		/// </summary>
		private void WriteStartMessage()
		{
			if (mOperation != null)
			{
				if (mThreadName != null)
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Starting operation ({0})\n- Thread Name: {1}\n- Managed Thread Id: {2}.",
						mOperation, mThreadName, mManagedThreadId);
				}
				else
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Starting operation ({0})\n- Managed Thread Id: {1}.",
						mOperation, mManagedThreadId);
				}
			}
			else
			{
				if (mThreadName != null)
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Starting operation\n- Thread Name: {0}\n- Managed Thread Id: {1}.",
						mThreadName, mManagedThreadId);
				}
				else
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Starting operation\n- Managed Thread Id: {0}.",
						mManagedThreadId);
				}
			}
		}

		/// <summary>
		/// Writes a log message indicating that the measured operation has finished.
		/// </summary>
		/// <param name="elapsed">Duration the measured operation took (in seconds).</param>
		private void WriteEndMessage(double elapsed)
		{
			elapsed *= 1000.0; // convert to ms

			if (mOperation != null)
			{
				if (mThreadName != null)
				{
				mLogWriter.Write(
					mLogLevel,
					"Timing: Operation ({0}) completed [{1:0.0000} ms]\n- Thread Name: {2}\n- Managed Thread Id: {3}.",
					mOperation, elapsed, mThreadName, mManagedThreadId);
				}
				else
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Operation ({0}) completed [{1:0.0000} ms]\n- Managed Thread Id: {2}.",
						mOperation, elapsed, mManagedThreadId);
				}
			}
			else
			{
				if (mThreadName != null)
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Operation completed [{0:0.0000} ms]\n- Thread Name: {1}\n- Managed Thread Id: {2}.",
						elapsed, mThreadName, mManagedThreadId);
				}
				else
				{
					mLogWriter.Write(
						mLogLevel,
						"Timing: Operation completed [{0:0.0000} ms]\n- Managed Thread Id: {1}.",
						elapsed, mManagedThreadId);
				}
			}
		}

	}
}
