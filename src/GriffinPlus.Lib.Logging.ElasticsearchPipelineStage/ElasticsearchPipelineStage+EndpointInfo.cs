///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	partial class ElasticsearchPipelineStage
	{
		/// <summary>
		/// Information about an Elasticsearch API endpoint.
		/// </summary>
		private sealed class EndpointInfo
		{
			private bool mIsOperational;
			private int  mErrorTickCount;

			/// <summary>
			/// Initializes a new instance of the <see cref="EndpointInfo"/> class.
			/// </summary>
			/// <param name="apiBaseUrl">Base URL of the API endpoint.</param>
			public EndpointInfo(Uri apiBaseUrl)
			{
				ApiBaseUrl = apiBaseUrl;
				BulkRequestApiUrl = new Uri(apiBaseUrl, "_bulk");

				// the endpoint is considered to be non-operational at start
				// (put the error tick count into the past, so the stage will try connecting immediately)
				mIsOperational = false;
				mErrorTickCount = Environment.TickCount - RetryEndpointAfterErrorTimeMs - 1;
			}

			/// <summary>
			/// Gets or sets the base URL of API endpoints.
			/// </summary>
			public Uri ApiBaseUrl { get; }

			/// <summary>
			/// Gets the URL of the bulk request endpoint.
			/// </summary>
			public Uri BulkRequestApiUrl { get; }

			/// <summary>
			/// Gets the tick count (<see cref="Environment.TickCount"/>) of last reported error.
			/// The value is 0, if no error has occurred.
			/// </summary>
			public int ErrorTickCount => mErrorTickCount;

			/// <summary>
			/// Gets or sets a value indicating whether the endpoint has been tried at least once.
			/// </summary>
			public bool HasTriedToConnect { get; set; }

			/// <summary>
			/// Gets or sets a value indicating whether the endpoint accepts new messages to index.
			/// </summary>
			public bool IsOperational
			{
				get => mIsOperational;
				set
				{
					mIsOperational = value;
					mErrorTickCount = mIsOperational ? 0 : Environment.TickCount;
				}
			}
		}
	}

}
