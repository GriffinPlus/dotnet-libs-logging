///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// HTTP message handler that does not send requests, but records the request content and answers each request
/// with a response created by a configurable callback (allows testing the handling of specific HTTP responses).
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
	private readonly Func<byte[], HttpResponseMessage> mResponseFactory;

	/// <summary>
	/// Initializes a new instance of the <see cref="StubHttpMessageHandler"/> class.
	/// </summary>
	/// <param name="responseFactory">Callback creating the response for a request (receives the request content).</param>
	public StubHttpMessageHandler(Func<byte[], HttpResponseMessage> responseFactory)
	{
		mResponseFactory = responseFactory ?? throw new ArgumentNullException(nameof(responseFactory));
	}

	/// <summary>
	/// Gets the contents of the received requests (in the order of reception).
	/// </summary>
	public List<byte[]> RequestContents { get; } = [];

	/// <summary>
	/// Records the content of the specified request and returns the response created by the response factory.
	/// </summary>
	/// <param name="request">The received request.</param>
	/// <param name="cancellationToken">Token that can be signaled to cancel the operation.</param>
	/// <returns>The response created by the response factory.</returns>
	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		byte[] content = request.Content != null
							 ? await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false)
							 : [];

		lock (RequestContents) RequestContents.Add(content);
		return mResponseFactory(content);
	}
}
