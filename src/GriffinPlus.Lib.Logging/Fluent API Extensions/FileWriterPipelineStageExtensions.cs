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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Fluent API extension methods for the <see cref="FileWriterPipelineStage"/> class.
	/// </summary>
	public static class FileWriterPipelineStageExtensions
	{
		/// <summary>
		/// Enables or disables flushing the file every time after a message is written.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="enable">
		/// <c>true</c> to enable flushing the file after writing a message;
		/// otherwise <c>false</c>.
		/// </param>
		/// <returns>The pipeline stage itself.</returns>
		public static FileWriterPipelineStage WithAutoFlush(this FileWriterPipelineStage @this, bool enable)
		{
			@this.AutoFlush = enable;
			return @this;
		}

	}
}
