///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

using Xunit;

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// Helpers shared by the unit tests of the <see cref="ElasticsearchPipelineStage"/> class and its nested classes.
/// </summary>
internal static class ElasticsearchPipelineStageTestHelpers
{
	/// <summary>
	/// Gets the texts of the messages contained in the prepared request of the specified send operation and asserts
	/// that the request is a well-formed bulk request (newline-terminated lines, each message document is a valid
	/// JSON document preceded by a valid <c>create</c> action document). The position of the content stream is preserved.
	/// </summary>
	/// <param name="operation">Send operation to query.</param>
	/// <returns>The texts of the messages in the request (in request order).</returns>
	internal static string[] GetRequestMessageTexts(ElasticsearchPipelineStage.SendOperation operation)
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
	internal static string[] GetBulkRequestMessageTexts(byte[] requestContent)
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
	internal static ElasticsearchPipelineStage CreateStageWithHttpStub(int maxSize, HttpMessageHandler handler)
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
	internal static ElasticsearchPipelineStage CreateStageWithSmallBulkRequestSize(out int maxSize)
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
	internal static LocalLogMessage CreateMessage(string text)
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
}
