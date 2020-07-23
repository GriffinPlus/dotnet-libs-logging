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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Exception that is thrown when there is a problem communicating with the local log service.
	/// </summary>
	class LocalLogServiceCommunicationException : LoggingException
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogServiceCommunicationException"/> class.
		/// </summary>
		public LocalLogServiceCommunicationException()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogServiceCommunicationException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		public LocalLogServiceCommunicationException(string message) : base(message)
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogServiceCommunicationException"/> class.
		/// </summary>
		/// <param name="message">Message describing the reason why the exception is thrown.</param>
		/// <param name="innerException">The original exception that led to the exception being thrown.</param>
		public LocalLogServiceCommunicationException(string message, Exception innerException) : base(message, innerException)
		{

		}

	}
}
