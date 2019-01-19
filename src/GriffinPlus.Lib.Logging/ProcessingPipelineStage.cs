///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://griffin.plus)
//
// Copyright 2018 Sascha Falk <sascha@falk-online.eu>
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
using System.Threading;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline.
	/// </summary>
	public abstract class ProcessingPipelineStage<T> : IProcessingPipelineStage
		where T: ProcessingPipelineStage<T>
	{
		/// <summary>
		/// Log message processing pipeline stages to call after the current stage has completed processing.
		/// </summary>
		protected IProcessingPipelineStage[] mNextStages;

		/// <summary>
		/// Object to use for monitor synchronization (protects changes to the stage).
		/// </summary>
		protected readonly object mSync = new object();

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class.
		/// </summary>
		public ProcessingPipelineStage()
		{
			mNextStages = new IProcessingPipelineStage[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class by copying another instance.
		/// </summary>
		/// <param name="other">Instance to copy.</param>
		protected ProcessingPipelineStage(T other)
		{
			mNextStages = new IProcessingPipelineStage[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStage{T}"/> class (for internal use only).
		/// </summary>
		/// <param name="nextStages">The next processing pipeline stages.</param>
		private ProcessingPipelineStage(IProcessingPipelineStage[] nextStages)
		{
			mNextStages = nextStages;
		}

		/// <summary>
		/// Gets all pipeline stages following the current stage (including the current one).
		/// </summary>
		/// <param name="stages">Set to add the pipeline stages to.</param>
		public void GetAllStages(HashSet<IProcessingPipelineStage> stages)
		{
			stages.Add(this);
			var nextStages = Volatile.Read(ref mNextStages);
			for (int i = 0; i < nextStages.Length; i++) {
				nextStages[i].GetAllStages(stages);
			}
		}

		/// <summary>
		/// When overridden in a derived class, returns a dictionary containing the default settings the
		/// pipeline stage operates with.
		/// </summary>
		/// <returns>Dictionary with default settings</returns>
		public virtual IDictionary<string,string> GetDefaultSettings()
		{
			return new Dictionary<string, string>();
		}

		/// <summary>
		/// When overridden in a derived class, processes the specified log message and passes the log message
		/// to the next processing stages. This class simply passes the log message to the next pipeline stages.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <remarks>
		/// Do not keep a reference to the passed log message object as it returns to a pool.
		/// Log message objects are re-used to reduce garbage collection pressure.
		/// </remarks>
		public virtual void Process(LocalLogMessage message)
		{
			// pass log message to the next pipeline stages
			var stages = Volatile.Read(ref mNextStages);
			for (int i = 0; i < stages.Length; i++) {
				stages[i].Process(message);
			}
		}

		/// <summary>
		/// Links the specified pipeline stages to the current stage.
		/// </summary>
		/// <param name="nextStages">Pipeline stages to pass log messages to, when the current stage has completed.</param>
		/// <returns>A new pipeline stage of the same type containing the update.</returns>
		public T FollowedBy(params IProcessingPipelineStage[] nextStages)
		{
			lock (mSync)
			{
				int count = mNextStages.Length + nextStages.Length;
				IProcessingPipelineStage[] newNextStages = new IProcessingPipelineStage[count];
				Array.Copy(mNextStages, newNextStages, mNextStages.Length);
				Array.Copy(nextStages, 0, newNextStages, mNextStages.Length, nextStages.Length);
				Volatile.Write(ref mNextStages, newNextStages);
			}

			return this as T;
		}
	}

}
