///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Text;

using GriffinPlus.Lib.Conversion;

namespace GriffinPlus.Lib.Logging
{

	partial class ProcessingPipelineStage
	{
		/// <summary>
		/// A proxy for a pipeline stage specific setting.
		/// Used by derived classes from <see cref="ProcessingPipelineStage"/> to bind a setting to a pipeline stage
		/// configuration that could be exchanged afterwards. The proxy is adjusted by <see cref="ProcessingPipelineStage"/>
		/// when a pipeline stage configuration is set. This avoids breaking the link between a pipeline stage and its
		/// configuration.
		/// </summary>
		internal class SettingProxy<T> : ISettingProxy<T>
		{
			private readonly ProcessingPipelineStage               mStage;
			private readonly string                                mSettingName;
			private readonly T                                     mDefaultSettingValue;
			private readonly ObjectToStringConversionDelegate<T>   mValueToStringConverter;
			private readonly StringToObjectConversionDelegate<T>   mStringToValueConverter;
			private readonly object                                mSync = new object();
			private          IProcessingPipelineStageConfiguration mConfiguration;
			private          IProcessingPipelineStageSetting<T>    mSetting;

			/// <summary>
			/// Initializes a new instance of the <see cref="SettingProxy{T}"/> class.
			/// </summary>
			/// <param name="stage">The processing pipeline stage the setting belongs to.</param>
			/// <param name="configuration">The pipeline stage configuration containing the setting.</param>
			/// <param name="name">Name of the setting.</param>
			/// <param name="defaultValue">Default value of the setting.</param>
			internal SettingProxy(
				ProcessingPipelineStage               stage,
				IProcessingPipelineStageConfiguration configuration,
				string                                name,
				T                                     defaultValue) :
				this(stage, configuration, name, defaultValue, null, null) { }

			/// <summary>
			/// Initializes a new instance of the <see cref="SettingProxy{T}"/> class.
			/// </summary>
			/// <param name="stage">The processing pipeline stage the setting belongs to.</param>
			/// <param name="configuration">The pipeline stage configuration containing the setting.</param>
			/// <param name="name">Name of the setting.</param>
			/// <param name="defaultValue">Default value of the setting.</param>
			/// <param name="valueToStringConverter">Delegate that converts a setting value to its string representation.</param>
			/// <param name="stringToValueConverter">Delegate that converts the string representation of a setting value to an object of the specified type.</param>
			internal SettingProxy(
				ProcessingPipelineStage               stage,
				IProcessingPipelineStageConfiguration configuration,
				string                                name,
				T                                     defaultValue,
				ObjectToStringConversionDelegate<T>   valueToStringConverter,
				StringToObjectConversionDelegate<T>   stringToValueConverter)
			{
				mStage = stage;
				mSettingName = name;
				mDefaultSettingValue = defaultValue;
				mValueToStringConverter = valueToStringConverter;
				mStringToValueConverter = stringToValueConverter;

				(this as IUntypedSettingProxy).SetProxyTarget(configuration, false);
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
			public void RegisterSettingChangedEventHandler(
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
			public void UnregisterSettingChangedEventHandler(EventHandler<SettingChangedEventArgs> handler)
			{
				SettingChanged -= handler;
			}

			/// <summary>
			/// Binds the setting proxy to another pipeline stage configuration.
			/// </summary>
			/// <param name="configuration">The configuration the proxy should bind to.</param>
			/// <param name="raiseChangedEvent">
			/// <c>true</c> to notify clients that the setting has changed;<br/>
			/// otherwise <c>false</c>.
			/// </param>
			void IUntypedSettingProxy.SetProxyTarget(IProcessingPipelineStageConfiguration configuration, bool raiseChangedEvent)
			{
				lock (mSync)
				{
					if (mConfiguration != configuration)
					{
						// Exchange the configuration, rebind the PropertyChanged handler and fire the PropertyChanged event
						// to notify clients to re-evaluate the setting.
						mConfiguration = configuration;
						mSetting?.UnregisterSettingChangedEventHandler(OnSettingPropertyChanged);
						mSetting = mValueToStringConverter != null && mStringToValueConverter != null
							           ? mConfiguration.RegisterSetting(mSettingName, mDefaultSettingValue, mValueToStringConverter, mStringToValueConverter)
							           : mConfiguration.RegisterSetting(mSettingName, mDefaultSettingValue);
						mSetting?.RegisterSettingChangedEventHandler(OnSettingPropertyChanged, false);
						if (raiseChangedEvent) mStage.ProcessSettingChanged(this);
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
			/// Gets a value indicating whether the setting has valid value (<c>true</c>) or just its default value (<c>false</c>).
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
			/// Returns the default value of the setting, if parsing the setting value fails.
			/// The incident is logged using the system logger (see <see cref="Log.SystemLogger"/>).
			/// </summary>
			public T Value
			{
				get
				{
					lock (mSync)
					{
						IProcessingPipelineStageSetting<T> setting = mSetting;

						try
						{
							return setting.Value;
						}
						catch (Exception ex)
						{
							// getting the value failed (most probably due to a parsing error)
							// => log this incident and try to fall back to the default value
							var builder = new StringBuilder();
							builder.AppendLine($"Getting pipeline stage setting '{Name}' failed.");
							try
							{
								T defaultValue = setting.DefaultValue;
								string defaultValueAsString = setting.DefaultValueAsString;
								builder.AppendLine($"Falling back to default value '{defaultValueAsString}'.");
								mStage.WritePipelineError(builder.ToString(), ex);
								return defaultValue;
							}
							catch (Exception)
							{
								// getting the default value failed as well
								// => fall back to the default value of the type
								mStage.WritePipelineError(builder.ToString(), ex);
								return default;
							}
						}
					}
				}

				set
				{
					lock (mSync) mSetting.Value = value;
				}
			}

			/// <summary>
			/// Gets or sets the value of the setting.
			/// Returns the default value of the setting, if parsing the setting value fails.
			/// The incident is logged using the system logger (see <see cref="Log.SystemLogger"/>).
			/// </summary>
			object IUntypedProcessingPipelineStageSetting.Value
			{
				get
				{
					lock (mSync)
					{
						IUntypedProcessingPipelineStageSetting setting = mSetting;

						try
						{
							return setting.Value;
						}
						catch (Exception ex)
						{
							// getting the value failed (most probably due to a parsing error)
							// => log this incident and try to fall back to the default value
							var builder = new StringBuilder();
							builder.AppendLine($"Getting pipeline stage setting '{Name}' failed.");
							try
							{
								object defaultValue = setting.DefaultValue;
								string defaultValueAsString = setting.DefaultValueAsString;
								builder.AppendLine($"Falling back to default value '{defaultValueAsString}'.");
								mStage.WritePipelineError(builder.ToString(), ex);
								return defaultValue;
							}
							catch (Exception)
							{
								// getting the default value failed as well
								// => fall back to the default value of the type
								mStage.WritePipelineError(builder.ToString(), ex);
								return default;
							}
						}
					}
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
			/// Gets or sets the default value of the setting as a string (for serialization purposes).
			/// </summary>
			public string DefaultValueAsString
			{
				get
				{
					lock (mSync) return mSetting.DefaultValueAsString;
				}
			}

			/// <summary>
			/// Is called by a worker thread when the backing setting changes.
			/// </summary>
			/// <param name="sender">The backing setting.</param>
			/// <param name="e">Event arguments.</param>
			private void OnSettingPropertyChanged(object sender, SettingChangedEventArgs e)
			{
				mStage.ProcessSettingChanged(this);

				EventHandler<SettingChangedEventArgs> handler = SettingChanged;
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
