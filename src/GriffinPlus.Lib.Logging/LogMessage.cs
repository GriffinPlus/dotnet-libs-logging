///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

using GriffinPlus.Lib.Events;

// ReSharper disable InconsistentNaming
// ReSharper disable NonReadonlyMemberInGetHashCode

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A log message for general purpose use (thread-safe).
/// </summary>
public class LogMessage : ILogMessage, ILogMessageInitializer, IEquatable<ILogMessage>, INotifyPropertyChanged
{
	/// <summary>
	/// Object that is used when synchronizing access to the log message.
	/// </summary>
	protected readonly object Sync = new();

	/// <summary>
	/// Initializes a new instance of the <see cref="LogMessage"/> class.
	/// </summary>
	public LogMessage() { }

	/// <summary>
	/// Initializes a new instance of the <see cref="LogMessage"/> class copying the specified one.
	/// </summary>
	/// <param name="other">Message to copy.</param>
	public LogMessage(ILogMessage other)
	{
		mTimestamp = other.Timestamp;
		mHighPrecisionTimestamp = other.HighPrecisionTimestamp;
		mLogWriterName = other.LogWriterName;
		mTags = other.Tags as TagSet ?? new TagSet(other.Tags);
		mLogLevelName = other.LogLevelName;
		mApplicationName = other.ApplicationName;
		mProcessName = other.ProcessName;
		mProcessId = other.ProcessId;
		mText = other.Text;
	}

	#region Initialization (Common)

	/// <summary>
	/// Indicates whether the log message is initialized.
	/// </summary>
	protected internal bool IsInitializedInternal = true;

	/// <summary>
	/// Gets a value indicating whether the log message is initialized.
	/// (This property can be <see langword="false"/>, if the log message is configured to be initialized asynchronously,
	/// but has not been initialized, yet).
	/// </summary>
	public bool IsInitialized
	{
		get
		{
			lock (Sync) return IsInitializedInternal;
		}
	}

	#endregion

	#region Synchronous Initialization

	/// <summary>
	/// Initializes the log message atomically.
	/// The <see cref="PropertyChanged"/> event is only fired once.
	/// </summary>
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
	public LogMessage InitWith(
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
			if (IsReadOnlyInternal)
				throw new NotSupportedException("The log message is read-only.");

			if (IsAsyncInitPending)
				throw new InvalidOperationException("Initializing manually is not allowed as an asynchronous initialization is pending.");

			mTimestamp = timestamp;
			mHighPrecisionTimestamp = highPrecisionTimestamp;
			mLostMessageCount = lostMessageCount;
			mLogWriterName = logWriterName;
			mLogLevelName = logLevelName;
			mTags = tags;
			mApplicationName = applicationName;
			mProcessName = processName;
			mProcessId = processId;
			mText = text;

			IsInitializedInternal = true;
		}

		OnPropertyChanged(null);

		return this;
	}

	#endregion

	#region Asynchronous Initialization

	/// <summary>
	/// Indicates whether the log message is initialized asynchronously and initialization is still pending.
	/// </summary>
	protected internal bool IsAsyncInitPending;

	/// <summary>
	/// Creates a new log message and prepares it for asynchronous initialization.
	/// (<see cref="IsInitialized"/> is <see langword="false"/> at first and set to <see langword="true"/> as soon as the message is initialized).
	/// </summary>
	/// <param name="readOnly">
	/// <see langword="true"/> to create a read-only message that can only be set by the returned initializer;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	/// <param name="initializer">Receives the initializer that allows to update the log message.</param>
	/// <returns>The created log message.</returns>
	public static LogMessage CreateWithAsyncInit(bool readOnly, out ILogMessageInitializer initializer)
	{
		var message = new LogMessage
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
	LogMessage ILogMessageInitializer.Initialize(
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
			if (!IsAsyncInitPending)
				throw new InvalidOperationException("The log message is not prepared for asynchronous initialization.");

			if (IsInitializedInternal)
				throw new InvalidOperationException("The log message is already initialized.");

			mTimestamp = timestamp;
			mHighPrecisionTimestamp = highPrecisionTimestamp;
			mLostMessageCount = lostMessageCount;
			mLogWriterName = logWriterName;
			mLogLevelName = logLevelName;
			mTags = tags;
			mApplicationName = applicationName;
			mProcessName = processName;
			mProcessId = processId;
			mText = text;

			IsAsyncInitPending = false;
			IsInitializedInternal = true;
		}

		OnPropertyChanged(null);

		return this;
	}

	#endregion

	#region Message Properties

	private int            mLostMessageCount;
	private DateTimeOffset mTimestamp;
	private long           mHighPrecisionTimestamp;
	private string         mLogWriterName;
	private string         mLogLevelName;
	private TagSet         mTags;
	private string         mApplicationName;
	private string         mProcessName;
	private int            mProcessId = -1;
	private string         mText;

	/// <summary>
	/// Gets or sets the number of preceding messages that have been lost before this message
	/// (useful when dealing with message streams).
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public int LostMessageCount
	{
		get
		{
			lock (Sync) return mLostMessageCount;
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

				if (mLostMessageCount != value)
				{
					mLostMessageCount = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the date/time the message was written to the log.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public DateTimeOffset Timestamp
	{
		get
		{
			lock (Sync) return mTimestamp;
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

				if (mTimestamp != value)
				{
					mTimestamp = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the timestamp for relative time measurements with high precision
	/// (in nanoseconds, but the actual precision depends on the system timer).
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public long HighPrecisionTimestamp
	{
		get
		{
			lock (Sync) return mHighPrecisionTimestamp;
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

				if (mHighPrecisionTimestamp != value)
				{
					mHighPrecisionTimestamp = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the name of the log writer associated with the log message.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public string LogWriterName
	{
		get
		{
			lock (Sync) return mLogWriterName;
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

				if (mLogWriterName != value)
				{
					mLogWriterName = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the name of the log level associated with the log message.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public string LogLevelName
	{
		get
		{
			lock (Sync) return mLogLevelName;
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

				if (mLogLevelName != value)
				{
					mLogLevelName = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets tags attached to the log message.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public TagSet Tags
	{
		get
		{
			lock (Sync) return mTags;
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

				if (mTags != value)
				{
					mTags = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets tags attached to the log message.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	ITagSet ILogMessage.Tags
	{
		get
		{
			lock (Sync) return mTags;
		}

		set
		{
			if (value is TagSet newTagSet)
			{
				bool changed = false;

				lock (Sync)
				{
					if (IsReadOnlyInternal)
						throw new NotSupportedException("The log message is read-only.");

					if (IsAsyncInitPending)
						throw new InvalidOperationException("Setting the property is not allowed as an asynchronous initialization is pending.");

					if (mTags != newTagSet)
					{
						mTags = newTagSet;
						changed = true;
					}
				}

				if (changed)
					OnPropertyChanged();
			}
			else
			{
				throw new ArgumentException($"The specified tag set is not a {typeof(TagSet).FullName}");
			}
		}
	}

	/// <summary>
	/// Gets or sets the name of the application emitting the log message
	/// (can differ from the process name, if the application is using an interpreter (the actual process)).
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public string ApplicationName
	{
		get
		{
			lock (Sync) return mApplicationName;
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

				if (mApplicationName != value)
				{
					mApplicationName = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the name of the process emitting the log message.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public string ProcessName
	{
		get
		{
			lock (Sync) return mProcessName;
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

				if (mProcessName != value)
				{
					mProcessName = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the id of the process emitting the log message
	/// (-1, if the process id is invalid/unset).
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public int ProcessId
	{
		get
		{
			lock (Sync) return mProcessId;
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

				if (mProcessId != value)
				{
					mProcessId = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Gets or sets the actual text the log message is about.
	/// </summary>
	/// <exception cref="NotSupportedException">The log message is read-only, setting the property is not supported.</exception>
	/// <exception cref="InvalidOperationException">Setting the property is not allowed as an asynchronous initialization is pending.</exception>
	public string Text
	{
		get
		{
			lock (Sync) return mText;
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

				if (mText != value)
				{
					mText = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	#endregion

	#region Write Protection

	/// <summary>
	/// Indicates whether the log message is protected.
	/// If <see langword="true"/>, property setters will throw <see cref="NotSupportedException"/> when invoked.
	/// </summary>
	protected internal bool IsReadOnlyInternal;

	/// <summary>
	/// Gets a value indicating whether the log message is protected.
	/// If <see langword="true"/>, property setters will throw <see cref="NotSupportedException"/> when invoked.
	/// </summary>
	public bool IsReadOnly
	{
		get
		{
			lock (Sync) return IsReadOnlyInternal;
		}

		private set
		{
			bool changed = false;

			lock (Sync)
			{
				if (IsReadOnlyInternal != value)
				{
					IsReadOnlyInternal = value;
					changed = true;
				}
			}

			if (changed)
				OnPropertyChanged();
		}
	}

	/// <summary>
	/// Protects the log message, so it cannot be modified any further.
	/// </summary>
	/// <returns>The log message itself.</returns>
	public LogMessage Protect()
	{
		IsReadOnly = true;
		return this;
	}

	#endregion

	#region Equality Check

	/// <summary>
	/// Checks whether the current log message equals the specified one.
	/// </summary>
	/// <param name="other">Log message to compare with.</param>
	/// <returns>
	/// <see langword="true"/> if the current log message equals the specified one;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public virtual bool Equals(ILogMessage other)
	{
		switch (other)
		{
			case null:
				return false;

			case LogMessage otherLogMessage:
				return Equals(otherLogMessage);

			default:
				lock (Sync)
				{
					return Timestamp.Equals(other.Timestamp) &&
					       HighPrecisionTimestamp == other.HighPrecisionTimestamp &&
					       LogWriterName == other.LogWriterName &&
					       LogLevelName == other.LogLevelName &&
					       ApplicationName == other.ApplicationName &&
					       ProcessName == other.ProcessName &&
					       ProcessId == other.ProcessId &&
					       Text == other.Text &&
					       Equals(Tags, other.Tags);
				}
		}
	}

	/// <summary>
	/// Checks whether the current log message equals the specified one.
	/// The following properties are _not_ taken into account:
	/// - <see cref="IsReadOnly"/>
	/// </summary>
	/// <param name="other">Log message to compare with.</param>
	/// <returns>
	/// <see langword="true"/> if the current log message equals the specified one;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public virtual bool Equals(LogMessage other)
	{
		if (other == null) return false;

		lock (Sync)
		{
			return IsInitializedInternal == other.IsInitializedInternal &&
			       mLostMessageCount == other.mLostMessageCount &&
			       mTimestamp == other.mTimestamp &&
			       mHighPrecisionTimestamp == other.mHighPrecisionTimestamp &&
			       mLogWriterName == other.mLogWriterName &&
			       mLogLevelName == other.mLogLevelName &&
			       mApplicationName == other.mApplicationName &&
			       mProcessName == other.mProcessName &&
			       mProcessId == other.mProcessId &&
			       mText == other.mText &&
			       Equals(mTags, other.mTags);
		}
	}

	/// <summary>
	/// Checks whether the current log message equals the specified one.
	/// </summary>
	/// <param name="obj">Log message to compare with.</param>
	/// <returns>
	/// <see langword="true"/> if the current log message equals the specified one;<br/>
	/// otherwise, <see langword="false"/>.
	/// </returns>
	public override bool Equals(object obj)
	{
		return ReferenceEquals(this, obj) || (obj is ILogMessage other && Equals(other));
	}

	/// <summary>
	/// Gets the hash code of the log message.
	/// The following properties are _not_ taken into account:
	/// - <see cref="IsReadOnly"/>
	/// </summary>
	/// <returns>Hash code of the log message.</returns>
	public override int GetHashCode()
	{
		lock (Sync)
		{
			unchecked
			{
				int hashCode = IsInitializedInternal.GetHashCode();
				hashCode = (hashCode * 397) ^ mTimestamp.GetHashCode();
				hashCode = (hashCode * 397) ^ mHighPrecisionTimestamp.GetHashCode();
				hashCode = (hashCode * 397) ^ mLostMessageCount;
				hashCode = (hashCode * 397) ^ mProcessId;
				hashCode = (hashCode * 397) ^ (mLogWriterName != null ? mLogWriterName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (mLogLevelName != null ? mLogLevelName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (mTags != null ? mTags.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (mApplicationName != null ? mApplicationName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (mProcessName != null ? mProcessName.GetHashCode() : 0);
				hashCode = (hashCode * 397) ^ (mText != null ? mText.GetHashCode() : 0);
				return hashCode;
			}
		}
	}

	#endregion

	#region Implementation of INotifyPropertyChanged

	/// <summary>
	/// Counter that controls whether the <see cref="INotifyPropertyChanged.PropertyChanged"/> event is raised on changes.
	/// May be disabled temporarily in derived classes by incrementing the counter.
	/// </summary>
	protected int PropertyChangedSuspensionCounter = 0;

	/// <summary>
	/// Occurs when one of the properties has changed.
	/// The handler is invoked in the context of the thread that registers the event, if <see cref="SynchronizationContext.Current"/> is set appropriately.
	/// If the synchronization context is the same when registering and firing the event, the handler is called directly (in the context of the thread
	/// raising the event).
	/// If the synchronization context is not set when registering the event, the handler is always called directly.
	/// </summary>
	public event PropertyChangedEventHandler PropertyChanged
	{
		add => PropertyChangedEventManager.RegisterEventHandler(this, value, SynchronizationContext.Current, false);
		remove => PropertyChangedEventManager.UnregisterEventHandler(this, value);
	}

	/// <summary>
	/// Raises the <see cref="PropertyChanged"/> event.
	/// </summary>
	/// <param name="propertyName">
	/// Name of the property that has changed
	/// (<see langword="null"/> to indicate that all properties (might) have changed).
	/// </param>
	protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
	{
		if (PropertyChangedSuspensionCounter == 0)
			PropertyChangedEventManager.FireEvent(this, propertyName);
	}

	#endregion
}
