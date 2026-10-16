///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;

using Xunit;

using static GriffinPlus.Lib.Logging.Elasticsearch.Tests.ElasticsearchPipelineStageTestHelpers;

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

// Tests targeting ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted().
public partial class ElasticsearchPipelineStageSendOperationTests
{
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

	/// <summary>
	/// Test data for <see cref="ProcessSendCompleted_WhenResponseDoesNotMatchRequest_KeepsAllMessages"/>
	/// (scenario, response body for a request with three messages).
	/// </summary>
	public static TheoryData<string, string> ProcessSendCompleted_ResponseMismatch_Data => new()
	{
		// the response does not contain any items
		{
			"items missing",
			"{\"took\":1,\"errors\":false}"
		},
		// the response contains less items than messages were sent
		{
			"too few items",
			"{\"took\":1,\"errors\":false,\"items\":[{\"create\":{\"status\":201}},{\"create\":{\"status\":201}}]}"
		},
		// the response contains more items than messages were sent
		{
			"too many items",
			"{\"took\":1,\"errors\":false,\"items\":[{\"create\":{\"status\":201}},{\"create\":{\"status\":201}},{\"create\":{\"status\":201}},{\"create\":{\"status\":201}}]}"
		},
		// an item does not contain a 'create' result
		{
			"item without create",
			"{\"took\":1,\"errors\":true,\"items\":[{\"create\":{\"status\":201}},{\"index\":{\"status\":201}},{\"create\":{\"status\":201}}]}"
		}
	};

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> keeps all messages and
	/// considers the endpoint not operational, if the bulk response does not contain exactly one <c>create</c> result
	/// for each sent message, so no message is released or sent twice due to a wrong assignment.
	/// </summary>
	/// <param name="scenario">Name of the test scenario.</param>
	/// <param name="responseBody">Body of the HTTP 200 response.</param>
	[Theory]
	[MemberData(nameof(ProcessSendCompleted_ResponseMismatch_Data))]
	public void ProcessSendCompleted_WhenResponseDoesNotMatchRequest_KeepsAllMessages(string scenario, string responseBody)
	{
		// Arrange
		_ = scenario;
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
		});
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages = [CreateMessage("a"), CreateMessage("b"), CreateMessage("c")];

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
			Assert.Equal(3, operation.MessageCount);
			Assert.All(messages, message => Assert.Equal(2, message.RefCount));
			Assert.Equal(["a", "b", "c"], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> assigns the items of a
	/// bulk response only to the messages contained in the sent request, if the send operation holds more messages than
	/// fit into the request: the response of a request containing only the first message is accepted and the failed
	/// item discards only that message, while the other messages are kept for subsequent requests.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenOperationHoldsMoreMessagesThanSent_EvaluatesOnlySentMessages()
	{
		// Arrange
		// (three messages of about 12 KB fit into a 40 KB request, but only one fits into half of it;
		// the first request is rejected as too large, the second one contains only the first message that fails to index)
		string[] texts = [new('a', 12 * 1024), new('b', 12 * 1024), new('c', 12 * 1024)];
		int requestCount = 0;
		var handler = new StubHttpMessageHandler(_ =>
		{
			if (Interlocked.Increment(ref requestCount) == 1)
				return new HttpResponseMessage((HttpStatusCode)413);

			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(
					"{\"took\":1,\"errors\":true,\"items\":[{\"create\":{\"status\":400,\"error\":{\"type\":\"mapper_parsing_exception\",\"reason\":\"failed to parse\"}}}]}",
					Encoding.UTF8,
					"application/json")
			};
		});
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages = texts.Select(CreateMessage).ToArray();

		try
		{
			foreach (LocalLogMessage message in messages) Assert.True(operation.AddMessage(message));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));
			Assert.True(operation.ProcessSendCompleted()); // 413 => request now contains only message 'a'
			Assert.Equal([texts[0]], GetRequestMessageTexts(operation));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));

			// Act
			bool operational = operation.ProcessSendCompleted();

			// Assert
			// the response is accepted and the endpoint is considered operational
			Assert.True(operational);

			// only the failed message 'a' is discarded and released, messages 'b' and 'c' are kept
			Assert.Equal(2, operation.MessageCount);
			Assert.Equal(1, messages[0].RefCount);
			Assert.Equal(2, messages[1].RefCount);
			Assert.Equal(2, messages[2].RefCount);

			// the next request contains message 'b' only
			Assert.Equal([texts[1]], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> keeps only the messages
	/// the server rejected due to overload (item status 429), releases the indexed messages, considers the endpoint
	/// operational and reports the throttling including the delay specified in the <c>Retry-After</c> header.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenItemsThrottled_KeepsThrottledMessagesAndReportsThrottling()
	{
		// Arrange
		var handler = new StubHttpMessageHandler(_ =>
		{
			var response = new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent(
					"{\"took\":1,\"errors\":true,\"items\":[{\"create\":{\"status\":201}},{\"create\":{\"status\":429}},{\"create\":{\"status\":201}}]}",
					Encoding.UTF8,
					"application/json")
			};
			response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
			return response;
		});
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages = [CreateMessage("a"), CreateMessage("b"), CreateMessage("c")];

		try
		{
			foreach (LocalLogMessage message in messages) Assert.True(operation.AddMessage(message));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));

			// Act
			bool operational = operation.ProcessSendCompleted();

			// Assert
			// the endpoint is considered operational, but sending should be paused as requested by the server
			Assert.True(operational);
			Assert.True(operation.WasThrottled);
			Assert.Equal(TimeSpan.FromSeconds(5), operation.RetryAfter);

			// only the throttled message 'b' is kept, the indexed messages are released
			Assert.Equal(1, operation.MessageCount);
			Assert.Equal(1, messages[0].RefCount);
			Assert.Equal(2, messages[1].RefCount);
			Assert.Equal(1, messages[2].RefCount);
			Assert.Equal(["b"], GetRequestMessageTexts(operation));
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> keeps all messages,
	/// considers the endpoint operational and reports the throttling without a delay, if the server rejects the entire
	/// request due to overload (HTTP status 429 without <c>Retry-After</c> header).
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenRequestThrottled_KeepsAllMessagesAndReportsThrottling()
	{
		// Arrange
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage((HttpStatusCode)429));
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
			// the endpoint is considered operational, but sending should be paused (no delay specified by the server)
			Assert.True(operational);
			Assert.True(operation.WasThrottled);
			Assert.Null(operation.RetryAfter);

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

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> releases all messages,
	/// leaves the send operation empty and considers the endpoint operational, if the server indexed all messages
	/// successfully. Throttling must not be reported in this case.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenAllMessagesIndexed_ReleasesAllMessages()
	{
		// Arrange
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
		{
			Content = new StringContent(
				"{\"took\":1,\"errors\":false,\"items\":[{\"create\":{\"status\":201}},{\"create\":{\"status\":201}},{\"create\":{\"status\":201}}]}",
				Encoding.UTF8,
				"application/json")
		});
		ElasticsearchPipelineStage stage = CreateStageWithHttpStub(40 * 1024, handler);
		var operation = new ElasticsearchPipelineStage.SendOperation(stage);
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(new Uri("http://127.0.0.1:9200/"));
		LocalLogMessage[] messages = [CreateMessage("a"), CreateMessage("b"), CreateMessage("c")];

		try
		{
			foreach (LocalLogMessage message in messages) Assert.True(operation.AddMessage(message));
			Assert.True(operation.StartSending(endpoint, CancellationToken.None));
			Assert.True(SpinWait.SpinUntil(() => operation.HasCompletedSending, 10000));

			// Act
			bool operational = operation.ProcessSendCompleted();

			// Assert
			// the endpoint is considered operational
			Assert.True(operational);

			// all messages are released and the operation is empty
			Assert.Equal(0, operation.MessageCount);
			Assert.All(messages, message => Assert.Equal(1, message.RefCount));

			// throttling is not reported
			Assert.False(operation.WasThrottled);
			Assert.Null(operation.RetryAfter);
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.ProcessSendCompleted"/> handles a request that
	/// does not complete within the configured <see cref="ElasticsearchPipelineStage.RequestTimeout"/> like a network
	/// error: the endpoint is considered non-operational and all messages are kept and put into the rebuilt request,
	/// so they can be sent once again.
	/// </summary>
	[Fact]
	public void ProcessSendCompleted_WhenRequestTimesOut_KeepsMessages()
	{
		// Arrange
		// (the server stub responds after 30 seconds, but the request times out after 1 second)
		var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))
		{
			ResponseDelay = TimeSpan.FromSeconds(30)
		};
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		stage.RequestTimeout = 1;
		stage.HttpMessageHandlerTestHook = handler;
		stage.ReloadConfigurationIfNecessary();
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
			// the endpoint is not considered operational, but the request was not throttled
			Assert.False(operational);
			Assert.False(operation.WasThrottled);

			// all messages are kept and put into the rebuilt request
			Assert.Single(handler.RequestContents);
			Assert.Equal(2, operation.MessageCount);
			Assert.Equal(["a", "b"], GetRequestMessageTexts(operation));
			Assert.All(messages, message => Assert.Equal(2, message.RefCount));
		}
		finally
		{
			operation.Reset();
			foreach (LocalLogMessage message in messages) message.Release();
			handler.Dispose();
		}
	}
}
