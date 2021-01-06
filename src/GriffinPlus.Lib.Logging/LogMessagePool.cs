///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Contracts;

// ReSharper disable UnusedMember.Global

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A pool of log messages allowing log messages to be re-used to reduce garbage collection pressure (thread-safe).
	/// </summary>
	public class LogMessagePool
	{
		private readonly ConcurrentBag<LogMessage> mMessages;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessagePool"/> class.
		/// </summary>
		public LogMessagePool()
		{
			mMessages = new ConcurrentBag<LogMessage>();
		}

		/// <summary>
		/// Gets the default log message pool.
		/// </summary>
		public static LogMessagePool Default { get; } = new LogMessagePool();

		/// <summary>
		/// Gets an empty log message from the pool.
		/// Creates a new log message, if the pool is empty.
		/// Call <see cref="LogMessage.InitWith"/> or <see cref="LogMessage"/> properties to initialize it.
		/// The <see cref="LogMessage.IsInitialized"/> property is <c>true</c> right from start.
		/// </summary>
		/// <returns>
		/// The requested log message.
		/// Please call <see cref="IReferenceManagement.Release()"/> to return it to the pool.
		/// Not releasing the message properly will not allow the pool to re-use messages.
		/// </returns>
		public LogMessage GetMessage()
		{
			if (mMessages.TryTake(out var message))
			{
				// ReSharper disable once RedundantAssignment
				int refCount = message.AddRef();
				Debug.Assert(refCount == 1);
			}
			else
			{
				message = new LogMessage(this);
			}

			return message;
		}

		/// <summary>
		/// Gets a log message from the pool and initializes it.
		/// Creates a new log message, if the pool is empty.
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
		/// <param name="processId">Id of the process emitting the log message.</param>
		/// <param name="text">The actual text the log message is about.</param>
		/// <returns>
		/// The requested log message.
		/// Please call <see cref="IReferenceManagement.Release()"/> to return it to the pool.
		/// Not releasing the message properly will not allow the pool to re-use messages.
		/// </returns>
		public LogMessage GetMessage(
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
			if (mMessages.TryTake(out var message))
			{
				// ReSharper disable once RedundantAssignment
				int refCount = message.AddRef();
				Debug.Assert(refCount == 1);
			}
			else
			{
				message = new LogMessage(this);
			}

			message.InitWith(
				id,
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

			return message;
		}

		/// <summary>
		/// Gets an uninitialized log message from the pool and prepares it for asynchronous initialization.
		/// Creates a new log message, if the pool is empty.
		/// The <see cref="LogMessage.IsInitialized"/> property is <c>false</c> at start and set to <c>true</c>
		/// when <see cref="ILogMessageInitializer.Initialize"/> is called to initialize the message later on.
		/// </summary>
		/// <param name="readOnly">
		/// true to get a read-only message that can only be set by the returned initializer, but not using any properties or <see cref="LogMessage.InitWith"/>;
		/// false to get a regular message that can be modified as usual using properties and <see cref="LogMessage.InitWith"/> after it has been initialized
		/// asynchronously.
		/// </param>
		/// <param name="initializer">
		/// Receives the initializer that allows to initialize the log message.
		/// If you intend to store the initializer somewhere to initialize the message later on, please call
		/// <see cref="IReferenceManagement.AddRef"/> to increment the reference counter and signal that the
		/// message is referenced somewhere else. This avoids returning the message to the pool although
		/// initialization is still pending.
		/// gets 0.
		/// </param>
		/// <returns>
		/// The requested log message.
		/// Please call <see cref="IReferenceManagement.Release()"/> to return it to the pool.
		/// Not releasing the message properly will not allow the pool to re-use messages.
		/// </returns>
		public LogMessage GetMessageWithAsyncInit(bool readOnly, out ILogMessageInitializer initializer)
		{
			if (mMessages.TryTake(out var message))
			{
				// ReSharper disable once RedundantAssignment
				int refCount = message.AddRef();
				Debug.Assert(refCount == 1);
			}
			else
			{
				message = new LogMessage(this);
			}

			// update the administrative state
			message.IsInitializedInternal = false;
			message.IsAsyncInitPending = true;
			message.IsReadOnlyInternal = readOnly;

			// the message is its own initializer
			initializer = message;

			return message;
		}

		/// <summary>
		/// Returns a log message to the pool, so it can be re-used.
		/// This message is called by the messages, if their reference counter gets 0.
		/// </summary>
		/// <param name="message">Message to return to the pool.</param>
		internal void ReturnMessage(LogMessage message)
		{
			Contract.Assert(message.IsAsyncInitPending, "Returning a log message with pending asynchronous initialization is not allowed.");

			if (message.IsAsyncInitPending)
				return;

			message.Reset();
			mMessages.Add(message);
		}
	}

}
