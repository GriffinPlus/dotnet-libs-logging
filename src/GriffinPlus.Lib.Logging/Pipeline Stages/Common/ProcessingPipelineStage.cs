///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

using GriffinPlus.Lib.Conversion;

// ReSharper disable InconsistentNaming
// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging;

/// <summary>
/// Base class for the <see cref="SyncProcessingPipelineStage"/> and the <see cref="AsyncProcessingPipelineStage"/> class
/// that should be used for concrete pipeline stage implementations.
/// </summary>
public abstract partial class ProcessingPipelineStage
{
	/// <summary>
	/// Indicates whether the pipeline stage is initialized, i.e. attached to the logging subsystem.
	/// </summary>
	protected bool mInitialized;

	/// <summary>
	/// Indicates whether the pipeline stage is being initialized.
	/// </summary>
	protected bool mInitializing;

	/// <summary>
	/// Processing pipeline stages that are called after the current stage has completed processing.
	/// </summary>
	protected ProcessingPipelineStage[] mNextStages = Array.Empty<ProcessingPipelineStage>();

	/// <summary>
	/// Initializes a new instance of the <see cref="SyncProcessingPipelineStage"/> class.
	/// </summary>
	protected ProcessingPipelineStage()
	{
		ILogConfiguration configuration = null;

		if (sNameOfPipelineStageToCreate.Value.Count > 0)
		{
			ConstructionData constructionData = sNameOfPipelineStageToCreate.Value.Peek();
			Name = constructionData.Name;
			configuration = constructionData.Configuration;
		}
		else
		{
			Name = $"Unnamed ({Guid.NewGuid():D})";
		}

		lock (mSettingProxies) // ensures that a configuration change does not disturb the setup
		{
			// set the log configuration
			mConfiguration = configuration ?? new VolatileLogConfiguration();
			mConfiguration.RegisterChangedEventHandler(OnLogConfigurationChanged, false);

			// get the settings associated with the pipeline stage to create
			mSettings =
				mConfiguration.ProcessingPipeline.Stages.FirstOrDefault(x => x.Name == Name) ??
				mConfiguration.ProcessingPipeline.Stages.AddNew(Name);

			// setting proxies do not need to be rebound as there are no proxies, yet
			Debug.Assert(mSettingProxies.Count == 0);
		}
	}

	/// <summary>
	/// Gets the name of the processing pipeline stage identifying the stage
	/// (unique throughout the entire processing pipeline).
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Gets a value indicating whether the stage is the default stage that was created by the logging subsystem at start.
	/// </summary>
	public bool IsDefaultStage { get; internal set; }

	/// <summary>
	/// Gets the object to use for synchronization of changes to the pipeline stage using a monitor.
	/// </summary>
	protected object Sync { get; } = new();

	#region Creating Named Pipeline Stages

	private struct ConstructionData
	{
		public string            Name;
		public ILogConfiguration Configuration;
	}

	private static readonly ThreadLocal<Stack<ConstructionData>> sNameOfPipelineStageToCreate = new(() => new Stack<ConstructionData>());

	/// <summary>
	/// Creates a new pipeline stage with the specified name and settings.
	/// </summary>
	/// <typeparam name="TPipelineStage">Type of the pipeline stage to create.</typeparam>
	/// <param name="name">Name of the pipeline stage to create.</param>
	/// <param name="configuration">The logging configuration (<c>null</c> to set up a volatile configuration with defaults).</param>
	/// <returns>The created pipeline stage.</returns>
	public static TPipelineStage Create<TPipelineStage>(
		string            name,
		ILogConfiguration configuration)
		where TPipelineStage : ProcessingPipelineStage, new()
	{
		try
		{
			var constructionData = new ConstructionData { Name = name, Configuration = configuration };
			sNameOfPipelineStageToCreate.Value.Push(constructionData);
			return new TPipelineStage();
		}
		finally
		{
			sNameOfPipelineStageToCreate.Value.Pop();
		}
	}

	#endregion

	#region Initialization / Shutdown

	/// <summary>
	/// Gets a value indicating whether the pipeline stage is initialized, i.e. it is attached to the logging subsystem.
	/// </summary>
	public bool IsInitialized
	{
		get
		{
			lock (Sync) return mInitialized;
		}
	}

	/// <summary>
	/// Gets a value indicating whether the pipeline stage is being initialized.
	/// </summary>
	protected bool IsInitializing
	{
		get
		{
			lock (Sync) return mInitializing;
		}
	}

	/// <summary>
	/// Initializes the processing pipeline stage.
	/// This method is called by the logging subsystem and should not be called explicitly.
	/// </summary>
	public void Initialize()
	{
		lock (Sync)
		{
			if (mInitialized)
			{
				throw new InvalidOperationException("The pipeline stage is already initialized.");
			}

			try
			{
				// the pipeline is being initialized
				mInitializing = true;

				// perform the actual initialization
				OnInitializeBase();

				// initialize the following pipeline stages as well (must be done within the pipeline lock of the
				// current stage to ensure that all pipeline stages or none at all are initialized)
				try
				{
					InitializeNextStages();
				}
				catch (Exception)
				{
					OnShutdownBase();
					throw;
				}

				// the pipeline stage is initialized now
				mInitialized = true;
			}
			finally
			{
				mInitializing = false;
			}
		}
	}

	/// <summary>
	/// Is called on behalf of <see cref="ProcessingPipelineStage.Initialize"/> (for internal use only).
	/// </summary>
	internal abstract void OnInitializeBase();

	/// <summary>
	/// When overridden in a derived class, performs pipeline stage specific initialization tasks that must run when
	/// the pipeline stage is attached to the logging subsystem. This method is called from within the pipeline stage
	/// lock (<see cref="Sync"/>).
	/// </summary>
	protected virtual void OnInitialize() { }

	/// <summary>
	/// Shuts the processing pipeline stage down gracefully (works for a partially initialized pipeline stage as well).
	/// This method is called by the logging subsystem and should not be called explicitly.
	/// This method must not throw exceptions.
	/// </summary>
	public void Shutdown()
	{
		lock (Sync)
		{
			// shut down the following pipeline stages first
			ShutdownNextStages();

			// shut the stage itself down
			OnShutdownBase();

			// the stage has completed shutting down
			mInitialized = false;
		}
	}

	/// <summary>
	/// Is called on behalf of <see cref="ProcessingPipelineStage.Shutdown"/> (for internal use only).
	/// This method must not throw exceptions.
	/// </summary>
	internal abstract void OnShutdownBase();

	/// <summary>
	/// When overridden in a derived class, performs pipeline stage specific cleanup tasks that must run when the
	/// pipeline stage is about to be detached from the logging subsystem. This method is called from within the
	/// pipeline stage lock (<see cref="Sync"/>). This method must not throw exceptions.
	/// </summary>
	protected internal virtual void OnShutdown() { }

	#endregion

	#region Chaining Pipeline Stages

	/// <summary>
	/// Gets or sets processing pipeline stages that are called after the current stage has completed processing.
	/// </summary>
	public ProcessingPipelineStage[] NextStages
	{
		get
		{
			lock (Sync)
			{
				var copy = new ProcessingPipelineStage[mNextStages.Length];
				Array.Copy(mNextStages, copy, mNextStages.Length);
				return copy;
			}
		}

		set
		{
			// ensure that the new stages are not null and not initialized
			if (value == null) throw new ArgumentNullException();
			for (int i = 0; i < value.Length; i++)
			{
				if (value[i] == null) throw new ArgumentException("The collection of following stages must not contain a null reference");
				if (value[i].IsInitialized) throw new ArgumentException("The new stages must not be initialized, yet.");
				if (!ReferenceEquals(value[i].Configuration, Configuration)) throw new ArgumentException("The new stages must use the same configuration as the current stage.");
			}

			lock (Sync)
			{
				// shut the following stages down, if the stage is attached to the logging subsystem
				if (mInitialized) ShutdownNextStages();

				// set new following stages
				ProcessingPipelineStage[] oldNextStages = mNextStages;
				var copy = new ProcessingPipelineStage[value.Length];
				Array.Copy(value, copy, value.Length);
				mNextStages = copy;

				// initialize the following stages, if this stage is initialized
				if (mInitialized)
				{
					try
					{
						InitializeNextStages();
					}
					catch (Exception)
					{
						// restore following stages
						// an error occurred
						mNextStages = oldNextStages;
						InitializeNextStages();
						throw;
					}
				}
			}
		}
	}

	/// <summary>
	/// Gets all pipeline stages following the current stage recursively (including the current one).
	/// </summary>
	/// <param name="stages">Set to add the pipeline stages to.</param>
	public void GetAllStages(HashSet<ProcessingPipelineStage> stages)
	{
		lock (Sync)
		{
			stages.Add(this);
			for (int i = 0; i < mNextStages.Length; i++)
			{
				mNextStages[i].GetAllStages(stages);
			}
		}
	}

	/// <summary>
	/// Creates, configures and adds a pipeline stage of the specified type to receive log messages, when the current stage has
	/// completed running its <see cref="ProcessMessage"/> method. The method must return <c>true</c> to call the following stage.
	/// The configuration associated with the current pipeline stage is also associated with the new pipeline stage.
	/// </summary>
	/// <typeparam name="TProcessingPipelineStage">The pipeline stage that should follow the current stage.</typeparam>
	/// <param name="name">Name of the pipeline stage to add.</param>
	/// <param name="initializer">Callback that configures the pipeline stage (may be <c>null</c>).</param>
	/// <returns>The added pipeline stage.</returns>
	public TProcessingPipelineStage AddNextStage<TProcessingPipelineStage>(
		string                                                       name,
		ProcessingPipelineStageInitializer<TProcessingPipelineStage> initializer = null)
		where TProcessingPipelineStage : ProcessingPipelineStage, new()
	{
		var stage = Create<TProcessingPipelineStage>(name, Configuration);
		initializer?.Invoke(stage);
		AddNextStage(stage);
		return stage;
	}

	/// <summary>
	/// Configures the specified pipeline stage to receive log messages, when the current stage has completed running
	/// its <see cref="ProcessMessage"/> method. The method must return <c>true</c> to call the following stage.
	/// The pipeline stage must use the same log configuration as the current stage and must not be initialized, yet.
	/// </summary>
	/// <param name="stage">The pipeline stage that should follow the current stage.</param>
	/// <exception cref="ArgumentNullException">
	/// <paramref name="stage"/> is <c>null</c>.
	/// </exception>
	/// <exception cref="ArgumentException">
	/// <paramref name="stage"/> is already initialized or its configuration is not the same as the configuration of the current state.
	/// </exception>
	public void AddNextStage(ProcessingPipelineStage stage)
	{
		if (stage == null) throw new ArgumentNullException(nameof(stage));
		if (stage.IsInitialized) throw new ArgumentException("The stage to add must not be initialized.", nameof(stage));
		if (!ReferenceEquals(stage.Configuration, Configuration))
			throw new ArgumentException("The stage must use the same configuration as the current stage.", nameof(stage));

		lock (Sync)
		{
			ProcessingPipelineStage[] oldNextStages = mNextStages;
			var copy = new ProcessingPipelineStage[mNextStages.Length + 1];
			Array.Copy(mNextStages, copy, mNextStages.Length);
			copy[^1] = stage;
			mNextStages = copy;

			// initialize the added stage, if this stage is initialized
			if (mInitialized)
			{
				try
				{
					stage.Initialize();
				}
				catch (Exception)
				{
					try
					{
						stage.Shutdown();
					}
					catch (Exception ex)
					{
						// swallow exception to avoid crashing the application, if the exception is not handled properly
						Debug.Fail("The pipeline stage threw an exception while shutting down", ex.ToString());
					}

					mNextStages = oldNextStages;
					throw;
				}
			}
		}
	}

	/// <summary>
	/// Removes the specified pipeline stage from the list of following pipeline stages.
	/// </summary>
	/// <param name="stage">Pipeline stage to remove.</param>
	/// <returns>
	/// <c>true</c> if the specified pipeline stage was removed successfully;<br/>
	/// <c>false</c> if the specified pipeline stage is not one of the following pipeline stages of the current stage.
	/// </returns>
	public bool RemoveNextStage(ProcessingPipelineStage stage)
	{
		if (stage == null) throw new ArgumentNullException(nameof(stage));

		lock (Sync)
		{
			for (int i = 0; i < mNextStages.Length; i++)
			{
				if (mNextStages[i] == stage)
				{
					// found the stage

					// shut the stage down, if necessary
					if (mInitialized)
					{
						try
						{
							stage.Shutdown();
						}
						catch (Exception ex)
						{
							// swallow exception to avoid crashing the application, if the exception is not handled properly
							Debug.Fail("The pipeline stage threw an exception while shutting down", ex.ToString());
						}
					}

					// remove the stage
					var copy = new ProcessingPipelineStage[mNextStages.Length - 1];
					for (int j = 0, k = 0; j < mNextStages.Length; j++, k++)
					{
						if (mNextStages[j] == stage)
						{
							k--;
							continue;
						}

						copy[k] = mNextStages[j];
					}

					mNextStages = copy;

					return true;
				}
			}

			return false;
		}
	}

	#endregion

	#region Settings Backed by the Log Configuration

	// all members below are synchronized using mSettingsSync
	private readonly ILogConfiguration                     mConfiguration;
	private          IProcessingPipelineStageConfiguration mSettings;
	private readonly List<IUntypedSettingProxy>            mSettingProxies = [];
	private readonly object                                mSettingsSync   = new();

	/// <summary>
	/// Gets the logging configuration.
	/// </summary>
	/// <exception cref="ArgumentNullException">The specified configuration is <c>null</c>.</exception>
	public ILogConfiguration Configuration
	{
		get
		{
			lock (mSettingsSync)
			{
				return mConfiguration;
			}
		}
	}

	/// <summary>
	/// Gets the configuration the pipeline stage operates with.
	/// </summary>
	/// <returns>Configuration of the pipeline stage.</returns>
	public IProcessingPipelineStageConfiguration Settings
	{
		get
		{
			lock (mSettingsSync)
			{
				return mSettings;
			}
		}
	}

	/// <summary>
	/// Registers a pipeline stage setting and returns a setting proxy that refers to the current configuration.
	/// The proxy is rebound when a new pipeline stage configuration is set. This avoids breaking the link between
	/// the pipeline stage and its configuration.
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	/// <param name="name">Name of the setting.</param>
	/// <param name="defaultValue">Default value of the setting.</param>
	/// <returns>A setting proxy that allows to access the underlying exchangeable pipeline stage configuration.</returns>
	/// <exception cref="InvalidOperationException">The setting has already been registered.</exception>
	protected IProcessingPipelineStageSetting<T> RegisterSetting<T>(string name, T defaultValue)
	{
		lock (mSettingsSync)
		{
			if (mSettingProxies.Any(x => x.Name == name))
				throw new InvalidOperationException("The setting has already been registered.");

			var proxy = new SettingProxy<T>(this, mSettings, name, defaultValue);
			mSettingProxies.Add(proxy);
			return proxy;
		}
	}

	/// <summary>
	/// Registers a pipeline stage setting and returns a setting proxy that refers to the current configuration.
	/// The setting uses the specified convert delegates to convert an object of the specified type to its string
	/// representation and vice versa. The proxy is rebound when a new pipeline stage configuration is set.
	/// This avoids breaking the link between the pipeline stage and its configuration.
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	/// <param name="name">Name of the setting.</param>
	/// <param name="defaultValue">Default value of the setting.</param>
	/// <param name="valueToStringConverter">Delegate that converts a setting value to its string representation.</param>
	/// <param name="stringToValueConverter">Delegate that converts the string representation of a setting value to an object of the specified type.</param>
	/// <returns>A setting proxy that allows to access the underlying exchangeable pipeline stage configuration.</returns>
	/// <exception cref="InvalidOperationException">The setting has already been registered.</exception>
	protected IProcessingPipelineStageSetting<T> RegisterSetting<T>(
		string                              name,
		T                                   defaultValue,
		ObjectToStringConversionDelegate<T> valueToStringConverter,
		StringToObjectConversionDelegate<T> stringToValueConverter)
	{
		lock (mSettingsSync)
		{
			if (mSettingProxies.Any(x => x.Name == name))
				throw new InvalidOperationException("The setting has already been registered.");

			var proxy = new SettingProxy<T>(
				this,
				mSettings,
				name,
				defaultValue,
				valueToStringConverter,
				stringToValueConverter);

			mSettingProxies.Add(proxy);
			return proxy;
		}
	}

	/// <summary>
	/// Rebinds setting proxies to the current configuration.
	/// </summary>
	private void RebindSettingProxies()
	{
		lock (mSettingProxies)
		{
			// get the settings associated with the pipeline stage to create
			IProcessingPipelineStageConfiguration settings =
				mConfiguration.ProcessingPipeline.Stages.FirstOrDefault(x => x.Name == Name) ??
				mConfiguration.ProcessingPipeline.Stages.AddNew(Name);

			// abort, if the pipeline stage configuration is still the same
			// (there are other mechanisms that handle changes to settings in a pipeline stage configuration)
			if (ReferenceEquals(mSettings, settings))
				return;

			// the pipeline stage configuration has changed
			// => update setting proxies to use the new settings
			mSettings = settings;
			foreach (IUntypedSettingProxy proxy in mSettingProxies)
			{
				proxy.SetProxyTarget(mSettings, true);
			}
		}
	}

	/// <summary>
	/// Is called when the log configuration changes (always invoked by a worker thread).
	/// </summary>
	/// <param name="sender">The log configuration that has changed.</param>
	/// <param name="e">Event arguments (not used).</param>
	private void OnLogConfigurationChanged(object sender, EventArgs e)
	{
		RebindSettingProxies();
	}

	/// <summary>
	/// When overridden in a derived class, notifies that the specified setting has changed (for internal use only).
	/// </summary>
	/// <param name="setting">The setting that has changed.</param>
	internal abstract void ProcessSettingChanged(IUntypedSettingProxy setting);

	#endregion

	#region Processing Messages and Notifications

	/// <summary>
	/// Processes that a new log level was added to the logging subsystem.
	/// </summary>
	/// <param name="level">The new log level.</param>
	internal void ProcessLogLevelAdded(LogLevel level)
	{
		Debug.Assert(Monitor.IsEntered(Log.Sync));

		lock (Sync)
		{
			// call OnLogLevelAdded() of this stage
			try
			{
				OnLogLevelAdded(level);
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage threw an exception processing a notification about a new log level", ex.ToString());
			}

			// call OnLogLevelAdded() of following stages
			for (int i = 0; i < mNextStages.Length; i++)
			{
				try
				{
					mNextStages[i].ProcessLogLevelAdded(level);
				}
				catch (Exception ex)
				{
					// swallow exception to avoid crashing the application, if the exception is not handled properly
					Debug.Fail("A following pipeline stage threw an exception processing a notification about a new log level", ex.ToString());
				}
			}
		}
	}

	/// <summary>
	/// Is called when a new log level was added to the logging subsystem
	/// (the pipeline stage lock <see cref="Sync"/> is acquired when this method is called).
	/// This method must not throw exceptions.
	/// </summary>
	/// <param name="level">The new log level.</param>
	protected virtual void OnLogLevelAdded(LogLevel level) { }

	/// <summary>
	/// Processes that a new log writer was added to the logging subsystem.
	/// </summary>
	/// <param name="writer">the new log writer.</param>
	internal void ProcessLogWriterAdded(LogWriter writer)
	{
		Debug.Assert(Monitor.IsEntered(Log.Sync));

		lock (Sync)
		{
			// call OnLogWriterAdded() of this stage
			try
			{
				OnLogWriterAdded(writer);
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage threw an exception processing a notification about a new log writer", ex.ToString());
			}

			// call OnLogWriterAdded() of following stages
			for (int i = 0; i < mNextStages.Length; i++)
			{
				try
				{
					mNextStages[i].ProcessLogWriterAdded(writer);
				}
				catch (Exception ex)
				{
					// swallow exception to avoid crashing the application, if the exception is not handled properly
					Debug.Fail("A following pipeline stage threw an exception processing a notification about a new log writer", ex.ToString());
				}
			}
		}
	}

	/// <summary>
	/// Is called when a new log writer was added to the logging subsystem
	/// (the pipeline stage lock <see cref="Sync"/> is acquired when this method is called).
	/// This method must not throw exceptions.
	/// </summary>
	/// <param name="writer">The new log writer.</param>
	protected virtual void OnLogWriterAdded(LogWriter writer) { }

	/// <summary>
	/// Processes that a new log writer tag was added to the logging subsystem.
	/// </summary>
	/// <param name="tag">the new log writer tag.</param>
	internal void ProcessLogWriterTagAdded(LogWriterTag tag)
	{
		Debug.Assert(Monitor.IsEntered(Log.Sync));

		lock (Sync)
		{
			// call OnLogWriterTagAdded() of this stage
			try
			{
				OnLogWriterTagAdded(tag);
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage threw an exception processing a notification about a new log writer tag", ex.ToString());
			}

			// call OnLogWriterTagAdded() of following stages
			for (int i = 0; i < mNextStages.Length; i++)
			{
				try
				{
					mNextStages[i].ProcessLogWriterTagAdded(tag);
				}
				catch (Exception ex)
				{
					// swallow exception to avoid crashing the application, if the exception is not handled properly
					Debug.Fail("A following pipeline stage threw an exception processing a notification about a new log writer tag", ex.ToString());
				}
			}
		}
	}

	/// <summary>
	/// Is called when a new log writer tag was added to the logging subsystem
	/// (the pipeline stage lock <see cref="Sync"/> is acquired when this method is called).
	/// This method must not throw exceptions.
	/// </summary>
	/// <param name="tag">The new log writer tag.</param>
	protected virtual void OnLogWriterTagAdded(LogWriterTag tag) { }

	/// <summary>
	/// Processes the specified log message synchronously and passes the log message to the next processing stages,
	/// if appropriate. This method must not throw exceptions.
	/// </summary>
	/// <param name="message">Message to process.</param>
	public void ProcessMessage(LocalLogMessage message)
	{
		if (message == null) throw new ArgumentNullException(nameof(message));

		lock (Sync)
		{
			if (!mInitialized)
			{
				throw new InvalidOperationException("The pipeline stage is not initialized. Ensure it is attached to the logging subsystem.");
			}

			try
			{
				if (OnProcessMessageBase(message))
				{
					// pass log message to the next pipeline stages
					for (int i = 0; i < mNextStages.Length; i++)
					{
						mNextStages[i].ProcessMessage(message);
					}
				}
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage threw an exception processing a message.", ex.ToString());
			}
		}
	}

	/// <summary>
	/// Is called on behalf of <see cref="ProcessMessage"/> (for internal use only).
	/// This method must not throw exceptions.
	/// </summary>
	/// <param name="message">Message to process.</param>
	/// <returns>
	/// <c>true</c> to pass the message to the following stages;<br/>
	/// <c>false</c> to stop processing the message.
	/// </returns>
	internal abstract bool OnProcessMessageBase(LocalLogMessage message);

	#endregion

	#region Helpers

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/>, if the pipeline stage is not initialized (attached to the logging subsystem).
	/// It's also ok, if the pipeline stage is initializing at the moment.
	/// </summary>
	protected void EnsureAttachedToLoggingSubsystem()
	{
		if (!mInitialized && !mInitializing)
			throw new InvalidOperationException("The pipeline stage is not initialized. Consider attaching the pipeline stage to the logging subsystem before.");
	}

	/// <summary>
	/// Throws an <see cref="InvalidOperationException"/>, if the pipeline stage is already initialized (attached to the logging subsystem).
	/// Also, the pipeline stage must not be initializing.
	/// </summary>
	protected void EnsureNotAttachedToLoggingSubsystem()
	{
		if (mInitialized || mInitializing)
			throw new InvalidOperationException("The pipeline stage is already initialized. Configure the stage before attaching it to the logging subsystem.");
	}

	/// <summary>
	/// Writes a pipeline info message
	/// (can be used by pipeline stages to emit error information before the pipeline is set up).
	/// </summary>
	/// <param name="message">Message describing the error condition.</param>
	protected void WritePipelineInfo(string message)
	{
		var builder = new StringBuilder();
		builder.AppendLine($"Pipeline Stage: {Name}");
		builder.AppendLine($"Message: {message}");
		Log.SystemLogger.WriteInfo(builder.ToString());
	}

	/// <summary>
	/// Writes a pipeline warning message
	/// (can be used by pipeline stages to emit error information before the pipeline is set up).
	/// </summary>
	/// <param name="message">Message describing the error condition.</param>
	protected void WritePipelineWarning(string message)
	{
		var builder = new StringBuilder();
		builder.AppendLine($"Pipeline Stage: {Name}");
		builder.AppendLine($"Message: {message}");
		Log.SystemLogger.WriteWarning(builder.ToString());
	}

	/// <summary>
	/// Writes a pipeline error message
	/// (can be used by pipeline stages to emit error information before the pipeline is set up).
	/// </summary>
	/// <param name="message">Message describing the error condition.</param>
	/// <param name="ex">Exception that lead to the error (if any).</param>
	protected void WritePipelineError(string message, Exception ex)
	{
		var builder = new StringBuilder();
		builder.AppendLine($"Pipeline Stage: {Name}");
		builder.AppendLine($"Message: {message}");

		if (ex != null)
		{
			builder.AppendLine("Exception:");
			builder.AppendLine(LogWriter.UnwrapException(ex));
		}

		Log.SystemLogger.WriteError(builder.ToString());
	}

	/// <summary>
	/// Initializes the stages in <see cref="mNextStages"/>.
	/// </summary>
	private void InitializeNextStages()
	{
		for (int i = 0; i < mNextStages.Length; i++)
		{
			try
			{
				mNextStages[i].Initialize();
			}
			catch (Exception)
			{
				// an error occurred initializing the following stages
				// => shut the already initialized stages down
				for (int j = 0; j < i; j++)
				{
					try
					{
						mNextStages[j].Shutdown();
					}
					catch
					{
						Debug.Fail("Stages must not throw exceptions when shutting down!");
					}
				}

				throw;
			}
		}
	}

	/// <summary>
	/// Initializes the stages in <see cref="mNextStages"/>.
	/// </summary>
	private void ShutdownNextStages()
	{
		for (int i = 0; i < mNextStages.Length; i++)
		{
			try
			{
				mNextStages[i].Shutdown();
			}
			catch (Exception)
			{
				Debug.Fail("Stages must not throw exceptions when shutting down!");
			}
		}
	}

	#endregion
}
