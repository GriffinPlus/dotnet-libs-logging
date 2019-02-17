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

using System;
using System.Globalization;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Fluent API extension methods for the <see cref="TextWriterPipelineStage{STAGE}"/> class.
	/// </summary>
	public static class TextWriterPipelineStageExtensions
	{
		/// <summary>
		/// Sets the format provider to use when formatting log messages.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="provider">The format provider to use (default: <see cref="CultureInfo.InvariantCulture"/>).</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithFormatProvider<STAGE>(this STAGE @this, IFormatProvider provider) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.FormatProvider = provider;
			return @this;
		}

		/// <summary>
		/// Enables the timestamp column and sets the format of the timestamps written to the console
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithTimestamp<STAGE>(this STAGE @this, string format = "u") where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddTimestampColumn(format);
			return @this;
		}

		/// <summary>
		/// Enables the column showing the id of the process that has written a log message.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithProcessId<STAGE>(this STAGE @this) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddProcessIdColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the process that has written a log message.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithProcessName<STAGE>(this STAGE @this) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddProcessNameColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the application that has written a log message.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithApplicationName<STAGE>(this STAGE @this) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddApplicationNameColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the log writer that was used to write a log message.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithLogWriter<STAGE>(this STAGE @this) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddLogWriterColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the log level that was used to write a log message.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithLogLevel<STAGE>(this STAGE @this) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddLogLevelColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the message text.
		/// </summary>
		/// <param name="this">The pipeline stage.</param>
		/// <returns>The modified pipeline stage.</returns>
		public static STAGE WithText<STAGE>(this STAGE @this) where STAGE: TextWriterPipelineStage<STAGE>
		{
			@this.AddTextColumn();
			return @this;
		}

	}
}
