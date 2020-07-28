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
			foreach (var path in mTemporaryFiles)
			{
				try { File.Delete(path); } catch { /* swallow */ }
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
		void Create()
		{
			var stage = CreateStage("File");
			Assert.Empty(stage.Settings);
		}

		public static IEnumerable<object[]> Process_TestData
		{
			get
			{
				foreach (var messages in TestData.LocalLogMessageSet)
				{
					yield return new object[] { messages };
				}
			}
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
			((IProcessingPipelineStage) stage).Initialize();

			// process the message and determine the expected output in stdout/stderr
			StringBuilder expected = new StringBuilder();
			foreach (var message in messages)
			{
				((IProcessingPipelineStage) stage).ProcessMessage(message);
				expected.Append(formatter.Format(message));
				expected.AppendLine(); // a newline is automatically added after a message
			}

			// shut the pipeline stage down to release the file
			((IProcessingPipelineStage) stage).Shutdown();

			// the file should contain the expected output now
			using (var fs = new FileStream(stage.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (var reader = new StreamReader(fs))
			{
				var content = reader.ReadToEnd();
				Assert.Equal(expected.ToString(), content);
			}
		}

	}
}
