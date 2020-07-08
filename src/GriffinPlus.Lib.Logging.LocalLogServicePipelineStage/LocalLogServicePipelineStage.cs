using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A log message processing pipeline stage that forwards log message to the local log service (proprietary, windows only).
	/// </summary>
	public class LocalLogServicePipelineStage : ProcessingPipelineStage<LocalLogServicePipelineStage>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LocalLogServicePipelineStage"/> class.
		/// </summary>
		public LocalLogServicePipelineStage()
		{

		}

		/// <summary>
		/// Initializes the pipeline stage when the stage is attached to the logging subsystem.
		/// </summary>
		protected override void OnInitialize()
		{
			base.OnInitialize();
		}

		/// <summary>
		/// Shuts the pipeline stage down when the stage is detached from the logging system.
		/// </summary>
		protected override void OnShutdown()
		{
			base.OnShutdown();
		}

		/// <summary>
		/// Processes a log message synchronously.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// true to continue processing (pass message to the following stages);
		/// false to stop processing.
		/// </returns>
		protected override bool ProcessSync(LocalLogMessage message)
		{
			return base.ProcessSync(message);
		}
	}
}
