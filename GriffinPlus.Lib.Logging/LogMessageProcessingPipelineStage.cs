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

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Base class for stages in the log message processing pipeline (thread-safe, since immutable).
	/// </summary>
	public abstract class LogMessageProcessingPipelineStage<T> : ILogMessageProcessingPipelineStage
		where T: LogMessageProcessingPipelineStage<T>, new()
	{
		/// <summary>
		/// Log message processing pipeline stages to call after the current stage has completed processing (immutable).
		/// </summary>
		protected ILogMessageProcessingPipelineStage[] mNextStages;

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageProcessingPipelineStage{T}"/> class.
		/// </summary>
		public LogMessageProcessingPipelineStage()
		{
			mNextStages = new ILogMessageProcessingPipelineStage[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageProcessingPipelineStage{T}"/> class by copying another instance.
		/// </summary>
		/// <param name="other">Instance to copy.</param>
		protected LogMessageProcessingPipelineStage(T other)
		{
			mNextStages = new ILogMessageProcessingPipelineStage[0];
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="LogMessageProcessingPipelineStage{T}"/> class (for internal use only).
		/// </summary>
		/// <param name="nextStages">The next processing pipeline stages.</param>
		private LogMessageProcessingPipelineStage(ILogMessageProcessingPipelineStage[] nextStages)
		{
			mNextStages = nextStages;
		}

		/// <summary>
		/// Creates a copy of the current pipeline stage.
		/// </summary>
		/// <returns>A copy of the current pipeline stage.</returns>
		/// <remarks>
		/// This method must be overridden by each and every derived class to ensure that the correct type is created.
		/// The implementation should use the copy constructor to init base class members.
		/// </remarks>
		public abstract T Dupe();

		/// <summary>
		/// Gets all pipeline stages following the current stage (including the current one).
		/// </summary>
		/// <param name="stages">Set to add the pipeline stages to.</param>
		public void GetAllStages(HashSet<ILogMessageProcessingPipelineStage> stages)
		{
			stages.Add(this);
			for (int i = 0; i < mNextStages.Length; i++) {
				mNextStages[i].GetAllStages(stages);
			}
		}

		/// <summary>
		/// When overridden in a derived class, initializes the specified dictionary with default settings the
		/// pipeline stage operates with.
		/// </summary>
		/// <param name="settings">Dictionary to populate with default settings.</param>
		public virtual void InitializeDefaultSettings(IDictionary<string, string> settings)
		{

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
		public virtual void Process(LogMessage message)
		{
			// pass log message to the next pipeline stages
			for (int i = 0; i < mNextStages.Length; i++) {
				mNextStages[i].Process(message);
			}
		}

		/// <summary>
		/// Links the specified pipeline stages to the current stage.
		/// </summary>
		/// <param name="nextStages">Pipeline stages to pass log messages to, when the current stage has completed.</param>
		/// <returns>A new pipeline stage of the same type containing the update.</returns>
		public T FollowedBy(params ILogMessageProcessingPipelineStage[] nextStages)
		{
			int count = mNextStages.Length + nextStages.Length;
			ILogMessageProcessingPipelineStage[] newNextStages = new ILogMessageProcessingPipelineStage[count];
			Array.Copy(mNextStages, newNextStages, mNextStages.Length);
			Array.Copy(nextStages, 0, newNextStages, mNextStages.Length, nextStages.Length);
			T copy = Dupe();
			copy.mNextStages = newNextStages;
			return copy;
		}
	}

}
