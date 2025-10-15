///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Interface of a log configuration.
/// Must be implemented thread-safe.
/// </summary>
public interface ILogConfiguration : ILogWriterConfiguration, IDisposable
{
	#region Changed Event

	/// <summary>
	/// Occurs when the log configuration changes.
	/// The event handler is invoked in the synchronization context of the registering thread, if the thread
	/// has a synchronization context. Otherwise, the event handler is invoked by a worker thread. The execution
	/// of the event handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
	/// </summary>
	event EventHandler<EventArgs> Changed;

	/// <summary>
	/// Registers the specified handler for the <see cref="Changed"/> event.
	/// Depending on <paramref name="invokeInCurrentSynchronizationContext"/> the event handler is invoked in the
	/// synchronization context of the current thread (if any) or in a worker thread. The execution of the event
	/// handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
	/// </summary>
	/// <param name="handler">Event handler to register.</param>
	/// <param name="invokeInCurrentSynchronizationContext">
	/// <see langword="true"/> to invoke the event handler in the synchronization context of the current thread;<br/>
	/// <see langword="false"/> to invoke the event handler in a worker thread.
	/// </param>
	void RegisterChangedEventHandler(
		EventHandler<EventArgs> handler,
		bool                    invokeInCurrentSynchronizationContext);

	/// <summary>
	/// Unregisters the specified handler from the <see cref="Changed"/> event.
	/// </summary>
	/// <param name="handler">Event handler to unregister.</param>
	void UnregisterChangedEventHandler(EventHandler<EventArgs> handler);

	/// <summary>
	/// Suspends raising the <see cref="Changed"/> event.
	/// </summary>
	/// <returns>
	/// An <see cref="IDisposable"/> that needs to be disposed to resume raiding the event.
	/// </returns>
	IDisposable SuspendChangedEvent();

	#endregion

	/// <summary>
	/// Gets a value indicating whether the configuration is the default configuration that was created
	/// by the logging subsystem at start.
	/// </summary>
	bool IsDefaultConfiguration { get; }

	/// <summary>
	/// Gets or sets the name of the application.
	/// </summary>
	string ApplicationName { get; set; }

	/// <summary>
	/// Gets the configuration of the processing pipeline.
	/// </summary>
	IProcessingPipelineConfiguration ProcessingPipeline { get; }

	/// <summary>
	/// Saves the configuration.
	/// </summary>
	/// <param name="includeDefaults">
	/// <see langword="true"/> to include the default value of settings that have not been explicitly set;<br/>
	/// <see langword="false"/> to save only settings that have not been explicitly set.
	/// </param>
	void Save(bool includeDefaults = false);
}
