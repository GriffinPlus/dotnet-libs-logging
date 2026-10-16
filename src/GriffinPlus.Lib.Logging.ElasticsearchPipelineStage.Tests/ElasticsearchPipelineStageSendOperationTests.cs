///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Net.Http.Headers;
using System.Text;

using Xunit;

using static GriffinPlus.Lib.Logging.Elasticsearch.Tests.ElasticsearchPipelineStageTestHelpers;

// ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// Unit tests targeting the <see cref="ElasticsearchPipelineStage.SendOperation"/> class.
/// The tests do not require an Elasticsearch server.
/// </summary>
[Trait("Category", "Unit")]
public partial class ElasticsearchPipelineStageSendOperationTests
{
	/// <summary>
	/// Fixed point in time used as the current time in <see cref="GetRetryAfter_Data"/>.
	/// </summary>
	private static readonly DateTimeOffset sRetryAfterNow = new(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);

	#region AddMessage(LocalLogMessage)

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

	#region GetRetryAfter(RetryConditionHeaderValue, DateTimeOffset)

	/// <summary>
	/// Test data for <see cref="GetRetryAfter_WhenCalled_ReturnsDelay"/>
	/// (scenario, header value, expected delay).
	/// </summary>
	public static TheoryData<string, RetryConditionHeaderValue, TimeSpan?> GetRetryAfter_Data => new()
	{
		// no header
		{
			"no header",
			null,
			null
		},
		// delay in seconds
		{
			"delta",
			new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)),
			TimeSpan.FromSeconds(7)
		},
		// point in time in the future
		{
			"date in future",
			new RetryConditionHeaderValue(sRetryAfterNow.AddSeconds(10)),
			TimeSpan.FromSeconds(10)
		},
		// point in time in the past (no negative delay)
		{
			"date in past",
			new RetryConditionHeaderValue(sRetryAfterNow.AddSeconds(-10)),
			TimeSpan.Zero
		}
	};

	/// <summary>
	/// Tests whether <see cref="ElasticsearchPipelineStage.SendOperation.GetRetryAfter"/> determines the delay specified
	/// in a <c>Retry-After</c> header as a delay or as a point in time.
	/// </summary>
	/// <param name="scenario">Name of the test scenario.</param>
	/// <param name="header">Value of the <c>Retry-After</c> header.</param>
	/// <param name="expected">Expected delay.</param>
	[Theory]
	[MemberData(nameof(GetRetryAfter_Data))]
	public void GetRetryAfter_WhenCalled_ReturnsDelay(string scenario, RetryConditionHeaderValue header, TimeSpan? expected)
	{
		// Arrange
		_ = scenario;

		// Act
		TimeSpan? delay = ElasticsearchPipelineStage.SendOperation.GetRetryAfter(header, sRetryAfterNow);

		// Assert
		Assert.Equal(expected, delay);
	}

	#endregion

	#region GetErrorResponseDescription(byte[])

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

	#region RebuildContentStream()

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
}
