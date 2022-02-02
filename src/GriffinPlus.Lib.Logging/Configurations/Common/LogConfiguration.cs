///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

using GriffinPlus.Lib.Disposables;
using GriffinPlus.Lib.Events;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Delegate for callbacks that are invoked to set up a <see cref="LogWriterConfiguration"/> using a <see cref="LogWriterConfigurationBuilder"/>.
	/// </summary>
	/// <param name="writer"></param>
	public delegate void LogWriterConfigurationCallback(LogWriterConfigurationBuilder writer);

	/// <summary>
	/// The base class for log configurations.
	/// </summary>
	public abstract class LogConfiguration<TConfiguration> : ILogConfiguration
		where TConfiguration : LogConfiguration<TConfiguration>
	{
		#region Changed Event

		private readonly object mChangedEventSync          = new object();
		private          int    mSuspendChangeEventCounter = 0;
		private          bool   mChangedEventPending       = false;

		/// <summary>
		/// Occurs when the log configuration changes.
		/// The event handler is invoked in the synchronization context of the registering thread, if the thread
		/// has a synchronization context. Otherwise the event handler is invoked by a worker thread. The execution
		/// of the event handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
		/// </summary>
		public event EventHandler<EventArgs> Changed
		{
			add => RegisterChangedEventHandler(value, true);
			remove => UnregisterChangedEventHandler(value);
		}

		/// <summary>
		/// Registers the specified handler for the <see cref="Changed"/> event.
		/// Depending on <paramref name="invokeInCurrentSynchronizationContext"/> the event handler is invoked in the
		/// synchronization context of the current thread (if any) or in a worker thread. The execution of the event
		/// handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
		/// </summary>
		/// <param name="handler">Event handler to register.</param>
		/// <param name="invokeInCurrentSynchronizationContext">
		/// <c>true</c> to invoke the event handler in the synchronization context of the current thread;
		/// <c>false</c> to invoke the event handler in a worker thread.
		/// </param>
		public void RegisterChangedEventHandler(
			EventHandler<EventArgs> handler,
			bool                    invokeInCurrentSynchronizationContext)
		{
			EventManager<EventArgs>.RegisterEventHandler(
				this,
				nameof(Changed),
				handler,
				invokeInCurrentSynchronizationContext ? SynchronizationContext.Current : null,
				true);
		}

		/// <summary>
		/// Unregisters the specified handler from the <see cref="Changed"/> event.
		/// </summary>
		/// <param name="handler">Event handler to unregister.</param>
		public void UnregisterChangedEventHandler(EventHandler<EventArgs> handler)
		{
			EventManager<EventArgs>.UnregisterEventHandler(
				this,
				nameof(Changed),
				handler);
		}

		/// <summary>
		/// Suspends raising the <see cref="Changed"/> event.
		/// </summary>
		/// <returns>
		/// An <see cref="IDisposable"/> that needs to be disposed to resume raiding the event.
		/// </returns>
		public IDisposable SuspendChangedEvent()
		{
			lock (mChangedEventSync) mSuspendChangeEventCounter++;
			return new AnonymousDisposable(
				() =>
				{
					lock (mChangedEventSync)
					{
						Debug.Assert(mSuspendChangeEventCounter > 0);
						if (--mSuspendChangeEventCounter == 0 && mChangedEventPending)
							OnChanged();
					}
				});
		}

		/// <summary>
		/// Schedules raising the <see cref="Changed"/> event.
		/// Multiple calls to this method are bundled up to keep the amount of event invocations as low as possible
		/// and reduce the load caused by handling the changes.
		/// </summary>
		protected internal void OnChanged()
		{
			lock (mChangedEventSync)
			{
				if (mSuspendChangeEventCounter == 0)
				{
					// reset pending indicator to avoid raising the event twice
					mChangedEventPending = false;

					// abort, if no handler is registered or raising the event has already been scheduled
					if (!EventManager<EventArgs>.IsHandlerRegistered(this, nameof(Changed)))
						return;

					// fire the event
					EventManager<EventArgs>.FireEvent(
						this,
						nameof(Changed),
						this,
						EventArgs.Empty);
				}
				else
				{
					// raising the event has been suspended
					// => remember that it should be raised when raising the event is resumed
					mChangedEventPending = true;
				}
			}
		}

		#endregion

		/// <summary>
		/// Disposes the object cleaning up unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		/// <summary>
		/// Disposes the configuration cleaning up unmanaged resources
		/// </summary>
		/// <param name="disposing">
		/// true, if called explicitly;
		/// false, if called due to finalization.
		/// </param>
		protected abstract void Dispose(bool disposing);

		/// <summary>
		/// Gets the object to use when synchronizing access to the log configuration.
		/// </summary>
		protected internal object Sync { get; } = new object();

		/// <summary>
		/// Gets a value indicating whether the configuration is the default configuration that was created
		/// by the logging subsystem at start.
		/// </summary>
		public bool IsDefaultConfiguration { get; internal set; }

		/// <summary>
		/// Gets or sets the name of the application.
		/// </summary>
		public abstract string ApplicationName { get; set; }

		/// <summary>
		/// Gets the configuration of the processing pipeline.
		/// </summary>
		public abstract IProcessingPipelineConfiguration ProcessingPipeline { get; }

		/// <summary>
		/// Gets a bit mask in which each bit is associated with a log level with the same id
		/// and expresses whether the corresponding log level is active for the specified writer.
		/// </summary>
		/// <param name="writer">Log writer to get the active log level mask for.</param>
		/// <returns>The requested active log level mask.</returns>
		public abstract LogLevelBitMask GetActiveLogLevelMask(LogWriter writer);

		/// <summary>
		/// Gets the current log writer settings.
		/// </summary>
		/// <returns>A copy of the internal log writer settings.</returns>
		public abstract IEnumerable<LogWriterConfiguration> GetLogWriterSettings();

		/// <summary>
		/// Sets the log writer settings to use.
		/// </summary>
		/// <param name="settings">Settings to use.</param>
		public abstract void SetLogWriterSettings(IEnumerable<LogWriterConfiguration> settings);

		/// <summary>
		/// Saves the configuration.
		/// </summary>
		/// <param name="includeDefaults">
		/// true to include the default value of settings that have not been explicitly set;
		/// false to save only settings that have not been explicitly set.
		/// </param>
		public abstract void Save(bool includeDefaults = false);

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <typeparam name="T">The type whose full name should serve as the log writer name the configuration should apply to.</typeparam>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWriter<T>(LogWriterConfigurationCallback configuration = null)
		{
			AddLogWriter(typeof(T).FullName, configuration);
		}

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <param name="type">The type whose full name should serve as the log writer name the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWriter(Type type, LogWriterConfigurationCallback configuration = null)
		{
			AddLogWriter(type.FullName, configuration);
		}

		/// <summary>
		/// Adds a log writer configuration using the full name of the specified type as its name.
		/// The configuration will match exactly the log writer with this name.
		/// </summary>
		/// <param name="name">Name of the log writer the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWriter(string name, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingExactly(name);
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Adds a log writer configuration, using the specified wildcard pattern to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="pattern">A wildcard pattern matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWritersByWildcard(string pattern, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingWildcardPattern(pattern);
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Adds a log writer configuration using the specified regular expression to match the name of log writers the configuration should apply to.
		/// </summary>
		/// <param name="regex">A regular expression matching the name of log writers the configuration should apply to.</param>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		public void AddLogWritersByRegex(string regex, LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingRegex(regex);
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Adds a log writer configuration for the internal 'Timing' log writer.
		/// By default, the log writer is used by <see cref="TimingLogger"/> when logging time measurements.
		/// </summary>
		public void AddLogWriterTiming()
		{
			AppendLogWriterConfiguration(LogWriterConfiguration.TimingWriter);
		}

		/// <summary>
		/// Adds a log writer configuration which matches any log writer name catching all log writers that were not handled in
		/// a preceding step. This log writer configuration should be added last.
		/// </summary>
		/// <param name="configuration">Callback that adjusts the log writer configuration (may be null).</param>
		/// <returns>The updated log configuration.</returns>
		public void AddLogWriterDefault(LogWriterConfigurationCallback configuration = null)
		{
			var writer = LogWriterConfigurationBuilder.New.MatchingWildcardPattern("*");
			configuration?.Invoke(writer);
			AppendLogWriterConfiguration(writer.Build());
		}

		/// <summary>
		/// Appends the specified log writer configuration to the configuration already stored in the log configuration.
		/// </summary>
		/// <param name="writer">Log writer configuration to append to the log configuration.</param>
		private void AppendLogWriterConfiguration(LogWriterConfiguration writer)
		{
			var settings = new List<LogWriterConfiguration>(GetLogWriterSettings().Where(x => !x.IsDefault)) { writer };
			SetLogWriterSettings(settings);
			OnChanged();
		}
	}

}
