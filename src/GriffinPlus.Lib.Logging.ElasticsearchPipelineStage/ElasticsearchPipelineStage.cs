///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Collections;
using GriffinPlus.Lib.Threading;

// ReSharper disable ConvertToConstant.Local
// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	/// <summary>
	/// A log message processing pipeline stage that forwards log messages to an Elasticsearch cluster.
	/// </summary>
	public partial class ElasticsearchPipelineStage : ProcessingPipelineStage<ElasticsearchPipelineStage>
	{
		/// <summary>
		/// Maximum time the processing thread is allowed to run after the stage has started to shut down (in ms).
		/// </summary>
		private const int MaxProcessingOverrunTimeMs = 30000;

		/// <summary>
		/// The time an endpoint is not used after an error occurred (in ms).
		/// </summary>
		private const int RetryEndpointAfterErrorTimeMs = 30000;

		/// <summary>
		/// The processing queue, passes messages from the logging thread to the processing thread.
		/// Synchronized via monitor using itself.
		/// </summary>
		private readonly Queue<LocalLogMessage> mProcessingQueue = new Queue<LocalLogMessage>();

		/// <summary>
		/// The maximum number of messages in <see cref="mProcessingQueue"/>.
		/// </summary>
		private int mProcessingQueueSize = 0;

		/// <summary>
		/// Indicates whether the stage is operational.
		/// </summary>
		private volatile bool mIsOperational = true;

		/// <summary>
		/// Cancellation token source that is signaled when the stage is shutting down.
		/// </summary>
		private CancellationTokenSource mShutdownCancellationTokenSource = null;

		// members managed by the processing thread
		private volatile bool                   mReloadConfiguration        = true;
		private volatile bool                   mIsShutdownRequested        = false;
		private readonly Deque<EndpointInfo>    mEndpoints                  = new Deque<EndpointInfo>();
		private readonly Deque<LocalLogMessage> mMessagesPreparedToSend     = new Deque<LocalLogMessage>();
		private readonly EcsMessage             mEcsMessage                 = new EcsMessage();
		private readonly BulkRequestAction      mBulkRequestAction          = new BulkRequestAction { Index = new BulkRequestAction_Index() };
		private readonly JsonWriterOptions      mJsonWriterOptions          = new JsonWriterOptions { SkipValidation = false };
		private readonly JsonSerializerOptions  mJsonSerializerOptions      = new JsonSerializerOptions { IgnoreNullValues = true, NumberHandling = JsonNumberHandling.Strict };
		private readonly MemoryStream           mContentStream              = new MemoryStream();
		private readonly Utf8JsonWriter         mRequestContentWriter       = null;
		private          HttpClient             mHttpClient                 = null;
		private          string                 mIndexName                  = null; // caches IndexName
		private          int                    mBulkRequestMaxSize         = 0;    // caches BulkRequestMaxSize
		private          int                    mBulkRequestMaxMessageCount = 0;    // caches BulkRequestMaxMessageCount
		private          string                 mOrganizationId             = null; // caches OrganizationId
		private          string                 mOrganizationName           = null; // caches OrganizationName

		// defaults of settings determining the behavior of the stage
		private static readonly Uri[]                sDefault_ApiBaseUrls                        = { new Uri("http://127.0.0.1:9200/") };
		private static readonly AuthenticationScheme sDefault_Server_Authentication_Schemes      = AuthenticationScheme.PasswordBased;
		private static readonly string               sDefault_Server_Authentication_Username     = "";
		private static readonly string               sDefault_Server_Authentication_Password     = "";
		private static readonly string               sDefault_Server_Authentication_Domain       = "";
		private static readonly int                  sDefault_Server_BulkRequest_MaxMessageCount = 1000;
		private static readonly int                  sDefault_Server_BulkRequest_MaxSize         = 5 * 1024 * 1024;
		private static readonly string               sDefault_Server_IndexName                   = "logs";
		private static readonly string               sDefault_Data_Organization_Id               = "griffin.plus";
		private static readonly string               sDefault_Data_Organization_Name             = "Griffin+";
		private static readonly int                  sDefault_Stage_SendQueueSize                = 50000;

		// the settings determining the behavior of the stage
		private readonly IProcessingPipelineStageSetting<Uri[]>                mSetting_Server_ApiBaseUrls;
		private readonly IProcessingPipelineStageSetting<AuthenticationScheme> mSetting_Server_Authentication_Schemes;
		private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_Authentication_Username;
		private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_Authentication_Password;
		private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_Authentication_Domain;
		private readonly IProcessingPipelineStageSetting<int>                  mSetting_Server_BulkRequest_MaxMessageCount;
		private readonly IProcessingPipelineStageSetting<int>                  mSetting_Server_BulkRequest_MaxSize;
		private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_IndexName;
		private readonly IProcessingPipelineStageSetting<string>               mSetting_Data_Organization_Id;
		private readonly IProcessingPipelineStageSetting<string>               mSetting_Organization_Name;
		private readonly IProcessingPipelineStageSetting<int>                  mSetting_Stage_SendQueueSize;

		/// <summary>
		/// Initializes a new instance of the <see cref="ElasticsearchPipelineStage"/> class.
		/// Connection settings are loaded from the log configuration.
		/// </summary>
		/// <param name="name">Name of the pipeline stage (must be unique throughout the entire processing pipeline).</param>
		public ElasticsearchPipelineStage(string name) : base(name)
		{
			mRequestContentWriter = new Utf8JsonWriter(mContentStream, mJsonWriterOptions);

			mSetting_Server_ApiBaseUrls = RegisterSetting("Server.ApiBaseUrls", sDefault_ApiBaseUrls, UriArrayToString, StringToUriArray);
			mSetting_Server_ApiBaseUrls.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_Authentication_Schemes = RegisterSetting("Server.Authentication.Schemes", sDefault_Server_Authentication_Schemes);
			mSetting_Server_Authentication_Schemes.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_Authentication_Username = RegisterSetting("Server.Authentication.Username", sDefault_Server_Authentication_Username);
			mSetting_Server_Authentication_Username.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_Authentication_Password = RegisterSetting("Server.Authentication.Password", sDefault_Server_Authentication_Password);
			mSetting_Server_Authentication_Password.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_Authentication_Domain = RegisterSetting("Server.Authentication.Domain", sDefault_Server_Authentication_Domain);
			mSetting_Server_Authentication_Domain.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_BulkRequest_MaxMessageCount = RegisterSetting("Server.BulkRequest.MaxMessageCount", sDefault_Server_BulkRequest_MaxMessageCount);
			mSetting_Server_BulkRequest_MaxMessageCount.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_BulkRequest_MaxSize = RegisterSetting("Server.BulkRequest.MaxSize", sDefault_Server_BulkRequest_MaxSize);
			mSetting_Server_BulkRequest_MaxSize.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Server_IndexName = RegisterSetting("Server.IndexName", sDefault_Server_IndexName);
			mSetting_Server_IndexName.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Data_Organization_Id = RegisterSetting("Data.Organization.Id", sDefault_Data_Organization_Id);
			mSetting_Data_Organization_Id.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Organization_Name = RegisterSetting("Data.Organization.Name", sDefault_Data_Organization_Name);
			mSetting_Organization_Name.RegisterSettingChangedEventHandler(OnSettingChanged, false);

			mSetting_Stage_SendQueueSize = RegisterSetting("Stage.SendQueueSize", sDefault_Stage_SendQueueSize);
			mSetting_Stage_SendQueueSize.RegisterSettingChangedEventHandler(OnSettingChanged, false);
			mProcessingQueueSize = mSetting_Stage_SendQueueSize.Value;
		}

		#region Stage Settings

		#region ApiBaseUrls

		/// <summary>
		/// Gets the API endpoints of the Elasticsearch cluster.
		/// </summary>
		/// <exception cref="ArgumentNullException">The property is set and the specified list of endpoints is <c>null</c>.</exception>
		public IReadOnlyList<Uri> ApiBaseUrls
		{
			get => mSetting_Server_ApiBaseUrls.Value;
			set => mSetting_Server_ApiBaseUrls.Value = value.ToArray();
		}

		/// <summary>
		/// Converts an array of <see cref="Uri"/> to a string as used in the configuration.
		/// </summary>
		/// <param name="uris">Array of <see cref="Uri"/> to convert to a string.</param>
		/// <returns>The formatted array of <see cref="Uri"/>.</returns>
		private static string UriArrayToString(Uri[] uris)
		{
			if (uris == null) return "";
			return string.Join("; ", uris.Select(x => x.ToString()));
		}

		/// <summary>
		/// Converts a string to an array of <see cref="Uri"/>.
		/// The string is expected to contain the uris separated by semicolons.
		/// </summary>
		/// <param name="s">String to convert to an array of <see cref="Uri"/>.</param>
		/// <returns>An array of <see cref="Uri"/> corresponding to the specified string.</returns>
		private static Uri[] StringToUriArray(string s)
		{
			var apiEndpoints = new List<Uri>();
			foreach (string endpointToken in s.Trim().Split(';'))
			{
				string apiEndpointString = endpointToken.Trim();
				if (apiEndpointString.Length > 0)
				{
					var uri = new Uri(apiEndpointString);
					apiEndpoints.Add(uri);
				}
			}

			return apiEndpoints.ToArray();
		}

		#endregion

		#region AuthenticationSchemes

		/// <summary>
		/// Gets the authentication types (flags) to use when authenticating against the Elasticsearch cluster.
		/// </summary>
		public AuthenticationScheme AuthenticationSchemes
		{
			get => mSetting_Server_Authentication_Schemes.Value;
			set => mSetting_Server_Authentication_Schemes.Value = value;
		}

		#endregion

		#region Username

		/// <summary>
		/// Gets the username to use when authenticating against the Elasticsearch cluster.
		/// </summary>
		public string Username
		{
			get => mSetting_Server_Authentication_Username.Value;
			set => mSetting_Server_Authentication_Username.Value = value;
		}

		#endregion

		#region Password

		/// <summary>
		/// Gets the password to use when authenticating against the Elasticsearch cluster.
		/// </summary>
		public string Password
		{
			get => mSetting_Server_Authentication_Password.Value;
			set => mSetting_Server_Authentication_Password.Value = value;
		}

		#endregion

		#region Domain

		/// <summary>
		/// Gets or sets the domain used when authenticating against the Elasticsearch cluster
		/// (needed for authentication via Digest, NTLM, Kerberos and Negotiate).
		/// </summary>
		public string Domain
		{
			get => mSetting_Server_Authentication_Domain.Value;
			set => mSetting_Server_Authentication_Domain.Value = value;
		}

		#endregion

		#region BulkRequestMaxMessageCount

		/// <summary>
		/// Gets or sets the maximum number of messages to bundle in a bulk request.
		/// </summary>
		public int BulkRequestMaxMessageCount
		{
			get => mSetting_Server_BulkRequest_MaxMessageCount.Value;
			set => mSetting_Server_BulkRequest_MaxMessageCount.Value = value;
		}

		#endregion

		#region BulkRequestMaxSize

		/// <summary>
		/// Gets or sets the maximum size of a bulk request (in bytes).
		/// </summary>
		public int BulkRequestMaxSize
		{
			get => mSetting_Server_BulkRequest_MaxSize.Value;
			set => mSetting_Server_BulkRequest_MaxSize.Value = value;
		}

		#endregion

		#region IndexName

		/// <summary>
		/// Gets the name of the Elasticsearch index to ingest messages into.
		/// </summary>
		public string IndexName
		{
			get => mSetting_Server_IndexName.Value;
			set => mSetting_Server_IndexName.Value = value;
		}

		#endregion

		#region OrganizationId

		/// <summary>
		/// Gets the string to put into the ECS field 'organization.id' of every a written message.
		/// </summary>
		public string OrganizationId
		{
			get => mSetting_Data_Organization_Id.Value;
			set => mSetting_Data_Organization_Id.Value = value;
		}

		#endregion

		#region OrganizationName

		/// <summary>
		/// Gets the string to put into the ECS field 'organization.name' of every a written message.
		/// </summary>
		public string OrganizationName
		{
			get => mSetting_Organization_Name.Value;
			set => mSetting_Organization_Name.Value = value;
		}

		#endregion

		#region SendQueueSize

		/// <summary>
		/// Gets the size of the send queue buffering messages to send to the Elasticsearch cluster (in messages).
		/// </summary>
		public int SendQueueSize
		{
			get => mSetting_Stage_SendQueueSize.Value;
			set => mSetting_Stage_SendQueueSize.Value = value;
		}

		#endregion

		#region Handling Setting Changes

		/// <summary>
		/// Is called by a worker thread when the configuration changes.
		/// </summary>
		private void OnSettingChanged(object sender, SettingChangedEventArgs e)
		{
			// reload the maximum number of messages in the processing queue
			lock (mProcessingQueue)
			{
				mProcessingQueueSize = mSetting_Stage_SendQueueSize.Value;
			}

			// tell the process thread to reload its configuration
			mReloadConfiguration = true;
		}

		#endregion

		#endregion

		#region Overrides

		/// <summary>
		/// Initializes the pipeline stage when the stage is attached to the logging subsystem.
		/// </summary>
		protected override void OnInitialize()
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			Debug.Assert(mShutdownCancellationTokenSource == null);
			mShutdownCancellationTokenSource = new CancellationTokenSource();
			mReloadConfiguration = true;
			mIsShutdownRequested = false;
		}

		/// <summary>
		/// Shuts the pipeline stage down when the stage is detached from the logging system.
		/// </summary>
		protected override void OnShutdown()
		{
			Debug.Assert(Monitor.IsEntered(Sync));
			Debug.Assert(mShutdownCancellationTokenSource != null);
			Debug.Assert(!mShutdownCancellationTokenSource.IsCancellationRequested);

			try
			{
				// tell the processing thread to shut down
				mIsShutdownRequested = true;

				// cancel hanging operations after some time
				mShutdownCancellationTokenSource.CancelAfter(MaxProcessingOverrunTimeMs);

				// release processing thread, so it can terminate, if appropriate
				mProcessingNeededEvent.Set();

				// wait for the processing thread to complete
				while (true)
				{
					lock (mProcessingTriggerSync)
					{
						if (!mProcessingThreadRunning) break;
					}

					Thread.Sleep(10);
				}
			}
			finally
			{
				mShutdownCancellationTokenSource.Dispose();
				mShutdownCancellationTokenSource = null;
			}

			// discard messages that are still in the processing queue
			// (under normal conditions there should be nothing left)
			lock (mProcessingQueue)
			{
				Debug.Assert(mProcessingQueue.Count == 0);
				while (mProcessingQueue.Count > 0)
				{
					mProcessingQueue.Dequeue().Release();
				}
			}
		}

		#endregion

		#region Processing

		// all fields are synchronized using mProcessingTriggerSync
		private readonly object               mProcessingTriggerSync   = new object();
		private readonly ManualResetEventSlim mProcessingNeededEvent   = new ManualResetEventSlim();
		private          bool                 mProcessingThreadRunning = false;

		/// <summary>
		/// Processes a log message synchronously.
		/// </summary>
		/// <param name="message">Message to process.</param>
		/// <returns>
		/// Always <c>true</c>, so the message is passed to following stages.
		/// </returns>
		protected override bool ProcessSync(LocalLogMessage message)
		{
			Debug.Assert(Monitor.IsEntered(Sync));

			lock (mProcessingQueue)
			{
				// abort and discard the message, if the processing queue is full
				if (mProcessingQueue.Count >= mProcessingQueueSize)
					return true;

				// enqueue the message for processing
				message.AddRef();
				mProcessingQueue.Enqueue(message);
			}

			TriggerProcessing();
			return true;
		}

		/// <summary>
		/// Triggers the processing thread that send messages to the Elasticsearch cluster.
		/// </summary>
		private void TriggerProcessing()
		{
			lock (mProcessingTriggerSync)
			{
				mProcessingNeededEvent.Set();
				if (mProcessingThreadRunning) return;
				ThreadPool.QueueUserWorkItem(DoProcessing, mShutdownCancellationTokenSource.Token);
				mProcessingThreadRunning = true;
			}
		}

		/// <summary>
		/// The entry point of the processing thread that bundles messages and sends them to the Elasticsearch cluster.
		/// </summary>
		/// <param name="obj">The cancellation token that is signaled when the stage is shutting down</param>
		private void DoProcessing(object obj)
		{
			var cancellationToken = (CancellationToken)obj;

			try
			{
				while (true)
				{
					// wait for new messages to process
					if (!mProcessingNeededEvent.Wait(100, cancellationToken))
					{
						// abort, if there are no new messages in the processing queue...
						lock (mProcessingTriggerSync)
						{
							if (!mProcessingNeededEvent.IsSet)
							{
								mProcessingThreadRunning = false;
								return;
							}
						}
					}

					// reset processing event to avoid triggering the next run without data
					// (the thread drains the processing queue, if possible, before waiting for the processing event again)
					mProcessingNeededEvent.Reset();

					while (true)
					{
						// reload configuration and prepare the http client, if necessary
						ReloadConfigurationIfNecessary();

						// dequeue messages to send
						lock (mProcessingQueue)
						{
							while (mProcessingQueue.Count > 0 && mMessagesPreparedToSend.Count < mBulkRequestMaxMessageCount)
							{
								var message = mProcessingQueue.Dequeue();
								mMessagesPreparedToSend.AddToBack(message);
							}
						}

						// abort, if there is nothing to send
						if (mMessagesPreparedToSend.Count == 0)
						{
							if (mIsShutdownRequested)
							{
								// the stage is shutting down
								// => let the processing thread exit...
								lock (mProcessingTriggerSync)
								{
									mProcessingThreadRunning = false;
									return;
								}
							}

							// normal operation
							// => return to waiting for more messages...
							break;
						}

						// prepare request body...
						PrepareRequestBody();

						// ... and send it to the server
						int delay = SendRequest(cancellationToken);

						// slow down processing, if no endpoints are operational
						if (delay > 0)
							Task.Delay(delay, cancellationToken).WaitAndUnwrapException(cancellationToken);
					}
				}
			}
			catch (OperationCanceledException ex)
			{
				// the stage is shutting down and the processing thread has exceeded its overrun time
				Debug.Assert(ex.CancellationToken == cancellationToken);

				// discard messages that are still in the request preparation buffer
				for (int i = 0; i < mMessagesPreparedToSend.Count; i++) mMessagesPreparedToSend[i].Release();
				mMessagesPreparedToSend.Clear();

				// let the processing thread exit...
				lock (mProcessingTriggerSync)
				{
					mProcessingThreadRunning = false;
				}
			}
		}

		/// <summary>
		/// Reloads the configuration and populates processing thread specific data.
		/// </summary>
		private void ReloadConfigurationIfNecessary()
		{
			if (mReloadConfiguration)
			{
				// reset reload indicator to notice changes just occur just jiffy later
				mReloadConfiguration = false;

				// recreate the http client to take potential changes to authentication settings into account
				RecreateHttpClient();

				// reload bulk request settings
				mIndexName = IndexName;
				mBulkRequestMaxMessageCount = BulkRequestMaxMessageCount;
				mBulkRequestMaxSize = BulkRequestMaxSize;

				// reload fixed fields
				// (set to null, if the string is empty to omit these fields when generating requests)
				mOrganizationId = string.IsNullOrWhiteSpace(OrganizationId) ? null : OrganizationId;
				mOrganizationName = string.IsNullOrWhiteSpace(OrganizationName) ? null : OrganizationName;

				// rebuild request URLs
				mEndpoints.Clear();
				foreach (var apiBaseUrl in ApiBaseUrls)
				{
					mEndpoints.AddToBack(new EndpointInfo(apiBaseUrl));
				}
			}
		}

		/// <summary>
		/// Recreates the HTTP client used by the stage.
		/// </summary>
		private void RecreateHttpClient()
		{
			// dispose old http client
			mHttpClient?.Dispose();
			mHttpClient = null;

			// create new http client handler
			// (allow up to 5 redirects and enable automatic decompression)
			var handler = new HttpClientHandler
			{
				AllowAutoRedirect = true,
				MaxAutomaticRedirections = 5,
				PreAuthenticate = true,
				AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip
			};

			// use specific username/password for authentication (if configured),
			// otherwise use credentials of the user currently logged in
			var authTypes = AuthenticationSchemes;
			if (authTypes != AuthenticationScheme.None)
			{
				bool useCustomCredentials = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
				var credentials = useCustomCredentials
					                  ? new NetworkCredential(Username, Password, Domain)
					                  : CredentialCache.DefaultNetworkCredentials;

				var cache = new CredentialCache();
				foreach (var uriPrefix in ApiBaseUrls)
				{
					if (useCustomCredentials) // Basic and Digest authentication are not supported with default credentials (security issue)
					{
						if (authTypes.HasFlag(AuthenticationScheme.Basic)) cache.Add(uriPrefix, "Basic", credentials);
						if (authTypes.HasFlag(AuthenticationScheme.Digest)) cache.Add(uriPrefix, "Digest", credentials);
					}

					if (authTypes.HasFlag(AuthenticationScheme.Ntlm)) cache.Add(uriPrefix, "NTLM", credentials);
					if (authTypes.HasFlag(AuthenticationScheme.Kerberos)) cache.Add(uriPrefix, "Kerberos", credentials);
					if (authTypes.HasFlag(AuthenticationScheme.Negotiate)) cache.Add(uriPrefix, "Negotiate", credentials);
				}

				handler.Credentials = cache;
			}

			// create new http client using the new settings
			mHttpClient = new HttpClient(handler);
		}

		/// <summary>
		/// Prepares the body of the next bulk request to index messages buffered in <see cref="mMessagesPreparedToSend"/>.
		/// The resulting body is stored in <see cref="mContentStream"/>.
		/// </summary>
		private void PrepareRequestBody()
		{
			mContentStream.SetLength(0);
			long lastStreamPosition = -1;
			for (int i = 0; i < mMessagesPreparedToSend.Count; i++)
			{
				// init bulk request item object for indexing
				var message = mMessagesPreparedToSend[i];
				mBulkRequestAction.Index.Index = mIndexName;

				// serialize bulk request item object for indexing
				mRequestContentWriter.Reset();
				JsonSerializer.Serialize(mRequestContentWriter, mBulkRequestAction, mJsonSerializerOptions);

				// finalize line with a newline character
				mContentStream.WriteByte((byte)'\n');

				// serialize the actual log message
				mRequestContentWriter.Reset();
				mEcsMessage.Initialize(message, mOrganizationId, mOrganizationName);
				JsonSerializer.Serialize(mRequestContentWriter, mEcsMessage, mJsonSerializerOptions);

				// finalize line with a newline character
				mContentStream.WriteByte((byte)'\n');

				// abort, if the maximum number of bytes in the bulk request is reached
				if (mContentStream.Position > mBulkRequestMaxSize)
				{
					if (lastStreamPosition < 0)
					{
						// the current message is so big that it does not fit into a bulk request
						// => discard it
						WritePipelineError(
							$"Message is too large ({mContentStream.Position} bytes) to fit into a bulk request (max. {mBulkRequestMaxSize} bytes). Discarding message...",
							null);
						message.Release();
						mMessagesPreparedToSend.RemoveAt(i--);
						continue;
					}

					// truncate the stream to contain only the last messages to avoid exceeding the maximum request size
					mContentStream.SetLength(lastStreamPosition);
					break;
				}

				lastStreamPosition = mContentStream.Position;
			}
		}

		/// <summary>
		/// Sends the request body that has been prepared in <see cref="mContentStream"/> to one of the configured
		/// bulk request endpoints in <see cref="mEndpoints"/>.
		/// </summary>
		/// <param name="cancellationToken">Token that can be signaled to cancel the operation.</param>
		/// <returns>Delay to wait before trying again (in ms).</returns>
		private int SendRequest(CancellationToken cancellationToken)
		{
			while (true)
			{
				// take the first endpoint in the list
				// (the best choice is always at the beginning of the list, non-operational endpoints are put to the end of the list)
				var endpoint = mEndpoints[0];

				// abort, if the endpoint failed recently
				// (the first endpoint in the list is the most promising one...)
				int ticks = Environment.TickCount;
				if (!endpoint.IsOperational && ticks - endpoint.ErrorTickCount < RetryEndpointAfterErrorTimeMs)
					return RetryEndpointAfterErrorTimeMs - ticks + endpoint.ErrorTickCount;

				// create http request telling Elasticsearch to index the messages
				mContentStream.Position = 0;
				var content = new StreamContent(mContentStream);
				content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-ndjson");
				var request = new HttpRequestMessage(HttpMethod.Post, endpoint.BulkRequestApiUrl) { Content = content };

				try
				{
					var response = mHttpClient
						.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
						.WaitAndUnwrapException(cancellationToken);

					if (response.StatusCode == HttpStatusCode.OK)
					{
						// the bulk request was processed by the server
						// => check outcome of actions in the response
						byte[] responseData = response.Content.ReadAsByteArrayAsync().WaitAndUnwrapException(cancellationToken);
						var bulkResponse = JsonSerializer.Deserialize<BulkResponse>(responseData, mJsonSerializerOptions);
						Debug.Assert(bulkResponse != null, nameof(bulkResponse) + " != null");

						// check response for elasticsearch errors
						if (bulkResponse.Errors)
						{
							// there are messages that were not indexed successfully
							// => evaluate the the response
							var bulkResponseItems = bulkResponse.Items;
							for (int i = bulkResponseItems.Length - 1; i >= 0; i--)
							{
								int status = bulkResponseItems[i].Index.Status;
								if (status >= 200 && status <= 299) // usually 201 (created)
								{
									// message was indexed successfully
									mMessagesPreparedToSend[i].Release();
									mMessagesPreparedToSend.RemoveAt(i);
								}
								else if (status == 429) // rate limit
								{
									// the server is under heavy load
									// => keep message and try again later
									WritePipelineWarning("Elasticsearch server seems to be overloaded (responded with 429). Trying again later...");
								}
								else
								{
									// indexing failed (no chance to solve this issue automatically)
									// => log error and discard message
									WritePipelineError(
										$"Elasticsearch endpoint ({request.RequestUri}) responded with {status}. Discarding message to index..." +
										Environment.NewLine +
										$"Error: {bulkResponseItems[i].Index.Error}",
										null);

									mMessagesPreparedToSend[i].Release();
									mMessagesPreparedToSend.RemoveAt(i);
								}
							}
						}
						else
						{
							// all messages were indexed successfully
							int count = bulkResponse.Items.Length;
							for (int i = 0; i < count; i++) mMessagesPreparedToSend[i].Release();
							mMessagesPreparedToSend.RemoveRange(0, count);
						}

						// request was send to the Elasticsearch cluster
						// => the endpoint can be considered operational...
						SetEndpointOperational(endpoint, true);
						break;
					}

					// the bulk request failed entirely which is a severe condition
					// => the endpoint is not operational...
					SetEndpointOperational(endpoint, false);

					// log incident...
					WritePipelineError(
						$"The request to endpoint {request.RequestUri} failed with error code {(int)response.StatusCode} ({response.ReasonPhrase}).",
						null);
				}
				catch (HttpRequestException ex)
				{
					// an error that occurred transporting the request to the elasticsearch server -or-
					// a timeout occurred (only .NET Framework)
					// => log and keep messages
					WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({request.RequestUri}) failed.", ex);
					SetEndpointOperational(endpoint, false);
				}
				catch (OperationCanceledException ex)
				{
					// the cancellation token was signaled -or-
					// a timeout occurred (only .NET Core, .NET 5.0 or higher)
					if (ex.CancellationToken == cancellationToken) throw;
					WritePipelineError($"Sending HTTP request to Elasticsearch endpoint ({request.RequestUri}) failed.", ex);
					SetEndpointOperational(endpoint, false);
				}
			}

			return 0;
		}

		/// <summary>
		/// Sets the operational state of the specified endpoint.
		/// </summary>
		/// <param name="endpoint">Endpoint to set.</param>
		/// <param name="isOperational">
		/// <c>true</c> if the endpoint is operational;
		/// otherwise <c>false</c>.
		/// </param>
		private void SetEndpointOperational(EndpointInfo endpoint, bool isOperational)
		{
			endpoint.IsOperational = isOperational;
			mIsOperational = mEndpoints.Any(x => x.IsOperational);

			if (isOperational)
			{
				// endpoint is operational
				// => move it to the head of the list
				if (mEndpoints[0] != endpoint)
				{
					mEndpoints.Remove(endpoint);
					mEndpoints.AddToFront(endpoint);
				}
			}
			else
			{
				// endpoint is not operational
				// => move it to the tail of the list
				if (mEndpoints[mEndpoints.Count - 1] != endpoint)
				{
					mEndpoints.Remove(endpoint);
					mEndpoints.AddToBack(endpoint);
				}
			}
		}

		#endregion

		#region Checking Connection is Operational

		/// <summary>
		/// Gets a value indicating whether the connection to the Elasticsearch cluster is operational.
		/// </summary>
		public bool IsOperational => mIsOperational;

		#endregion
	}

}
