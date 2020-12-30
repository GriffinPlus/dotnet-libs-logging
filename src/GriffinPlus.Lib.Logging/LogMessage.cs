///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A log message for general purpose use (thread-safe).
	/// </summary>
	public sealed class LogMessage : ILogMessage, IEquatable<ILogMessage>, INotifyPropertyChanged
	{
		private readonly object mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage" /> class.
		/// </summary>
		public LogMessage()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage" /> class.
		/// </summary>
		/// <param name="pool">The pool the message belongs to.</param>
		internal LogMessage(LogMessagePool pool)
		{
			Pool = pool;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessage" /> class copying the specified one.
		/// </summary>
		/// <param name="other">Message to copy.</param>
		public LogMessage(ILogMessage other)
		{
			mId = other.Id;
			mTimestamp = other.Timestamp;
			mHighPrecisionTimestamp = other.HighPrecisionTimestamp;
			mLogWriterName = other.LogWriterName;
			mTags = other.Tags;
			mLogLevelName = other.LogLevelName;
			mApplicationName = other.ApplicationName;
			mProcessName = other.ProcessName;
			mProcessId = other.ProcessId;
			mText = other.Text;
		}

		#region Message Properties

		private long           mId = -1;
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
		/// Gets or sets the id uniquely identifying the message in a certain context, e.g. a collection or log file
		/// (-1, if the id is invalid).
		/// </summary>
		public long Id
		{
			get
			{
				lock (mSync) return mId;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mId != value)
					{
						mId = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the number of preceding messages that have been lost before this message
		/// (useful when dealing with message streams).
		/// </summary>
		public int LostMessageCount
		{
			get
			{
				lock (mSync) return mLostMessageCount;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mLostMessageCount != value)
					{
						mLostMessageCount = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the date/time the message was written to the log.
		/// </summary>
		public DateTimeOffset Timestamp
		{
			get
			{
				lock (mSync) return mTimestamp;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mTimestamp != value)
					{
						mTimestamp = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the timestamp for relative time measurements with high precision
		/// (in nanoseconds, but the actual precision depends on the system timer).
		/// </summary>
		public long HighPrecisionTimestamp
		{
			get
			{
				lock (mSync) return mHighPrecisionTimestamp;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mHighPrecisionTimestamp != value)
					{
						mHighPrecisionTimestamp = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the name of the log writer associated with the log message.
		/// </summary>
		public string LogWriterName
		{
			get
			{
				lock (mSync) return mLogWriterName;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mLogWriterName != value)
					{
						mLogWriterName = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the name of the log level associated with the log message.
		/// </summary>
		public string LogLevelName
		{
			get
			{
				lock (mSync) return mLogLevelName;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mLogLevelName != value)
					{
						mLogLevelName = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets tags attached to the log message.
		/// </summary>
		public TagSet Tags
		{
			get
			{
				lock (mSync) return mTags;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mTags != value)
					{
						mTags = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the name of the application emitting the log message
		/// (can differ from the process name, if the application is using an interpreter (the actual process)).
		/// </summary>
		public string ApplicationName
		{
			get
			{
				lock (mSync) return mApplicationName;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mApplicationName != value)
					{
						mApplicationName = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the name of the process emitting the log message.
		/// </summary>
		public string ProcessName
		{
			get
			{
				lock (mSync) return mProcessName;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mProcessName != value)
					{
						mProcessName = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the id of the process emitting the log message
		/// (-1, if the process id is invalid/unset).
		/// </summary>
		public int ProcessId
		{
			get
			{
				lock (mSync) return mProcessId;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mProcessId != value)
					{
						mProcessId = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Gets or sets the actual text the log message is about.
		/// </summary>
		public string Text
		{
			get
			{
				lock (mSync) return mText;
			}

			set
			{
				lock (mSync)
				{
					if (mIsReadOnly)
						throw new NotSupportedException("The log message is read-only.");

					if (mText != value)
					{
						mText = value;
						OnPropertyChanged();
					}
				}
			}
		}

		#endregion

		#region Write Protection

		private bool mIsReadOnly;

		/// <summary>
		/// Gets a value indicating whether the log message is protected.
		/// If <c>true</c>, property setters will throw <see cref="NotSupportedException" /> when invoked.
		/// </summary>
		public bool IsReadOnly
		{
			get
			{
				lock (mSync) return mIsReadOnly;
			}

			private set
			{
				lock (mSync)
				{
					if (mIsReadOnly != value)
					{
						mIsReadOnly = value;
						OnPropertyChanged();
					}
				}
			}
		}

		/// <summary>
		/// Protects the log message, so it cannot be modified any further.
		/// </summary>
		public void Protect()
		{
			IsReadOnly = true;
		}

		#endregion

		#region Pooling Support

		private int mRefCount = 1;

		/// <summary>
		/// Increments the reference counter of the log message (needed for pool messages only).
		/// Call it to indicate that the message is still in use and avoid that it returns to the pool.
		/// </summary>
		/// <returns>The reference counter after incrementing.</returns>
		public int AddRef()
		{
			return Interlocked.Increment(ref mRefCount);
		}

		/// <summary>
		/// Decrements the reference counter of the log message (needed for pool messages only).
		/// The message returns to the pool, if the counter gets 0.
		/// </summary>
		/// <returns>The reference counter after decrementing.</returns>
		public int Release()
		{
			int refCount = Interlocked.Decrement(ref mRefCount);

			if (refCount < 0)
			{
				Interlocked.Increment(ref mRefCount);
				throw new InvalidOperationException("The reference count is already 0.");
			}

			if (refCount == 0)
			{
				Pool?.ReturnMessage(this);
			}

			return refCount;
		}

		/// <summary>
		/// Gets the current value of the reference counter of the log message.
		/// </summary>
		public int RefCount => Volatile.Read(ref mRefCount);

		/// <summary>
		/// Gets the pool the log message belongs to.
		/// </summary>
		internal LogMessagePool Pool { get; }

		/// <summary>
		/// Initializes the log message
		/// </summary>
		/// <param name="id">
		/// Gets or sets the id uniquely identifying the message in a certain scope, e.g. a log file;
		/// -1, if the id is invalid.
		/// </param>
		/// <param name="timestamp">Time the message was written to the log.</param>
		/// <param name="highPrecisionTimestamp">
		/// Timestamp for relative time measurements with high precision
		/// (the actual precision depends on the <see cref="System.Diagnostics.Stopwatch" /> class).
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
		/// <returns>The log message itself.</returns>
		public LogMessage InitWith(
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
			lock (mSync)
			{
				if (mIsReadOnly)
					throw new NotSupportedException("The log message is read-only.");

				mId = id;
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

				OnPropertyChanged(null);

				return this;
			}
		}

		/// <summary>
		/// Resets the log message to defaults
		/// (called by the pool to prepare the log message for re-use).
		/// </summary>
		internal void Reset()
		{
			lock (mSync)
			{
				// set fields to defaults
				mId = -1;
				mTimestamp = default;
				mHighPrecisionTimestamp = 0;
				mLostMessageCount = 0;
				mLogWriterName = null;
				mLogLevelName = null;
				mTags = null;
				mApplicationName = null;
				mProcessName = null;
				mProcessId = -1;
				mText = null;
				mIsReadOnly = false;

				// unregister event handlers that might have not been unregistered properly
				PropertyChangedEventManager.UnregisterEventHandlers(this);
			}
		}

		#endregion

		#region Equality Check

		/// <summary>
		/// Checks whether the current log message equals the specified one.
		/// </summary>
		/// <param name="other">Log message to compare with.</param>
		/// <returns>
		/// true, if the current log message equals the specified one;
		/// otherwise false.
		/// </returns>
		public bool Equals(ILogMessage other)
		{
			switch (other)
			{
				case null:
					return false;

				case LogMessage otherLogMessage:
					return Equals(otherLogMessage);

				default:
					lock (mSync)
					{
						return Id == other.Id &&
						       Timestamp.Equals(other.Timestamp) &&
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
		/// - <see cref="IsReadOnly" />
		/// - <see cref="RefCount" />
		/// </summary>
		/// <param name="other">Log message to compare with.</param>
		/// <returns>
		/// true, if the current log message equals the specified one;
		/// otherwise false.
		/// </returns>
		public bool Equals(LogMessage other)
		{
			if (other == null) return false;

			lock (mSync)
			{
				return Id == other.Id &&
				       LostMessageCount == other.LostMessageCount &&
				       Timestamp == other.Timestamp &&
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

		/// <summary>
		/// Checks whether the current log message equals the specified one.
		/// </summary>
		/// <param name="obj">Log message to compare with.</param>
		/// <returns>
		/// true, if the current log message equals the specified one;
		/// otherwise false.
		/// </returns>
		public override bool Equals(object obj)
		{
			return ReferenceEquals(this, obj) || obj is ILogMessage other && Equals(other);
		}

		/// <summary>
		/// Gets the hash code of the log message.
		/// The following properties are _not_ taken into account:
		/// - <see cref="IsReadOnly" />
		/// - <see cref="RefCount" />
		/// </summary>
		/// <returns>Hash code of the log message.</returns>
		public override int GetHashCode()
		{
			lock (mSync)
			{
				unchecked
				{
					int hashCode = Timestamp.GetHashCode();
					hashCode = (hashCode * 397) ^ Id.GetHashCode();
					hashCode = (hashCode * 397) ^ LostMessageCount;
					hashCode = (hashCode * 397) ^ HighPrecisionTimestamp.GetHashCode();
					hashCode = (hashCode * 397) ^ (LogWriterName != null ? LogWriterName.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ (LogLevelName != null ? LogLevelName.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ (Tags != null ? Tags.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ (ApplicationName != null ? ApplicationName.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ (ProcessName != null ? ProcessName.GetHashCode() : 0);
					hashCode = (hashCode * 397) ^ ProcessId;
					hashCode = (hashCode * 397) ^ (Text != null ? Text.GetHashCode() : 0);
					return hashCode;
				}
			}
		}

		#endregion

		#region Implementation of INotifyPropertyChanged

		/// <summary>
		/// Occurs when one of the properties has changed.
		/// The handler is invoked in the context of the thread that registers the event, if <see cref="SynchronizationContext.Current" /> is set appropriately.
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
		/// Raises the <see cref="PropertyChanged" /> event.
		/// </summary>
		/// <param name="propertyName">
		/// Name of the property that has changed
		/// (null to indicate that all properties (might) have changed).
		/// </param>
		private void OnPropertyChanged([CallerMemberName] string propertyName = null)
		{
			PropertyChangedEventManager.FireEvent(this, propertyName);
		}

		#endregion
	}

}
