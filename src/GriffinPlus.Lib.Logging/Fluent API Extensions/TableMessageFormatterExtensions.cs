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
using System.Globalization;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Fluent API extension methods for the <see cref="TableMessageFormatterExtensions"/> class.
	/// </summary>
	public static class TableMessageFormatterExtensions
	{
		/// <summary>
		/// Sets the format provider to use when formatting log messages.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <param name="provider">The format provider to use (default: <see cref="CultureInfo.InvariantCulture"/>).</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithFormatProvider(this TableMessageFormatter @this, IFormatProvider provider)
		{
			@this.FormatProvider = provider;
			return @this;
		}

		/// <summary>
		/// Enables the timestamp column and sets the format of the timestamps written to the console
		/// (default: "u", conversion to UTC and output using the format yyyy-MM-dd HH:mm:ssZ)
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <param name="format">
		/// The timestamp format (see https://msdn.microsoft.com/en-us/library/bb351892(v=vs.110).aspx" for details).
		/// </param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithTimestamp(this TableMessageFormatter @this, string format = "u")
		{
			@this.AddTimestampColumn(format);
			return @this;
		}

		/// <summary>
		/// Enables the column showing the id of the process that has written a log message.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithProcessId(this TableMessageFormatter @this)
		{
			@this.AddProcessIdColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the process that has written a log message.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithProcessName(this TableMessageFormatter @this)
		{
			@this.AddProcessNameColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the application that has written a log message.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithApplicationName(this TableMessageFormatter @this)
		{
			@this.AddApplicationNameColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the log writer that was used to write a log message.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithLogWriter(this TableMessageFormatter @this)
		{
			@this.AddLogWriterColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the name of the log level that was used to write a log message.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithLogLevel(this TableMessageFormatter @this)
		{
			@this.AddLogLevelColumn();
			return @this;
		}

		/// <summary>
		/// Enables the column showing the message text.
		/// </summary>
		/// <param name="this">The formatter.</param>
		/// <returns>The modified formatter.</returns>
		public static TableMessageFormatter WithText(this TableMessageFormatter @this)
		{
			@this.AddTextColumn();
			return @this;
		}

	}
}
