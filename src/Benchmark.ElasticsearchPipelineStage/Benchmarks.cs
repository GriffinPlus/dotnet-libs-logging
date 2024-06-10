///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

using GriffinPlus.Lib.Logging;
using GriffinPlus.Lib.Logging.Elasticsearch;

namespace Benchmark.Elasticsearch;

/// <summary>
/// Benchmarks targeting the <see cref="ElasticsearchPipelineStage"/> class.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, warmupCount: 1, iterationCount: 5)]
public class Benchmarks
{
	private const           int                        MaxTestMessageCount = 1000000;
	private static readonly LogWriter                  sTaggingLogWriter   = LogWriter.Get<Benchmarks>().WithTags("Tag-1", "Tag-2");
	private static readonly List<LocalLogMessage>      sMessages           = new();
	private                 ElasticsearchPipelineStage mStage;

	static Benchmarks()
	{
		const int lineCount = 10;
		const int lineLength = 100;

		// prepare test text
		var textBuilder = new StringBuilder();
		for (int i = 0; i < lineCount; i++)
		{
			textBuilder.Append(new string('x', lineLength));
			if (i + 1 < lineCount)
				textBuilder.Append('\n');
		}

		string testText = textBuilder.ToString();

		// create a log message pool for messages to test
		var pool = new LocalLogMessagePool();

		// create a set of messages that can be used to feed the stage
		DateTimeOffset timestamp = DateTimeOffset.Now;
		for (int i = 0; i < MaxTestMessageCount; i++)
		{
			LocalLogMessage message = pool.GetMessage(
				timestamp,
				long.MaxValue, // maximum length of the timestamp
				sTaggingLogWriter,
				LogLevel.Notice,
				"My Application",
				"My Process",
				12345,
				testText);

			sMessages.Add(message);
			timestamp += TimeSpan.FromMilliseconds(1);
		}
	}

	/// <summary>
	/// Global setup logic.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// create a new pipeline stage
		mStage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", null);
		mStage.ApiBaseUrls = new[] { new Uri("http://127.0.0.1:9200/") };
		mStage.IndexName = "pipeline-stage-benchmark";
		mStage.BulkRequestMaxMessageCount = int.MaxValue;
		mStage.DiscardMessagesIfQueueFull = false;
	}

	/// <summary>
	/// Global teardown logic.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup() { }

	/// <summary>
	/// Benchmarks processing messages in the pipeline stage.
	/// </summary>
	[Benchmark]
	[Arguments(MaxTestMessageCount)]
	[InvocationCount(1, 1)]
	public void Process(int messageCount)
	{
		// initialize the pipeline stage
		mStage.Initialize();

		for (int i = 0; i < messageCount; i++)
		{
			mStage.ProcessMessage(sMessages[i]);
		}

		// shut the pipeline stage down
		mStage.Shutdown();
	}
}
