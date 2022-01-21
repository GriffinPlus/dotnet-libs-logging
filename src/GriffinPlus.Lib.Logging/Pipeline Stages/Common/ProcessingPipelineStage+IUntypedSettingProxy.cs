///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace GriffinPlus.Lib.Logging
{

	partial class ProcessingPipelineStage
	{
		/// <summary>
		/// Untyped interface for a setting proxy (must be implemented thread-safe).
		/// </summary>
		internal interface IUntypedSettingProxy : IUntypedProcessingPipelineStageSetting
		{
			/// <summary>
			/// Binds the setting proxy to another pipeline stage configuration.
			/// </summary>
			/// <param name="configuration">The configuration the proxy should bind to.</param>
			void SetProxyTarget(IProcessingPipelineStageConfiguration configuration);
		}
	}

}
