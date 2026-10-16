///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Elasticsearch;

partial class ElasticsearchPipelineStage
{
	/// <summary>
	/// Information about an Elasticsearch API endpoint.
	/// </summary>
	internal sealed class EndpointInfo
	{
		private bool mIsOperational;

		/// <summary>
		/// Initializes a new instance of the <see cref="EndpointInfo"/> class.
		/// </summary>
		/// <param name="apiBaseUrl">
		/// Base URL of the API endpoint. The path of the URL is treated as a directory, even if it does not end with a slash,
		/// so the bulk request endpoint is always appended to the complete path (e.g. <c>http://proxy/es</c> results in
		/// <c>http://proxy/es/_bulk</c>).
		/// </param>
		public EndpointInfo(Uri apiBaseUrl)
		{
			if (!apiBaseUrl.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
			{
				var builder = new UriBuilder(apiBaseUrl);
				builder.Path += "/";
				apiBaseUrl = builder.Uri;
			}

			BulkRequestApiUrl = new Uri(apiBaseUrl, "_bulk");

			// the endpoint is considered to be non-operational at start
			// (put the error tick count into the past, so the stage will try connecting immediately)
			mIsOperational = false;
			ErrorTickCount = Environment.TickCount - RetryEndpointAfterErrorTimeMs - 1;
			ProbingConnection = true;
		}

		/// <summary>
		/// Gets the URL of the bulk request endpoint.
		/// </summary>
		public Uri BulkRequestApiUrl { get; }

		/// <summary>
		/// Gets the tick count (<see cref="Environment.TickCount"/>) of last reported error.
		/// The value is 0, if no error has occurred.
		/// </summary>
		public int ErrorTickCount { get; private set; }

		/// <summary>
		/// Gets or sets a value indicating whether the endpoint has been tried at least once.
		/// </summary>
		public bool HasTriedToConnect { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the endpoint should be probed to determine whether the
		/// server is reachable (avoids sending too many requests that will fail eventually spamming the system log).
		/// </summary>
		public bool ProbingConnection { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the endpoint accepts new messages to index.
		/// </summary>
		public bool IsOperational
		{
			get => mIsOperational;
			set
			{
				mIsOperational = value;
				ErrorTickCount = mIsOperational ? 0 : Environment.TickCount;
			}
		}
	}
}
