///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// The collection of file-backed configurations for pipeline stages (thread-safe).
	/// </summary>
	public class FileBackedProcessingPipelineStageConfigurations : IProcessingPipelineStageConfigurations
	{
		private readonly FileBackedLogConfiguration                  mLogConfiguration;
		private readonly List<IProcessingPipelineStageConfiguration> mStageConfigurations = new List<IProcessingPipelineStageConfiguration>();

		/// <summary>
		/// Initializes a new instance of the <see cref="VolatileProcessingPipelineStageConfiguration" /> class.
		/// </summary>
		/// <param name="configuration">The log configuration the processing pipeline configuration belongs to.</param>
		internal FileBackedProcessingPipelineStageConfigurations(FileBackedLogConfiguration configuration)
		{
			mLogConfiguration = configuration;
		}

		/// <summary>
		/// Gets the pipeline stage configuration at the specified index.
		/// </summary>
		/// <param name="index">Index of the pipeline stage configuration to get.</param>
		/// <returns>The pipeline stage configuration with the specified index.</returns>
		public IProcessingPipelineStageConfiguration this[int index]
		{
			get
			{
				lock (mLogConfiguration.Sync)
				{
					return mStageConfigurations[index];
				}
			}
		}

		/// <summary>
		/// Gets the number of pipeline stage configuration in the configuration.
		/// </summary>
		public int Count
		{
			get
			{
				lock (mLogConfiguration.Sync) return mStageConfigurations.Count;
			}
		}

		/// <summary>
		/// Gets an enumerator for iterating over the pipeline stage configurations
		/// (the enumerator keeps the configuration locked until it is disposed, so ensure it's Dispose() method is called).
		/// </summary>
		/// <returns>An enumerator for iterating over the pipeline stage configurations.</returns>
		public IEnumerator<IProcessingPipelineStageConfiguration> GetEnumerator()
		{
			lock (mLogConfiguration.Sync)
			{
				return new MonitorSynchronizedEnumerator<IProcessingPipelineStageConfiguration>(mStageConfigurations.GetEnumerator(), mLogConfiguration.Sync);
			}
		}

		/// <summary>
		/// Gets an enumerator for iterating over the pipeline stage configurations
		/// (the enumerator keeps the configuration locked until it is disposed, so ensure it's Dispose() method is called).
		/// </summary>
		/// <returns>An enumerator for iterating over the pipeline stage configurations.</returns>
		IEnumerator IEnumerable.GetEnumerator()
		{
			lock (mLogConfiguration.Sync)
			{
				return new MonitorSynchronizedEnumerator<IProcessingPipelineStageConfiguration>(mStageConfigurations.GetEnumerator(), mLogConfiguration.Sync);
			}
		}

		/// <summary>
		/// Adds a configuration for a pipeline stage with the specified name.
		/// </summary>
		/// <param name="name">Name of the pipeline stage.</param>
		/// <returns>Configuration for the pipeline stage with the specified name.</returns>
		public IProcessingPipelineStageConfiguration AddNew(string name)
		{
			if (name == null) throw new ArgumentNullException(nameof(name));

			lock (mLogConfiguration.Sync)
			{
				var stage = mStageConfigurations.FirstOrDefault(x => x.Name == name);
				if (stage != null) throw new ArgumentException($"The collection already contains a configuration for the pipeline stage with the specified name ({name}).", nameof(name));
				stage = new FileBackedProcessingPipelineStageConfiguration(mLogConfiguration, name);
				mStageConfigurations.Add(stage);
				return stage;
			}
		}
	}

}
