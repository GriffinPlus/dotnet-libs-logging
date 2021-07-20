///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	partial class ProcessingPipelineBaseStage
	{
		/// <summary>
		/// A proxy for a pipeline stage specific setting.
		/// Used by derived classes from <see cref="ProcessingPipelineBaseStage"/> to bind a setting to a pipeline stage
		/// configuration that could be exchanged afterwards. The proxy is adjusted by <see cref="ProcessingPipelineBaseStage"/>
		/// when a pipeline stage configuration is set. This avoids breaking the link between a pipeline stage and its
		/// configuration.
		/// </summary>
		internal class SettingProxy<T> : ISettingProxy<T>
		{
			private readonly string                                mSettingName;
			private readonly T                                     mDefaultSettingValue;
			private readonly object                                mSync;
			private          IProcessingPipelineStageConfiguration mConfiguration;
			private          IProcessingPipelineStageSetting<T>    mSetting;

			/// <summary>
			/// Initializes a new instance of the <see cref="SettingProxy{T}"/> class.
			/// </summary>
			/// <param name="configuration">The pipeline stage configuration containing the setting.</param>
			/// <param name="name">Name of the setting.</param>
			/// <param name="defaultValue">Default value of the setting.</param>
			/// <param name="sync">The synchronization object of the pipeline stage.</param>
			public SettingProxy(
				IProcessingPipelineStageConfiguration configuration,
				string                                name,
				T                                     defaultValue,
				object                                sync)
			{
				mSettingName = name;
				mDefaultSettingValue = defaultValue;
				mSync = sync;

				SetProxyTarget(configuration);
			}

			/// <summary>
			/// Occurs when the setting changes.
			/// The event handler is always invoked by a worker thread to avoid deadlocks that might be caused by lock inversion.
			/// </summary>
			public event EventHandler<SettingChangedEventArgs> SettingChanged;

			/// <summary>
			/// Registers the specified <see cref="EventHandler{SettingChangedEventArgs}"/> for the <see cref="SettingChanged"/> event.
			/// The event handler is always invoked by a worker thread to avoid deadlocks that might be caused by lock inversion.
			/// </summary>
			/// <param name="handler">Event handler to register.</param>
			/// <param name="invokeInCurrentSynchronizationContext">Must always be <c>false</c>, invokes the event handler in a worker thread.</param>
			/// <exception cref="NotSupportedException">Invoking handler in the current synchronization context is not supported.</exception>
			public void RegisterPropertyChangedEventHandler(
				EventHandler<SettingChangedEventArgs> handler,
				bool                                  invokeInCurrentSynchronizationContext = false)
			{
				// ensure the caller wants its handler invoked by a worker thread
				// (this is always done due to the integration with the underlying configuration)
				if (invokeInCurrentSynchronizationContext)
					throw new NotSupportedException("Invoking handler in the current synchronization context is not supported.");

				SettingChanged += handler;
			}

			/// <summary>
			/// Unregisters the specified <see cref="EventHandler{SettingChangedEventArgs}"/> from the <see cref="SettingChanged"/> event.
			/// </summary>
			/// <param name="handler">Event handler to unregister.</param>
			public void UnregisterPropertyChangedEventHandler(EventHandler<SettingChangedEventArgs> handler)
			{
				SettingChanged -= handler;
			}

			/// <summary>
			/// Binds the setting proxy to another pipeline stage configuration.
			/// </summary>
			/// <param name="configuration">The configuration the proxy should bind to.</param>
			public void SetProxyTarget(IProcessingPipelineStageConfiguration configuration)
			{
				lock (mSync)
				{
					if (mConfiguration != configuration)
					{
						// Exchange the configuration, rebind the PropertyChanged handler and fire the PropertyChanged event
						// to notify clients to re-evaluate the setting. It's necessary to let the executing thread invoke event
						// handlers synchronously to ensure the stage is set up properly at the end.
						mConfiguration = configuration;
						mSetting?.UnregisterPropertyChangedEventHandler(OnSettingPropertyChanged);
						mSetting = mConfiguration.RegisterSetting(mSettingName, mDefaultSettingValue);
						mSetting?.RegisterPropertyChangedEventHandler(OnSettingPropertyChanged, false);
						OnSettingPropertyChanged(this, SettingChangedEventArgs.Default);
					}
				}
			}

			/// <summary>
			/// Gets the name of the setting.
			/// </summary>
			public string Name
			{
				get
				{
					lock (mSync) return mSetting.Name;
				}
			}

			/// <summary>
			/// Gets the type of the value.
			/// </summary>
			public Type ValueType
			{
				get
				{
					lock (mSync) return mSetting.ValueType;
				}
			}

			/// <summary>
			/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
			/// </summary>
			public bool HasValue
			{
				get
				{
					lock (mSync) return mSetting.HasValue;
				}
			}

			/// <summary>
			/// Gets or sets the value of the setting.
			/// </summary>
			public T Value
			{
				get
				{
					lock (mSync) return mSetting.Value;
				}
				set
				{
					lock (mSync) mSetting.Value = value;
				}
			}

			/// <summary>
			/// Gets or sets the value of the setting.
			/// </summary>
			object IUntypedProcessingPipelineStageSetting.Value
			{
				get
				{
					lock (mSync) return ((IUntypedProcessingPipelineStageSetting)mSetting).Value;
				}
				set
				{
					lock (mSync) ((IUntypedProcessingPipelineStageSetting)mSetting).Value = value;
				}
			}

			/// <summary>
			/// Gets or sets the value of the setting as a string (for serialization purposes).
			/// </summary>
			public string ValueAsString
			{
				get
				{
					lock (mSync) return mSetting.ValueAsString;
				}
				set
				{
					lock (mSync) mSetting.ValueAsString = value;
				}
			}

			/// <summary>
			/// Gets a value indicating whether the setting has valid default value.
			/// </summary>
			public bool HasDefaultValue
			{
				get
				{
					lock (mSync) return mSetting.HasDefaultValue;
				}
			}

			/// <summary>
			/// Gets the default value of the setting.
			/// </summary>
			public T DefaultValue
			{
				get
				{
					lock (mSync) return mSetting.DefaultValue;
				}
			}

			/// <summary>
			/// Gets the default value of the setting.
			/// </summary>
			object IUntypedProcessingPipelineStageSetting.DefaultValue
			{
				get
				{
					lock (mSync) return ((IUntypedProcessingPipelineStageSetting)mSetting).DefaultValue;
				}
			}

			/// <summary>
			/// Is called by a worker thread when the backing setting changes.
			/// </summary>
			/// <param name="sender">The backing setting.</param>
			/// <param name="e">Event arguments.</param>
			private void OnSettingPropertyChanged(object sender, SettingChangedEventArgs e)
			{
				var handler = SettingChanged;
				handler?.Invoke(this, e);
			}

			/// <summary>
			/// Gets the string representation of the setting.
			/// </summary>
			/// <returns>String representation of the setting.</returns>
			public override string ToString()
			{
				lock (mSync)
				{
					return mSetting.ToString();
				}
			}
		}
	}

}
