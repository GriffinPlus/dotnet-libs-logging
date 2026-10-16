///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using Xunit;

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
		{ "ApiBaseUrls", "Server.ApiBaseUrls", "http://127.0.0.1:9200/" },
		// password based authentication schemes
		{ "AuthenticationSchemes", "Server.Authentication.Schemes", AuthenticationScheme.PasswordBased },
		// no custom credentials
		{ "Username", "Server.Authentication.Username", "" },
		{ "Password", "Server.Authentication.Password", "" },
		{ "Domain", "Server.Authentication.Domain", "" },
		// bulk request limits (message count 0 = unlimited)
		{ "BulkRequestMaxConcurrencyLevel", "Server.BulkRequest.MaxConcurrencyLevel", 5 },
		{ "BulkRequestMaxMessageCount", "Server.BulkRequest.MaxMessageCount", 0 },
		{ "BulkRequestMaxSize", "Server.BulkRequest.MaxSize", 5 * 1024 * 1024 },
		// default index
		{ "IndexName", "Server.IndexName", "logs" },
		// no organization information
		{ "OrganizationId", "Data.Organization.Id", "" },
		{ "OrganizationName", "Data.Organization.Name", "" },
		// size of the send queue
		{ "SendQueueSize", "Stage.SendQueueSize", 50000 }
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

	#region SendOperation.AddMessage(LocalLogMessage)

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.AddMessage"/> discards and releases a single
	/// message that exceeds the maximum bulk request size, so the message does not block the processing queue,
	/// and whether the send operation accepts regular messages afterward.
	/// </summary>
	[Fact]
	public void AddMessage_WhenMessageExceedsMaxRequestSize_DiscardsMessage()
	{
		// Arrange
		ElasticsearchPipelineStage stage = CreateStageWithSmallBulkRequestSize(out int maxSize);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		LocalLogMessage oversizedMessage = CreateMessage(new string('x', 2 * maxSize));
		LocalLogMessage smallMessage = CreateMessage("small");

		try
		{
			// Act
			bool added = operation.AddMessage(oversizedMessage);

			// Assert
			// the oversized message is consumed (discarded) and released
			Assert.True(added);
			Assert.Equal(0, operation.MessageCount);
			Assert.False(operation.IsFull);
			Assert.Equal(1, oversizedMessage.RefCount);

			// the send operation still accepts regular messages
			Assert.True(operation.AddMessage(smallMessage));
			Assert.Equal(1, operation.MessageCount);
			Assert.Equal(2, smallMessage.RefCount);
		}
		finally
		{
			operation.Reset();
			oversizedMessage.Release();
			smallMessage.Release();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.AddMessage"/> rejects (but does not discard)
	/// a message that fits into an empty request, but not into a request that already contains a message,
	/// so it can be sent with the next request.
	/// </summary>
	[Fact]
	public void AddMessage_WhenRequestCannotHoldMessage_RejectsMessage()
	{
		// Arrange
		// (each text takes more than half of the maximum request size, so two messages never fit into one request,
		// but a single message fits as long as the per-message overhead is below half of the maximum request size)
		ElasticsearchPipelineStage stage = CreateStageWithSmallBulkRequestSize(out int maxSize);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var probeOperation = new ElasticsearchPipelineStage.SendOperation(stage);
		LocalLogMessage firstMessage = CreateMessage(new string('a', maxSize / 2 + 1));
		LocalLogMessage secondMessage = CreateMessage(new string('b', maxSize / 2 + 1));

		try
		{
			// verify the precondition: the second message fits into an empty request on its own
			// (the send operation takes over a reference, so add one for the probe operation)
			secondMessage.AddRef();
			Assert.True(probeOperation.AddMessage(secondMessage));
			Assert.Equal(1, probeOperation.MessageCount);
			Assert.False(probeOperation.IsFull);
			probeOperation.Reset(); // releases the reference held by the probe operation
			Assert.Equal(2, secondMessage.RefCount);

			Assert.True(operation.AddMessage(firstMessage));

			// Act
			bool added = operation.AddMessage(secondMessage);

			// Assert
			// the second message does not fit anymore => rejected, but not discarded
			Assert.False(added);
			Assert.True(operation.IsFull);
			Assert.Equal(1, operation.MessageCount);
			Assert.Equal(2, secondMessage.RefCount);
		}
		finally
		{
			operation.Reset();
			firstMessage.Release();
			secondMessage.Release();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.AddMessage"/> discards and releases a message
	/// that fails to serialize, removes the partially written data from the request and keeps accepting messages afterward.
	/// </summary>
	[Fact]
	public void AddMessage_WhenSerializationFails_DiscardsMessage()
	{
		// Arrange
		ElasticsearchPipelineStage stage = CreateStageWithSmallBulkRequestSize(out int _);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		LocalLogMessage firstMessage = CreateMessage("first");
		LocalLogMessage failingMessage = CreateMessage("failing");
		LocalLogMessage lastMessage = CreateMessage("last");
		operation.SerializationTestHook = message =>
		{
			if (ReferenceEquals(message, failingMessage))
				throw new InvalidOperationException("Simulated serialization failure.");
		};

		try
		{
			Assert.True(operation.AddMessage(firstMessage));

			// Act
			bool added = operation.AddMessage(failingMessage);

			// Assert
			// the failing message is consumed (discarded) and released
			Assert.True(added);
			Assert.Equal(1, operation.MessageCount);
			Assert.False(operation.IsFull);
			Assert.Equal(1, failingMessage.RefCount);

			// the send operation still accepts regular messages and the request contains no partial data
			// (the request must be a well-formed bulk request containing the regular messages only)
			Assert.True(operation.AddMessage(lastMessage));
			Assert.Equal(2, operation.MessageCount);
			Assert.Equal(["first", "last"], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			firstMessage.Release();
			failingMessage.Release();
			lastMessage.Release();
		}
	}

	#endregion

	#region SendOperation.RebuildContentStream()

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.RebuildContentStream"/> discards and releases
	/// only a message that fails to serialize, while the other messages are kept in the rebuilt request.
	/// </summary>
	[Fact]
	public void RebuildContentStream_WhenSerializationFails_DiscardsOnlyFailingMessage()
	{
		// Arrange
		ElasticsearchPipelineStage stage = CreateStageWithSmallBulkRequestSize(out int _);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		LocalLogMessage firstMessage = CreateMessage("first");
		LocalLogMessage failingMessage = CreateMessage("failing");
		LocalLogMessage lastMessage = CreateMessage("last");

		try
		{
			Assert.True(operation.AddMessage(firstMessage));
			Assert.True(operation.AddMessage(failingMessage));
			Assert.True(operation.AddMessage(lastMessage));
			operation.SerializationTestHook = message =>
			{
				if (ReferenceEquals(message, failingMessage))
					throw new InvalidOperationException("Simulated serialization failure.");
			};

			// Act
			operation.RebuildContentStream();

			// Assert
			// only the failing message is discarded and released
			Assert.Equal(2, operation.MessageCount);
			Assert.False(operation.IsFull);
			Assert.Equal(1, failingMessage.RefCount);
			Assert.Equal(2, firstMessage.RefCount);
			Assert.Equal(2, lastMessage.RefCount);

			// the request is a well-formed bulk request containing the remaining messages only
			Assert.Equal(["first", "last"], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			firstMessage.Release();
			failingMessage.Release();
			lastMessage.Release();
		}
	}

	#endregion

	#region WriteJsonMessage_ECS110(Utf8JsonWriter, LocalLogMessage)

	/// <summary>
	/// Message texts containing valid and invalid UTF-16 sequences along with the expected text in the written JSON document.
	/// </summary>
	public static TheoryData<string, string, string> WriteJsonMessage_Utf16_Data => new()
	{
		// a valid surrogate pair is preserved
		{ "Valid surrogate pair", "abc\uD83D\uDE00def", "abc\uD83D\uDE00def" },
		// a high surrogate without a following low surrogate is replaced
		{ "Lone high surrogate", "abc\uD800def", "abc\uFFFDdef" },
		// a low surrogate without a preceding high surrogate is replaced
		{ "Lone low surrogate", "abc\uDC00def", "abc\uFFFDdef" },
		// a high surrogate at the end of the text is replaced
		{ "Trailing high surrogate", "abc\uD800", "abc\uFFFD" },
		// a surrogate pair in reversed order consists of two lone surrogates, both are replaced
		{ "Reversed surrogate pair", "abc\uDE00\uD83Ddef", "abc\uFFFD\uFFFDdef" }
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

	/// <summary>
	/// Gets the texts of the messages contained in the prepared request of the specified send operation and asserts
	/// that the request is a well-formed bulk request (newline-terminated lines, each message document is a valid
	/// JSON document preceded by a valid <c>create</c> action document). The position of the content stream is preserved.
	/// </summary>
	/// <param name="operation">Send operation to query.</param>
	/// <returns>The texts of the messages in the request (in request order).</returns>
	private static string[] GetRequestMessageTexts(ElasticsearchPipelineStage.SendOperation operation)
	{
		// copy the request content
		Stream contentStream = operation.ContentStream;
		long position = contentStream.Position;
		var copy = new MemoryStream();
		try
		{
			contentStream.Position = 0;
			contentStream.CopyTo(copy);
		}
		finally
		{
			contentStream.Position = position;
		}

		// the request consists of newline-terminated lines
		string content = Encoding.UTF8.GetString(copy.ToArray());
		if (content.Length == 0) return [];
		Assert.EndsWith("\n", content);
		string[] lines = content.Substring(0, content.Length - 1).Split('\n');

		// each message is represented by an action line followed by a document line
		Assert.Equal(0, lines.Length % 2);
		string[] texts = new string[lines.Length / 2];
		for (int i = 0; i < lines.Length; i += 2)
		{
			using (JsonDocument action = JsonDocument.Parse(lines[i]))
			{
				Assert.True(action.RootElement.TryGetProperty("create", out var _));
			}

			using (JsonDocument document = JsonDocument.Parse(lines[i + 1]))
			{
				texts[i / 2] = document.RootElement.GetProperty("message").GetString();
			}
		}

		return texts;
	}

	/// <summary>
	/// Creates a stage with the smallest supported bulk request size and loads its configuration.
	/// </summary>
	/// <param name="maxSize">Receives the effective maximum bulk request size (in bytes).</param>
	/// <returns>The created stage.</returns>
	private static ElasticsearchPipelineStage CreateStageWithSmallBulkRequestSize(out int maxSize)
	{
		maxSize = 10 * 1024; // minimum bulk request size supported by the stage
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.BulkRequestMaxSize = maxSize;
		stage.ReloadConfigurationIfNecessary();
		return stage;
	}

	/// <summary>
	/// Creates a log message with the specified text
	/// (an additional reference is held, so the test can check whether the stage releases the message).
	/// </summary>
	/// <param name="text">Text of the message.</param>
	/// <returns>The created message.</returns>
	private static LocalLogMessage CreateMessage(string text)
	{
		LocalLogMessage message = LocalLogMessagePool.Default.GetMessage(
			DateTimeOffset.Now,
			0,
			LogWriter.Get<ElasticsearchPipelineStageTests>(),
			LogLevel.Error,
			"Test Application",
			"Test Process",
			42,
			text);

		message.AddRef(); // reference held by the test
		return message;
	}

	#endregion
}
