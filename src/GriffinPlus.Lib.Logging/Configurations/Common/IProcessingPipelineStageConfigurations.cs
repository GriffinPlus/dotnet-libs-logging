///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// Interface for the pipeline stage configuration collection (must be implemented thread-safe).
	/// </summary>
	public interface IProcessingPipelineStageConfigurations : IReadOnlyList<IProcessingPipelineStageConfiguration>
	{
		/// <summary>
		/// Adds a configuration for a pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage.</param>
		/// <returns>Configuration for the pipeline stage with the specified name.</returns>
		IProcessingPipelineStageConfiguration AddNew(string name);
	}

}
