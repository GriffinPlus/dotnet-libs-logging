///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Untyped interface for a setting in a <see cref="IProcessingPipelineStageConfiguration"/>.
	/// Must be implemented thread-safe.
	/// </summary>
	public interface IUntypedProcessingPipelineStageSetting
	{
		/// <summary>
		/// Occurs when the setting changes.
		/// The event handler is invoked in the synchronization context of the registering thread, if the thread
		/// has a synchronization context. Otherwise the event handler is invoked by a worker thread. The execution
		/// of the event handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
		/// </summary>
		event EventHandler<SettingChangedEventArgs> SettingChanged;

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Gets the type of the value.
		/// </summary>
		Type ValueType { get; }

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (<c>true</c>) or just its default value (<c>false</c>).
		/// </summary>
		bool HasValue { get; }

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		object Value { get; set; }

		/// <summary>
		/// Gets or sets the value of the setting as a string (for serialization purposes).
		/// </summary>
		string ValueAsString { get; set; }

		/// <summary>
		/// Gets a value indicating whether the setting has valid default value.
		/// </summary>
		bool HasDefaultValue { get; }

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		object DefaultValue { get; }

		/// <summary>
		/// Gets the default value of the setting as a string (for serialization purposes).
		/// </summary>
		string DefaultValueAsString { get; }

		/// <summary>
		/// Registers the specified <see cref="EventHandler{SettingChangedEventArgs}"/> for the <see cref="SettingChanged"/> event.
		/// Depending on <paramref name="invokeInCurrentSynchronizationContext"/> the event handler is invoked in the
		/// synchronization context of the current thread (if any) or in a worker thread. The execution of the event
		/// handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
		/// </summary>
		/// <param name="handler">Event handler to register.</param>
		/// <param name="invokeInCurrentSynchronizationContext">
		/// <c>true</c> to invoke the event handler in the synchronization context of the current thread;<br/>
		/// <c>false</c> to invoke the event handler in a worker thread.
		/// </param>
		void RegisterSettingChangedEventHandler(
			EventHandler<SettingChangedEventArgs> handler,
			bool                                  invokeInCurrentSynchronizationContext);

		/// <summary>
		/// Unregisters the specified <see cref="EventHandler{SettingChangedEventArgs}"/> from the <see cref="SettingChanged"/> event.
		/// </summary>
		/// <param name="handler">Event handler to unregister.</param>
		void UnregisterSettingChangedEventHandler(EventHandler<SettingChangedEventArgs> handler);
	}

}
