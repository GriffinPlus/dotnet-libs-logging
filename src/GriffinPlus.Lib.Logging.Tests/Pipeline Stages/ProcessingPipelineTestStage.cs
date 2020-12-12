///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Pipeline stage that only provides functionality of the <see cref="ProcessingPipelineStage{STAGE}" /> class.
	/// It is used for testing purposes only.
	/// </summary>
	public class ProcessingPipelineTestStage : ProcessingPipelineStage<ProcessingPipelineTestStage>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineTestStage" /> class.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		public ProcessingPipelineTestStage(string name) : base(name)
		{
		}

		/// <summary>
		/// Gets a value indicating whether <see cref="OnInitialize" /> was called.
		/// </summary>
		public bool OnInitializeWasCalled { get; private set; }

		/// <summary>
		/// Gets a value indicating whether <see cref="OnShutdown" /> was called.
		/// </summary>
		public bool OnShutdownWasCalled { get; private set; }

		/// <summary>
		/// Gets a value indicating whether <see cref="ProcessSync" /> was called.
		/// </summary>
		public bool ProcessSyncWasCalled { get; private set; }

		/// <summary>
		/// Gets the message that was passed to <see cref="ProcessSync" /> with the last call.
		/// </summary>
		public LocalLogMessage MessagePassedToProcessSync { get; private set; }

		protected override void OnInitialize()
		{
			OnInitializeWasCalled = true;
			base.OnInitialize();
		}

		protected internal override void OnShutdown()
		{
			OnShutdownWasCalled = true;
			base.OnShutdown();
		}

		protected override bool ProcessSync(LocalLogMessage message)
		{
			ProcessSyncWasCalled = true;
			MessagePassedToProcessSync?.Release();
			MessagePassedToProcessSync = message;
			MessagePassedToProcessSync.AddRef();
			return base.ProcessSync(message);
		}
	}

}
