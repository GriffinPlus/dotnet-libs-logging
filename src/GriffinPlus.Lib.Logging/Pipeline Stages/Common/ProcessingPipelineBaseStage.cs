///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2018-2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

// ReSharper disable InconsistentNaming
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable EmptyConstructor

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline.
	/// Messages are always processed in the context of the thread writing the message.
	/// Therefore only lightweight processing should be done that does not involve any i/o operations that might block.
	/// </summary>
	public abstract class ProcessingPipelineBaseStage : IProcessingPipelineStage
	{
		private IProcessingPipelineStageConfiguration mSettings;

		/// <summary>
		/// Indicates whether the pipeline stage is initialized, i.e. attached to the logging subsystem.
		/// </summary>
		protected bool mInitialized;

		/// <summary>
		/// Processing pipeline stages that are called after the current stage has completed processing.
		/// </summary>
		protected IProcessingPipelineStage[] mNextStages = new IProcessingPipelineStage[0];

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		protected ProcessingPipelineBaseStage(string name)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Settings = new VolatileProcessingPipelineStageConfiguration(name, Sync);
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
		protected object Sync { get; } = new object();

		#region Initialization / Shutdown

		/// <summary>
		/// Gets a value indicating whether the pipeline stage is initialized, i.e. it is attached to the logging subsystem.
		/// </summary>
		public bool IsInitialized
		{
			get { lock (Sync) return mInitialized; }
		}

		/// <summary>
		/// Initializes the processing pipeline stage.
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// </summary>
		void IProcessingPipelineStage.Initialize()
		{
			lock (Sync)
			{
				if (mInitialized)
				{
					throw new InvalidOperationException("The pipeline stage is already initialized.");
				}

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
		}

		/// <summary>
		/// Is called on behalf of <see cref="IProcessingPipelineStage.Initialize"/> (for internal use only).
		/// </summary>
		internal abstract void OnInitializeBase();

		/// <summary>
		/// When overridden in a derived class, performs pipeline stage specific initialization tasks that must run when
		/// the pipeline stage is attached to the logging subsystem. This method is called from within the pipeline stage
		/// lock (<see cref="Sync"/>).
		/// </summary>
		protected virtual void OnInitialize()
		{

		}

		/// <summary>
		/// Shuts the processing pipeline stage down gracefully (works for a partially initialized pipeline stage as well).
		/// This method is called by the logging subsystem and should not be called explicitly.
		/// This method must not throw exceptions.
		/// </summary>
		void IProcessingPipelineStage.Shutdown()
		{
			lock (Sync)
			{
				// shut down the following pipeline stages first
				ShutdownNextStages();

				// shut the stage itself down
				OnShutdownBase();

				mInitialized = false;
			}
		}

		/// <summary>
		/// Is called on behalf of <see cref="IProcessingPipelineStage.Shutdown"/> (for internal use only).
		/// This method must not throw exceptions.
		/// </summary>
		internal abstract void OnShutdownBase();

		/// <summary>
		/// When overridden in a derived class, performs pipeline stage specific cleanup tasks that must run when the
		/// pipeline stage is about to be detached from the logging subsystem. This method is called from within the
		/// pipeline stage lock (<see cref="Sync"/>). This method must not throw exceptions.
		/// </summary>
		protected internal virtual void OnShutdown()
		{

		}

		#endregion

		#region Chaining Pipeline Stages

		/// <summary>
		/// Gets or sets processing pipeline stages that are called after the current stage has completed processing.
		/// </summary>
		public IProcessingPipelineStage[] NextStages
		{
			get
			{
				lock (Sync)
				{
					IProcessingPipelineStage[] copy = new IProcessingPipelineStage[mNextStages.Length];
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
				}

				lock (Sync)
				{
					// shut the following stages down, if the stage is attached to the logging subsystem
					if (mInitialized) ShutdownNextStages();

					// set new following stages
					var oldNextStages = mNextStages;
					IProcessingPipelineStage[] copy = new IProcessingPipelineStage[value.Length];
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
		public void GetAllStages(HashSet<IProcessingPipelineStage> stages)
		{
			lock (Sync)
			{
				stages.Add(this);
				for (int i = 0; i < mNextStages.Length; i++) {
					mNextStages[i].GetAllStages(stages);
				}
			}
		}

		/// <summary>
		/// Configures the specified pipeline stage to receive log messages, when the current stage has completed running
		/// its <see cref="IProcessingPipelineStage.ProcessMessage"/> method. The method must return <c>true</c> to call the following stage.
		/// </summary>
		/// <param name="stage">The pipeline stage that should follow the current stage.</param>
		public void AddNextStage(IProcessingPipelineStage stage)
		{
			if (stage == null) throw new ArgumentNullException(nameof(stage));
			if (stage.IsInitialized) throw new ArgumentException("The stage to add must not be initialized.", nameof(stage));

			lock (Sync)
			{
				var oldNextStages = mNextStages;
				IProcessingPipelineStage[] copy = new IProcessingPipelineStage[mNextStages.Length + 1];
				Array.Copy(mNextStages, copy, mNextStages.Length);
				copy[copy.Length - 1] = stage;
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
		/// true, if the specified pipeline stage was removed successfully;
		/// false, if the specified pipeline stage is not one of the following pipeline stages of the current stage.
		/// </returns>
		public bool RemoveNextStage(IProcessingPipelineStage stage)
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
						IProcessingPipelineStage[] copy = new IProcessingPipelineStage[mNextStages.Length - 1];
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

		/// <summary>
		/// Gets or sets the configuration the pipeline stage operates with.
		/// </summary>
		/// <returns>Configuration of the pipeline stage.</returns>
		public IProcessingPipelineStageConfiguration Settings
		{
			get
			{
				lock (Sync)
				{
					return mSettings;
				}
			}

			set
			{
				lock (Sync)
				{
					if (mSettings != value)
					{
						var newConfiguration = value ?? new VolatileProcessingPipelineStageConfiguration(Name, null);
						mSettings = newConfiguration;
						BindSettings();
					}
				}
			}
		}

		/// <summary>
		/// Is called to allow a derived stage bind its settings when the <see cref="Settings"/> property has changed
		/// (the pipeline stage lock <see cref="Sync"/> is acquired when this method is called).
		/// </summary>
		protected virtual void BindSettings()
		{

		}

		#endregion

		#region Processing Messages and Notifications

		/// <summary>
		/// Processes that a new log level was added to the logging subsystem.
		/// </summary>
		/// <param name="level">The new log level.</param>
		void IProcessingPipelineStage.ProcessLogLevelAdded(LogLevel level)
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
		protected virtual void OnLogLevelAdded(LogLevel level)
		{

		}

		/// <summary>
		/// Processes that a new log writer was added to the logging subsystem.
		/// </summary>
		/// <param name="writer">the new log writer.</param>
		void IProcessingPipelineStage.ProcessLogWriterAdded(LogWriter writer)
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
		protected virtual void OnLogWriterAdded(LogWriter writer)
		{

		}

		/// <summary>
		/// Processes the specified log message synchronously and passes the log message to the next processing stages,
		/// if appropriate. This method must not throw exceptions.
		/// </summary>
		/// <param name="message">Message to process.</param>
		void IProcessingPipelineStage.ProcessMessage(LocalLogMessage message)
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
		/// Is called on behalf of <see cref="IProcessingPipelineStage.Shutdown"/> (for internal use only).
		/// This method must not throw exceptions.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// true to pass the message to the following stages;
		/// false to stop processing the message.
		/// </returns>
		internal abstract bool OnProcessMessageBase(LocalLogMessage message);

		#endregion

		#region Helpers

		/// <summary>
		/// Throws an <see cref="InvalidOperationException"/>, if the pipeline stage is already initialized (attached to the logging subsystem).
		/// </summary>
		protected void EnsureNotAttachedToLoggingSubsystem()
		{
			if (mInitialized) {
				throw new InvalidOperationException("The pipeline stage is already initialized. Configure the stage before attaching it to the logging subsystem.");
			}
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
					// => shut the already initialized stages down and restore the old stages
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

}
