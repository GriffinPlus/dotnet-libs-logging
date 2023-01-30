///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A processing pipeline stage that splits writing a log message up and calls multiple other stages unconditionally (thread-safe).
	/// </summary>
	public class SplitterPipelineStage : SyncProcessingPipelineStage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SplitterPipelineStage"/> class.
		/// </summary>
		public SplitterPipelineStage() { }
	}

}
