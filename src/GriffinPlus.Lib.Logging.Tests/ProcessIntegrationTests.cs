///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Unit tests targeting the <see cref="ProcessIntegration"/> class.
	/// </summary>
	public class ProcessIntegrationTests
	{
		[Theory]
		[InlineData("stdout")]
		[InlineData("stderr")]
		void IntegrateIntoLogging(string stream)
		{
			const string testDataFile = "TestData_IntegrateIntoLogging.json";

			// it is sufficient to test one newline character sequence as newlines are mangled when traveling from the executed process to this process
			// (nevertheless it is necessary to use a deterministic character sequence to build up the test data to compare the output with)
			const string newline = "\n"; 

			// clear the synchronization context to avoid the XUnit synchronization context mix up the order of events
			// fired when output/error stream data is passed to the client
			SynchronizationContext.SetSynchronizationContext(null);

			// create a file containing mix log messages as JSON
			var data = JsonMessageReaderTests.GetTestData(
				1,              // generate one test set only
				100000, 100000, // the test set should contain 100000 log messages
				newline,        // use specified newline character sequence only to make reconstruction possible when receiving data via stdout/stderr
				false).First(); // do not inject random whitespaces between log messages as these would screw up printing to the console and vice versa
			File.WriteAllText(testDataFile, data.Item1, Encoding.UTF8);

			// run the console printer to emit the file over stdout/stderr
#if NETCOREAPP
			string dotnetExecutable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";
			ProcessStartInfo startInfo = new ProcessStartInfo(dotnetExecutable, $"ConsolePrinter.dll {stream} {testDataFile}");
			Process process = new Process { StartInfo = startInfo };
			var integration = ProcessIntegration.IntegrateIntoLogging(process);
			Assert.Equal($"External Process ({dotnetExecutable})", integration.LogWriter.Name);
#elif NETFRAMEWORK
			ProcessStartInfo startInfo = new ProcessStartInfo("ConsolePrinter.exe", $"{stream} {testDataFile}");
			Process process = new Process { StartInfo = startInfo };
			var integration = ProcessIntegration.IntegrateIntoLogging(process);
			Assert.Equal("External Process (ConsolePrinter.exe)", integration.LogWriter.Name);
#endif
			Assert.Same(process, integration.Process);
			Assert.True(integration.IsLoggingMessagesEnabled);

			StringBuilder stdoutReceivedText = new StringBuilder();
			List<ILogMessage> stdoutReceivedMessages = new List<ILogMessage>();
			ManualResetEventSlim stdoutTextFinishedEvent = new ManualResetEventSlim();
			ManualResetEventSlim stdoutMessageFinishedEvent = new ManualResetEventSlim();

			StringBuilder stderrReceivedText = new StringBuilder();
			List<ILogMessage> stderrReceivedMessages = new List<ILogMessage>();
			ManualResetEventSlim stderrTextFinishedEvent = new ManualResetEventSlim();
			ManualResetEventSlim stderrMessageFinishedEvent = new ManualResetEventSlim();

			integration.OutputStreamReceivedText += (sender, args) =>
			{
				// abort, if the process has exited
				if (args.Line == null)
				{
					stdoutTextFinishedEvent.Set();
					return;
				}

				stdoutReceivedText.Append(args.Line);
				stdoutReceivedText.Append(newline);
			};

			integration.OutputStreamReceivedMessage += (sender, args) =>
			{
				// abort, if the process has exited
				if (args.Message == null)
				{
					stdoutMessageFinishedEvent.Set();
					return;
				}

				stdoutReceivedMessages.Add(args.Message);
			};

			integration.ErrorStreamReceivedText += (sender, args) =>
			{
				// abort, if the process has exited
				if (args.Line == null)
				{
					stderrTextFinishedEvent.Set();
					return;
				}

				stderrReceivedText.Append(args.Line);
				stderrReceivedText.Append(newline);
			};

			integration.ErrorStreamReceivedMessage += (sender, args) =>
			{
				// abort, if the process has exited
				if (args.Message == null)
				{
					stderrMessageFinishedEvent.Set();
					return;
				}

				stderrReceivedMessages.Add(args.Message);
			};

			integration.IsLoggingMessagesEnabled = false;
			integration.StartProcess();
			process.WaitForExit();

			// wait for all event handlers to receive the terminating event arguments
			Assert.True(stdoutTextFinishedEvent.Wait(5000), "Waiting for terminating event args for 'OutputStreamReceivedText' event timed out.");
			Assert.True(stdoutMessageFinishedEvent.Wait(5000), "Waiting for terminating event args for 'OutputStreamReceivedMessage' event timed out.");
			Assert.True(stderrTextFinishedEvent.Wait(5000), "Waiting for terminating event args for 'ErrorStreamReceivedText' event timed out.");
			Assert.True(stderrMessageFinishedEvent.Wait(5000), "Waiting for terminating event args for 'ErrorStreamReceivedMessage' event timed out.");

			if (stream == "stdout")
			{
				// stderr should not have received anything
				Assert.Equal(0, stderrReceivedText.Length);
				Assert.Empty(stderrReceivedMessages);

				// stdout should have received the text emitted by the console printer...
				Assert.Equal(data.Item1.Length, stdoutReceivedText.Length);
				Assert.Equal(data.Item1, stdoutReceivedText.ToString());

				// ... and the text should have been parsed into log messages
				Assert.Equal(data.Item2.Length, stdoutReceivedMessages.Count);
				Assert.Equal(data.Item2, stdoutReceivedMessages.ToArray());
			}
			else
			{
				// stdout should not have received anything
				Assert.Equal(0, stdoutReceivedText.Length);
				Assert.Empty(stdoutReceivedMessages);

				// stderr should have received the text emitted by the console printer...
				Assert.Equal(data.Item1.Length, stderrReceivedText.Length);
				Assert.Equal(data.Item1, stderrReceivedText.ToString());

				// ... and the text should have been parsed into log messages
				Assert.Equal(data.Item2.Length, stderrReceivedMessages.Count);
				Assert.Equal(data.Item2, stderrReceivedMessages.ToArray());
			}
		}
	}
}
