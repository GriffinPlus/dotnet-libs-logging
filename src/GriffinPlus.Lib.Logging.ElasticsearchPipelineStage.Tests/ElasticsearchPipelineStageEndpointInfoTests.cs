///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

using Xunit;

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// Unit tests targeting the <see cref="ElasticsearchPipelineStage.EndpointInfo"/> class.
/// </summary>
[Trait("Category", "Unit")]
public class ElasticsearchPipelineStageEndpointInfoTests
{
	#region EndpointInfo(Uri)

	/// <summary>
	/// Test data for <see cref="Constructor_WhenCalled_BuildsBulkRequestApiUrl"/>:
	/// scenario, API base URL and expected bulk request API URL.
	/// </summary>
	public static TheoryData<string, string, string> Constructor_BulkRequestApiUrl_Data => new()
	{
		// root path with trailing slash
		{
			"Root with slash",
			"http://host:9200/",
			"http://host:9200/_bulk"
		},
		// root path without trailing slash (Uri normalizes the empty path to '/')
		{
			"Root without slash",
			"http://host:9200",
			"http://host:9200/_bulk"
		},
		// path prefix with trailing slash
		{
			"Prefix with slash",
			"http://proxy/es/",
			"http://proxy/es/_bulk"
		},
		// path prefix without trailing slash (last segment must not be replaced)
		{
			"Prefix without slash",
			"http://proxy/es",
			"http://proxy/es/_bulk"
		},
		// multi-segment path prefix without trailing slash
		{
			"Nested prefix without slash",
			"http://proxy/a/b",
			"http://proxy/a/b/_bulk"
		}
	};

	/// <summary>
	/// Tests whether the constructor appends the bulk request endpoint to the complete path of the API base URL,
	/// independent of whether the path ends with a slash, and leaves the endpoint in its initial non-operational state.
	/// </summary>
	/// <param name="scenario">Description of the test scenario.</param>
	/// <param name="apiBaseUrl">API base URL passed to the constructor.</param>
	/// <param name="expectedBulkRequestApiUrl">Expected bulk request API URL.</param>
	[Theory]
	[MemberData(nameof(Constructor_BulkRequestApiUrl_Data))]
	public void Constructor_WhenCalled_BuildsBulkRequestApiUrl(string scenario, string apiBaseUrl, string expectedBulkRequestApiUrl)
	{
		// Arrange
		_ = scenario;
		var baseUrl = new Uri(apiBaseUrl);

		// Act
		var endpoint = new ElasticsearchPipelineStage.EndpointInfo(baseUrl);

		// Assert
		Assert.Equal(expectedBulkRequestApiUrl, endpoint.BulkRequestApiUrl.AbsoluteUri);
		Assert.False(endpoint.IsOperational);
		Assert.Equal(apiBaseUrl, baseUrl.OriginalString); // the passed base URL is not modified
	}

	#endregion
}
