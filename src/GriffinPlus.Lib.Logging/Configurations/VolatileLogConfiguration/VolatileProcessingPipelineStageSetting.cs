///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Threading;

using GriffinPlus.Lib.Events;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A setting in a <see cref="VolatileProcessingPipelineStageConfiguration"/> (thread-safe).
	/// </summary>
	/// <typeparam name="T">Type of the setting value.</typeparam>
	public class VolatileProcessingPipelineStageSetting<T> : IProcessingPipelineStageSetting<T>
	{
		private readonly VolatileProcessingPipelineStageConfiguration mConfiguration;
		private readonly Func<T, string>                              mValueToStringConverter;
		private readonly Func<string, T>                              mStringToValueConverter;
		private          bool                                         mHasValue;
		private          T                                            mValue;
		private          string                                       mValueAsString;
		private          bool                                         mHasDefaultValue;
		private          T                                            mDefaultValue;
		private          string                                       mDefaultValueAsString;

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
			Func<T, string>                              valueToStringConverter,
			Func<string, T>                              stringToValueConverter)
		{
			mConfiguration = configuration;
			mValueToStringConverter = valueToStringConverter;
			mStringToValueConverter = stringToValueConverter;
			Name = name;
			mDefaultValue = mValue = defaultValue;
			mDefaultValueAsString = mValueAsString = mValueToStringConverter(defaultValue);
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
			Func<T, string>                              valueToStringConverter,
			Func<string, T>                              stringToValueConverter)
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
		/// <c>true</c> to invoke the event handler in the synchronization context of the current thread;
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
					// if the setting has a value, make a deep copy of the value to avoid that the value gets modified afterwards
					if (mHasValue) return mStringToValueConverter(mValueToStringConverter(mValue));
					return mStringToValueConverter(mValueToStringConverter(mDefaultValue));
				}
			}

			set
			{
				lock (mConfiguration.Sync)
				{
					string oldValueAsString = mHasValue ? mValueToStringConverter(mValue) : mHasDefaultValue ? mValueToStringConverter(mDefaultValue) : null;
					string newValueAsString = mValueToStringConverter(value);
					if (oldValueAsString == newValueAsString) return;
					mValue = mStringToValueConverter(newValueAsString); // deep copy value avoids that it can get modified afterwards
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
					if (mHasValue) return mValueAsString;
					return mDefaultValueAsString;
				}
			}

			set
			{
				lock (mConfiguration.Sync)
				{
					if (mValueAsString == value) return;
					Value = mStringToValueConverter(value); // ensures that mValueAsString is set properly
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
				if (!mHasDefaultValue) throw new InvalidOperationException("The item does not have a default value.");
				return mStringToValueConverter(mValueToStringConverter(mDefaultValue));
			}

			internal set
			{
				lock (mConfiguration.Sync)
				{
					string valueAsString = mValueToStringConverter(value);
					mDefaultValue = mStringToValueConverter(valueAsString);
					mDefaultValueAsString = valueAsString;
					mHasDefaultValue = true;
				}
			}
		}

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		object IUntypedProcessingPipelineStageSetting.DefaultValue => DefaultValue;

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

}
