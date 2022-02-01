///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.ComponentModel;

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
			/// <param name="raiseChangedEvent">
			/// <c>true</c> to notify clients that the associated setting has changed;
			/// otherwise <c>false</c>.
			/// </param>
			void SetProxyTarget(IProcessingPipelineStageConfiguration configuration, bool raiseChangedEvent);
		}
	}

}
