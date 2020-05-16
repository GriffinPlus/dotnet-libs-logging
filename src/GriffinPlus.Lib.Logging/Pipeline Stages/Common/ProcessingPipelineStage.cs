﻿///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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

// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable EmptyConstructor

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline.
	/// Messages are always processed in the context of the thread writing the message.
	/// Therefore only lightweight processing should be done that does not involve any i/o operations that might block.
	/// </summary>
	public abstract class ProcessingPipelineStage<STAGE> : IProcessingPipelineStage
		where STAGE: ProcessingPipelineStage<STAGE>
	{
		private bool mInitialized;

		/// <summary>
		/// Processing pipeline stages that are called after the current stage has completed processing.
		/// </summary>
		protected IProcessingPipelineStage[] mNextStages = new IProcessingPipelineStage[0];

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		protected ProcessingPipelineStage(string name)
		{
			Name = name ?? throw new ArgumentNullException(nameof(name));
			Settings = new ProcessingPipelineStageConfiguration(Sync);
		}

		/// <summary>
		/// Gets the name of the processing pipeline stage identifying the stage
		/// (unique throughout the entire processing pipeline).
		/// </summary>
		public string Name { get; private set; }

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
				if (mInitialized) {
					throw new InvalidOperationException("The pipeline stage is already initialized.");
				}

				try
				{
					// perform pipeline stage specific initializations
					OnInitialize();

					// initialize the following pipeline stages as well (must be done within the pipeline lock of the
					// current stage to ensure that all pipeline stages or none at all are initialized)
					for (int i = 0; i < mNextStages.Length; i++) {
						mNextStages[i].Initialize();
					}

					// the pipeline stage is initialized now
					mInitialized = true;
				}
				catch (Exception)
				{
					(this as IProcessingPipelineStage).Shutdown();
					throw;
				}
			}
		}

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
				for (int i = 0; i < mNextStages.Length; i++) {
					mNextStages[i].Shutdown();
				}

				// perform pipeline stage specific cleanup
				try {
					OnShutdown();
				} catch (Exception ex) {
					Debug.Fail("OnShutdown() failed.", ex.ToString());
				}

				// shutting down completed
				mInitialized = false;
			}
		}

		/// <summary>
		/// When overridden in a derived class, performs pipeline stage specific cleanup tasks that must run when the
		/// pipeline stage is about to be detached from the logging subsystem. This method is called from within the
		/// pipeline stage lock (<see cref="Sync"/>).
		/// </summary>
		protected internal virtual void OnShutdown()
		{

		}

		#endregion

		#region Chaining Pipeline Stages

		/// <summary>
		/// Gets or sets processing pipeline stages that are called after the current stage has completed processing.
		/// The return value of <see cref="ProcessSync(LocalLogMessage)"/> determines whether these stages are called.
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
				if (value == null) throw new ArgumentNullException();

				lock (Sync)
				{
					EnsureNotAttachedToLoggingSubsystem();
					IProcessingPipelineStage[] copy = new IProcessingPipelineStage[value.Length];
					Array.Copy(value, copy, value.Length);
					mNextStages = copy;
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
			lock (Sync)
			{
				EnsureNotAttachedToLoggingSubsystem();
				IProcessingPipelineStage[] copy = new IProcessingPipelineStage[mNextStages.Length + 1];
				Array.Copy(mNextStages, copy, mNextStages.Length);
				copy[copy.Length - 1] = stage;
				NextStages = copy;
			}
		}

		#endregion

		#region Pipeline Stage Settings

		/// <summary>
		/// Gets the configuration the pipeline stage operates with.
		/// </summary>
		/// <returns>Configuration of the pipeline stage.</returns>
		public IProcessingPipelineStageConfiguration Settings { get; }

		#endregion

		#region Processing Messages and Notifications

		/// <summary>
		/// Processes that a new log level was added to the logging subsystem.
		/// </summary>
		/// <param name="level">The new log level.</param>
		void IProcessingPipelineStage.ProcessLogLevelAdded(LogLevel level)
		{
			Debug.Assert(Monitor.IsEntered(Log.Sync));

			List<Exception> exceptions = null;

			// call OnLogLevelAdded() of this stage
			try
			{
				OnLogLevelAdded(level);
			}
			catch (Exception ex)
			{
				exceptions = new List<Exception> { ex };
			}

			// call OnLogLevelAdded() of following stages
			for (int i = 0; i < mNextStages.Length; i++)
			{
				try
				{
					mNextStages[i].ProcessLogLevelAdded(level);
				}
				catch (AggregateException ex)
				{
					// unwrap exceptions to avoid returning a nested aggregate exception
					if (exceptions == null) exceptions = new List<Exception>();
					exceptions.AddRange(ex.InnerExceptions);
				}
				catch (Exception ex)
				{
					if (exceptions == null) exceptions = new List<Exception>();
					exceptions.Add(ex);
				}
			}

			if (exceptions != null)
			{
				throw new AggregateException(exceptions);
			}
		}

		/// <summary>
		/// Is called when a new log level was added to the logging subsystem.
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

			List<Exception> exceptions = null;

			// call OnLogWriterAdded() of this stage
			try
			{
				OnLogWriterAdded(writer);
			}
			catch (Exception ex)
			{
				exceptions = new List<Exception> { ex };
			}

			// call OnLogWriterAdded() of following stages
			for (int i = 0; i < mNextStages.Length; i++)
			{
				try
				{
					mNextStages[i].ProcessLogWriterAdded(writer);
				}
				catch (AggregateException ex)
				{
					// unwrap exceptions to avoid returning a nested aggregate exception
					if (exceptions == null) exceptions = new List<Exception>();
					exceptions.AddRange(ex.InnerExceptions);
				}
				catch (Exception ex)
				{
					if (exceptions == null) exceptions = new List<Exception>();
					exceptions.Add(ex);
				}
			}

			if (exceptions != null)
			{
				throw new AggregateException(exceptions);
			}
		}

		/// <summary>
		/// Is called when a new log writer was added to the logging subsystem.
		/// </summary>
		/// <param name="writer">The new log writer.</param>
		protected virtual void OnLogWriterAdded(LogWriter writer)
		{

		}

		/// <summary>
		/// Processes the specified log message synchronously and passes the log message to the next processing stages,
		/// if <see cref="ProcessSync(LocalLogMessage)"/> returns <c>true</c>.
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

				if (ProcessSync(message))
				{
					// pass log message to the next pipeline stages
					for (int i = 0; i < mNextStages.Length; i++)
					{
						mNextStages[i].ProcessMessage(message);
					}
				}
			}
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log message synchronously.
		/// This method is called by the thread writing the message and from within the pipeline stage lock (<see cref="Sync"/>).
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// true to pass the message to the following pipeline stages;
		/// otherwise false.
		/// </returns>
		/// <remarks>
		/// Call <see cref="LocalLogMessage.AddRef"/> on a message that should be stored any longer to prevent it from
		/// returning to the log message pool too early. Call <see cref="LocalLogMessage.Release"/> as soon as you don't
		/// need the message any more.
		/// </remarks>
		protected virtual bool ProcessSync(LocalLogMessage message)
		{
			return true;
		}

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

		#endregion

	}

}