///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
	public abstract class ProcessingPipelineStageConfigurationTests_Base<TStageConfiguration> where TStageConfiguration : ProcessingPipelineStageConfigurationBase
	{
		[Flags]
		public enum ColorFlags
		{
			None        = 0x00,
			Red         = 0x01,
			Green       = 0x02,
			Blue        = 0x04,
			Cyan        = 0x08,
			Magenta     = 0x10,
			Yellow      = 0x20,
			RedAndGreen = Red | Green,
			All         = Red | Green | Blue | Cyan | Magenta | Yellow
		}

		/// <summary>
		/// Creates a new instance of the pipeline stage configuration to test.
		/// </summary>
		/// <param name="name">Name of the pipeline stage the configuration belongs to.</param>
		/// <param name="stageConfiguration">Receives the stage configuration to test.</param>
		/// <returns>The created configuration containing the stage configuration (must be disposed at the end of the test).</returns>
		protected abstract ILogConfiguration CreateConfiguration(string name, out TStageConfiguration stageConfiguration);

		public static IEnumerable<object[]> SettingTypeAndOneValue_TestData
		{
			get
			{
				// signed integers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(sbyte), sbyte.MinValue, "-128" };
				yield return new object[] { typeof(sbyte), sbyte.MaxValue, "127" };
				yield return new object[] { typeof(sbyte), (sbyte)0, "0" };

				yield return new object[] { typeof(short), short.MinValue, "-32768" };
				yield return new object[] { typeof(short), short.MaxValue, "32767" };
				yield return new object[] { typeof(short), (short)0, "0" };

				yield return new object[] { typeof(int), int.MinValue, "-2147483648" };
				yield return new object[] { typeof(int), int.MaxValue, "2147483647" };
				yield return new object[] { typeof(int), 0, "0" };

				yield return new object[] { typeof(long), long.MinValue, "-9223372036854775808" };
				yield return new object[] { typeof(long), long.MaxValue, "9223372036854775807" };
				yield return new object[] { typeof(long), 0L, "0" };

				// unsigned integers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(byte), byte.MinValue, "0" };
				yield return new object[] { typeof(byte), byte.MaxValue, "255" };
				yield return new object[] { typeof(byte), (byte)(byte.MaxValue / 2), "127" };

				yield return new object[] { typeof(ushort), ushort.MinValue, "0" };
				yield return new object[] { typeof(ushort), ushort.MaxValue, "65535" };
				yield return new object[] { typeof(ushort), (ushort)(ushort.MaxValue / 2), "32767" };

				yield return new object[] { typeof(uint), uint.MinValue, "0" };
				yield return new object[] { typeof(uint), uint.MaxValue, "4294967295" };
				yield return new object[] { typeof(uint), uint.MaxValue / 2, "2147483647" };

				yield return new object[] { typeof(ulong), ulong.MinValue, "0" };
				yield return new object[] { typeof(ulong), ulong.MaxValue, "18446744073709551615" };
				yield return new object[] { typeof(ulong), ulong.MaxValue / 2, "9223372036854775807" };

				// floating point numbers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(float), float.NegativeInfinity, "-Infinity" };
				yield return new object[] { typeof(float), float.PositiveInfinity, "Infinity" };
				yield return new object[] { typeof(float), 0.0f, "0" };

				yield return new object[] { typeof(double), double.NegativeInfinity, "-Infinity" };
				yield return new object[] { typeof(double), double.PositiveInfinity, "Infinity" };
				yield return new object[] { typeof(double), 0.0, "0" };

				// decimal numbers
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(decimal), decimal.MinValue, "-79228162514264337593543950335" };
				yield return new object[] { typeof(decimal), decimal.MaxValue, "79228162514264337593543950335" };
				yield return new object[] { typeof(decimal), 0.0m, "0.0" };

				// strings
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(string), "Value1", "Value1" };

				// enumerations
				// ----------------------------------------------------------------------------------------------------------------
				yield return new object[] { typeof(DateTimeKind), DateTimeKind.Utc, "Utc" };
				yield return new object[] { typeof(ColorFlags), ColorFlags.None, "None" };
				yield return new object[] { typeof(ColorFlags), ColorFlags.Red, "Red" };
				yield return new object[] { typeof(ColorFlags), ColorFlags.Red | ColorFlags.Green, "RedAndGreen" };
				yield return new object[] { typeof(ColorFlags), ColorFlags.Red | ColorFlags.Green | ColorFlags.Blue, "RedAndGreen, Blue" };
			}
		}

		public static IEnumerable<object[]> SettingTypeAndTwoValues_TestData
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
				yield return new object[] { typeof(ColorFlags), ColorFlags.None, "None", ColorFlags.Red, "Red" };
				yield return new object[] { typeof(ColorFlags), ColorFlags.Red, "Red", ColorFlags.None, "None" };
				yield return new object[]
				{
					typeof(ColorFlags),
					ColorFlags.Red | ColorFlags.Green | ColorFlags.Blue,
					"RedAndGreen, Blue",
					ColorFlags.Red | ColorFlags.Green,
					"RedAndGreen"
				};
			}
		}

		#region RegisterSetting()

		/// <summary>
		/// Tests registering a setting with a specific default value.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <param name="defaultValueAsString">String representation of the default value.</param>
		[Theory]
		[MemberData(nameof(SettingTypeAndOneValue_TestData))]
		public void RegisterSetting(
			Type   type,
			object defaultValue,
			string defaultValueAsString)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var method = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.RegisterSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);

				var setting1 = method.Invoke(configuration, new[] { "Setting", defaultValue }) as IUntypedProcessingPipelineStageSetting;

				// test untyped interface to the setting (IUntypedProcessingPipelineStageSetting)
				RegisterSetting_UntypedInterface(setting1, defaultValue, defaultValueAsString);

				// test typed interface to the setting (IProcessingPipelineStageSetting<T>)
				var typedTestMethod = typeof(ProcessingPipelineStageConfigurationTests_Base<TStageConfiguration>)
					.GetMethod(nameof(RegisterSetting_TypedInterface), BindingFlags.NonPublic | BindingFlags.Static)
					.MakeGenericMethod(type);
				typedTestMethod.Invoke(this, new[] { setting1, defaultValue, defaultValueAsString });

				// test getting the same setting once again (should succeed, if default value is the same)
				var setting2 = method.Invoke(configuration, new[] { "Setting", defaultValue }) as IUntypedProcessingPipelineStageSetting;
				Assert.Same(setting1, setting2);
			}
		}

		private static void RegisterSetting_UntypedInterface(
			IUntypedProcessingPipelineStageSetting setting,
			object                                 defaultValue,
			string                                 defaultValueAsString)
		{
			Assert.True(setting.HasDefaultValue);
			Assert.False(setting.HasValue);
			Assert.Equal(defaultValue, setting.DefaultValue);
			Assert.Equal(defaultValue, setting.Value);
			Assert.Equal(defaultValueAsString, setting.ValueAsString);
		}

		private static void RegisterSetting_TypedInterface<T>(
			IProcessingPipelineStageSetting<T> setting,
			object                             defaultValue,
			string                             defaultValueAsString)
		{
			Assert.True(setting.HasDefaultValue);
			Assert.False(setting.HasValue);
			Assert.Equal(defaultValue, setting.DefaultValue);
			Assert.Equal(defaultValue, setting.Value);
			Assert.Equal(defaultValueAsString, setting.ValueAsString);
		}

		/// <summary>
		/// Tests registering a setting with a specific default value, then registering it once again, but with different default values.
		/// Should throw an exception.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="defaultValue1">Default value of the setting.</param>
		/// <param name="defaultValueAsString1">String representation of the default value.</param>
		/// <param name="defaultValue2">Other default value that should cause registering to fail.</param>
		/// <param name="valueAsString2">String representation of the other default value.</param>
		[Theory]
		[MemberData(nameof(SettingTypeAndTwoValues_TestData))]
		public void RegisterSetting_RegisterTwiceWithDifferentDefaults(
			Type   type,
			object defaultValue1,
			string defaultValueAsString1,
			object defaultValue2,
			string valueAsString2)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var method = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.RegisterSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);
				method.Invoke(configuration, new[] { "Setting", defaultValue1 });
				Assert.IsType<ArgumentException>(Assert.Throws<TargetInvocationException>(() => method.Invoke(configuration, new[] { "Setting", defaultValue2 })).InnerException);
			}
		}

		#endregion

		#region RegisterSetting() followed by setting the value

		/// <summary>
		/// Tests registering a setting with a specific default value using <see cref="ProcessingPipelineStageConfigurationBase.RegisterSetting{T}"/>
		/// followed by setting the value to some other value using <see cref="IUntypedProcessingPipelineStageSetting.Value"/>
		/// and <see cref="IProcessingPipelineStageSetting{T}.Value"/>.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <param name="defaultValueAsString">String representation of the default value.</param>
		/// <param name="value">Value to set after creating the setting.</param>
		/// <param name="valueAsString">String representation of the setting value.</param>
		[Theory]
		[MemberData(nameof(SettingTypeAndTwoValues_TestData))]
		public void RegisterSettingAndSetValue(
			Type   type,
			object defaultValue,
			string defaultValueAsString,
			object value,
			string valueAsString)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var method = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.RegisterSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);
				var setting11 = method.Invoke(configuration, new[] { "Setting1", defaultValue }) as IUntypedProcessingPipelineStageSetting;
				var setting21 = method.Invoke(configuration, new[] { "Setting2", defaultValue }) as IUntypedProcessingPipelineStageSetting;

				// test untyped interface to the setting (IUntypedProcessingPipelineStageSetting)
				RegisterSettingAndSetValue_UntypedInterface(setting11, defaultValue, defaultValueAsString, value, valueAsString);

				// test typed interface to the setting (IProcessingPipelineStageSetting<T>)
				var typedTestMethod = typeof(ProcessingPipelineStageConfigurationTests_Base<TStageConfiguration>)
					.GetMethod(nameof(RegisterSettingAndSetValue_TypedInterface), BindingFlags.NonPublic | BindingFlags.Static)
					.MakeGenericMethod(type);
				typedTestMethod.Invoke(this, new[] { setting21, defaultValue, defaultValueAsString, value, valueAsString });
			}
		}

		private static void RegisterSettingAndSetValue_UntypedInterface(
			IUntypedProcessingPipelineStageSetting setting,
			object                                 defaultValue,
			string                                 defaultValueAsString,
			object                                 value,
			string                                 valueAsString)
		{
			Assert.True(setting.HasDefaultValue);
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

		private static void RegisterSettingAndSetValue_TypedInterface<T>(
			IProcessingPipelineStageSetting<T> setting,
			object                             defaultValue,
			string                             defaultValueAsString,
			object                             value,
			string                             valueAsString)
		{
			Assert.True(setting.HasDefaultValue);
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

		#endregion

		#region RegisterSetting() followed by GetSetting()

		/// <summary>
		/// Tests registering a setting with a specific default value using <see cref="ProcessingPipelineStageConfigurationBase.RegisterSetting{T}"/>
		/// followed by getting the setting with <see cref="ProcessingPipelineStageConfigurationBase.GetSetting{T}"/>.
		/// The same setting should be returned.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <param name="defaultValueAsString">String representation of the default value.</param>
		[Theory]
		[MemberData(nameof(SettingTypeAndOneValue_TestData))]
		public void RegisterSettingFollowedByGetSetting(
			Type   type,
			object defaultValue,
			string defaultValueAsString)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var registerSettingsMethod = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.RegisterSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);
				var getSettingMethod = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.GetSetting) && x.GetParameters().Length == 1)
					.MakeGenericMethod(type);
				var registeredSetting = registerSettingsMethod.Invoke(configuration, new[] { "Setting", defaultValue }) as IUntypedProcessingPipelineStageSetting;
				var setting = getSettingMethod.Invoke(configuration, new object[] { "Setting" }) as IUntypedProcessingPipelineStageSetting;
				Assert.Same(registeredSetting, setting);
			}
		}

		#endregion

		#region RegisterSetting() followed by SetSetting()

		/// <summary>
		/// Tests registering a setting with a specific default value using <see cref="ProcessingPipelineStageConfigurationBase.RegisterSetting{T}(string,T)"/>
		/// followed by setting the value to some other value using <see cref="ProcessingPipelineStageConfigurationBase.SetSetting{T}(string,T)"/>.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="defaultValue">Default value of the setting.</param>
		/// <param name="defaultValueAsString">String representation of the default value.</param>
		/// <param name="value">Value to set after creating the setting.</param>
		/// <param name="valueAsString">String representation of the setting value.</param>
		[Theory]
		[MemberData(nameof(SettingTypeAndTwoValues_TestData))]
		public void RegisterSettingAndSetSetting(
			Type   type,
			object defaultValue,
			string defaultValueAsString,
			object value,
			string valueAsString)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var registerMethod = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.RegisterSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);
				var setMethod = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.SetSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);

				// register setting with default value
				var registeredSetting = registerMethod.Invoke(configuration, new[] { "Setting", defaultValue }) as IUntypedProcessingPipelineStageSetting;
				Assert.True(registeredSetting.HasDefaultValue);
				Assert.False(registeredSetting.HasValue);
				Assert.Equal(defaultValue, registeredSetting.DefaultValue);
				Assert.Equal(defaultValue, registeredSetting.Value);
				Assert.Equal(defaultValueAsString, registeredSetting.ValueAsString);

				// set setting
				var setSetting = setMethod.Invoke(configuration, new[] { "Setting", value }) as IUntypedProcessingPipelineStageSetting;
				Assert.Same(registeredSetting, setSetting);
				Assert.True(setSetting.HasDefaultValue);
				Assert.True(setSetting.HasValue);
				Assert.Equal(defaultValue, setSetting.DefaultValue);
				Assert.Equal(value, setSetting.Value);
				Assert.Equal(valueAsString, setSetting.ValueAsString);
			}
		}

		#endregion

		#region GetSetting() - Setting does not exist

		public static IEnumerable<object[]> GetSettingTestData_SettingDoesNotExist
		{
			get
			{
				// signed integers
				yield return new object[] { typeof(sbyte) };
				yield return new object[] { typeof(short) };
				yield return new object[] { typeof(int) };
				yield return new object[] { typeof(long) };

				// unsigned integers
				yield return new object[] { typeof(byte) };
				yield return new object[] { typeof(ushort) };
				yield return new object[] { typeof(uint) };
				yield return new object[] { typeof(ulong) };

				// floating point numbers
				yield return new object[] { typeof(float) };
				yield return new object[] { typeof(double) };

				// other common types
				yield return new object[] { typeof(decimal) };
				yield return new object[] { typeof(string) };
				yield return new object[] { typeof(DateTimeKind) };
			}
		}

		/// <summary>
		/// Tests getting a setting that does not exist.
		/// <see cref="ProcessingPipelineStageConfigurationBase.GetSetting{T}(string)"/> should return <c>null</c>.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		[Theory]
		[MemberData(nameof(GetSettingTestData_SettingDoesNotExist))]
		public void GetSetting_SettingDoesNotExist(Type type)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var method = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.GetSetting) && x.GetParameters().Length == 1)
					.MakeGenericMethod(type);
				var setting = method.Invoke(configuration, new object[] { "Setting" }) as IUntypedProcessingPipelineStageSetting;
				Assert.Null(setting);
			}
		}

		#endregion

		#region SetSetting()

		/// <summary>
		/// Tests setting a setting using <see cref="ProcessingPipelineStageConfigurationBase.SetSetting{T}(string,T)"/>.
		/// The setting does not exist at start, so there is no default value associated with the setting.
		/// </summary>
		/// <param name="type">Type of setting value.</param>
		/// <param name="value">Value of the setting.</param>
		/// <param name="valueAsString">String representation of the value.</param>
		[Theory]
		[MemberData(nameof(SettingTypeAndOneValue_TestData))]
		public void SetSetting(
			Type   type,
			object value,
			string valueAsString)
		{
			using (CreateConfiguration("Stage", out var configuration))
			{
				var method = typeof(ProcessingPipelineStageConfigurationBase)
					.GetMethods()
					.Single(x => x.Name == nameof(ProcessingPipelineStageConfigurationBase.SetSetting) && x.GetParameters().Length == 2)
					.MakeGenericMethod(type);
				var setting1 = method.Invoke(configuration, new[] { "Setting1", value }) as IUntypedProcessingPipelineStageSetting;
				var setting2 = method.Invoke(configuration, new[] { "Setting2", value }) as IUntypedProcessingPipelineStageSetting;

				// test untyped interface to the setting (IUntypedProcessingPipelineStageSetting)
				SetSetting_UntypedInterface(setting1, value, valueAsString);

				// test typed interface to the setting (IProcessingPipelineStageSetting<T>)
				var typedTestMethod = typeof(ProcessingPipelineStageConfigurationTests_Base<TStageConfiguration>)
					.GetMethod(nameof(SetSetting_TypedInterface), BindingFlags.NonPublic | BindingFlags.Static)
					.MakeGenericMethod(type);
				typedTestMethod.Invoke(this, new[] { setting2, value, valueAsString });
			}
		}

		private static void SetSetting_UntypedInterface(
			IUntypedProcessingPipelineStageSetting setting,
			object                                 value,
			string                                 valueAsString)
		{
			Assert.False(setting.HasDefaultValue);
			Assert.Throws<InvalidOperationException>(() => setting.DefaultValue);

			Assert.True(setting.HasValue);
			Assert.Equal(value, setting.Value);
			Assert.Equal(valueAsString, setting.ValueAsString);
		}

		private static void SetSetting_TypedInterface<T>(
			IProcessingPipelineStageSetting<T> setting,
			object                             value,
			string                             valueAsString)
		{
			Assert.False(setting.HasDefaultValue);
			Assert.Throws<InvalidOperationException>(() => setting.DefaultValue);

			Assert.True(setting.HasValue);
			Assert.Equal(value, setting.Value);
			Assert.Equal(valueAsString, setting.ValueAsString);
		}

		#endregion
	}

}
