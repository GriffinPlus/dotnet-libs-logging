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

using System.Text;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A simple log message formatter for unit test purposes only.
	/// </summary>
	public class TestFormatter : ILogMessageFormatter
	{
		/// <summary>
		/// Formats the specified log message.
		/// </summary>
		/// <param name="message">Message to format.</param>
		/// <returns>The formatted log message.</returns>
		public string Format(ILogMessage message)
		{
			StringBuilder builder = new StringBuilder();

			builder.AppendFormat(
				"{0} ### {1} ### {2} ### {3} ### {4} ### {5} ### {6} ### {7}",
				message.Timestamp,
				message.HighPrecisionTimestamp,
				message.LogLevelName,
				message.LogWriterName,
				message.ProcessId,
				message.ProcessName,
				message.ApplicationName,
				message.Text);

			return builder.ToString();
		}
	}
}
