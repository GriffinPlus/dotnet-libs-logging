///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Text.Json;

using Xunit;

using static GriffinPlus.Lib.Logging.Elasticsearch.Tests.ElasticsearchPipelineStageTestHelpers;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// Unit tests targeting the <see cref="ElasticsearchPipelineStage"/> class.
/// The tests do not require an Elasticsearch server.
/// </summary>
[Trait("Category", "Unit")]
public class ElasticsearchPipelineStageTests
{
	#region Create<ElasticsearchPipelineStage>()

	/// <summary>
	/// Tests whether <see cref="ProcessingPipelineStage.Create{TStage}"/> creates an uninitialized,
	/// non-operational stage with the specified name and without following stages.
	/// </summary>
	[Fact]
	public void Create_WhenCalled_InitializesBaseProperties()
	{
		// Arrange
		const string name = "Elasticsearch";

		// Act
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>(name, null);

		// Assert
		Assert.Equal(name, stage.Name);
		Assert.False(stage.IsInitialized);
		Assert.False(stage.IsDefaultStage);
		Assert.False(stage.IsOperational);
		Assert.Empty(stage.NextStages);
	}

	/// <summary>
	/// Test data for <see cref="Create_WhenCalled_SetsSettingDefault"/>:
	/// scenario name, setting name, expected default value
	/// (<see cref="Uri"/> arrays are represented by their configuration string, see <see cref="UriArrayConverter"/>).
	/// </summary>
	public static TheoryData<string, string, object> Create_SettingDefaults_Data => new()
	{
		// a single local endpoint
		{
			"ApiBaseUrls",
			"Server.ApiBaseUrls",
			"http://127.0.0.1:9200/"
		},
		// password based authentication schemes
		{
			"AuthenticationSchemes",
			"Server.Authentication.Schemes",
			AuthenticationScheme.PasswordBased
		},
		// no custom credentials
		{
			"Username",
			"Server.Authentication.Username",
			""
		},
		{
			"Password",
			"Server.Authentication.Password",
			""
		},
		{
			"Domain",
			"Server.Authentication.Domain",
			""
		},
		// bulk request limits (message count 0 = unlimited)
		{
			"BulkRequestMaxConcurrencyLevel",
			"Server.BulkRequest.MaxConcurrencyLevel",
			5
		},
		{
			"BulkRequestMaxMessageCount",
			"Server.BulkRequest.MaxMessageCount",
			0
		},
		{
			"BulkRequestMaxSize",
			"Server.BulkRequest.MaxSize",
			5 * 1024 * 1024
		},
		// request timeout (in seconds)
		{
			"RequestTimeout",
			"Server.RequestTimeout",
			100
		},
		// default index
		{
			"IndexName",
			"Server.IndexName",
			"logs"
		},
		// no organization information
		{
			"OrganizationId",
			"Data.Organization.Id",
			""
		},
		{
			"OrganizationName",
			"Data.Organization.Name",
			""
		},
		// size of the send queue
		{
			"SendQueueSize",
			"Stage.SendQueueSize",
			50000
		}
	};

	/// <summary>
	/// Tests whether <see cref="ProcessingPipelineStage.Create{TStage}"/> registers the specified setting with the
	/// expected default value and whether the corresponding stage property reflects that value.
	/// </summary>
	/// <param name="scenario">Name of the scenario (for test output only).</param>
	/// <param name="settingName">Name of the setting to check.</param>
	/// <param name="expectedDefault">Expected default value of the setting.</param>
	[Theory]
	[MemberData(nameof(Create_SettingDefaults_Data))]
	public void Create_WhenCalled_SetsSettingDefault(string scenario, string settingName, object expectedDefault)
	{
		// Arrange
		// (nothing to do)

		// Act
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);

		// Assert
		GetSettingState(stage, settingName, out object defaultValue, out object value, out object propertyValue);
		Assert.Equal(expectedDefault, defaultValue);
		Assert.Equal(expectedDefault, value);
		Assert.Equal(expectedDefault, propertyValue);
	}

	#endregion

	#region ApiBaseUrls

	/// <summary>
	/// Tests whether setting <see cref="ElasticsearchPipelineStage.ApiBaseUrls"/> to <see langword="null"/> throws an
	/// <see cref="ArgumentNullException"/> and leaves the configured endpoints unchanged.
	/// </summary>
	[Fact]
	public void ApiBaseUrls_WhenSetToNull_ThrowsArgumentNullException()
	{
		// Arrange
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		Uri[] expectedUrls = [new("http://127.0.0.1:9200/")];

		// Act
		var ex = Assert.Throws<ArgumentNullException>(() => stage.ApiBaseUrls = null);

		// Assert
		Assert.Equal("value", ex.ParamName);
		Assert.Equal(expectedUrls, stage.ApiBaseUrls);
	}

	#endregion

	#region GetThrottlingDelay(TimeSpan?, ref int)

	/// <summary>
	/// Test data for <see cref="GetThrottlingDelay_WhenCalled_ReturnsDelayAndUpdatesBackoff"/>
	/// (scenario, delay specified by the server, current backoff, expected delay, expected next backoff).
	/// </summary>
	public static TheoryData<string, TimeSpan?, int, int, int> GetThrottlingDelay_Data => new()
	{
		// first throttling without delay specified by the server => initial backoff
		{
			"first, no retry-after",
			null,
			0,
			1000,
			1000
		},
		// subsequent throttling => backoff doubles
		{
			"second, no retry-after",
			null,
			1000,
			2000,
			2000
		},
		// backoff is limited to its maximum
		{
			"backoff limited",
			null,
			16000,
			30000,
			30000
		},
		// backoff stays at its maximum
		{
			"backoff at maximum",
			null,
			30000,
			30000,
			30000
		},
		// delay specified by the server takes precedence (backoff is updated nevertheless)
		{
			"retry-after",
			TimeSpan.FromSeconds(5),
			0,
			5000,
			1000
		},
		// delay specified by the server may exceed the backoff maximum
		{
			"retry-after above backoff maximum",
			TimeSpan.FromSeconds(60),
			0,
			60000,
			1000
		},
		// unreasonable delay specified by the server is limited
		{
			"retry-after limited",
			TimeSpan.FromHours(1),
			0,
			300000,
			1000
		},
		// zero delay specified by the server
		{
			"retry-after zero",
			TimeSpan.Zero,
			0,
			0,
			1000
		}
	};

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.GetThrottlingDelay"/> returns the delay specified by the
	/// server (limited to a reasonable maximum), if any, otherwise an exponentially increasing backoff time.
	/// </summary>
	/// <param name="scenario">Name of the test scenario.</param>
	/// <param name="retryAfter">Delay specified by the server.</param>
	/// <param name="backoff">Current backoff time (in ms).</param>
	/// <param name="expectedDelay">Expected delay (in ms).</param>
	/// <param name="expectedBackoff">Expected next backoff time (in ms).</param>
	[Theory]
	[MemberData(nameof(GetThrottlingDelay_Data))]
	public void GetThrottlingDelay_WhenCalled_ReturnsDelayAndUpdatesBackoff(
		string    scenario,
		TimeSpan? retryAfter,
		int       backoff,
		int       expectedDelay,
		int       expectedBackoff)
	{
		// Arrange
		_ = scenario;

		// Act
		int delay = ElasticsearchPipelineStage.GetThrottlingDelay(retryAfter, ref backoff);

		// Assert
		Assert.Equal(expectedDelay, delay);
		Assert.Equal(expectedBackoff, backoff);
	}

	#endregion

	#region EnqueueMessage(LocalLogMessage)

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.EnqueueMessage"/> enqueues messages (adding a reference) as
	/// long as there is space in the processing queue and discards and counts messages (without adding a reference)
	/// when the queue is full.
	/// </summary>
	[Fact]
	public void EnqueueMessage_WhenQueueIsFull_DiscardsAndCountsMessages()
	{
		// Arrange
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.SendQueueSize = 2;
		stage.UpdateProcessingQueueSize();
		LocalLogMessage[] messages = [CreateMessage("a"), CreateMessage("b"), CreateMessage("c"), CreateMessage("d")];

		try
		{
			// Act
			bool[] enqueued = messages.Select(stage.EnqueueMessage).ToArray();

			// Assert
			// the first two messages are enqueued, the others are discarded
			Assert.Equal([true, true, false, false], enqueued);

			// only the enqueued messages are referenced by the stage
			Assert.Equal(3, messages[0].RefCount);
			Assert.Equal(3, messages[1].RefCount);
			Assert.Equal(2, messages[2].RefCount);
			Assert.Equal(2, messages[3].RefCount);

			// the discarded messages are counted
			Assert.Equal(2, stage.TakeDiscardedMessageCount());
		}
		finally
		{
			// the stage is not initialized, so it must not be shut down
			// => release the references held by the processing queue manually
			messages[0].Release();
			messages[1].Release();
			foreach (LocalLogMessage message in messages) message.Release();
		}
	}

	#endregion

	#region TakeDiscardedMessageCount()

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.TakeDiscardedMessageCount"/> resets the counter of
	/// discarded messages, so every discarded message is reported only once.
	/// </summary>
	[Fact]
	public void TakeDiscardedMessageCount_WhenCalled_ResetsCounter()
	{
		// Arrange
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.SendQueueSize = 1;
		stage.UpdateProcessingQueueSize();
		LocalLogMessage[] messages = [CreateMessage("a"), CreateMessage("b")];

		try
		{
			Assert.True(stage.EnqueueMessage(messages[0]));
			Assert.False(stage.EnqueueMessage(messages[1]));

			// Act
			long first = stage.TakeDiscardedMessageCount();
			long second = stage.TakeDiscardedMessageCount();

			// Assert
			Assert.Equal(1, first);
			Assert.Equal(0, second);
		}
		finally
		{
			// the stage is not initialized, so it must not be shut down
			// => release the reference held by the processing queue manually
			messages[0].Release();
			foreach (LocalLogMessage message in messages) message.Release();
		}
	}

	#endregion

	#region ReloadConfigurationIfNecessary()

	/// <summary>
	/// Test data for <see cref="ReloadConfigurationIfNecessary_WhenCalled_AppliesRequestTimeout"/>:
	/// scenario, configured request timeout (in seconds) and expected timeout of the HTTP client.
	/// </summary>
	public static TheoryData<string, int, TimeSpan> ReloadConfiguration_RequestTimeout_Data => new()
	{
		// a valid timeout is applied as configured
		{
			"Configured",
			30,
			TimeSpan.FromSeconds(30)
		},
		// zero selects the default
		{
			"Zero",
			0,
			TimeSpan.FromSeconds(100)
		},
		// a negative timeout selects the default
		{
			"Negative",
			-1,
			TimeSpan.FromSeconds(100)
		}
	};

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.ReloadConfigurationIfNecessary"/> applies the configured
	/// <see cref="ElasticsearchPipelineStage.RequestTimeout"/> to the HTTP client and falls back to the default of
	/// 100 seconds, if the configured timeout is less than or equal to 0, without changing the configured value.
	/// </summary>
	/// <param name="scenario">Name of the test scenario.</param>
	/// <param name="requestTimeout">Configured request timeout (in seconds).</param>
	/// <param name="expectedTimeout">Expected timeout of the HTTP client.</param>
	[Theory]
	[MemberData(nameof(ReloadConfiguration_RequestTimeout_Data))]
	public void ReloadConfigurationIfNecessary_WhenCalled_AppliesRequestTimeout(
		string   scenario,
		int      requestTimeout,
		TimeSpan expectedTimeout)
	{
		// Arrange
		_ = scenario;
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.RequestTimeout = requestTimeout;

		// Act
		stage.ReloadConfigurationIfNecessary();

		// Assert
		Assert.Equal(expectedTimeout, stage.HttpClientTimeout);
		Assert.Equal(requestTimeout, stage.RequestTimeout);
	}

	#endregion

	#region WriteJsonMessage_ECS110(Utf8JsonWriter, LocalLogMessage)

	/// <summary>
	/// Message texts containing valid and invalid UTF-16 sequences along with the expected text in the written JSON document.
	/// </summary>
	public static TheoryData<string, string, string> WriteJsonMessage_Utf16_Data => new()
	{
		// a valid surrogate pair is preserved
		{
			"Valid surrogate pair",
			"abc\uD83D\uDE00def",
			"abc\uD83D\uDE00def"
		},
		// a high surrogate without a following low surrogate is replaced
		{
			"Lone high surrogate",
			"abc\uD800def",
			"abc\uFFFDdef"
		},
		// a low surrogate without a preceding high surrogate is replaced
		{
			"Lone low surrogate",
			"abc\uDC00def",
			"abc\uFFFDdef"
		},
		// a high surrogate at the end of the text is replaced
		{
			"Trailing high surrogate",
			"abc\uD800",
			"abc\uFFFD"
		},
		// a surrogate pair in reversed order consists of two lone surrogates, both are replaced
		{
			"Reversed surrogate pair",
			"abc\uDE00\uD83Ddef",
			"abc\uFFFD\uFFFDdef"
		}
	};

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.WriteJsonMessage_ECS110"/> writes a message text containing
	/// invalid UTF-16 sequences without throwing an exception and replaces each invalid code unit with the Unicode
	/// replacement character (U+FFFD), so the message is not lost, while valid surrogate pairs are preserved.
	/// </summary>
	/// <param name="scenario">Name of the test scenario.</param>
	/// <param name="text">Text of the message to write.</param>
	/// <param name="expectedText">Text expected in the <c>message</c> field of the written JSON document.</param>
	[Theory]
	[MemberData(nameof(WriteJsonMessage_Utf16_Data))]
	public void WriteJsonMessage_ECS110_WhenTextContainsUtf16Sequence_WritesExpectedText(
		string scenario,
		string text,
		string expectedText)
	{
		// Arrange
		_ = scenario;
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.ReloadConfigurationIfNecessary();
		LocalLogMessage message = CreateMessage(text);
		var stream = new MemoryStream();

		try
		{
			// Act
			using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { SkipValidation = true }))
			{
				stage.WriteJsonMessage_ECS110(writer, message);
			}

			// Assert
			// the written document is valid JSON and contains the expected text
			using JsonDocument document = JsonDocument.Parse(stream.ToArray());
			string writtenText = document.RootElement.GetProperty("message").GetString();
			Assert.Equal(expectedText, writtenText);
		}
		finally
		{
			message.Release();
			message.Release();
		}
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Gets the default value and the current value of the specified setting as well as the value of the corresponding
	/// stage property (<see cref="Uri"/> arrays are returned as their configuration string).
	/// </summary>
	/// <param name="stage">Stage to query.</param>
	/// <param name="settingName">Name of the setting.</param>
	/// <param name="defaultValue">Receives the default value of the setting.</param>
	/// <param name="value">Receives the current value of the setting.</param>
	/// <param name="propertyValue">Receives the value of the stage property corresponding to the setting.</param>
	/// <exception cref="ArgumentException">The setting is not known.</exception>
	private static void GetSettingState(
		ElasticsearchPipelineStage stage,
		string                     settingName,
		out object                 defaultValue,
		out object                 value,
		out object                 propertyValue)
	{
		switch (settingName)
		{
			case "Server.ApiBaseUrls":
				IProcessingPipelineStageSetting<Uri[]> uris = stage.Settings.GetSetting(settingName, UriArrayConverter.Format, UriArrayConverter.Parse);
				defaultValue = UriArrayConverter.Format(uris.DefaultValue);
				value = UriArrayConverter.Format(uris.Value);
				propertyValue = UriArrayConverter.Format(stage.ApiBaseUrls.ToArray());
				return;

			case "Server.Authentication.Schemes":
				GetTypedSettingState<AuthenticationScheme>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.AuthenticationSchemes;
				return;

			case "Server.Authentication.Username":
				GetTypedSettingState<string>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.Username;
				return;

			case "Server.Authentication.Password":
				GetTypedSettingState<string>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.Password;
				return;

			case "Server.Authentication.Domain":
				GetTypedSettingState<string>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.Domain;
				return;

			case "Server.BulkRequest.MaxConcurrencyLevel":
				GetTypedSettingState<int>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.BulkRequestMaxConcurrencyLevel;
				return;

			case "Server.BulkRequest.MaxMessageCount":
				GetTypedSettingState<int>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.BulkRequestMaxMessageCount;
				return;

			case "Server.BulkRequest.MaxSize":
				GetTypedSettingState<int>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.BulkRequestMaxSize;
				return;

			case "Server.RequestTimeout":
				GetTypedSettingState<int>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.RequestTimeout;
				return;

			case "Server.IndexName":
				GetTypedSettingState<string>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.IndexName;
				return;

			case "Data.Organization.Id":
				GetTypedSettingState<string>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.OrganizationId;
				return;

			case "Data.Organization.Name":
				GetTypedSettingState<string>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.OrganizationName;
				return;

			case "Stage.SendQueueSize":
				GetTypedSettingState<int>(stage, settingName, out defaultValue, out value);
				propertyValue = stage.SendQueueSize;
				return;

			default:
				throw new ArgumentException($"Setting '{settingName}' is not supported by the test.", nameof(settingName));
		}
	}

	/// <summary>
	/// Gets the default value and the current value of the specified setting.
	/// </summary>
	/// <typeparam name="T">Type of the setting value.</typeparam>
	/// <param name="stage">Stage to query.</param>
	/// <param name="settingName">Name of the setting.</param>
	/// <param name="defaultValue">Receives the default value of the setting.</param>
	/// <param name="value">Receives the current value of the setting.</param>
	private static void GetTypedSettingState<T>(
		ElasticsearchPipelineStage stage,
		string                     settingName,
		out object                 defaultValue,
		out object                 value)
	{
		IProcessingPipelineStageSetting<T> setting = stage.Settings.GetSetting<T>(settingName);
		defaultValue = setting.DefaultValue;
		value = setting.Value;
	}

	#endregion
}
