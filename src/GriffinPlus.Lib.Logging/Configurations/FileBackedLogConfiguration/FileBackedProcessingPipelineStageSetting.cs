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
	/// A setting in a <see cref="FileBackedProcessingPipelineStageConfiguration"/>.
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	public class FileBackedProcessingPipelineStageSetting<T> : IProcessingPipelineStageSetting<T>
	{
		private readonly FileBackedProcessingPipelineStageRawSetting                          mRawSetting;
		private readonly ProcessingPipelineStageConfigurationBase.ValueFromStringConverter<T> mFromStringConverter;
		private readonly ProcessingPipelineStageConfigurationBase.ValueToStringConverter<T>   mToStringConverter;
		private          string                                                               mCachedRawValue;
		private          T                                                                    mCachedValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageSetting{T}"/> class.
		/// </summary>
		/// <param name="rawSetting">The corresponding raw setting in the configuration.</param>
		/// <param name="valueFromStringConverter">Delegate that converts the setting value to a string.</param>
		/// <param name="valueToStringConverter">Delegate that converts a string to the setting value.</param>
		internal FileBackedProcessingPipelineStageSetting(
			FileBackedProcessingPipelineStageRawSetting                          rawSetting,
			ProcessingPipelineStageConfigurationBase.ValueFromStringConverter<T> valueFromStringConverter,
			ProcessingPipelineStageConfigurationBase.ValueToStringConverter<T>   valueToStringConverter)
		{
			mRawSetting = rawSetting;
			mFromStringConverter = valueFromStringConverter;
			mToStringConverter = valueToStringConverter;
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
			add => RegisterPropertyChangedEventHandler(value, true);
			remove => UnregisterPropertyChangedEventHandler(value);
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
		public void RegisterPropertyChangedEventHandler(
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
		public void UnregisterPropertyChangedEventHandler(EventHandler<SettingChangedEventArgs> handler)
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

		#region Access to the Raw Setting

		/// <summary>
		/// Gets the raw setting containing the string representation of the setting.
		/// </summary>
		internal FileBackedProcessingPipelineStageRawSetting Raw => mRawSetting;

		#endregion

		#region Implementation of IProcessingPipelineStageSetting<T> and IUntypedProcessingPipelineStageSetting

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		public string Name => mRawSetting.Name;

		/// <summary>
		/// Gets the type of the value.
		/// </summary>
		public Type ValueType => typeof(T);

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
		/// </summary>
		public bool HasValue
		{
			get
			{
				lock (mRawSetting.StageConfiguration.Sync) return mRawSetting.HasValue;
			}
		}

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		public T Value
		{
			get
			{
				lock (mRawSetting.StageConfiguration.Sync)
				{
					string rawValue = mRawSetting.Value;
					if (mCachedRawValue == rawValue) return mCachedValue;
					mCachedValue = mFromStringConverter(mRawSetting.Value);
					mCachedRawValue = rawValue;
					return mCachedValue;
				}
			}

			set
			{
				lock (mRawSetting.StageConfiguration.Sync)
				{
					string oldRawValue = mRawSetting.HasValue | mRawSetting.HasDefaultValue ? mRawSetting.Value : null;
					string newRawValue = mToStringConverter(value);
					if (oldRawValue == newRawValue) return;
					mRawSetting.Value = newRawValue;
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
			get => mRawSetting.Value;
			set
			{
				if (mRawSetting.Value == value) return;
				mRawSetting.Value = value;
				OnSettingChanged();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the setting has valid default value.
		/// </summary>
		public bool HasDefaultValue => mRawSetting.HasDefaultValue;

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		public T DefaultValue => mFromStringConverter(mRawSetting.DefaultValue);

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		object IUntypedProcessingPipelineStageSetting.DefaultValue => mFromStringConverter(mRawSetting.DefaultValue);

		#endregion

		/// <summary>
		/// Gets the string representation of the setting.
		/// </summary>
		/// <returns>String representation of the setting.</returns>
		public override string ToString()
		{
			return mRawSetting.ToString();
		}
	}

}
