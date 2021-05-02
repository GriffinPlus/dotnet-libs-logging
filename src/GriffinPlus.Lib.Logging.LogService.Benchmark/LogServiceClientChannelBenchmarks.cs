///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.Text;

using BenchmarkDotNet.Attributes;

using GriffinPlus.Lib.Logging.LogService;

namespace GriffinPlus.Lib.Logging.Demo
{

	/// <summary>
	/// Benchmarks targeting methods of the <see cref="LogServiceClientChannel"/> class.
	/// </summary>
	public class LogServiceClientChannelBenchmarks
	{
		private readonly ILogMessage mMessage;
		private readonly char[]      mBuffer = new char[32 * 1024];

		/// <summary>
		/// Initializes a new instance of the <see cref="LogServiceClientChannelBenchmarks"/> class.
		/// </summary>
		public LogServiceClientChannelBenchmarks()
		{
			int lineCount = 10;
			int lineLength = 100;

			// prepare message to send
			var textBuilder = new StringBuilder();
			for (int i = 0; i < lineCount; i++)
			{
				textBuilder.Append(new string('x', lineLength));
				if (i + 1 < lineCount)
					textBuilder.Append('\n');
			}

			mMessage = new LogMessage
			{
				Timestamp = DateTimeOffset.Now,
				HighPrecisionTimestamp = Log.GetHighPrecisionTimestamp(),
				ApplicationName = "My Application",
				ProcessName = Process.GetCurrentProcess().ProcessName,
				ProcessId = Process.GetCurrentProcess().Id,
				LogWriterName = "My Log Writer",
				LogLevelName = "Note",
				Tags = new TagSet("Tag-1", "Tag-2"),
				LostMessageCount = 1,
				Text = textBuilder.ToString()
			};
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand()
		{
			LogServiceClientChannel.FormatWriteCommand(mMessage, mBuffer);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_Timestamp()
		{
			LogServiceClientChannel.FormatWriteCommand_Timestamp(mBuffer, 0, mMessage.Timestamp);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand_HighPrecisionTimestamp"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_HighPrecisionTimestamp()
		{
			LogServiceClientChannel.FormatWriteCommand_HighPrecisionTimestamp(mBuffer, 0, mMessage.HighPrecisionTimestamp);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand_LostMessageCount"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_LostMessageCount()
		{
			LogServiceClientChannel.FormatWriteCommand_LostMessageCount(mBuffer, 0, mMessage.LostMessageCount);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand_LogWriterName"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_LogWriterName()
		{
			LogServiceClientChannel.FormatWriteCommand_LogWriterName(mBuffer, 0, mMessage.LogWriterName);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand_LogLevelName"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_LogLevelName()
		{
			LogServiceClientChannel.FormatWriteCommand_LogLevelName(mBuffer, 0, mMessage.LogLevelName);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand_Tags"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_Tags()
		{
			LogServiceClientChannel.FormatWriteCommand_Tags(mBuffer, 0, mMessage.Tags);
		}

		/// <summary>
		/// Benchmarks the <see cref="LogServiceClientChannel.FormatWriteCommand_Text"/> method.
		/// </summary>
		[Benchmark]
		public void FormatWriteCommand_Text()
		{
			LogServiceClientChannel.FormatWriteCommand_Text(mBuffer, 0, mMessage.Text);
		}
	}

}
