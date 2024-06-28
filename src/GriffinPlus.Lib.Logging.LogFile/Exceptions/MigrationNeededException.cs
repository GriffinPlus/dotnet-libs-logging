///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Exception that is thrown, if the log file needs to be migrated.
/// (most probably the file is opened read-only, so a migration cannot be done).
/// </summary>
public class MigrationNeededException : LogFileException
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationNeededException"/> class.
	/// </summary>
	public MigrationNeededException() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationNeededException"/> class.
	/// </summary>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	public MigrationNeededException(string message) : base(message) { }

	/// <summary>
	/// Initializes a new instance of the <see cref="MigrationNeededException"/> class.
	/// </summary>
	/// <param name="message">Message describing the reason why the exception is thrown.</param>
	/// <param name="innerException">The original exception that led to the exception being thrown.</param>
	public MigrationNeededException(string message, Exception innerException) : base(message, innerException) { }
}
