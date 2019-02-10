///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2019 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that logs messages to a file (thread-safe).
	/// </summary>
	public class FileWriterPipelineStage : TextWriterPipelineStage<FileWriterPipelineStage>
	{
		private readonly string mPath;
		private readonly bool mAppend;
		private FileStream mFile;
		private StreamWriter mWriter;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileWriterPipelineStage"/> class.
		/// </summary>
		/// <param name="path">Path of the file to write to.</param>
		/// <param name="append">
		/// true to append new messages to the specified file, if it exists already;
		/// false to truncate the file and start from scratch.
		/// </param>
		public FileWriterPipelineStage(string path, bool append)
		{
			mPath = path;
			mAppend = append; 
		}

		/// <summary>
		/// Performs pipeline stage specific initialization tasks that must run when the pipeline stage is attached to
		/// the logging subsystem. This method is called from within the pipeline stage lock.
		/// </summary>
		protected override void OnInitialize()
		{
			base.OnInitialize();

			mFile = new FileStream(mPath, mAppend ? FileMode.OpenOrCreate : FileMode.Create, FileAccess.Write, FileShare.Read);
			mWriter = new StreamWriter(mFile, Encoding.UTF8);
		}

		/// <summary>
		/// Performs pipeline stage specific cleanup tasks that must run when the pipeline stage is about to be detached
		/// from the logging subsystem. This method is called from within the pipeline stage lock.
		/// </summary>
		protected internal override void OnShutdown()
		{
			base.OnShutdown();

			mWriter?.Dispose();
			mWriter = null;
			mFile = null;
		}

		/// <summary>
		/// Emits the formatted log message.
		/// This method is called from within the pipeline stage lock (<see cref="AsyncProcessingPipelineStage{T}.Sync"/>).
		/// </summary>
		/// <param name="message">The current log message.</param>
		/// <param name="output">The formatted output of the current log message.</param>
		/// <param name="cancellationToken">Cancellation token that is signaled when the pipeline stage is shutting down.</param>
		protected override async Task EmitOutputAsync(LocalLogMessage message, StringBuilder output, CancellationToken cancellationToken)
		{
			await mWriter.WriteLineAsync(output.ToString());
			await mWriter.FlushAsync();
		}

	}
}
