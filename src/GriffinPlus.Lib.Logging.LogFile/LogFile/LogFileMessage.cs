///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A log message used in conjunction with the <see cref="LogFile"/> class and its collection wrapper
/// <see cref="Collections.FileBackedLogMessageCollection"/>.
/// </summary>
public sealed class LogFileMessage : LogMessage, IFileLogMessageInitializer
{
	/// <summary>
	/// Initializes a new instance of the <see cref="LogFileMessage"/> class.
	/// </summary>
	public LogFileMessage() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="LogFileMessage"/> class copying the specified one.
	/// </summary>
	/// <param name="other">Message to copy.</param>
	public LogFileMessage(LogFileMessage other) : base(other)
	{
		mId = other.Id;
	}

	#region Id

	private long mId = -1;

	/// <summary>
	/// Gets or sets the id uniquely identifying the message in a certain context, e.g. a collection or log file
	/// (-1, if the id is invalid).
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public long Id
	{
		get
		{
			lock (Sync) return mId;
		}

		set
		{
			bool changed = false;

			lock (Sync)
			{
				if (IsReadOnlyInternal)
					throw new NotSupportedException("The log message is read-only.");

				if (IsAsyncInitPending)
					throw new InvalidOperationException("Setting the property is not allowed as an asynchronous initialization is pending.");

				if (mId != value)
				{
					mId = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	#endregion

	#region Equality Check

	/// <summary>
	/// Checks whether the current log message equals the specified one.
	/// The following properties are _not_ taken into account:
	/// - <see cref="ILogMessage.IsReadOnly"/>
	/// </summary>
	/// <param name="other">Log message to compare with.</param>
	/// <returns>
	/// <see langword="true"/>, if the current log message equals the specified one;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public override bool Equals(LogMessage other)
	{
		if (other is LogFileMessage otherFileLogMessage)
		{
			lock (Sync)
			{
				return mId == otherFileLogMessage.Id && base.Equals(other);
			}
		}

		return base.Equals(other);
	}

	/// <summary>
	/// Gets the hash code of the log message.
	/// The following properties are _not_ taken into account:
	/// - <see cref="ILogMessage.IsReadOnly"/>
	/// </summary>
	/// <returns>Hash code of the log message.</returns>
	public override int GetHashCode()
	{
		lock (Sync)
		{
			unchecked
			{
				int hashCode = base.GetHashCode();
				hashCode = (hashCode * 397) ^ mId.GetHashCode();
				return hashCode;
			}
		}
	}

	#endregion

	#region Synchronous Initialization

	/// <summary>
	/// Initializes the log message atomically.
	/// The <see cref="INotifyPropertyChanged.PropertyChanged"/> event is only fired once.
	/// </summary>
	/// <param name="id">
	/// Gets or sets the id uniquely identifying the message in a certain scope, e.g. a log file;
	/// -1, if the id is invalid.
	/// </param>
	/// <param name="timestamp">Time the message was written to the log.</param>
	/// <param name="highPrecisionTimestamp">
	/// Timestamp for relative time measurements with high precision
	/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
	/// </param>
	/// <param name="lostMessageCount">
	/// Gets or sets the number of preceding messages that have been lost before this message
	/// (useful when dealing with message streams).
	/// </param>
	/// <param name="logWriterName">Name of the log writer that was used to emit the message.</param>
	/// <param name="logLevelName">Name of the log level that is associated with the message.</param>
	/// <param name="tags">Tags that are associated with the message.</param>
	/// <param name="applicationName">
	/// Name of the application emitting the log message
	/// (can differ from the process name, if the application is using an interpreter (the actual process)).
	/// </param>
	/// <param name="processName">Name of the process emitting the log message.</param>
	/// <param name="processId">ID of the process emitting the log message.</param>
	/// <param name="text">The actual text the log message is about.</param>
	/// <returns>The log message itself.</returns>
	/// <exception cref="NotSupportedException">The log message is read-only.</exception>
	/// <exception cref="InvalidOperationException">Initializing manually is not allowed as an asynchronous initialization is pending.</exception>
	public LogFileMessage InitWith(
		long           id,
		DateTimeOffset timestamp,
		long           highPrecisionTimestamp,
		int            lostMessageCount,
		string         logWriterName,
		string         logLevelName,
		TagSet         tags,
		string         applicationName,
		string         processName,
		int            processId,
		string         text)
	{
		lock (Sync)
		{
			// disable raising the PropertyChanged event
			PropertyChangedSuspensionCounter++;

			// set specific message fields
			mId = id;

			// set command message fields
			InitWith(
				timestamp,
				highPrecisionTimestamp,
				lostMessageCount,
				logWriterName,
				logLevelName,
				tags,
				applicationName,
				processName,
				processId,
				text);

			// enable raising the PropertyChanged event
			PropertyChangedSuspensionCounter--;
		}

		OnPropertyChanged(null);
		return this;
	}

	#endregion

	#region Asynchronous Initialization

	/// <summary>
	/// Creates a new log message and prepares it for asynchronous initialization.
	/// (<see cref="LogMessage.IsInitialized"/> is <see langword="false"/> at first and set to <see langword="true"/> as soon as the message is initialized).
	/// </summary>
	/// <param name="readOnly">
	/// <see langword="true"/> to create a read-only message that can only be set by the returned initializer;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <param name="initializer">Receives the initializer that allows to update the log message.</param>
	/// <returns>The created log message.</returns>
	public static LogFileMessage CreateWithAsyncInit(bool readOnly, out IFileLogMessageInitializer initializer)
	{
		var message = new LogFileMessage
		{
			IsInitializedInternal = false,
			IsAsyncInitPending = true,
			IsReadOnlyInternal = readOnly
		};

		initializer = message;
		return message;
	}

	/// <summary>
	/// Initializes the log message.
	/// </summary>
	/// <param name="id">ID uniquely identifying the message in the log file.</param>
	/// <param name="timestamp">Time the message was written to the log.</param>
	/// <param name="highPrecisionTimestamp">
	/// Timestamp for relative time measurements with high precision
	/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch"/> class).
	/// </param>
	/// <param name="lostMessageCount">
	/// Gets or sets the number of preceding messages that have been lost before this message
	/// (useful when dealing with message streams).
	/// </param>
	/// <param name="logWriterName">Name of the log writer that was used to emit the message.</param>
	/// <param name="logLevelName">Name of the log level that is associated with the message.</param>
	/// <param name="tags">Tags that are associated with the message.</param>
	/// <param name="applicationName">
	/// Name of the application emitting the log message
	/// (can differ from the process name, if the application is using an interpreter (the actual process)).
	/// </param>
	/// <param name="processName">Name of the process emitting the log message.</param>
	/// <param name="processId">ID of the process emitting the log message.</param>
	/// <param name="text">The actual text the log message is about.</param>
	/// <returns>The initialized log message.</returns>
	/// <exception cref="InvalidOperationException">The log message has not been prepared for asynchronous initialization.</exception>
	/// <exception cref="InvalidOperationException">The log message is already initialized.</exception>
	LogFileMessage IFileLogMessageInitializer.Initialize(
		long           id,
		DateTimeOffset timestamp,
		long           highPrecisionTimestamp,
		int            lostMessageCount,
		string         logWriterName,
		string         logLevelName,
		TagSet         tags,
		string         applicationName,
		string         processName,
		int            processId,
		string         text)
	{
		lock (Sync)
		{
			// disable raising the PropertyChanged event
			PropertyChangedSuspensionCounter++;

			// set specific message fields
			mId = id;

			// set command message fields
			((ILogMessageInitializer)this).Initialize(
				timestamp,
				highPrecisionTimestamp,
				lostMessageCount,
				logWriterName,
				logLevelName,
				tags,
				applicationName,
				processName,
				processId,
				text);

			// enable raising the PropertyChanged event
			PropertyChangedSuspensionCounter--;
		}

		OnPropertyChanged(null);

		return this;
	}

	#endregion
}
