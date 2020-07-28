///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
//
// Copyright 2020 Sascha Falk <sascha@falk-online.eu>
//
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance
// with the License. You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed
// on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// the specific language governing permissions and limitations under the License.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging
{
	/// <summary>
	/// A setting in a <see cref="FileBackedProcessingPipelineStageConfiguration"/>.
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	public class FileBackedProcessingPipelineStageSetting<T> : IProcessingPipelineStageSetting<T>, IUntypedProcessingPipelineStageSetting
	{
		private readonly FileBackedProcessingPipelineStageRawSetting mRawSetting;
		private readonly ProcessingPipelineStageConfigurationBase.ValueFromStringConverter<T> mFromStringConverter;
		private readonly ProcessingPipelineStageConfigurationBase.ValueToStringConverter<T> mToStringConverter;
		private string mCachedRawValue;
		private T mCachedValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="FileBackedProcessingPipelineStageSetting{T}"/> class.
		/// </summary>
		/// <param name="rawSetting">The corresponding raw setting in the configuration.</param>
		/// <param name="valueFromStringConverter">Delegate that converts the setting value to a string.</param>
		/// <param name="valueToStringConverter">Delegate that converts a string to the setting value.</param>
		internal FileBackedProcessingPipelineStageSetting(
			FileBackedProcessingPipelineStageRawSetting rawSetting,
			ProcessingPipelineStageConfigurationBase.ValueFromStringConverter<T> valueFromStringConverter,
			ProcessingPipelineStageConfigurationBase.ValueToStringConverter<T> valueToStringConverter)
		{
			mRawSetting = rawSetting;
			mFromStringConverter = valueFromStringConverter;
			mToStringConverter = valueToStringConverter;
		}

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		public string Name => mRawSetting.Name;

		/// <summary>
		/// Gets the type of the value.
		/// </summary>
		public Type ValueType => typeof(T);

		/// <summary>
		/// Gets a value indicating whether the setting has valid value (true) or just its default value (false).
		/// </summary>
		public bool HasValue
		{
			get
			{
				lock (mRawSetting.StageConfiguration.Sync) return mRawSetting.HasValue;
			}
		}

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		public T Value
		{
			get
			{
				lock (mRawSetting.StageConfiguration.Sync)
				{
					string rawValue = mRawSetting.Value;
					if (mCachedRawValue == rawValue) return mCachedValue;
					mCachedValue = mFromStringConverter(mRawSetting.Value);
					mCachedRawValue = rawValue;
					return mCachedValue;
				}
			}

			set
			{
				lock (mRawSetting.StageConfiguration.Sync)
				{
					mRawSetting.Value = mToStringConverter(value);
				}
			}
		}

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		object IUntypedProcessingPipelineStageSetting.Value
		{
			get => Value;
			set => Value = (T)value;
		}

		/// <summary>
		/// Gets or sets the value of the setting as a string (for serialization purposes).
		/// </summary>
		public string ValueAsString
		{
			get => mRawSetting.Value;
			set => mRawSetting.Value = value;
		}

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		public T DefaultValue => mFromStringConverter(mRawSetting.DefaultValue);

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		object IUntypedProcessingPipelineStageSetting.DefaultValue => mFromStringConverter(mRawSetting.DefaultValue);

		/// <summary>
		/// Gets the string representation of the setting.
		/// </summary>
		/// <returns>String representation of the setting.</returns>
		public override string ToString()
		{
			return mRawSetting.ToString();
		}
	}

}
