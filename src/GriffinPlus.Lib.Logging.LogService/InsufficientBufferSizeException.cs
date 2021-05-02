///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.LogService
{

	/// <summary>
	/// Exception that is thrown when a buffer is not large enough to keep the required data.
	/// The implementation of formatting functions often assumes the worst case when using a buffer,
	/// so this exception can be raised, although there actually is enough space in the buffer to store
	/// a specific piece of data.
	/// </summary>
	sealed class InsufficientBufferSizeException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="InsufficientBufferSizeException"/> class.
		/// </summary>
		public InsufficientBufferSizeException()
		{
		}
	}

}
