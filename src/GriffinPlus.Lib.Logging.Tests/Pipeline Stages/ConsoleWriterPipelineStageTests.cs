///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
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
using System.Threading;
using Xunit;

// ReSharper disable UseObjectOrCollectionInitializer

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="ConsoleWriterPipelineStage"/> class.
	/// </summary>
	public class ConsoleWriterPipelineStageTests : TextWriterPipelineStageBaseTests<ConsoleWriterPipelineStage>
	{
		private static Dictionary<string, object> sDefaultSettings = new Dictionary<string, object>()
		{
			{ "DefaultStream", ConsoleOutputStream.Stdout },
		};

		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected override ConsoleWriterPipelineStage CreateStage(string name)
		{
			return new ConsoleWriterPipelineStage(name);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
		/// (only non-default stuff is checked, the rest is done by the base test class).
		/// </summary>
		[Fact]
		public void Create()
		{
			var stage = CreateStage("Console");
			Assert.Equal(sDefaultSettings, stage.Settings.ToDictionary(x => x.Key, x => x.Value.Value));
			Assert.Equal(ConsoleOutputStream.Stdout, stage.DefaultStream);
			Assert.Empty(stage.StreamByLevelOverrides);
		}

		/// <summary>
		/// Test data for the tests concerning the mapping of log levels to console output streams (stdout/stderr).
		/// </summary>
		public static IEnumerable<object[]> LogLevelToStreamMapping_TestData
		{
			get
			{
				List<Tuple<LogLevel, ConsoleOutputStream>> mappings;

				// test adding log levels to the same stream
				foreach (var stream in new[] { ConsoleOutputStream.Stdout, ConsoleOutputStream.Stdout })
				{
					mappings = new List<Tuple<LogLevel, ConsoleOutputStream>>();
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Failure, stream));
					yield return new object[] { mappings };

					mappings = new List<Tuple<LogLevel, ConsoleOutputStream>>();
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Failure, stream));
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Error, stream));
					yield return new object[] { mappings };

					mappings = new List<Tuple<LogLevel, ConsoleOutputStream>>();
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Failure, stream));
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Error, stream));
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Warning, stream));
					yield return new object[] { mappings };

					mappings = new List<Tuple<LogLevel, ConsoleOutputStream>>();
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Failure, stream));
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Error, stream));
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Warning, stream));
					mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Note, stream));
					yield return new object[] { mappings };
				}

				// now test whether overwriting existing mappings work
				mappings = new List<Tuple<LogLevel, ConsoleOutputStream>>();
				mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Failure, ConsoleOutputStream.Stdout));
				mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Error, ConsoleOutputStream.Stdout));
				mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Warning, ConsoleOutputStream.Stdout));
				mappings.Add(new Tuple<LogLevel, ConsoleOutputStream>(LogLevel.Error, ConsoleOutputStream.Stderr)); // overwrite (!)
				yield return new object[] { mappings };
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
			var stage = CreateStage("Console");

			// add the mappings and calculate the expected result
			Dictionary<LogLevel, ConsoleOutputStream> expectedMapping = new Dictionary<LogLevel, ConsoleOutputStream>();
			foreach (var mapping in mappings)
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
			var stage = CreateStage("Console");
			Assert.Throws<ArgumentNullException>(() => stage.MapLogLevelToStream(null, ConsoleOutputStream.Stdout));
		}

		/// <summary>
		/// Tests whether <see cref="ConsoleWriterPipelineStage.MapLogLevelToStream"/> throws an exception,
		/// if the pipeline stage is initialized (attached to the logging subsystem).
		/// </summary>
		[Fact]
		public void MapLogLevelToStream_FailsIfInitialized()
		{
			var stage = CreateStage("Console");
			((IProcessingPipelineStage) stage).Initialize();
			Assert.Throws<InvalidOperationException>(() => stage.MapLogLevelToStream(LogLevel.Note, ConsoleOutputStream.Stdout));
		}

		/// <summary>
		/// Tests whether explicitly mapping log levels to a specific output stream using
		/// <see cref="ConsoleWriterPipelineStage.StreamByLevelOverrides"/> works as excepted.
		/// </summary>
		[Theory]
		[MemberData(nameof(LogLevelToStreamMapping_TestData))]
		public void StreamByLevelOverrides(List<Tuple<LogLevel, ConsoleOutputStream>> mappings)
		{
			var stage = CreateStage("Console");

			// add the mappings and calculate the expected result
			Dictionary<LogLevel, ConsoleOutputStream> expectedMapping = new Dictionary<LogLevel, ConsoleOutputStream>();
			foreach (var mapping in mappings) expectedMapping[mapping.Item1] = mapping.Item2;
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
			var stage = CreateStage("Console");
			Assert.Throws<ArgumentNullException>(() => stage.StreamByLevelOverrides = null);
		}

		/// <summary>
		/// Tests whether setting <see cref="ConsoleWriterPipelineStage.StreamByLevelOverrides"/> throws an exception,
		/// if the pipeline stage is initialized (attached to the logging subsystem).
		/// </summary>
		[Fact]
		public void StreamByLevelOverrides_FailsIfInitialized()
		{
			var stage = CreateStage("Console");
			((IProcessingPipelineStage) stage).Initialize();
			Assert.Throws<InvalidOperationException>(() => stage.StreamByLevelOverrides = new Dictionary<LogLevel, ConsoleOutputStream>());
		}

		/// <summary>
		/// Test data for the tests concerning the mapping of log levels to console output streams (stdout/stderr).
		/// </summary>
		public static IEnumerable<object[]> Process_TestData
		{
			get
			{
				foreach (var defaultStream in new[] { ConsoleOutputStream.Stdout, ConsoleOutputStream.Stderr })
				{
					foreach (var args in LogLevelToStreamMapping_TestData)
					{
						var mappings = (List<Tuple<LogLevel, ConsoleOutputStream>>)args[0];
						foreach (var messages in TestData.LocalLogMessageSet)
						{
							yield return new object[] { defaultStream, mappings, messages };
						}
					}
				}
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
		public void Process(ConsoleOutputStream defaultStream, List<Tuple<LogLevel, ConsoleOutputStream>> mappings, IEnumerable<LocalLogMessage> messages)
		{
			// replace the stdout/stderr streams of the console
			var stdoutStream = new MemoryStream();
			var stderrStream = new MemoryStream();
			var stdoutWriter = new StreamWriter(stdoutStream);
			var stderrWriter = new StreamWriter(stderrStream);
			Console.SetOut(stdoutWriter);
			Console.SetError(stderrWriter);

			// create a new pipeline stage
			var formatter = new TestFormatter();
			var stage = CreateStage("Console");
			stage.DefaultStream = defaultStream;
			stage.Formatter = formatter;

			// configure the stage
			// build a dictionary that contains the mappings from log level to the corresponding console stream
			Dictionary<LogLevel, ConsoleOutputStream> levelToStreamMap = new Dictionary<LogLevel, ConsoleOutputStream>();
			foreach (var mapping in mappings)
			{
				levelToStreamMap[mapping.Item1] = mapping.Item2;
				stage.MapLogLevelToStream(mapping.Item1, mapping.Item2);
			}

			// initialize the pipeline stage
			((IProcessingPipelineStage) stage).Initialize();

			// process the message and determine the expected output in stdout/stderr
			StringBuilder expectedStdout = new StringBuilder();
			StringBuilder expectedStderr = new StringBuilder();
			foreach (var message in messages)
			{
				((IProcessingPipelineStage) stage).ProcessMessage(message);

				if (!levelToStreamMap.TryGetValue(message.LogLevel, out var stream)) {
					stream = defaultStream;
				}

				var formattedOutput = formatter.Format(message);
				if (stream == ConsoleOutputStream.Stdout)
				{
					expectedStdout.Append(formattedOutput);
					expectedStdout.AppendLine(); // the console writer adds a newline
				}
				else
				{
					expectedStderr.Append(formattedOutput);
					expectedStderr.AppendLine(); // the console writer adds a newline
				}
			}

			// give the message some time to travel through the pipeline
			Thread.Sleep(50);

			// the streams should contain the output now
			var stdoutReader = new StreamReader(new MemoryStream(stdoutStream.ToArray()));
			var stderrReader = new StreamReader(new MemoryStream(stderrStream.ToArray()));
			var stdoutOutput = stdoutReader.ReadToEnd();
			var stderrOutput = stderrReader.ReadToEnd();
			Assert.Equal(expectedStdout.ToString(), stdoutOutput);
			Assert.Equal(expectedStderr.ToString(), stderrOutput);
		}
	}
}
