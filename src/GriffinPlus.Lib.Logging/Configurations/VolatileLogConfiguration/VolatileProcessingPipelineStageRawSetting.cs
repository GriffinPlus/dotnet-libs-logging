///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// A raw setting in a <see cref="VolatileProcessingPipelineStageConfiguration"/>.
	/// </summary>
	public class VolatileProcessingPipelineStageRawSetting : IProcessingPipelineStageRawSetting
	{
		private string mValue;
		private bool   mHasValue;
		private string mDefaultValue;
		private bool   mHasDefaultValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="VolatileProcessingPipelineStageRawSetting"/> class
		/// with a default value.
		/// </summary>
		/// <param name="configuration">The configuration the setting belongs to.</param>
		/// <param name="name">Name of the setting.</param>
		/// <param name="defaultValue">The default value of the setting.</param>
		internal VolatileProcessingPipelineStageRawSetting(
			VolatileProcessingPipelineStageConfiguration configuration,
			string                                       name,
			string                                       defaultValue)
		{
			StageConfiguration = configuration;
			Name = name;
			mDefaultValue = mValue = defaultValue;
			mHasValue = false;
			mHasDefaultValue = true;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="VolatileProcessingPipelineStageRawSetting"/> class
		/// without a default value.
		/// </summary>
		/// <param name="configuration">The configuration the setting belongs to.</param>
		/// <param name="name">Name of the setting.</param>
		internal VolatileProcessingPipelineStageRawSetting(
			VolatileProcessingPipelineStageConfiguration configuration,
			string                                       name)
		{
			StageConfiguration = configuration;
			Name = name;
			mDefaultValue = mValue = null;
			mHasValue = false;
			mHasDefaultValue = false;
		}

		/// <summary>
		/// Gets the configuration the setting belongs to.
		/// </summary>
		public VolatileProcessingPipelineStageConfiguration StageConfiguration { get; }

		/// <summary>
		/// Gets the configuration the setting belongs to.
		/// </summary>
		IProcessingPipelineStageConfiguration IProcessingPipelineStageRawSetting.Configuration => StageConfiguration;

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		public string Name { get; } // immutable => no sync necessary

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
		/// </summary>
		public bool HasValue
		{
			get
			{
				lock (StageConfiguration.Sync) return mHasValue;
			}
		}

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		public string Value
		{
			get
			{
				lock (StageConfiguration.Sync)
				{
					if (mHasValue) return mValue;
					return mDefaultValue;
				}
			}

			set
			{
				lock (StageConfiguration.Sync)
				{
					mValue = value;
					mHasValue = true;
				}
			}
		}

		/// <summary>
		/// Gets a value indicating whether the setting has valid default value.
		/// </summary>
		internal bool HasDefaultValue
		{
			get
			{
				lock (StageConfiguration.Sync) return mHasDefaultValue;
			}
		}

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		public string DefaultValue
		{
			get
			{
				lock (StageConfiguration.Sync)
				{
					if (!mHasDefaultValue) throw new InvalidOperationException("The item does not have a default value.");
					return mDefaultValue;
				}
			}

			internal set
			{
				lock (StageConfiguration.Sync)
				{
					mDefaultValue = value;
					mHasDefaultValue = true;
				}
			}
		}

		/// <summary>
		/// Gets the string representation of the setting.
		/// </summary>
		/// <returns>String representation of the setting.</returns>
		public override string ToString()
		{
			lock (StageConfiguration.Sync)
			{
				return HasValue
					       ? $"Name: '{Name}', Value: '{Value}'"
					       : $"Name: '{Name}', Value: <no value> (defaults to: '{DefaultValue}'";
			}
		}
	}

}
