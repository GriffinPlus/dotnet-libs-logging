///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Threading;

using GriffinPlus.Lib.Conversion;
using GriffinPlus.Lib.Events;

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// A setting in a <see cref="FileBackedProcessingPipelineStageConfiguration"/>.
/// </summary>
/// <typeparam name="T">Type of the setting value.</typeparam>
public class FileBackedProcessingPipelineStageSetting<T> : IProcessingPipelineStageSetting<T>
{
	private readonly ObjectToStringConversionDelegate<T> mValueToStringConverter;
	private readonly StringToObjectConversionDelegate<T> mStringToValueConverter;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageSetting{T}"/> class.
	/// </summary>
	/// <param name="rawSetting">The corresponding raw setting in the configuration.</param>
	/// <param name="valueToStringConverter">Delegate that converts a string to the setting value.</param>
	/// <param name="stringToValueConverter">Delegate that converts the setting value to a string.</param>
	internal FileBackedProcessingPipelineStageSetting(
		FileBackedProcessingPipelineStageRawSetting rawSetting,
		ObjectToStringConversionDelegate<T>         valueToStringConverter,
		StringToObjectConversionDelegate<T>         stringToValueConverter)
	{
		Raw = rawSetting;
		mValueToStringConverter = valueToStringConverter;
		mStringToValueConverter = stringToValueConverter;
	}

	#region SettingChanged Event

	/// <summary>
	/// Occurs when the setting changes.
	/// The event handler is invoked in the synchronization context of the registering thread, if the thread
	/// has a synchronization context. Otherwise, the event handler is invoked by a worker thread. The execution
	/// of the event handler is always scheduled to avoid deadlocks that might be caused by lock inversion.
	/// </summary>
	public event EventHandler<SettingChangedEventArgs> SettingChanged
	{
		add => RegisterSettingChangedEventHandler(value, true);
		remove => UnregisterSettingChangedEventHandler(value);
	}

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
	public void RegisterSettingChangedEventHandler(
		EventHandler<SettingChangedEventArgs> handler,
		bool                                  invokeInCurrentSynchronizationContext)
	{
		EventManager<SettingChangedEventArgs>.RegisterEventHandler(
			this,
			nameof(SettingChanged),
			handler,
			invokeInCurrentSynchronizationContext ? SynchronizationContext.Current : null,
			true);
	}

	/// <summary>
	/// Unregisters the specified <see cref="EventHandler{SettingChangedEventArgs}"/> from the <see cref="SettingChanged"/> event.
	/// </summary>
	/// <param name="handler">Event handler to unregister.</param>
	public void UnregisterSettingChangedEventHandler(EventHandler<SettingChangedEventArgs> handler)
	{
		EventManager<SettingChangedEventArgs>.UnregisterEventHandler(
			this,
			nameof(SettingChanged),
			handler);
	}

	/// <summary>
	/// Raises the <see cref="SettingChanged"/> event when a property has changed.
	/// </summary>
	private void OnSettingChanged()
	{
		// notify that the configuration the setting belongs to has changed
		Raw.StageConfiguration.LogConfiguration.OnChanged();

		// raise the SettingChanged event
		if (EventManager<SettingChangedEventArgs>.IsHandlerRegistered(this, nameof(SettingChanged)))
		{
			EventManager<SettingChangedEventArgs>.FireEvent(
				this,
				nameof(SettingChanged),
				this,
				SettingChangedEventArgs.Default);
		}
	}

	#endregion

	#region Access to the Raw Setting

	/// <summary>
	/// Gets the raw setting containing the string representation of the setting.
	/// </summary>
	internal FileBackedProcessingPipelineStageRawSetting Raw { get; }

	#endregion

	#region Implementation of IProcessingPipelineStageSetting<T> and IUntypedProcessingPipelineStageSetting

	/// <summary>
	/// Gets the name of the setting.
	/// </summary>
	public string Name => Raw.Name;

	/// <summary>
	/// Gets the type of the value.
	/// </summary>
	public Type ValueType => typeof(T);

	/// <summary>
	/// Gets a value indicating whether the setting has valid value (<c>true</c>)
	/// or just its default value (<c>false</c>).
	/// </summary>
	public bool HasValue => Raw.HasValue;

	/// <summary>
	/// Gets or sets the value of the setting.
	/// </summary>
	public T Value
	{
		get => mStringToValueConverter(Raw.Value, CultureInfo.InvariantCulture);

		set
		{
			lock (Raw.StageConfiguration.Sync)
			{
				string oldRawValue = Raw.HasValue | Raw.HasDefaultValue ? Raw.Value : null;
				string newRawValue = mValueToStringConverter(value, CultureInfo.InvariantCulture);
				if (Raw.HasValue && oldRawValue == newRawValue) return;
				Raw.Value = newRawValue;
				OnSettingChanged();
			}
		}
	}

	/// <summary>
	/// Gets or sets the value of the setting.
	/// </summary>
	object IUntypedProcessingPipelineStageSetting.Value
	{
		get => Value;
		set => Value = (T)value;
	}

	/// <summary>
	/// Gets or sets the value of the setting as a string (for serialization purposes).
	/// </summary>
	public string ValueAsString
	{
		get => Raw.Value;
		set
		{
			if (Raw.HasValue && Raw.Value == value) return;
			Raw.Value = value;
			OnSettingChanged();
		}
	}

	/// <summary>
	/// Gets a value indicating whether the setting has valid default value.
	/// </summary>
	public bool HasDefaultValue => Raw.HasDefaultValue;

	/// <summary>
	/// Gets the default value of the setting.
	/// </summary>
	public T DefaultValue => mStringToValueConverter(Raw.DefaultValue, CultureInfo.InvariantCulture);

	/// <summary>
	/// Gets the default value of the setting.
	/// </summary>
	object IUntypedProcessingPipelineStageSetting.DefaultValue => DefaultValue;

	/// <summary>
	/// Gets or sets the default value of the setting as a string (for serialization purposes).
	/// </summary>
	public string DefaultValueAsString => Raw.DefaultValue;

	#endregion

	/// <summary>
	/// Gets the string representation of the setting.
	/// </summary>
	/// <returns>String representation of the setting.</returns>
	public override string ToString()
	{
		return Raw.ToString();
	}
}
