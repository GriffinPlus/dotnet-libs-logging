///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Base class for stages in the log message processing pipeline.
	/// Messages are always processed in the context of the thread writing the message.
	/// Therefore only lightweight processing should be done that does not involve any i/o operations that might block.
	/// </summary>
	public abstract class SyncProcessingPipelineStage : ProcessingPipelineStage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyncProcessingPipelineStage"/> class.
		/// </summary>
		protected SyncProcessingPipelineStage() { }

		#region Initialization / Shutdown

		/// <summary>
		/// Initializes the processing pipeline stage (base class specific part).
		/// </summary>
		internal override void OnInitializeBase()
		{
			try
			{
				// perform pipeline stage specific initializations
				OnInitialize();
			}
			catch (Exception)
			{
				Shutdown();
				throw;
			}
		}

		/// <summary>
		/// Shuts the processing pipeline down (base class specific part).
		/// This method must not throw exceptions.
		/// </summary>
		internal override void OnShutdownBase()
		{
			try
			{
				OnShutdown();
			}
			catch (Exception ex)
			{
				Debug.Fail("OnShutdown() failed.", ex.ToString());
			}
		}

		#endregion

		#region Processing Setting Changes

		private readonly HashSet<IUntypedSettingProxy> mChangedSettings         = new HashSet<IUntypedSettingProxy>();
		private volatile bool                          mSettingChangedScheduled = false;

		/// <summary>
		/// Notifies that the specified setting has changed (for internal use only).
		/// </summary>
		/// <param name="setting">The setting that has changed.</param>
		internal override void ProcessSettingChanged(IUntypedSettingProxy setting)
		{
			// enqueue changed setting
			lock (mChangedSettings)
			{
				mChangedSettings.Add(setting);
			}

			// schedule calling OnSettingsChanged()
			if (!mSettingChangedScheduled)
			{
				mSettingChangedScheduled = true;
				ThreadPool.QueueUserWorkItem(
					obj =>
					{
						mSettingChangedScheduled = false;
						IUntypedProcessingPipelineStageSetting[] settings = null;
						lock (mChangedSettings)
						{
							if (mChangedSettings.Count > 0)
							{
								settings = mChangedSettings.Cast<IUntypedProcessingPipelineStageSetting>().ToArray();
								mChangedSettings.Clear();
							}
						}

						if (settings != null)
							OnSettingsChanged(settings);
					});
			}
		}

		/// <summary>
		/// When overridden in a derived class, processes pending changes to registered setting proxies
		/// (the method is executed by a worker thread).
		/// </summary>
		/// <param name="settings">Settings that have changed.</param>
		protected virtual void OnSettingsChanged(IUntypedProcessingPipelineStageSetting[] settings) { }

		#endregion

		#region Processing Messages

		/// <summary>
		/// Is called on behalf of <see cref="ProcessingPipelineStage.ProcessMessage"/> (for internal use only).
		/// This method must not throw exceptions.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// <c>true</c> to pass the message to the following stages;<br/>
		/// <c>false</c> to stop processing the message.
		/// </returns>
		internal override bool OnProcessMessageBase(LocalLogMessage message)
		{
			try
			{
				return ProcessSync(message);
			}
			catch (Exception ex)
			{
				// swallow exception to avoid crashing the application, if the exception is not handled properly
				Debug.Fail("The pipeline stage threw an exception processing the message.", ex.ToString());

				// let the following stages process the message
				// (hopefully this is the right decision in this case)
				return true;
			}
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log message synchronously.
		/// This method is called by the thread writing the message and from within the pipeline stage lock (<see cref="ProcessingPipelineStage.Sync"/>).
		/// This method must not throw exceptions.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// <c>true</c> to pass the message to the following pipeline stages;<br/>
		/// otherwise <c>false</c>.
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
	}

}
