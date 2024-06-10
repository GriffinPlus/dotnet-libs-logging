///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Threading;

using Xunit;

// ReSharper disable AccessToDisposedClosure

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Unit tests targeting the <see cref="ProcessIntegration"/> class.
/// </summary>
public class ProcessIntegrationTests
{
	public static IEnumerable<object[]> IntegrateIntoLogging_TestData
	{
		get
		{
			foreach (string stream in new[] { "stdout", "stderr" })
			{
				// test waiting for process exit using the synchronous method:
				// ProcessIntegration.WaitForExit() (wait infinitely)
				yield return
				[
					stream,
					"WaitForExit()",
					new Func<ProcessIntegration, Task>(
						integration =>
						{
							return Task.Factory.StartNew(
								() => // needed to avoid blocking the only thread in the AsyncContext!
								{
									integration.WaitForExit();
									Assert.True(integration.Process.HasExited);
									return Task.CompletedTask;
								},
								CancellationToken.None,
								TaskCreationOptions.LongRunning,
								TaskScheduler.Default);
						})
				];

				// test waiting for process exit using the synchronous method:
				// ProcessIntegration.WaitForExit(int milliseconds) with milliseconds = 0 (do not wait)
				yield return
				[
					stream,
					"WaitForExit(0)",
					new Func<ProcessIntegration, Task>(
						integration =>
						{
							bool success = integration.WaitForExit(0);
							Assert.False(success); // the ConsolePrinter process takes at least 1 second (fixed delay) to exit
							Assert.False(integration.Process.HasExited);
							return Task.CompletedTask;
						})
				];

				// test waiting for process exit using the synchronous method:
				// ProcessIntegration.WaitForExit(int milliseconds) with milliseconds = 100 (do not wait long enough)
				yield return
				[
					stream,
					"WaitForExit(100)",
					new Func<ProcessIntegration, Task>(
						integration =>
						{
							return Task.Factory.StartNew(
								() => // needed to avoid blocking the only thread in the AsyncContext!
								{
									bool success = integration.WaitForExit(100);
									Assert.False(success); // the ConsolePrinter process takes at least 1 second (fixed delay) to exit
									Assert.False(integration.Process.HasExited);
								},
								CancellationToken.None,
								TaskCreationOptions.LongRunning,
								TaskScheduler.Default);
						})
				];

				// test waiting for process exit using the synchronous method:
				// ProcessIntegration.WaitForExit(int milliseconds) with milliseconds = 2000 (time is sufficient to allow the process to exit)
				yield return
				[
					stream,
					"WaitForExit(2000)",
					new Func<ProcessIntegration, Task>(
						integration =>
						{
							return Task.Factory.StartNew(
								() => // needed to avoid blocking the only thread in the AsyncContext!
								{
									bool success = integration.WaitForExit(10000);
									Assert.True(
										success,
										"The process should have exited after 10000ms."); // the ConsolePrinter process takes at least 1 second (fixed delay) to exit
									Assert.True(integration.Process.HasExited);
								},
								CancellationToken.None,
								TaskCreationOptions.LongRunning,
								TaskScheduler.Default);
						})
				];

				// test waiting for process exit using the asynchronous method:
				// ProcessIntegration.WaitForExitAsync(CancellationToken cancellationToken) (wait infinitely)
				yield return
				[
					stream,
					"WaitForExitAsync(ct) with ct = CancellationToken.None)",
					new Func<ProcessIntegration, Task>(
						async integration =>
						{
							await integration
								.WaitForExitAsync(CancellationToken.None)
								.ConfigureAwait(false);

							Assert.True(integration.Process.HasExited);
						})
				];

				// test waiting for process exit using the asynchronous method:
				// ProcessIntegration.WaitForExitAsync(CancellationToken cancellationToken) (cancel before process completes)
				yield return
				[
					stream,
					"WaitForExitAsync(ct) with ct signaled after 100ms",
					new Func<ProcessIntegration, Task>(
						async integration =>
						{
							using var cts = new CancellationTokenSource(100);
							await Assert
								.ThrowsAsync<TaskCanceledException>(
									async () => await integration
										            .WaitForExitAsync(cts.Token)
										            .ConfigureAwait(false))
								.ConfigureAwait(false);

							Assert.False(integration.Process.HasExited);
						})
				];
			}
		}
	}

	[Theory]
	[MemberData(nameof(IntegrateIntoLogging_TestData))]
	private void IntegrateIntoLogging(string stream, string description, Func<ProcessIntegration, Task> waitForExit)
	{
		// it is sufficient to test one newline character sequence as newlines are mangled when traveling from the executed process to this process
		// (nevertheless it is necessary to use a deterministic character sequence to build up the test data to compare the output with)
		const string newline = "\n";

		// create a file containing mix log messages as JSON
		Tuple<string, ILogMessage[], HashSet<int>> data = JsonMessageReaderTests.GetTestData(
				1,       // generate one test set only
				10000,   // the test set should contain 10000 log messages
				10000,   //
				newline, // use specified newline character sequence only to make reconstruction possible when receiving data via stdout/stderr
				false)
			.First(); // do not inject random whitespaces between log messages as these would screw up printing to the console and vice versa

		// prepare unique file name to avoid issues with tests running in parallel
		string testDataFile = $"TestData_IntegrateIntoLogging_{Guid.NewGuid():D}.json";

		try
		{
			// write file containing the log messages in JSON format to feed into the Console Printer
			File.WriteAllText(testDataFile, data.Item1, Encoding.UTF8);

			// set up the process integration for running the console printer process
			using ProcessIntegration integration = PrepareConsolePrinterIntegration(stream, testDataFile);
			integration.IsLoggingMessagesEnabled = false;

			var stdoutReceivedText = new StringBuilder();
			var stdoutReceivedMessages = new List<ILogMessage>();
			var stdoutTextFinishedEvent = new AsyncManualResetEvent();
			var stdoutMessageFinishedEvent = new AsyncManualResetEvent();

			var stderrReceivedText = new StringBuilder();
			var stderrReceivedMessages = new List<ILogMessage>();
			var stderrTextFinishedEvent = new AsyncManualResetEvent();
			var stderrMessageFinishedEvent = new AsyncManualResetEvent();

			// run processing in a thread that supports marshalling calls into it
			// to avoid that event handlers are called out of order by pool threads
			var thread = new AsyncContextThread();
			thread.Factory.Run(
					async () =>
					{
						integration.OutputStreamReceivedText += (_, args) =>
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

						integration.OutputStreamReceivedMessage += (_, args) =>
						{
							// abort, if the process has exited
							if (args.Message == null)
							{
								stdoutMessageFinishedEvent.Set();
								return;
							}

							stdoutReceivedMessages.Add(args.Message);
						};

						integration.ErrorStreamReceivedText += (_, args) =>
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

						integration.ErrorStreamReceivedMessage += (_, args) =>
						{
							// abort, if the process has exited
							if (args.Message == null)
							{
								stderrMessageFinishedEvent.Set();
								return;
							}

							stderrReceivedMessages.Add(args.Message);
						};

						// start process
						integration.StartProcess();

						// run the various WaitForExit[Async] methods
						// (some may return before the process has actually exited)
						await waitForExit(integration);

						// wait for all event handlers to receive the terminating event arguments
						// (abort after 30 seconds)
						using var cts = new CancellationTokenSource(30000);
						await stdoutTextFinishedEvent.WaitAsync(cts.Token);
						await stdoutMessageFinishedEvent.WaitAsync(cts.Token);
						await stderrTextFinishedEvent.WaitAsync(cts.Token);
						await stderrMessageFinishedEvent.WaitAsync(cts.Token);
					})
				.WaitAndUnwrapException();

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
		finally
		{
			if (File.Exists(testDataFile))
			{
				File.Delete(testDataFile);
			}
		}
	}

	/// <summary>
	/// Sets up running the ConsolePrinter process and integrates it with the logging subsystem.
	/// </summary>
	/// <param name="stream">Stream the ConsolePrinter is expected to write the file to (can be 'stdout' or 'stderr').</param>
	/// <param name="testDataFile">File the ConsolePrinter should write to the output/error stream.</param>
	/// <returns>The prepared process integration.</returns>
	private static ProcessIntegration PrepareConsolePrinterIntegration(string stream, string testDataFile)
	{
#if NETCOREAPP
		// determine the path of the output directory
		string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		Assert.NotNull(directory);

		// start the process to print test data to stdout/stderr
		string dotnetExecutable = Environment.OSVersion.Platform == PlatformID.Win32NT ? "dotnet.exe" : "dotnet";
		string consolePrinterPath = Path.Combine(directory, "ConsolePrinter.dll");
		Assert.True(File.Exists(consolePrinterPath), $"{consolePrinterPath} does not exist.");
		var startInfo = new ProcessStartInfo(dotnetExecutable, $"\"{consolePrinterPath}\" {stream} {testDataFile}") { WorkingDirectory = directory };
		var process = new Process { StartInfo = startInfo };
		ProcessIntegration integration = ProcessIntegration.IntegrateIntoLogging(process);
		Assert.Equal($"External Process ({dotnetExecutable})", integration.LogWriter.Name);

#elif NETFRAMEWORK
		// determine the path of the output directory
		var codeBaseUrl = new Uri(Assembly.GetExecutingAssembly().CodeBase);
		string codeBasePath = Uri.UnescapeDataString(codeBaseUrl.AbsolutePath);
		string directory = Path.GetDirectoryName(codeBasePath);
		Assert.NotNull(directory);

		// start the process to print test data to stdout/stderr
		string consolePrinterPath = Path.Combine(directory, "ConsolePrinter.exe");
		Assert.True(File.Exists(consolePrinterPath), $"{consolePrinterPath} does not exist.");
		var startInfo = new ProcessStartInfo(consolePrinterPath, $"{stream} {testDataFile}") { WorkingDirectory = directory };
		var process = new Process { StartInfo = startInfo };
		ProcessIntegration integration = ProcessIntegration.IntegrateIntoLogging(process);
		Assert.Equal("External Process (ConsolePrinter.exe)", integration.LogWriter.Name);
#endif

		Assert.Same(process, integration.Process);
		Assert.True(integration.IsLoggingMessagesEnabled);
		return integration;
	}
}
