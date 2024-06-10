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
/// A setting in a <see cref="VolatileProcessingPipelineStageConfiguration"/> (thread-safe).
/// </summary>
/// <typeparam name="T">Type of the setting value.</typeparam>
public class VolatileProcessingPipelineStageSetting<T> : IProcessingPipelineStageSetting<T>
{
	// ReSharper disable once StaticMemberInGenericType
	private static readonly bool sUseDefensiveCopying;

	private readonly VolatileProcessingPipelineStageConfiguration mConfiguration;
	private readonly ObjectToStringConversionDelegate<T>          mValueToStringConverter;
	private readonly StringToObjectConversionDelegate<T>          mStringToValueConverter;
	private          bool                                         mHasValue;
	private          T                                            mValue;
	private          string                                       mValueAsString;
	private          bool                                         mHasDefaultValue;
	private          T                                            mDefaultValue;
	private          string                                       mDefaultValueAsString;

	/// <summary>
	/// Initializes the <see cref="VolatileProcessingPipelineStageSetting{T}"/> class.
	/// </summary>
	static VolatileProcessingPipelineStageSetting()
	{
		// Check whether the value type and derived types (if any) are immutable.
		// May return <c>false</c> although all derived types are immutable in practice,
		// but the analysis could not guarantee that this is always the case (false-negative).
		// => Immutable types can be shared by the setting and client code without defensive copying
		sUseDefensiveCopying = !Immutability.HasImmutableDerivationsOnly<T>();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="VolatileProcessingPipelineStageSetting{T}"/> class
	/// with a default value.
	/// </summary>
	/// <param name="configuration">The configuration the setting belongs to.</param>
	/// <param name="name">Name of the setting.</param>
	/// <param name="defaultValue">The default value of the setting.</param>
	/// <param name="valueToStringConverter">Delegate that converts a string to the setting value.</param>
	/// <param name="stringToValueConverter">Delegate that converts the setting value to a string.</param>
	internal VolatileProcessingPipelineStageSetting(
		VolatileProcessingPipelineStageConfiguration configuration,
		string                                       name,
		T                                            defaultValue,
		ObjectToStringConversionDelegate<T>          valueToStringConverter,
		StringToObjectConversionDelegate<T>          stringToValueConverter)
	{
		mConfiguration = configuration;
		mValueToStringConverter = valueToStringConverter;
		mStringToValueConverter = stringToValueConverter;
		Name = name;
		mDefaultValue = mValue = defaultValue;
		mDefaultValueAsString = mValueAsString = mValueToStringConverter(defaultValue, CultureInfo.InvariantCulture);
		mHasValue = false;
		mHasDefaultValue = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="VolatileProcessingPipelineStageSetting{T}"/> class
	/// without a default value.
	/// </summary>
	/// <param name="configuration">The configuration the setting belongs to.</param>
	/// <param name="name">Name of the setting.</param>
	/// <param name="valueToStringConverter">Delegate that converts a string to the setting value.</param>
	/// <param name="stringToValueConverter">Delegate that converts the setting value to a string.</param>
	internal VolatileProcessingPipelineStageSetting(
		VolatileProcessingPipelineStageConfiguration configuration,
		string                                       name,
		ObjectToStringConversionDelegate<T>          valueToStringConverter,
		StringToObjectConversionDelegate<T>          stringToValueConverter)
	{
		mConfiguration = configuration;
		mValueToStringConverter = valueToStringConverter;
		mStringToValueConverter = stringToValueConverter;
		Name = name;
		mDefaultValue = mValue = default;
		mDefaultValueAsString = mValueAsString = null;
		mHasValue = false;
		mHasDefaultValue = false;
	}

	#region SettingChanged Event

	/// <summary>
	/// Occurs when the setting changes.
	/// The event handler is invoked in the synchronization context of the registering thread, if the thread
	/// has a synchronization context. Otherwise the event handler is invoked by a worker thread. The execution
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
		mConfiguration.LogConfiguration.OnChanged();

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

	#region Implementation of IProcessingPipelineStageSetting<T> and IUntypedProcessingPipelineStageSetting

	/// <summary>
	/// Gets the name of the setting.
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets the type of the value.
	/// </summary>
	public Type ValueType => typeof(T);

	/// <summary>
	/// Gets a value indicating whether the setting has a valid value (<c>true</c>)
	/// or just its default value (<c>false</c>).
	/// </summary>
	public bool HasValue
	{
		get
		{
			lock (mConfiguration.Sync)
			{
				return mHasValue;
			}
		}
	}

	/// <summary>
	/// Gets or sets the value of the setting.
	/// </summary>
	public T Value
	{
		get
		{
			lock (mConfiguration.Sync)
			{
				if (sUseDefensiveCopying)
				{
					return mStringToValueConverter(
						mHasValue
							? mValueToStringConverter(mValue, CultureInfo.InvariantCulture)
							: mValueToStringConverter(mDefaultValue, CultureInfo.InvariantCulture),
						CultureInfo.InvariantCulture);
				}

				return mHasValue ? mValue : mDefaultValue;
			}
		}

		set
		{
			lock (mConfiguration.Sync)
			{
				string oldValueAsString = mHasValue ? mValueAsString : mHasDefaultValue ? mDefaultValueAsString : null;
				string newValueAsString = mValueToStringConverter(value, CultureInfo.InvariantCulture);
				if (mHasValue && oldValueAsString == newValueAsString) return;
				mValue = sUseDefensiveCopying ? mStringToValueConverter(newValueAsString, CultureInfo.InvariantCulture) : value;
				mValueAsString = newValueAsString;
				mHasValue = true;
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
		get
		{
			lock (mConfiguration.Sync)
			{
				return mHasValue ? mValueAsString : mDefaultValueAsString;
			}
		}

		set
		{
			lock (mConfiguration.Sync)
			{
				if (mHasValue && mValueAsString == value) return;
				Value = mStringToValueConverter(value, CultureInfo.InvariantCulture); // ensures that mValueAsString is set properly
			}
		}
	}

	/// <summary>
	/// Gets a value indicating whether the setting has valid default value.
	/// </summary>
	public bool HasDefaultValue
	{
		get
		{
			lock (mConfiguration.Sync)
			{
				return mHasDefaultValue;
			}
		}
	}

	/// <summary>
	/// Gets the default value of the setting.
	/// </summary>
	/// <exception cref="InvalidOperationException">The item does not have a default value.</exception>
	public T DefaultValue
	{
		get
		{
			lock (mConfiguration.Sync)
			{
				if (!mHasDefaultValue) throw new InvalidOperationException("The item does not have a default value.");
				return sUseDefensiveCopying
					       ? mStringToValueConverter(mDefaultValueAsString, CultureInfo.InvariantCulture)
					       : mDefaultValue;
			}
		}

		internal set
		{
			lock (mConfiguration.Sync)
			{
				string valueAsString = mValueToStringConverter(value, CultureInfo.InvariantCulture);
				mDefaultValue = sUseDefensiveCopying ? mStringToValueConverter(valueAsString, CultureInfo.InvariantCulture) : value;
				mDefaultValueAsString = valueAsString;
				mHasDefaultValue = true;
			}
		}
	}

	/// <summary>
	/// Gets the default value of the setting.
	/// </summary>
	object IUntypedProcessingPipelineStageSetting.DefaultValue => DefaultValue;

	/// <summary>
	/// Gets or sets the default value of the setting as a string (for serialization purposes).
	/// </summary>
	public string DefaultValueAsString
	{
		get
		{
			lock (mConfiguration.Sync)
			{
				if (!mHasDefaultValue) throw new InvalidOperationException("The item does not have a default value.");
				return mDefaultValueAsString;
			}
		}
	}

	#endregion

	/// <summary>
	/// Gets the string representation of the setting.
	/// </summary>
	/// <returns>String representation of the setting.</returns>
	public override string ToString()
	{
		lock (mConfiguration.Sync)
		{
			return mHasValue
				       ? $"Name: '{Name}', Value: '{mValueAsString}'"
				       : mHasDefaultValue
					       ? $"Name: '{Name}', Value: <no value> (defaults to: '{mDefaultValueAsString}'"
					       : $"Name: '{Name}', Value: <no value> (defaults to: <no value>)";
		}
	}
}
