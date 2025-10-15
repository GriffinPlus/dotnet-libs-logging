///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xunit;

// ReSharper disable UseObjectOrCollectionInitializer

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="ConsoleWriterPipelineStage"/> class.
/// </summary>
public class ConsoleWriterPipelineStageTests : TextWriterPipelineStageBaseTests<ConsoleWriterPipelineStage>
{
	private static readonly Dictionary<string, object> sDefaultSettings = new()
	{
		{ "DefaultStream", ConsoleOutputStream.Stdout }
	};

	/// <summary>
	/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
	/// (only non-default stuff is checked, the rest is done by the base test class).
	/// </summary>
	[Fact]
	public void Create()
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);
		Assert.Equal(sDefaultSettings, stage.Settings.ToDictionary(x => x.Key, x => x.Value.Value));
		Assert.Equal(ConsoleOutputStream.Stdout, stage.DefaultStream);
		Assert.Same(Console.Out, stage.OutputStream);
		Assert.Same(Console.Error, stage.ErrorStream);
		Assert.Empty(stage.StreamByLevelOverrides);
	}

	/// <summary>
	/// Test data for verifying the mapping of <see cref="LogLevel"/> values
	/// to console output streams (<see cref="ConsoleOutputStream.Stdout"/> or <see cref="ConsoleOutputStream.Stderr"/>).
	/// </summary>
	public static TheoryData<List<Tuple<LogLevel, ConsoleOutputStream>>> LogLevelToStreamMapping_TestData
	{
		get
		{
			var data = new TheoryData<List<Tuple<LogLevel, ConsoleOutputStream>>>();
			List<Tuple<LogLevel, ConsoleOutputStream>> mappings;

			// Test adding log levels to the same stream (progressively more mappings)
			// Note: Original code used Stdout twice — preserved as-is for consistency.
			foreach (ConsoleOutputStream stream in new[] { ConsoleOutputStream.Stdout, ConsoleOutputStream.Stdout })
			{
				mappings = [new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Emergency, stream)];
				data.Add(mappings);

				mappings =
				[
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Emergency, stream),
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Alert, stream)
				];
				data.Add(mappings);

				mappings =
				[
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Emergency, stream),
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Alert, stream),
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Critical, stream)
				];
				data.Add(mappings);

				mappings =
				[
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Emergency, stream),
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Alert, stream),
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Critical, stream),
					new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Error, stream)
				];
				data.Add(mappings);
			}

			// Test overwriting existing mappings (same LogLevel reassigned to a different stream)
			mappings =
			[
				new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Emergency, ConsoleOutputStream.Stdout),
				new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Alert, ConsoleOutputStream.Stdout),
				new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Critical, ConsoleOutputStream.Stdout),
				// Overwrite existing mapping of 'Alert' with Stderr
				new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Alert, ConsoleOutputStream.Stderr)
			];
			data.Add(mappings);

			return data;
		}
	}

	/// <summary>
	/// Tests whether explicitly mapping a log level to a specific output stream using
	/// <see cref="ConsoleWriterPipelineStage.MapLogLevelToStream(LogLevel, ConsoleOutputStream)"/> works as excepted.
	/// </summary>
	[Theory]
	[MemberData(nameof(LogLevelToStreamMapping_TestData))]
	public void MapLogLevelToStream(List<Tuple<LogLevel, ConsoleOutputStream>> mappings)
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);

		// add the mappings and calculate the expected result
		var expectedMapping = new Dictionary<LogLevel, ConsoleOutputStream>();
		foreach (Tuple<LogLevel, ConsoleOutputStream> mapping in mappings)
		{
			expectedMapping[mapping.Item1] = mapping.Item2;
			stage.MapLogLevelToStream(mapping.Item1, mapping.Item2);
		}

		// check that the 'StreamByLevelOverrides' property reflects the overrides
		Assert.Equal(expectedMapping, stage.StreamByLevelOverrides);
	}

	/// <summary>
	/// Tests whether <see cref="ConsoleWriterPipelineStage.MapLogLevelToStream"/> throws an exception,
	/// if the log level is a null reference.
	/// </summary>
	[Fact]
	public void MapLogLevelToStream_FailsIfLogLevelNull()
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);
		Assert.Throws<ArgumentNullException>(() => stage.MapLogLevelToStream(null, ConsoleOutputStream.Stdout));
	}

	/// <summary>
	/// Tests whether <see cref="ConsoleWriterPipelineStage.MapLogLevelToStream"/> throws an exception,
	/// if the pipeline stage is initialized (attached to the logging subsystem).
	/// </summary>
	[Fact]
	public void MapLogLevelToStream_FailsIfInitialized()
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);
		stage.Initialize();
		Assert.Throws<InvalidOperationException>(() => stage.MapLogLevelToStream(LogLevel.Notice, ConsoleOutputStream.Stdout));
		stage.Shutdown();
	}

	/// <summary>
	/// Tests whether explicitly mapping log levels to a specific output stream using
	/// <see cref="ConsoleWriterPipelineStage.StreamByLevelOverrides"/> works as excepted.
	/// </summary>
	[Theory]
	[MemberData(nameof(LogLevelToStreamMapping_TestData))]
	public void StreamByLevelOverrides(List<Tuple<LogLevel, ConsoleOutputStream>> mappings)
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);

		// add the mappings and calculate the expected result
		var expectedMapping = new Dictionary<LogLevel, ConsoleOutputStream>();
		foreach (Tuple<LogLevel, ConsoleOutputStream> mapping in mappings) expectedMapping[mapping.Item1] = mapping.Item2;
		stage.StreamByLevelOverrides = expectedMapping;

		// check that the 'StreamByLevelOverrides' property reflects the overrides
		Assert.Equal(expectedMapping, stage.StreamByLevelOverrides);
	}

	/// <summary>
	/// Tests whether setting <see cref="ConsoleWriterPipelineStage.StreamByLevelOverrides"/> throws an exception,
	/// if setting a null reference.
	/// </summary>
	[Fact]
	public void StreamByLevelOverrides_FailsIfNull()
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);
		Assert.Throws<ArgumentNullException>(() => stage.StreamByLevelOverrides = null);
	}

	/// <summary>
	/// Tests whether setting <see cref="ConsoleWriterPipelineStage.StreamByLevelOverrides"/> throws an exception,
	/// if the pipeline stage is initialized (attached to the logging subsystem).
	/// </summary>
	[Fact]
	public void StreamByLevelOverrides_FailsIfInitialized()
	{
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);
		stage.Initialize();
		Assert.Throws<InvalidOperationException>(() => stage.StreamByLevelOverrides = new Dictionary<LogLevel, ConsoleOutputStream>());
		stage.Shutdown();
	}

	/// <summary>
	/// Test data for verifying mapping of log levels to console output streams when processing messages.
	/// Combines a default stream selection, a set of log level => stream mappings, and a local message set.
	/// </summary>
	public static TheoryData<ConsoleOutputStream, List<Tuple<LogLevel, ConsoleOutputStream>>, IEnumerable<LocalLogMessage>> Process_TestData
	{
		get
		{
			var data = new TheoryData<ConsoleOutputStream, List<Tuple<LogLevel, ConsoleOutputStream>>, IEnumerable<LocalLogMessage>>();

			// Default stream options (stdout/stderr)
			var defaultStreams = new[] { ConsoleOutputStream.Stdout, ConsoleOutputStream.Stderr };

			// IMPORTANT:
			// LogLevelToStreamMapping_TestData is a TheoryData<List<Tuple<LogLevel, ConsoleOutputStream>>>
			// which enumerates as object[] with a single element (the mappings list).
			foreach (ConsoleOutputStream defaultStream in defaultStreams)
			{
				foreach (List<Tuple<LogLevel, ConsoleOutputStream>> entry in LogLevelToStreamMapping_TestData)
				{
					// Combine with all local message sets
					foreach (IEnumerable<LocalLogMessage> messages in TestData.LocalLogMessageSet)
					{
						data.Add(defaultStream, entry, messages);
					}
				}
			}

			return data;
		}
	}

	/// <summary>
	/// Tests whether messages that are passed to the pipeline stage are output to the configured console streams.
	/// </summary>
	/// <param name="defaultStream">Default console stream determining to which console stream is written by default.</param>
	/// <param name="mappings">Mappings determining whether a message with a certain log level is written to stdout or stderr.</param>
	/// <param name="messages">Messages that are passed to the stage.</param>
	[Theory]
	[MemberData(nameof(Process_TestData))]
	public async Task Process(ConsoleOutputStream defaultStream, List<Tuple<LogLevel, ConsoleOutputStream>> mappings, IEnumerable<LocalLogMessage> messages)
	{
		// create a new pipeline stage
		var formatter = new TestFormatter();
		var stage = ProcessingPipelineStage.Create<ConsoleWriterPipelineStage>("Console", null);
		stage.DefaultStream = defaultStream;
		stage.Formatter = formatter;

		// replace i/o streams to avoid mixing up test output with regular console output
		Stream stdoutStream = Stream.Synchronized(new MemoryStream()); // the stage writes to the stream asynchronously,
		Stream stderrStream = Stream.Synchronized(new MemoryStream()); // so synchronize access to the stream
		var stdoutWriter = new StreamWriter(stdoutStream);
		var stderrWriter = new StreamWriter(stderrStream);
		stage.OutputStream = stdoutWriter;
		stage.ErrorStream = stderrWriter;

		// configure the stage
		// build a dictionary that contains the mappings from log level to the corresponding console stream
		var levelToStreamMap = new Dictionary<LogLevel, ConsoleOutputStream>();
		foreach (Tuple<LogLevel, ConsoleOutputStream> mapping in mappings)
		{
			levelToStreamMap[mapping.Item1] = mapping.Item2;
			stage.MapLogLevelToStream(mapping.Item1, mapping.Item2);
		}

		// initialize the pipeline stage
		stage.Initialize();

		// process the message and determine the expected output in stdout/stderr
		var expectedStdout = new StringBuilder();
		var expectedStderr = new StringBuilder();
		foreach (LocalLogMessage message in messages)
		{
			stage.ProcessMessage(message);

			// ReSharper disable once CanSimplifyDictionaryTryGetValueWithGetValueOrDefault
			if (!levelToStreamMap.TryGetValue(message.LogLevel, out ConsoleOutputStream stream))
				stream = defaultStream;

			// add formatted message to the expected output
			// (the stage automatically adds a newline after each message)
			string formattedOutput = formatter.Format(message);
			if (stream == ConsoleOutputStream.Stdout) expectedStdout.AppendLine(formattedOutput);
			else expectedStderr.AppendLine(formattedOutput);
		}

		// give the messages some time (500ms) to travel through the pipeline
		for (int i = 0; i < 10; i++)
		{
			await Task.Delay(50);
			if (stdoutStream.Length >= expectedStdout.Length && stderrStream.Length >= expectedStderr.Length)
				break;
		}

		// the streams should contain the output now
		stdoutStream.Position = 0;
		stderrStream.Position = 0;
		byte[] stdoutData = new byte[stdoutStream.Length];
		byte[] stderrData = new byte[stderrStream.Length];
		int stdoutBytesReadCount = await stdoutStream.ReadAsync(stdoutData, 0, stdoutData.Length);
		int stderrBytesReadCount = await stderrStream.ReadAsync(stderrData, 0, stderrData.Length);
		Assert.Equal(stdoutData.Length, stdoutBytesReadCount);
		Assert.Equal(stderrData.Length, stderrBytesReadCount);
		var stdoutReader = new StreamReader(new MemoryStream(stdoutData));
		var stderrReader = new StreamReader(new MemoryStream(stderrData));
		string stdoutOutput = await stdoutReader.ReadToEndAsync();
		string stderrOutput = await stderrReader.ReadToEndAsync();
		Assert.Equal(expectedStdout.ToString(), stdoutOutput);
		Assert.Equal(expectedStderr.ToString(), stderrOutput);

		// shut the pipeline stage down
		stage.Shutdown();
		Assert.False(stage.IsInitialized);
	}
}
