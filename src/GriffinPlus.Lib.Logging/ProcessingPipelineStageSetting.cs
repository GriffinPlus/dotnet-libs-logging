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
	/// A setting in a <see cref="ProcessingPipelineStageConfiguration"/>.
	/// </summary>
	/// <typeparam name="T">Type of the setting value (can be a primitive type or string).</typeparam>
	public class ProcessingPipelineStageSetting<T> : IProcessingPipelineStageSetting<T>, IUntypedProcessingPipelineStageSetting
	{
		internal delegate string ValueToStringConverter(T obj);
		internal delegate T ValueFromStringConverter(string s);

		private readonly ProcessingPipelineStageConfiguration mConfiguration;
		private readonly ValueFromStringConverter mFromStringConverter;
		private readonly ValueToStringConverter mToStringConverter;
		private readonly string mName;
		private readonly T mDefaultValue;
		private T mValue;
		private bool mHasValue;

		/// <summary>
		/// Initializes a new instance of the <see cref="ProcessingPipelineStageSetting{T}"/> class.
		/// </summary>
		/// <param name="configuration">The configuration the setting belongs to.</param>
		/// <param name="valueFromStringConverter">Delegate that converts the setting value to a string.</param>
		/// <param name="valueToStringConverter">Delegate that converts a string to the setting value.</param>
		/// <param name="name">Name of the setting.</param>
		/// <param name="defaultValue">The default value of the setting.</param>
		internal ProcessingPipelineStageSetting(
			ProcessingPipelineStageConfiguration configuration,
			ValueFromStringConverter valueFromStringConverter,
			ValueToStringConverter valueToStringConverter,
			string name,
			T defaultValue)
		{
			mConfiguration = configuration;
			mFromStringConverter = valueFromStringConverter;
			mToStringConverter = valueToStringConverter;
			mName = name;
			mDefaultValue = mValue = defaultValue;
			mHasValue = false;
		}

		/// <summary>
		/// Gets the name of the setting.
		/// </summary>
		public string Name => mName; // immutable => no sync necessary

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
				lock (mConfiguration.Sync) return mHasValue;
			}
		}

		/// <summary>
		/// Gets or sets the value of the setting.
		/// </summary>
		public T Value
		{
			get
			{
				lock (mConfiguration.Sync)
				{
					if (mHasValue) return mValue;
					return mDefaultValue;
				}
			}

			set
			{
				lock (mConfiguration.Sync)
				{
					mValue = value;
					mHasValue = true;
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
			get { return mToStringConverter(Value); }
			set { Value = mFromStringConverter(value); }
		}

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		public T DefaultValue => mDefaultValue; // immutable => no sync necessary

		/// <summary>
		/// Gets the default value of the setting.
		/// </summary>
		object IUntypedProcessingPipelineStageSetting.DefaultValue => mDefaultValue; // immutable => no sync necessary

	}

}
