///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

using GriffinPlus.Lib.Text;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log writer (thread-safe).
	/// </summary>
	public sealed class LogWriter
	{
		private static readonly IFormatProvider                sDefaultFormatProvider = CultureInfo.InvariantCulture;
		private static readonly ThreadLocal<StringBuilder>     sBuilder               = new ThreadLocal<StringBuilder>(() => new StringBuilder());
		private static readonly char[]                         sLineSeparators        = Unicode.NewLineCharacters.ToCharArray();
		private static          int                            sNextId;
		private readonly        List<WeakReference<LogWriter>> mSecondaryWriters;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogWriter"/> class (for primary writers).
		/// </summary>
		/// <param name="name">Name of the log writer.</param>
		internal LogWriter(string name)
		{
			// global logging lock is acquired here...
			CheckName(name);
			mSecondaryWriters = new List<WeakReference<LogWriter>>();
			PrimaryWriter = this;
			Id = sNextId++;
			Name = name;
			ActiveLogLevelMask = LogLevelBitMask.Zeros;
			Tags = LogWriterTagSet.Empty;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogWriter"/> class (for secondary writers).
		/// </summary>
		/// <param name="writer">The original (non-tagging) log writer.</param>
		/// <param name="tags">Tags the tagging log writer should attach to written messages.</param>
		internal LogWriter(LogWriter writer, LogWriterTagSet tags)
		{
			// global logging lock is acquired here...
			PrimaryWriter = writer.PrimaryWriter;
			Id = PrimaryWriter.Id;
			Name = PrimaryWriter.Name;
			ActiveLogLevelMask = LogLevelBitMask.Zeros;
			Tags = tags;
		}

		/// <summary>
		/// Gets the id of the log writer.
		/// </summary>
		public int Id { get; }

		/// <summary>
		/// Gets the name of the log writer.
		/// </summary>
		public string Name { get; }

		/// <summary>
		/// Gets the tags the log writer attaches to a written message.
		/// </summary>
		public LogWriterTagSet Tags { get; }

		/// <summary>
		/// Gets the primary log writer
		/// (the initial log writer with the same name that does not modify messages when writing them).
		/// </summary>
		public LogWriter PrimaryWriter { get; }

		/// <summary>
		/// Gets or sets the bit mask indicating which log levels are active for the log writer.
		/// </summary>
		internal LogLevelBitMask ActiveLogLevelMask { get; set; }

		/// <summary>
		/// Checks whether the specified string is a valid log writer name
		/// (log writer names may consist of all characters except line separators).
		/// </summary>
		/// <param name="name">Name to check.</param>
		/// <exception cref="ArgumentNullException">The specified name is <c>null</c>.</exception>
		/// <exception cref="ArgumentException">The specified name is invalid.</exception>
		public static void CheckName(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));
			if (name.IndexOfAny(sLineSeparators) >= 0)
			{
				string message =
					$"The specified name ({name}) is not a valid log writer name.\n" +
					"Valid names may consist of all characters except line separators.\n";
				throw new ArgumentException(message);
			}
		}

		/// <summary>
		/// Checks whether the specified log level is active, so a message written using that level
		/// really gets into the log. The special log levels <see cref="LogLevel.None"/> and <see cref="LogLevel.All"/>
		/// are always active, but must not be used write log messages. These log levels are mapped to
		/// <see cref="LogLevel.Error"/> and get always logged - regardless of the configuration.
		/// </summary>
		/// <param name="level">Log level to check.</param>
		/// <returns>true, if the specified log level is active; otherwise false.</returns>
		public bool IsLogLevelActive(LogLevel level)
		{
			if (level.Id < 0 || level.Id == int.MaxValue)
				return true;

			if (ActiveLogLevelMask.Size == 0)
			{
				// active log level mask is empty
				// => the log configuration is not initialized, yet
				// => let the static constructor of the Log class create a default configuration...
				lock (Log.Sync) { } // the property accessor triggers the static constructor, if necessary
			}

			return ActiveLogLevelMask.IsBitSet(level.Id);
		}

		/// <summary>
		/// Creates a new log writer that attaches the specified tag to written log messages.
		/// </summary>
		/// <param name="tag">Tag the new log writer should attach to written log messages (may be null).</param>
		/// <returns>A log writer that attaches the specified tag to written log messages.</returns>
		public LogWriter WithTag(string tag)
		{
			if (tag == null) return this;
			var newTags = new LogWriterTagSet(new List<LogWriterTag>(Tags) { Log.GetWriterTag(tag) });
			if (newTags.Count == Tags.Count) return this;

			lock (Log.Sync)
			{
				RemoveCollectedSecondaryWriters();
				var newWriter = new LogWriter(PrimaryWriter, newTags);
				PrimaryWriter.mSecondaryWriters.Add(new WeakReference<LogWriter>(newWriter));
				return newWriter;
			}
		}

		/// <summary>
		/// Creates a new log writer that attaches the specified tags to written log messages.
		/// </summary>
		/// <param name="tags">Tags the new log writer should attach to written log messages (may be null).</param>
		/// <returns>A log writer that attaches the specified tags to written log messages.</returns>
		public LogWriter WithTags(params string[] tags)
		{
			if (tags == null) return this;
			var tagList = new List<LogWriterTag>(Tags);
			foreach (string tag in tags) tagList.Add(Log.GetWriterTag(tag));
			var newTags = new LogWriterTagSet(tagList);
			if (newTags.Count == Tags.Count) return this;

			lock (Log.Sync)
			{
				RemoveCollectedSecondaryWriters();
				var newWriter = new LogWriter(PrimaryWriter, newTags);
				PrimaryWriter.mSecondaryWriters.Add(new WeakReference<LogWriter>(newWriter));
				return newWriter;
			}
		}

		/// <summary>
		/// Updates the log writer and associated secondary log writers.
		/// </summary>
		/// <param name="configuration">The log configuration.</param>
		internal void Update(ILogConfiguration configuration)
		{
			Debug.Assert(Monitor.IsEntered(Log.Sync));
			ActiveLogLevelMask = configuration.GetActiveLogLevelMask(this);
			if (mSecondaryWriters != null)
			{
				for (int i = mSecondaryWriters.Count - 1; i >= 0; i--)
				{
					if (mSecondaryWriters[i].TryGetTarget(out var writer))
					{
						writer.Update(configuration);
					}
					else
					{
						// the secondary log writer was collected
						// => remove it from the list
						mSecondaryWriters.RemoveAt(i);
					}
				}
			}
		}

		/// <summary>
		/// Removes secondary log writers that have been collected meanwhile.
		/// </summary>
		private void RemoveCollectedSecondaryWriters()
		{
			if (mSecondaryWriters != null)
			{
				for (int i = mSecondaryWriters.Count - 1; i >= 0; i--)
				{
					if (!mSecondaryWriters[i].TryGetTarget(out _))
					{
						mSecondaryWriters.RemoveAt(i);
					}
				}
			}
		}

		/// <summary>
		/// Writes a message to the log.
		/// </summary>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="message">Message to write to the log.</param>
		public void Write(LogLevel level, string message)
		{
			if (IsLogLevelActive(level))
			{
				Log.WriteMessage(this, level, Tags, message);
			}
		}

		/// <summary>
		/// Writes a formatted message with a variable number of placeholders to the log.
		/// </summary>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="args">Arguments to put into the placeholders.</param>
		public void Write(LogLevel level, string format, params object[] args)
		{
			Write(sDefaultFormatProvider, level, format, args);
		}

		/// <summary>
		/// Writes a formatted message with a variable number of placeholders to the log
		/// (bypasses filters induced by the log configuration).
		/// </summary>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="args">Arguments to put into the placeholders.</param>
		public void ForceWrite(LogLevel level, string format, params object[] args)
		{
			ForceWrite(sDefaultFormatProvider, level, format, args);
		}

		/// <summary>
		/// Writes a formatted message with one placeholders to the log.
		/// </summary>
		/// <typeparam name="T">Type of the placeholder.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg">Argument to put into placeholder {0}.</param>
		public void Write<T>(LogLevel level, string format, T arg)
		{
			Write(sDefaultFormatProvider, level, format, arg);
		}

		/// <summary>
		/// Writes a formatted message with two placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		public void Write<T0, T1>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1);
		}

		/// <summary>
		/// Writes a formatted message with three placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		public void Write<T0, T1, T2>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2);
		}

		/// <summary>
		/// Writes a formatted message with four placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		public void Write<T0, T1, T2, T3>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3);
		}

		/// <summary>
		/// Writes a formatted message with five placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		public void Write<T0, T1, T2, T3, T4>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4);
		}

		/// <summary>
		/// Writes a formatted message with six placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		public void Write<T0, T1, T2, T3, T4, T5>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5);
		}

		/// <summary>
		/// Writes a formatted message with seven placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6);
		}

		/// <summary>
		/// Writes a formatted message with eight placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
		}

		/// <summary>
		/// Writes a formatted message with nine placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
		}

		/// <summary>
		/// Writes a formatted message with ten placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8,
			T9       arg9)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
		}

		/// <summary>
		/// Writes a formatted message with eleven placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8,
			T9       arg9,
			T10      arg10)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
		}

		/// <summary>
		/// Writes a formatted message with twelve placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8,
			T9       arg9,
			T10      arg10,
			T11      arg11)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
		}

		/// <summary>
		/// Writes a formatted message with 13 placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <typeparam name="T12">Type of placeholder 12.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		/// <param name="arg12">Argument to put into placeholder {12}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8,
			T9       arg9,
			T10      arg10,
			T11      arg11,
			T12      arg12)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
		}

		/// <summary>
		/// Writes a formatted message with 14 placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <typeparam name="T12">Type of placeholder 12.</typeparam>
		/// <typeparam name="T13">Type of placeholder 13.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		/// <param name="arg12">Argument to put into placeholder {12}.</param>
		/// <param name="arg13">Argument to put into placeholder {13}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8,
			T9       arg9,
			T10      arg10,
			T11      arg11,
			T12      arg12,
			T13      arg13)
		{
			Write(sDefaultFormatProvider, level, format, arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
		}

		/// <summary>
		/// Writes a formatted message with 15 placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <typeparam name="T12">Type of placeholder 12.</typeparam>
		/// <typeparam name="T13">Type of placeholder 13.</typeparam>
		/// <typeparam name="T14">Type of placeholder 14.</typeparam>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		/// <param name="arg12">Argument to put into placeholder {12}.</param>
		/// <param name="arg13">Argument to put into placeholder {13}.</param>
		/// <param name="arg14">Argument to put into placeholder {14}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
			LogLevel level,
			string   format,
			T0       arg0,
			T1       arg1,
			T2       arg2,
			T3       arg3,
			T4       arg4,
			T5       arg5,
			T6       arg6,
			T7       arg7,
			T8       arg8,
			T9       arg9,
			T10      arg10,
			T11      arg11,
			T12      arg12,
			T13      arg13,
			T14      arg14)
		{
			Write(
				sDefaultFormatProvider,
				level,
				format,
				arg0,
				arg1,
				arg2,
				arg3,
				arg4,
				arg5,
				arg6,
				arg7,
				arg8,
				arg9,
				arg10,
				arg11,
				arg12,
				arg13,
				arg14);
		}

		/// <summary>
		/// Writes a formatted message with a variable number of placeholders to the log.
		/// </summary>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="args">Arguments to put into the placeholders.</param>
		public void Write(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			params object[] args)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object[] modifiedArgs = args;
				for (int i = 0; i < modifiedArgs.Length; i++)
				{
					object obj = modifiedArgs[i];
					if (obj is Exception exception)
					{
						if (modifiedArgs == args) modifiedArgs = (object[])args.Clone();
						modifiedArgs[i] = UnwrapException(exception);
					}
				}

				args = modifiedArgs;

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, args);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with a variable number of placeholders to the log
		/// (bypasses filters induced by the log configuration).
		/// </summary>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="args">Arguments to put into the placeholders.</param>
		public void ForceWrite(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			params object[] args)
		{
			// unwrap exceptions to ensure inner exceptions are logged as well
			object[] modifiedArgs = args;
			for (int i = 0; i < modifiedArgs.Length; i++)
			{
				object obj = modifiedArgs[i];
				if (obj is Exception exception)
				{
					if (modifiedArgs == args) modifiedArgs = (object[])args.Clone();
					modifiedArgs[i] = UnwrapException(exception);
				}
			}

			args = modifiedArgs;

			// write message to the log
			var builder = sBuilder.Value;
			builder.Clear();
			builder.AppendFormat(provider, format, args);
			Log.WriteMessage(this, level, Tags, builder.ToString());
		}

		/// <summary>
		/// Writes a formatted message with one placeholders to the log.
		/// </summary>
		/// <typeparam name="T">Type of the placeholder.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg">Argument to put into placeholder {0}.</param>
		public void Write<T>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T               arg)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg = PrepareArgument(arg);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with two placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		public void Write<T0, T1>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with three placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		public void Write<T0, T1, T2>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with four placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		public void Write<T0, T1, T2, T3>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with five placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		public void Write<T0, T1, T2, T3, T4>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with six placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		public void Write<T0, T1, T2, T3, T4, T5>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with seven placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with eight placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6, carg7);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with nine placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6, carg7, carg8);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with ten placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8,
			T9              arg9)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);
				object carg9 = PrepareArgument(arg9);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6, carg7, carg8, carg9);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with eleven placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8,
			T9              arg9,
			T10             arg10)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);
				object carg9 = PrepareArgument(arg9);
				object carg10 = PrepareArgument(arg10);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6, carg7, carg8, carg9, carg10);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with twelve placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8,
			T9              arg9,
			T10             arg10,
			T11             arg11)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);
				object carg9 = PrepareArgument(arg9);
				object carg10 = PrepareArgument(arg10);
				object carg11 = PrepareArgument(arg11);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6, carg7, carg8, carg9, carg10, carg11);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with 13 placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <typeparam name="T12">Type of placeholder 12.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		/// <param name="arg12">Argument to put into placeholder {12}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8,
			T9              arg9,
			T10             arg10,
			T11             arg11,
			T12             arg12)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);
				object carg9 = PrepareArgument(arg9);
				object carg10 = PrepareArgument(arg10);
				object carg11 = PrepareArgument(arg11);
				object carg12 = PrepareArgument(arg12);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(provider, format, carg0, carg1, carg2, carg3, carg4, carg5, carg6, carg7, carg8, carg9, carg10, carg11, carg12);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with 14 placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <typeparam name="T12">Type of placeholder 12.</typeparam>
		/// <typeparam name="T13">Type of placeholder 13.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		/// <param name="arg12">Argument to put into placeholder {12}.</param>
		/// <param name="arg13">Argument to put into placeholder {13}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8,
			T9              arg9,
			T10             arg10,
			T11             arg11,
			T12             arg12,
			T13             arg13)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);
				object carg9 = PrepareArgument(arg9);
				object carg10 = PrepareArgument(arg10);
				object carg11 = PrepareArgument(arg11);
				object carg12 = PrepareArgument(arg12);
				object carg13 = PrepareArgument(arg13);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(
					provider,
					format,
					carg0,
					carg1,
					carg2,
					carg3,
					carg4,
					carg5,
					carg6,
					carg7,
					carg8,
					carg9,
					carg10,
					carg11,
					carg12,
					carg13);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Writes a formatted message with 15 placeholders to the log.
		/// </summary>
		/// <typeparam name="T0">Type of placeholder 0.</typeparam>
		/// <typeparam name="T1">Type of placeholder 1.</typeparam>
		/// <typeparam name="T2">Type of placeholder 2.</typeparam>
		/// <typeparam name="T3">Type of placeholder 3.</typeparam>
		/// <typeparam name="T4">Type of placeholder 4.</typeparam>
		/// <typeparam name="T5">Type of placeholder 5.</typeparam>
		/// <typeparam name="T6">Type of placeholder 6.</typeparam>
		/// <typeparam name="T7">Type of placeholder 7.</typeparam>
		/// <typeparam name="T8">Type of placeholder 8.</typeparam>
		/// <typeparam name="T9">Type of placeholder 9.</typeparam>
		/// <typeparam name="T10">Type of placeholder 10.</typeparam>
		/// <typeparam name="T11">Type of placeholder 11.</typeparam>
		/// <typeparam name="T12">Type of placeholder 12.</typeparam>
		/// <typeparam name="T13">Type of placeholder 13.</typeparam>
		/// <typeparam name="T14">Type of placeholder 14.</typeparam>
		/// <param name="provider">Format provider to use when formatting the message.</param>
		/// <param name="level">Log level to write the message to.</param>
		/// <param name="format">A composite format string containing placeholders (formatting as usual in .NET).</param>
		/// <param name="arg0">Argument to put into placeholder {0}.</param>
		/// <param name="arg1">Argument to put into placeholder {1}.</param>
		/// <param name="arg2">Argument to put into placeholder {2}.</param>
		/// <param name="arg3">Argument to put into placeholder {3}.</param>
		/// <param name="arg4">Argument to put into placeholder {4}.</param>
		/// <param name="arg5">Argument to put into placeholder {5}.</param>
		/// <param name="arg6">Argument to put into placeholder {6}.</param>
		/// <param name="arg7">Argument to put into placeholder {7}.</param>
		/// <param name="arg8">Argument to put into placeholder {8}.</param>
		/// <param name="arg9">Argument to put into placeholder {9}.</param>
		/// <param name="arg10">Argument to put into placeholder {10}.</param>
		/// <param name="arg11">Argument to put into placeholder {11}.</param>
		/// <param name="arg12">Argument to put into placeholder {12}.</param>
		/// <param name="arg13">Argument to put into placeholder {13}.</param>
		/// <param name="arg14">Argument to put into placeholder {14}.</param>
		public void Write<T0, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(
			IFormatProvider provider,
			LogLevel        level,
			string          format,
			T0              arg0,
			T1              arg1,
			T2              arg2,
			T3              arg3,
			T4              arg4,
			T5              arg5,
			T6              arg6,
			T7              arg7,
			T8              arg8,
			T9              arg9,
			T10             arg10,
			T11             arg11,
			T12             arg12,
			T13             arg13,
			T14             arg14)
		{
			if (IsLogLevelActive(level))
			{
				// unwrap exceptions to ensure inner exceptions are logged as well
				object carg0 = PrepareArgument(arg0);
				object carg1 = PrepareArgument(arg1);
				object carg2 = PrepareArgument(arg2);
				object carg3 = PrepareArgument(arg3);
				object carg4 = PrepareArgument(arg4);
				object carg5 = PrepareArgument(arg5);
				object carg6 = PrepareArgument(arg6);
				object carg7 = PrepareArgument(arg7);
				object carg8 = PrepareArgument(arg8);
				object carg9 = PrepareArgument(arg9);
				object carg10 = PrepareArgument(arg10);
				object carg11 = PrepareArgument(arg11);
				object carg12 = PrepareArgument(arg12);
				object carg13 = PrepareArgument(arg13);
				object carg14 = PrepareArgument(arg14);

				// write message to the log
				var builder = sBuilder.Value;
				builder.Clear();
				builder.AppendFormat(
					provider,
					format,
					carg0,
					carg1,
					carg2,
					carg3,
					carg4,
					carg5,
					carg6,
					carg7,
					carg8,
					carg9,
					carg10,
					carg11,
					carg12,
					carg13,
					carg14);
				Log.WriteMessage(this, level, Tags, builder.ToString());
			}
		}

		/// <summary>
		/// Converts the specified argument into a form that can be fed into the logging subsystem (e.g. unwraps exceptions).
		/// </summary>
		/// <typeparam name="T">Type of the argument.</typeparam>
		/// <param name="arg">Argument to prepare.</param>
		/// <returns>Object to feed into the logging subsystem.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static object PrepareArgument<T>(T arg)
		{
			if (arg is Exception exception) return UnwrapException(exception);
			return arg;
		}

		/// <summary>
		/// Gets a string containing the type, the message and the stacktrace of the specified exception and all of its inner exception.
		/// </summary>
		/// <param name="exception"></param>
		/// <returns></returns>
		public static string UnwrapException(Exception exception)
		{
			var builder = new StringBuilder();
			builder.AppendLine();

			int innerExceptionLevel = 0;
			var current = exception;
			while (current != null)
			{
				builder.Append(
					innerExceptionLevel == 0
						? "--- Exception ---------------------------------------------------------------------------------------------\r\n"
						: "--- Inner Exception ---------------------------------------------------------------------------------------\r\n");
				builder.AppendFormat("--- Exception Type: {0}\r\n", exception.GetType().FullName);
				builder.AppendFormat("--- Message: {0}\r\n", current.Message);
				builder.AppendFormat("--- Stacktrace:\r\n{0}", current.StackTrace);
				builder.AppendLine();

				current = current.InnerException;
				innerExceptionLevel++;
			}

			return builder.ToString();
		}

		/// <summary>
		/// Gets the string representation of the log writer.
		/// </summary>
		/// <returns>String representation of the log writer.</returns>
		public override string ToString()
		{
			return $"{Name} ({Id})";
		}
	}

}
