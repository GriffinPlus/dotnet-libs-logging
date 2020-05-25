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
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Output streams the <see cref="ConsoleWriterPipelineStage"/> can emit log messages to.
	/// </summary>
	public enum ConsoleOutputStream
	{
		/// <summary>
		/// The standard output stream.
		/// </summary>
		Stdout,

		/// <summary>
		/// The standard error stream.
		/// </summary>
		Stderr
	};

	/// <summary>
	/// A log message processing pipeline stage that writes log messages to stdout/stderr (thread-safe).
	/// By default all log messages are written to stdout.
	/// </summary>
	public class ConsoleWriterPipelineStage : TextWriterPipelineStage<ConsoleWriterPipelineStage>
	{
		private readonly Dictionary<LogLevel, ConsoleOutputStream> mStreamByLevel = new Dictionary<LogLevel, ConsoleOutputStream>();
		private readonly StringBuilder mStdoutBuilder = new StringBuilder();
		private readonly StringBuilder mStderrBuilder = new StringBuilder();
		private IProcessingPipelineStageSetting<ConsoleOutputStream> mDefaultStreamSetting;
		private const string SettingName_DefaultStream = "DefaultStream";

		/// <summary>
		/// Initializes a new instance of the <see cref="ConsoleWriterPipelineStage"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		public ConsoleWriterPipelineStage(string name) : base(name)
		{

		}

		/// <summary>
		/// Gets or sets the default stream log messages are emitted to by default.
		/// </summary>
		public ConsoleOutputStream DefaultStream
		{
			get
			{
				return mDefaultStreamSetting.Value;
			}

			set
			{
				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mDefaultStreamSetting.Value = value;
				}
			}
		}

		/// <summary>
		/// Gets or sets the explicit mapping overrides of log levels to output streams.
		/// A message written using an explictly mapped log level will go out on the defined steam.
		/// Others will be emitted using the streams defined by the <see cref="DefaultStream"/> property.
		/// </summary>
		public IReadOnlyDictionary<LogLevel, ConsoleOutputStream> StreamByLevelOverrides
		{
			get
			{
				lock (Sync)
				{
					// copy mappings to avoid modifications being done in an unsynchronized way
					return new Dictionary<LogLevel, ConsoleOutputStream>(mStreamByLevel);
				}
			}

			set
			{
				if (value == null) throw new ArgumentNullException(nameof(value));
				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					mStreamByLevel.Clear();
					foreach (var kvp in value) mStreamByLevel.Add(kvp.Key, kvp.Value);
				}
			}
		}

		/// <summary>
		/// Configures the console writer to emit log messages written using the specified log level to the specified stream.
		/// Only necessary, if the stream is different from the default stream (<see cref="DefaultStream"/>).
		/// </summary>
		/// <param name="level">Log level of messages to emit to the specified stream.</param>
		/// <param name="stream">Output stream to emit log messages written using the specified log level to.</param>
		public void MapLogLevelToStream(LogLevel level, ConsoleOutputStream stream)
		{
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();
				mStreamByLevel[level] = stream;
			}
		}

		/// <summary>
		/// Is called to allow a derived stage bind its settings when the <see cref="AsyncProcessingPipelineStage{STAGE}.Settings"/> property has changed
		/// (the pipeline stage lock <see cref="AsyncProcessingPipelineStage{STAGE}.Sync"/> is acquired when this method is called).
		/// </summary>
		protected override void BindSettings()
		{
			mDefaultStreamSetting = Settings.GetSetting(SettingName_DefaultStream, ConsoleOutputStream.Stdout);
		}

		/// <summary>
		/// Emits the formatted log messages (should not throw any exceptions).
		/// </summary>
		/// <param name="messages">The formatted log messages.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override async Task<int> EmitOutputAsync(FormattedMessage[] messages, CancellationToken cancellationToken)
		{
			mStdoutBuilder.Clear();
			mStderrBuilder.Clear();

			for (int i = 0; i < messages.Length; i++)
			{
				var message = messages[i];

				// NOTE: After attaching the pipeline stage to the logging subsystem, mStreamByLevel will not change.
				if (!mStreamByLevel.TryGetValue(message.Message.LogLevel, out ConsoleOutputStream stream))
				{
					stream = mDefaultStreamSetting.Value;
				}

				if (stream == ConsoleOutputStream.Stdout)
				{
					mStdoutBuilder.Append(message.Output);
					mStdoutBuilder.AppendLine();
				}
				else
				{
					mStderrBuilder.Append(message.Output);
					mStderrBuilder.AppendLine();
				}
			}

			// try to write to the console
			// (usually this should not fail, but who knows...)
			try
			{
				if (mStdoutBuilder.Length > 0)
				{
					await Console.Out.WriteAsync(mStdoutBuilder.ToString()).ConfigureAwait(false);
					await Console.Out.FlushAsync().ConfigureAwait(false);
				}

				if (mStderrBuilder.Length > 0)
				{
					await Console.Error.WriteAsync(mStderrBuilder.ToString()).ConfigureAwait(false);
					await Console.Error.FlushAsync().ConfigureAwait(false);
				}
			}
			catch
			{
				// swallow exceptions
				// (i/o errors should not impact the application)

				// return that no messages have been written at all
				// (could be wrong, if the console printed some of the messages and can lead to printing messages multiple times in case of errors)
				return 0;
			}

			return messages.Length;
		}

	}
}
