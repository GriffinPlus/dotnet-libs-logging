///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;

using Xunit;

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

	#region SendOperation.ProcessSendCompleted()

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> handles a request the
	/// server rejects as too large (HTTP status 413) by reducing the maximum bulk request size in effect to half of the
	/// rejected request size and rebuilding the request, while keeping all messages in the send operation and
	/// considering the endpoint operational.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenRequestTooLarge_ReducesMaxSizeAndKeepsMessages()
	{
		// Arrange
		// (three messages of about 12 KB fit into a 40 KB request, but only one fits into half of it)
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode)413));
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages =
		[
			CreateMessage(new string('a', 12 * 1024)),
			CreateMessage(new string('b', 12 * 1024)),
			CreateMessage(new string('c', 12 * 1024))
		];

		try
		{
			foreach (LocalLogMessage message in messages) Assert.True(operation.AddMessage(message));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));
			int rejectedRequestSize = Assert.Single(handler.RequestContents).Length;

			// Act
			bool operational = operation.ProcessSendCompleted();

			// Assert
			// the endpoint is considered operational and the maximum request size is halved
			Assert.True(operational);
			Assert.Equal(rejectedRequestSize / 2, stage.EffectiveBulkRequestMaxSize);

			// all messages are kept, but the rebuilt request contains only the first message
			Assert.Equal(3, operation.MessageCount);
			Assert.True(operation.IsFull);
			Assert.Equal([new string('a', 12 * 1024)], GetRequestMessageTexts(operation));
			Assert.All(messages, message => Assert.Equal(2, message.RefCount));
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> delivers every message
	/// of a request the server rejected as too large (HTTP status 413) exactly once and in the original order: after the
	/// rejection, the request is rebuilt with the reduced maximum size and each successful response releases only the
	/// messages it acknowledges, while the remaining messages are sent with subsequent requests.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenRequestTooLarge_DeliversAllMessagesExactlyOnce()
	{
		// Arrange
		// (three messages of about 12 KB fit into a 40 KB request, but only one fits into half of it;
		// the first request is rejected as too large, subsequent requests are acknowledged entirely)
		string[] texts = [new('a', 12 * 1024), new('b', 12 * 1024), new('c', 12 * 1024)];
		int requestCount = 0;
		var handler = new StubHttpMessageHandler(content =>
		{
			if (Interlocked.Increment(ref requestCount) == 1)
				return new HttpResponseMessage((HttpStatusCode)413);

			int itemCount = GetBulkRequestMessageTexts(content).Length;
			string items = string.Join(",", Enumerable.Repeat("{\"create\":{\"status\":201}}", itemCount));
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent($"{{\"took\":1,\"errors\":false,\"items\":[{items}]}}", Encoding.UTF8, "application/json")
			};
		});
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages = texts.Select(CreateMessage).ToArray();

		try
		{
			foreach (LocalLogMessage message in messages) Assert.True(operation.AddMessage(message));

			// Act
			// (send until the operation does not contain any messages anymore, at most once per message plus the rejected request)
			for (int i = 0; i <= messages.Length && operation.MessageCount > 0; i++)
			{
				Assert.True(operation.StartSending(endpoint, CancellationToken.None));
				Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));
				Assert.True(operation.ProcessSendCompleted());
			}

			// Assert
			// all messages have been delivered and released
			Assert.Equal(0, operation.MessageCount);
			Assert.All(messages, message => Assert.Equal(1, message.RefCount));

			// expected requests:
			// 1. all messages (rejected with 413)
			// 2. message 'a' only (acknowledged)
			// 3. message 'b' only (acknowledged)
			// 4. message 'c' only (acknowledged)
			string[][] sentTexts = handler.RequestContents.Select(GetBulkRequestMessageTexts).ToArray();
			Assert.Equal(4, sentTexts.Length);
			Assert.Equal([texts[0], texts[1], texts[2]], sentTexts[0]);
			Assert.Equal([texts[0]], sentTexts[1]);
			Assert.Equal([texts[1]], sentTexts[2]);
			Assert.Equal([texts[2]], sentTexts[3]);
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> discards the oldest
	/// message when the server rejects a request as too large (HTTP status 413), although the maximum bulk request size
	/// in effect is already at its minimum, so the same request is not sent over and over again.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenRequestTooLargeAtMinimumSize_DiscardsOldestMessage()
	{
		// Arrange
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode)413));
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(10 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage firstMessage = CreateMessage("first");
		LocalLogMessage lastMessage = CreateMessage("last");

		try
		{
			Assert.True(operation.AddMessage(firstMessage));
			Assert.True(operation.AddMessage(lastMessage));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));

			// Act
			bool operational = operation.ProcessSendCompleted();

			// Assert
			// the endpoint is considered operational and the maximum request size stays at its minimum
			Assert.True(operational);
			Assert.Equal(10 * 1024, stage.EffectiveBulkRequestMaxSize);

			// the oldest message is discarded and released, the other message is kept
			Assert.Equal(1, operation.MessageCount);
			Assert.Equal(1, firstMessage.RefCount);
			Assert.Equal(2, lastMessage.RefCount);
			Assert.Equal(["last"], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			firstMessage.Release();
			lastMessage.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> treats a request the
	/// server rejected entirely (HTTP status 400) as an endpoint failure: no message is discarded and the unchanged
	/// request is prepared to be sent once again.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenBadRequest_KeepsAllMessages()
	{
		// Arrange
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
		{
			Content = new StringContent(
				"{\"error\":{\"type\":\"invalid_index_name_exception\",\"reason\":\"Invalid index name\"},\"status\":400}",
				Encoding.UTF8,
				"application/json")
		});
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages = [CreateMessage("a"), CreateMessage("b")];

		try
		{
			foreach (LocalLogMessage message in messages) Assert.True(operation.AddMessage(message));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));

			// Act
			bool operational = operation.ProcessSendCompleted();

			// Assert
			// the endpoint is considered not operational
			Assert.False(operational);

			// all messages are kept and the request is rebuilt unchanged
			Assert.Equal(2, operation.MessageCount);
			Assert.All(messages, message => Assert.Equal(2, message.RefCount));
			Assert.Equal(["a", "b"], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	#endregion

	#region SendOperation.GetErrorResponseDescription(byte[])

	/// <summary>
	/// Test data for <see cref="GetErrorResponseDescription_WhenCalled_ReturnsDescription"/>
	/// (scenario, response body, expected description).
	/// </summary>
	public static TheoryData<string, string, string> GetErrorResponseDescription_Data => new()
	{
		// typical Elasticsearch error response
		{
			"type and reason",
			"{\"error\":{\"root_cause\":[],\"type\":\"invalid_index_name_exception\",\"reason\":\"Invalid index name\"},\"status\":400}",
			"invalid_index_name_exception: Invalid index name"
		},
		// error object with type only
		{
			"type only",
			"{\"error\":{\"type\":\"some_exception\"}}",
			"some_exception"
		},
		// error object with reason only
		{
			"reason only",
			"{\"error\":{\"reason\":\"Something failed\"}}",
			"Something failed"
		},
		// error object without type and reason
		{
			"empty error object",
			"{\"error\":{}}",
			null
		},
		// error reported as plain string (older versions/proxies)
		{
			"error string",
			"{\"error\":\"Something failed\"}",
			"Something failed"
		},
		// error property of an unexpected kind
		{
			"error number",
			"{\"error\":42}",
			null
		},
		// JSON object without error property
		{
			"no error property",
			"{\"status\":400}",
			null
		},
		// JSON value that is not an object
		{
			"json array",
			"[1,2,3]",
			null
		},
		// body that is not JSON (e.g. an HTML page of a proxy)
		{
			"invalid json",
			"<html>Bad Request</html>",
			null
		},
		// empty body
		{
			"empty body",
			"",
			null
		}
	};

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.GetErrorResponseDescription(byte[])"/> extracts
	/// the error details from the body of an error response and returns <see langword="null"/> for bodies that do not
	/// provide any error details.
	/// </summary>
	/// <param name="scenario">Name of the test scenario.</param>
	/// <param name="body">Body of the error response.</param>
	/// <param name="expected">Expected description.</param>
	[Theory]
	[MemberData(nameof(GetErrorResponseDescription_Data))]
	public void GetErrorResponseDescription_WhenCalled_ReturnsDescription(string scenario, string body, string expected)
	{
		// Arrange
		_ = scenario;
		byte[] data = Encoding.UTF8.GetBytes(body);

		// Act
		string description = ElasticsearchPipelineStage.SendOperation.GetErrorResponseDescription(data);

		// Assert
		Assert.Equal(expected, description);
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

		return GetBulkRequestMessageTexts(copy.ToArray());
	}

	/// <summary>
	/// Gets the texts of the messages contained in the specified bulk request content and asserts that the content is
	/// a well-formed bulk request (newline-terminated lines, each message document is a valid JSON document preceded by
	/// a valid <c>create</c> action document).
	/// </summary>
	/// <param name="requestContent">Content of the bulk request.</param>
	/// <returns>The texts of the messages in the request (in request order).</returns>
	private static string[] GetBulkRequestMessageTexts(byte[] requestContent)
	{
		// the request consists of newline-terminated lines
		string content = Encoding.UTF8.GetString(requestContent);
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
				Assert.True(action.RootElement.TryGetProperty("create", out JsonElement _));
			}

			using (JsonDocument document = JsonDocument.Parse(lines[i + 1]))
			{
				texts[i / 2] = document.RootElement.GetProperty("message").GetString();
			}
		}

		return texts;
	}

	/// <summary>
	/// Creates a stage with the specified maximum bulk request size that sends requests to the specified HTTP message
	/// handler instead of a real server and loads its configuration.
	/// </summary>
	/// <param name="maxSize">Maximum bulk request size (in bytes).</param>
	/// <param name="handler">HTTP message handler receiving the requests.</param>
	/// <returns>The created stage.</returns>
	private static ElasticsearchPipelineStage CreateStageWithHttpStub(int maxSize, HttpMessageHandler handler)
	{
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.BulkRequestMaxSize = maxSize;
		stage.HttpMessageHandlerTestHook = handler;
		stage.ReloadConfigurationIfNecessary();
		return stage;
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
