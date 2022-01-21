///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Xunit;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Unit tests targeting the <see cref="FileWriterPipelineStage"/> class.
	/// </summary>
	public class FileWriterPipelineStageTests : TextWriterPipelineStageBaseTests<FileWriterPipelineStage>, IDisposable
	{
		private readonly List<string> mTemporaryFiles = new List<string>();

		/// <summary>
		/// Disposes the test cleaning up temporary files.
		/// </summary>
		public void Dispose()
		{
			// some stages are initialized, but not shut down in tests, so they block their log file
			// => collect these stages and let finalizers release their files before deleting these files
			GC.Collect();
			GC.WaitForPendingFinalizers();
			foreach (string path in mTemporaryFiles)
			{
				try { File.Delete(path); }
				catch
				{
					/* swallow */
				}
			}
		}

		/// <summary>
		/// Creates a new instance of the pipeline stage.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		/// <returns>The created stage.</returns>
		protected override FileWriterPipelineStage CreateStage(string name)
		{
			string path = Path.GetFullPath($"TestLog_{Guid.NewGuid():N}.log");
			mTemporaryFiles.Add(path);
			return new FileWriterPipelineStage(name, path, false);
		}

		/// <summary>
		/// Tests whether creating a new instance of the pipeline stage succeeds and the stage is in the expected state
		/// (only non-default stuff is checked, the rest is done by the base test class).
		/// </summary>
		[Fact]
		private void Create()
		{
			var stage = CreateStage("File");
			Assert.Empty(stage.Settings);
		}

		public static IEnumerable<object[]> Process_TestData
		{
			get { return TestData.LocalLogMessageSet.Select(messages => new object[] { messages }); }
		}

		/// <summary>
		/// Tests whether messages that are passed to the pipeline stage are correctly written to the log file.
		/// </summary>
		/// <param name="messages">Messages to pass to the stage.</param>
		[Theory]
		[MemberData(nameof(Process_TestData))]
		public void Process(IEnumerable<LocalLogMessage> messages)
		{
			// create a new pipeline stage
			var formatter = new TestFormatter();
			var stage = CreateStage("File");
			stage.Formatter = formatter;

			// initialize the pipeline stage
			stage.Initialize();

			// process the message and determine the expected output in stdout/stderr
			var expected = new StringBuilder();
			foreach (var message in messages)
			{
				stage.ProcessMessage(message);
				expected.Append(formatter.Format(message));
				expected.AppendLine(); // a newline is automatically added after a message
			}

			// shut the pipeline stage down to release the file
			stage.Shutdown();

			// the file should contain the expected output now
			using (var fs = new FileStream(stage.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var reader = new StreamReader(fs))
			{
				string content = reader.ReadToEnd();
				Assert.Equal(expected.ToString(), content);
			}
		}
	}

}
