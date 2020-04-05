///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Exception that is thrown when something is wrong in the logging subsystem.
	/// </summary>
	public class LoggingException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingException"/> class.
		/// </summary>
		public LoggingException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LoggingException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LoggingException"/> class.
		/// </summary>
		/// <param name="format">Format string for the message describing the reason why the exception is thrown.</param>
		/// <param name="args">Arguments to use when formatting the format string.</param>
		public LoggingException(string format, params object[] args) : base(string.Format(format, args))
		{

		}
	}
}
