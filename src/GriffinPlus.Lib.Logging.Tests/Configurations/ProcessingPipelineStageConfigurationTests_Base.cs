///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Reflection;

using Xunit;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
// ReSharper disable PossibleNullReferenceException

namespace GriffinPlus.Lib.Logging
{

	/// <summary>
	/// Common unit tests targeting the <see cref="VolatileProcessingPipelineStageConfiguration"/> and the
	/// <see cref="FileBackedProcessingPipelineConfiguration"/> class.
	/// </summary>
	public abstract class ProcessingPipelineStageConfigurationTests_Base<TConfiguration> where TConfiguration : ProcessingPipelineStageConfigurationBase
	{
		/// <summary>
		/// Creates a new instance of the pipeline stage configuration to test.
		/// </summary>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		/// <returns>The created pipeline stage configuration.</returns>
		protected abstract TConfiguration CreateConfiguration(string name);

		public static IEnumerable<object[]> GetSetting_TestData
		{
			get
			{
				// signed integers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(sbyte), sbyte.MinValue, "-128", (sbyte)0, "0" };
				yield return new object[] { typeof(sbyte), sbyte.MaxValue, "127", (sbyte)0, "0" };
				yield return new object[] { typeof(sbyte), (sbyte)0, "0", sbyte.MinValue, "-128" };
				yield return new object[] { typeof(sbyte), (sbyte)0, "0", sbyte.MaxValue, "127" };

				yield return new object[] { typeof(short), short.MinValue, "-32768", (short)0, "0" };
				yield return new object[] { typeof(short), short.MaxValue, "32767", (short)0, "0" };
				yield return new object[] { typeof(short), (short)0, "0", short.MinValue, "-32768" };
				yield return new object[] { typeof(short), (short)0, "0", short.MaxValue, "32767" };

				yield return new object[] { typeof(int), int.MinValue, "-2147483648", 0, "0" };
				yield return new object[] { typeof(int), int.MaxValue, "2147483647", 0, "0" };
				yield return new object[] { typeof(int), 0, "0", int.MinValue, "-2147483648" };
				yield return new object[] { typeof(int), 0, "0", int.MaxValue, "2147483647" };

				yield return new object[] { typeof(long), long.MinValue, "-9223372036854775808", 0L, "0" };
				yield return new object[] { typeof(long), long.MaxValue, "9223372036854775807", 0L, "0" };
				yield return new object[] { typeof(long), 0L, "0", long.MinValue, "-9223372036854775808" };
				yield return new object[] { typeof(long), 0L, "0", long.MaxValue, "9223372036854775807" };

				// unsigned integers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(byte), byte.MinValue, "0", (byte)(byte.MaxValue / 2), "127" };
				yield return new object[] { typeof(byte), byte.MaxValue, "255", (byte)(byte.MaxValue / 2), "127" };
				yield return new object[] { typeof(byte), (byte)(byte.MaxValue / 2), "127", byte.MinValue, "0" };
				yield return new object[] { typeof(byte), (byte)(byte.MaxValue / 2), "127", byte.MaxValue, "255" };

				yield return new object[] { typeof(ushort), ushort.MinValue, "0", (ushort)(ushort.MaxValue / 2), "32767" };
				yield return new object[] { typeof(ushort), ushort.MaxValue, "65535", (ushort)(ushort.MaxValue / 2), "32767" };
				yield return new object[] { typeof(ushort), (ushort)(ushort.MaxValue / 2), "32767", ushort.MinValue, "0" };
				yield return new object[] { typeof(ushort), (ushort)(ushort.MaxValue / 2), "32767", ushort.MaxValue, "65535" };

				yield return new object[] { typeof(uint), uint.MinValue, "0", uint.MaxValue / 2, "2147483647" };
				yield return new object[] { typeof(uint), uint.MaxValue, "4294967295", uint.MaxValue / 2, "2147483647" };
				yield return new object[] { typeof(uint), uint.MaxValue / 2, "2147483647", uint.MinValue, "0" };
				yield return new object[] { typeof(uint), uint.MaxValue / 2, "2147483647", uint.MaxValue, "4294967295" };

				yield return new object[] { typeof(ulong), ulong.MinValue, "0", ulong.MaxValue / 2, "9223372036854775807" };
				yield return new object[] { typeof(ulong), ulong.MaxValue, "18446744073709551615", ulong.MaxValue / 2, "9223372036854775807" };
				yield return new object[] { typeof(ulong), ulong.MaxValue / 2, "9223372036854775807", ulong.MinValue, "0" };
				yield return new object[] { typeof(ulong), ulong.MaxValue / 2, "9223372036854775807", ulong.MaxValue, "18446744073709551615" };

				// floating point numbers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(float), float.NegativeInfinity, "-Infinity", 0.0f, "0" };
				yield return new object[] { typeof(float), float.PositiveInfinity, "Infinity", 0.0f, "0" };
				yield return new object[] { typeof(float), 0.0f, "0", float.NegativeInfinity, "-Infinity" };
				yield return new object[] { typeof(float), 0.0f, "0", float.PositiveInfinity, "Infinity" };

				yield return new object[] { typeof(double), double.NegativeInfinity, "-Infinity", 0.0, "0" };
				yield return new object[] { typeof(double), double.PositiveInfinity, "Infinity", 0.0, "0" };
				yield return new object[] { typeof(double), 0.0, "0", double.NegativeInfinity, "-Infinity" };
				yield return new object[] { typeof(double), 0.0, "0", double.PositiveInfinity, "Infinity" };

				// decimal numbers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(decimal), decimal.MinValue, "-79228162514264337593543950335", 0.0m, "0.0" };
				yield return new object[] { typeof(decimal), decimal.MaxValue, "79228162514264337593543950335", 0.0m, "0.0" };
				yield return new object[] { typeof(decimal), 0.0m, "0.0", decimal.MinValue, "-79228162514264337593543950335" };
				yield return new object[] { typeof(decimal), 0.0m, "0.0", decimal.MaxValue, "79228162514264337593543950335" };

				// strings
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(string), "Value1", "Value1", "Value2", "Value2" };

				// enumerations
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(DateTimeKind), DateTimeKind.Utc, "Utc", DateTimeKind.Local, "Local" };
			}
		}

		/// <summary>
		/// Tests getting/creating a setting with a specific default value, then setting the value to overwrite the default value.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <param name="defaultValueAsString">String representation of the default value.</param>
		/// <param name="value">Value to set after creating the setting.</param>
		/// <param name="valueAsString">String representation of the setting value.</param>
		[Theory]
		[MemberData(nameof(GetSetting_TestData))]
		public void GetSetting(
			Type   type,
			object defaultValue,
			string defaultValueAsString,
			object value,
			string valueAsString)
		{
			var method = typeof(ProcessingPipelineStageConfigurationBase).GetMethod("GetSetting").MakeGenericMethod(type);
			var configuration = CreateConfiguration("Stage");
			var setting11 = method.Invoke(configuration, new[] { "Setting1", defaultValue }) as IUntypedProcessingPipelineStageSetting;
			var setting21 = method.Invoke(configuration, new[] { "Setting2", defaultValue }) as IUntypedProcessingPipelineStageSetting;

			// test untyped interface to the setting (IUntypedProcessingPipelineStageSetting)
			GetSetting_UntypedInterface(setting11, defaultValue, defaultValueAsString, value, valueAsString);

			// test typed interface to the setting (IProcessingPipelineStageSetting<T>)
			var typedTestMethod = typeof(ProcessingPipelineStageConfigurationTests_Base<TConfiguration>)
				.GetMethod(nameof(GetSetting_TypedInterface), BindingFlags.NonPublic | BindingFlags.Static)
				.MakeGenericMethod(type);
			typedTestMethod.Invoke(this, new[] { setting21, defaultValue, defaultValueAsString, value, valueAsString });

			// test getting the same setting once again (should succeed, if default value is the same)
			var setting12 = method.Invoke(configuration, new[] { "Setting1", defaultValue }) as IUntypedProcessingPipelineStageSetting;
			var setting22 = method.Invoke(configuration, new[] { "Setting2", defaultValue }) as IUntypedProcessingPipelineStageSetting;
			Assert.Same(setting11, setting12);
			Assert.Same(setting21, setting22);

			// test whether getting the same setting with different default values fails as expected
			Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(configuration, new[] { "Setting1", value })).InnerException);
			Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(configuration, new[] { "Setting2", value })).InnerException);
		}

		private static void GetSetting_UntypedInterface(
			IUntypedProcessingPipelineStageSetting setting,
			object                                 defaultValue,
			string                                 defaultValueAsString,
			object                                 value,
			string                                 valueAsString)
		{
			Assert.False(setting.HasValue);
			Assert.Equal(defaultValue, setting.DefaultValue);
			Assert.Equal(defaultValue, setting.Value);
			Assert.Equal(defaultValueAsString, setting.ValueAsString);

			setting.Value = value;

			Assert.True(setting.HasValue);
			Assert.Equal(defaultValue, setting.DefaultValue);
			Assert.Equal(value, setting.Value);
			Assert.Equal(valueAsString, setting.ValueAsString);
		}

		private static void GetSetting_TypedInterface<T>(
			IProcessingPipelineStageSetting<T> setting,
			object                             defaultValue,
			string                             defaultValueAsString,
			object                             value,
			string                             valueAsString)
		{
			Assert.False(setting.HasValue);
			Assert.Equal(defaultValue, setting.DefaultValue);
			Assert.Equal(defaultValue, setting.Value);
			Assert.Equal(defaultValueAsString, setting.ValueAsString);

			setting.Value = (T)value;

			Assert.True(setting.HasValue);
			Assert.Equal(defaultValue, setting.DefaultValue);
			Assert.Equal(value, setting.Value);
			Assert.Equal(valueAsString, setting.ValueAsString);
		}
	}

}
