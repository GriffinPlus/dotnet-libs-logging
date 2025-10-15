///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using GriffinPlus.Lib.Collections;
using GriffinPlus.Lib.Threading;

// ReSharper disable ConvertToConstant.Local
// ReSharper disable ForCanBeConvertedToForeach

namespace GriffinPlus.Lib.Logging.Elasticsearch;

/// <summary>
/// A log message processing pipeline stage that forwards log messages to an Elasticsearch cluster.
/// </summary>
public sealed partial class ElasticsearchPipelineStage : SyncProcessingPipelineStage
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
	/// Minimum size of a bulk request (in bytes).
	/// </summary>
	private const int MinimumBulkRequestSize = 10 * 1024;

	/// <summary>
	/// Maximum size of a bulk request (in bytes).
	/// </summary>
	private const int MaximumBulkRequestSize = 100 * 1024 * 1024;

	/// <summary>
	/// The fully qualified domain name of the local computer.
	/// </summary>
	private static readonly string sHostname;

	/// <summary>
	/// The processing queue, passes messages from the logging thread to the processing thread.
	/// Synchronized via monitor using itself.
	/// </summary>
	private readonly Deque<LocalLogMessage> mProcessingQueue = [];

	/// <summary>
	/// Event that is signaled to trigger the processing thread.
	/// </summary>
	private readonly ManualResetEventSlim mProcessingNeededEvent = new();

	/// <summary>
	/// Indicates whether the stage discards messages, if the queue is full (default).
	/// This should only be disabled for testing purposes as blocking for sending a log message is usually not desirable.
	/// Synchronized via monitor using <see cref="mProcessingQueue"/>.
	/// </summary>
	private bool mDiscardMessagesIfQueueFull = true;

	/// <summary>
	/// The maximum number of messages in <see cref="mProcessingQueue"/>.
	/// </summary>
	private int mProcessingQueueSize = 0;

	/// <summary>
	/// Indicates whether the stage is operational.
	/// </summary>
	private volatile bool mIsOperational = false;

	/// <summary>
	/// The processing thread.
	/// </summary>
	private Thread mProcessingThread = null;

	/// <summary>
	/// Cancellation token source that is signaled when the stage is shutting down.
	/// </summary>
	private CancellationTokenSource mShutdownCancellationTokenSource = null;

	/// <summary>
	/// Initializes the <see cref="ElasticsearchPipelineStage"/> class.
	/// </summary>
	static ElasticsearchPipelineStage()
	{
		// try to determine the fully qualified domain name of the computer
		try
		{
			sHostname = Dns.GetHostEntry("").HostName;
		}
		catch (Exception)
		{
			// swallow...
			Debug.Fail("Determining the hostname of the local computer failed.");
		}
	}

	// members managed by the processing thread
	private volatile bool                 mReloadConfiguration            = true;          // tells the processing thread to reload the configuration
	private volatile bool                 mIsShutdownRequested            = false;         // tells the processing thread to shut down
	private readonly Deque<EndpointInfo>  mEndpoints                      = [];            // elasticsearch endpoints to send requests to
	private readonly BulkResponsePool     mBulkResponsePool               = new();         // a pool of objects used when deserializing bulk responses
	private readonly Deque<SendOperation> mFreeSendOperations             = [];            // free send operations
	private readonly Deque<SendOperation> mScheduledSendOperations        = [];            // send operations ready for sending
	private readonly List<SendOperation>  mPendingSendOperations          = [];            // send operations on the line
	private          HttpClient           mHttpClient                     = null;          // http client to use when sending requests to elasticsearch
	private          string               mIndexName                      = null;          // caches IndexName
	private          int                  mBulkRequestMaxConcurrencyLevel = 0;             // caches BulkRequestMaxConcurrencyLevel
	private          int                  mBulkRequestMaxSize             = 0;             // caches BulkRequestMaxSize
	private          int                  mBulkRequestMaxMessageCount     = 0;             // caches BulkRequestMaxMessageCount
	private          string               mOrganizationId                 = null;          // caches OrganizationId
	private          string               mOrganizationName               = null;          // caches OrganizationName
	private          TimeSpan             mLastTimezoneOffset             = TimeSpan.Zero; // Timezone formatted into mLastTimezoneOffsetAsString
	private          string               mLastTimezoneOffsetAsString     = "+00:00";      // caches the timezone in its string representation

	// defaults of settings determining the behavior of the stage
	private static readonly Uri[]                sDefault_ApiBaseUrls                            = [new("http://127.0.0.1:9200/")];
	private static readonly AuthenticationScheme sDefault_Server_Authentication_Schemes          = AuthenticationScheme.PasswordBased;
	private static readonly string               sDefault_Server_Authentication_Username         = "";
	private static readonly string               sDefault_Server_Authentication_Password         = "";
	private static readonly string               sDefault_Server_Authentication_Domain           = "";
	private static readonly int                  sDefault_Server_BulkRequest_MaxConcurrencyLevel = 5;
	private static readonly int                  sDefault_Server_BulkRequest_MaxMessageCount     = 0; // unlimited
	private static readonly int                  sDefault_Server_BulkRequest_MaxSize             = 5 * 1024 * 1024;
	private static readonly string               sDefault_Server_IndexName                       = "logs";
	private static readonly string               sDefault_Data_Organization_Id                   = "";
	private static readonly string               sDefault_Data_Organization_Name                 = "";
	private static readonly int                  sDefault_Stage_SendQueueSize                    = 50000;

	// the settings determining the behavior of the stage
	private readonly IProcessingPipelineStageSetting<Uri[]>                mSetting_Server_ApiBaseUrls;
	private readonly IProcessingPipelineStageSetting<AuthenticationScheme> mSetting_Server_Authentication_Schemes;
	private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_Authentication_Username;
	private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_Authentication_Password;
	private readonly IProcessingPipelineStageSetting<string>               mSetting_Server_Authentication_Domain;
	private readonly IProcessingPipelineStageSetting<int>                  mSetting_Server_BulkRequest_MaxConcurrencyLevel;
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
	public ElasticsearchPipelineStage()
	{
		mSetting_Server_ApiBaseUrls = RegisterSetting("Server.ApiBaseUrls", sDefault_ApiBaseUrls, UriArrayToString, StringToUriArray);
		mSetting_Server_Authentication_Schemes = RegisterSetting("Server.Authentication.Schemes", sDefault_Server_Authentication_Schemes);
		mSetting_Server_Authentication_Username = RegisterSetting("Server.Authentication.Username", sDefault_Server_Authentication_Username);
		mSetting_Server_Authentication_Password = RegisterSetting("Server.Authentication.Password", sDefault_Server_Authentication_Password);
		mSetting_Server_Authentication_Domain = RegisterSetting("Server.Authentication.Domain", sDefault_Server_Authentication_Domain);
		mSetting_Server_BulkRequest_MaxConcurrencyLevel = RegisterSetting("Server.BulkRequest.MaxConcurrencyLevel", sDefault_Server_BulkRequest_MaxConcurrencyLevel);
		mSetting_Server_BulkRequest_MaxMessageCount = RegisterSetting("Server.BulkRequest.MaxMessageCount", sDefault_Server_BulkRequest_MaxMessageCount);
		mSetting_Server_BulkRequest_MaxSize = RegisterSetting("Server.BulkRequest.MaxSize", sDefault_Server_BulkRequest_MaxSize);
		mSetting_Server_IndexName = RegisterSetting("Server.IndexName", sDefault_Server_IndexName);
		mSetting_Data_Organization_Id = RegisterSetting("Data.Organization.Id", sDefault_Data_Organization_Id);
		mSetting_Organization_Name = RegisterSetting("Data.Organization.Name", sDefault_Data_Organization_Name);
		mSetting_Stage_SendQueueSize = RegisterSetting("Stage.SendQueueSize", sDefault_Stage_SendQueueSize);
		mProcessingQueueSize = mSetting_Stage_SendQueueSize.Value;
	}

	#region Stage Settings (Backed by Configuration)

	#region ApiBaseUrls

	/// <summary>
	/// Gets the API endpoints of the Elasticsearch cluster.
	/// </summary>
	/// <exception cref="ArgumentNullException">The property is set and the specified list of endpoints is <see langword="null"/>.</exception>
	public IReadOnlyList<Uri> ApiBaseUrls
	{
		get => mSetting_Server_ApiBaseUrls.Value;
		set => mSetting_Server_ApiBaseUrls.Value = value.ToArray();
	}

	/// <summary>
	/// Converts an array of <see cref="Uri"/> to a string as used in the configuration.
	/// </summary>
	/// <param name="uris">Array of <see cref="Uri"/> to convert to a string.</param>
	/// <param name="provider">Format provider to use (<see langword="null"/> to use <see cref="CultureInfo.InvariantCulture"/>).</param>
	/// <returns>The formatted array of <see cref="Uri"/>.</returns>
	private static string UriArrayToString(Uri[] uris, IFormatProvider provider = null)
	{
		return uris != null ? string.Join("; ", uris.Select(x => x.ToString())) : "";
	}

	/// <summary>
	/// Converts a string to an array of <see cref="Uri"/>.
	/// The string is expected to contain the uris separated by semicolons.
	/// </summary>
	/// <param name="s">String to convert to an array of <see cref="Uri"/>.</param>
	/// <param name="provider">Format provider to use (<see langword="null"/> to use <see cref="CultureInfo.InvariantCulture"/>).</param>
	/// <returns>An array of <see cref="Uri"/> corresponding to the specified string.</returns>
	private static Uri[] StringToUriArray(string s, IFormatProvider provider = null)
	{
		return (
			       from endpointToken in s.Trim().Split(';')
			       select endpointToken.Trim() into apiEndpointString
			       where apiEndpointString.Length > 0
			       select new Uri(apiEndpointString)).ToArray();
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

	#region BulkRequestMaxConcurrencyLevel

	/// <summary>
	/// Gets or sets the maximum number of bulk requests that may are on the line at the same time.
	/// There is always one request on the line. More requests are sent, if there are enough messages to fill them up entirely.
	/// Default: 5
	/// </summary>
	public int BulkRequestMaxConcurrencyLevel
	{
		get => mSetting_Server_BulkRequest_MaxConcurrencyLevel.Value;
		set => mSetting_Server_BulkRequest_MaxConcurrencyLevel.Value = value;
	}

	#endregion

	#region BulkRequestMaxMessageCount

	/// <summary>
	/// Gets or sets the maximum number of messages to bundle in a bulk request.
	/// Default: 0 (unlimited)
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
	/// Default: 5242880 bytes (5 MiB)
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
	/// Default: &lt;empty&gt;
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
	/// Default: &lt;empty&gt;
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

	#endregion

	#region Stage Settings (Not Backed by Configuration)

	#region DiscardMessagesIfQueueFull

	/// <summary>
	/// Indicates whether the stage discards messages, if the queue is full (default).
	/// This should only be disabled for testing purposes as blocking for sending a log message is usually not desirable.
	/// </summary>
	public bool DiscardMessagesIfQueueFull
	{
		get
		{
			lock (mProcessingQueue) return mDiscardMessagesIfQueueFull;
		}
		set
		{
			lock (mProcessingQueue) mDiscardMessagesIfQueueFull = value;
		}
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
		Debug.Assert(mProcessingThread == null);

		mShutdownCancellationTokenSource = new CancellationTokenSource();
		mReloadConfiguration = true;
		mIsShutdownRequested = false;
		mIsOperational = false;
		mProcessingThread = new Thread(ProcessingThreadProc) { Name = "Elasticsearch Pipeline Stage Processing Thread", IsBackground = true };
		mProcessingThread.Start(mShutdownCancellationTokenSource.Token);
	}

	/// <summary>
	/// Shuts the pipeline stage down when the stage is detached from the logging system.
	/// The method might also be called to clean up a failed initialization.
	/// </summary>
	protected override void OnShutdown()
	{
		Debug.Assert(Monitor.IsEntered(Sync));
		Debug.Assert(mShutdownCancellationTokenSource != null);
		Debug.Assert(!mShutdownCancellationTokenSource.IsCancellationRequested);
		Debug.Assert(mProcessingThread != null);

		try
		{
			// tell the processing thread to shut down
			mIsShutdownRequested = true;

			// cancel waiting for the next processing loop in the processing thread
			mShutdownCancellationTokenSource?.Cancel();

			// release processing thread, so it can terminate, if appropriate
			mProcessingNeededEvent?.Set();

			// wait for the processing thread to complete
			mProcessingThread?.Join();
		}
		finally
		{
			mShutdownCancellationTokenSource?.Dispose();
			mShutdownCancellationTokenSource = null;
			mProcessingThread = null;
		}

		// discard messages that are still in the processing queue
		lock (mProcessingQueue)
		{
			while (mProcessingQueue.Count > 0)
			{
				mProcessingQueue.RemoveFromFront().Release();
			}
		}
	}

	/// <summary>
	/// Processes pending changes to registered setting proxies (the method is executed by a worker thread).
	/// </summary>
	/// <param name="settings">Settings that have changed.</param>
	protected override void OnSettingsChanged(IUntypedProcessingPipelineStageSetting[] settings)
	{
		// reload the maximum number of messages in the processing queue
		lock (mProcessingQueue)
		{
			mProcessingQueueSize = mSetting_Stage_SendQueueSize.Value;
			if (mProcessingQueueSize < 1) mProcessingQueueSize = 1;
		}

		// tell the process thread to reload its configuration
		mReloadConfiguration = true;
	}

	#endregion

	#region Processing

	/// <summary>
	/// Processes a log message synchronously.
	/// </summary>
	/// <param name="message">Message to process.</param>
	/// <returns>
	/// Always <see langword="true"/>, so the message is passed to following stages.
	/// </returns>
	protected override bool ProcessSync(LocalLogMessage message)
	{
		Debug.Assert(Monitor.IsEntered(Sync));

		while (true)
		{
			lock (mProcessingQueue)
			{
				// enqueue message for sending, if there is space in the queue
				if (mProcessingQueue.Count < mProcessingQueueSize)
				{
					message.AddRef();
					mProcessingQueue.AddToBack(message);
					break;
				}

				// the queue is full
				// => discard the message, if configured (default)
				if (mDiscardMessagesIfQueueFull)
					return true;
			}

			// the queue is full
			// => wait some time before trying to enqueue it again
			Thread.Sleep(10);
		}

		// the message was enqueued successfully
		// => trigger processing
		TriggerProcessing();
		return true;
	}

	/// <summary>
	/// Triggers the processing thread that send messages to the Elasticsearch cluster.
	/// </summary>
	private void TriggerProcessing()
	{
		if (!mProcessingNeededEvent.IsSet)
			mProcessingNeededEvent.Set();
	}

	/// <summary>
	/// The entry point of the processing thread that bundles messages and sends them to the Elasticsearch cluster.
	/// </summary>
	/// <param name="obj">The cancellation token that is signaled when the stage has exceeded the time to shut down gracefully.</param>
	private void ProcessingThreadProc(object obj)
	{
		var threadCancellationToken = (CancellationToken)obj;
		var operationCancellationTokenSource = new CancellationTokenSource();
		CancellationToken operationCancellationToken = operationCancellationTokenSource.Token;
		int forcedWait = 0;

		// load the configuration and create endpoints before waiting for work the first time
		// (endpoints are necessary in the OperationCancelledException handler below to decide whether
		// to delay the shutdown until one endpoint is operational or all endpoints have failed to connect)
		ReloadConfigurationIfNecessary();

		try
		{
			while (true)
			{
				try
				{
					// wait for something to do
					// --------------------------------------------------------------------------------------------------------------
					if (forcedWait == 0)
					{
						mProcessingNeededEvent.Wait(threadCancellationToken);
						mProcessingNeededEvent.Reset();
					}
					else
					{
						Task.Delay(forcedWait, threadCancellationToken).WaitAndUnwrapException(threadCancellationToken);
						forcedWait = 0;
					}

					// reload configuration and prepare the http client, if necessary
					// (ensure there are no send pending operations to avoid mixing up send operations)
					// --------------------------------------------------------------------------------------------------------------
					if (mReloadConfiguration && mScheduledSendOperations.Count == 0 && mPendingSendOperations.Count == 0)
						ReloadConfigurationIfNecessary();

					// abort, if there are no endpoints specified
					// --------------------------------------------------------------------------------------------------------------
					if (mEndpoints.Count == 0) continue;

					// process completed send operations
					// --------------------------------------------------------------------------------------------------------------
					for (int i = 0; i < mPendingSendOperations.Count; i++)
					{
						SendOperation operation = mPendingSendOperations[i];

						if (operation.HasCompletedSending)
						{
							// the operation has completed
							mPendingSendOperations.RemoveAt(i--);

							if (operation.ProcessSendCompleted())
							{
								// the endpoint is operational
								// (elasticsearch processed the request)
								SetEndpointOperational(operation.Endpoint, true);

								if (operation.MessageCount == 0)
								{
									// the request was processed entirely
									// => prepare operation for re-use
									mFreeSendOperations.AddToBack(operation);
								}
								else
								{
									// there are still messages left (most probably due to an overload condition)
									// => enqueue send operation once again to send remaining messages
									mScheduledSendOperations.AddToFront(operation); // old messages must be sent first!
								}
							}
							else
							{
								// the endpoint is not operational
								// => try to send the entire request once again using some other endpoint...
								SetEndpointOperational(operation.Endpoint, false);
								mScheduledSendOperations.AddToFront(operation); // old messages must be sent first!
							}
						}
					}

					// return to waiting, if the currently selected endpoint failed recently (calms the processing loop down, if no endpoint is operational)
					// (the best choice is always at the beginning of the endpoint list, non-operational endpoints are put to the end of the list)
					// --------------------------------------------------------------------------------------------------------------
					EndpointInfo endpoint = mEndpoints[0];
					int ticks = Environment.TickCount;
					if (!endpoint.IsOperational && ticks - endpoint.ErrorTickCount < RetryEndpointAfterErrorTimeMs && !mIsShutdownRequested)
					{
						forcedWait = RetryEndpointAfterErrorTimeMs - ticks + endpoint.ErrorTickCount;
						continue;
					}

					if (!mReloadConfiguration)
					{
						// add messages to send operations (free => scheduled)
						// --------------------------------------------------------------------------------------------------------------
						// Try to optimize for different load scenarios:
						// - low load (not enough messages to fill a request):
						//   => send only one request at a time (may not be full)
						//   => latency is determined by the roundtrip time of a bulk request
						// - high load (enough messages to fill a request):
						//   => send multiple full requests concurrently
						//   => throughput is optimized by sending concurrently
						while (mFreeSendOperations.Count > 0)
						{
							// get the first available send operation from the list
							// (may already contain messages)
							SendOperation operation = mFreeSendOperations[0];

							// add more messages to the request, if it is not full, yet
							while (true)
							{
								LocalLogMessage message = DequeueMessage();
								if (message == null) break;
								if (!operation.AddMessage(message))
								{
									PushMessageBack(message);
									break;
								}
							}

							// abort, if the request buffer does not contain any messages or
							// the request is not full and there is no request scheduled or already on the line
							if (operation.MessageCount == 0 || (!operation.IsFull && mScheduledSendOperations.Count + mPendingSendOperations.Count != 0))
								break;

							// schedule the send operation
							mFreeSendOperations.RemoveFromFront();
							mScheduledSendOperations.AddToBack(operation);
						}
					}

					// start scheduled send operations
					// --------------------------------------------------------------------------------------------------------------
					if (endpoint.IsOperational || !endpoint.HasTriedToConnect || Environment.TickCount - endpoint.ErrorTickCount >= RetryEndpointAfterErrorTimeMs)
					{
						while (mScheduledSendOperations.Count > 0 && mPendingSendOperations.Count < mBulkRequestMaxConcurrencyLevel)
						{
							// abort if the endpoint should be tested with a single request only before sending multiple requests concurrently
							if (endpoint.ProbingConnection && mPendingSendOperations.Count > 0)
								break;

							SendOperation operation = mScheduledSendOperations[0];

							// start sending the request
							if (operation.StartSending(endpoint, operationCancellationToken))
							{
								// the send operation was started successfully
								// => track operation in the 'in progress' list
								operation = mScheduledSendOperations.RemoveFromFront();
								mPendingSendOperations.Add(operation);
							}
							else
							{
								// the send operation could not be started
								// => the endpoint seems to be inoperable
								// (moves the endpoint to the end of the endpoint list, so the next attempt will use another endpoint)
								SetEndpointOperational(endpoint, false);
								break;
							}
						}
					}

					// abort, if shutdown is requested and no send operations are pending
					// --------------------------------------------------------------------------------------------------------------
					if (!mReloadConfiguration && mPendingSendOperations.Count == 0 && mIsShutdownRequested)
						break;
				}
				catch (OperationCanceledException ex)
				{
					// rethrow to terminate the thread, if the operation cancellation token has been signaled
					if (ex.CancellationToken == operationCancellationToken)
						throw;

					// the thread cancellation token should be the token that has been triggered
					Debug.Assert(ex.CancellationToken == threadCancellationToken);
					Debug.Assert(mIsShutdownRequested);

					// abort the thread only, if all endpoints have been tried at least once and no endpoint is operational
					// (otherwise messages might get lost in short running applications)
					if (mEndpoints.All(x => x.HasTriedToConnect && !x.IsOperational))
						throw;

					// keep the loop running to send buffered messages
					operationCancellationTokenSource.CancelAfter(MaxProcessingOverrunTimeMs);
					threadCancellationToken = operationCancellationToken;
					TriggerProcessing();
				}
				catch (Exception ex)
				{
					WritePipelineError("Unhandled exception in ProcessingThreadProc().", ex);
				}
			}
		}
		catch (OperationCanceledException)
		{
			// put scheduled send operations back into the list of free send operations...
			foreach (SendOperation operation in mScheduledSendOperations)
			{
				operation.Reset();
				mFreeSendOperations.AddToBack(operation);
			}

			// put pending send operations back into the list of free send operations...
			foreach (SendOperation operation in mPendingSendOperations)
			{
				operation.Reset();
				mFreeSendOperations.AddToBack(operation);
			}

			// ... and reset send operations
			foreach (SendOperation operation in mFreeSendOperations) operation.Reset();
		}
		finally
		{
			operationCancellationTokenSource?.Dispose();
		}
	}

	/// <summary>
	/// Reloads the configuration and populates processing thread specific data.
	/// </summary>
	private void ReloadConfigurationIfNecessary()
	{
		if (mReloadConfiguration)
		{
			// reset reload indicator to notice changes occur just jiffy later
			mReloadConfiguration = false;

			// recreate the http client to take potential changes to authentication settings into account
			RecreateHttpClient();

			// reload bulk request settings
			mIndexName = IndexName;
			mBulkRequestMaxConcurrencyLevel = BulkRequestMaxConcurrencyLevel;
			mBulkRequestMaxMessageCount = BulkRequestMaxMessageCount;
			mBulkRequestMaxSize = BulkRequestMaxSize;

			// reload fixed fields
			// (set to null, if the string is empty to omit these fields when generating requests)
			mOrganizationId = string.IsNullOrWhiteSpace(OrganizationId) ? null : OrganizationId;
			mOrganizationName = string.IsNullOrWhiteSpace(OrganizationName) ? null : OrganizationName;

			// adjust settings
			mBulkRequestMaxConcurrencyLevel = mBulkRequestMaxConcurrencyLevel > 1 ? mBulkRequestMaxConcurrencyLevel : 1;
			mBulkRequestMaxMessageCount = mBulkRequestMaxMessageCount > 0 ? mBulkRequestMaxMessageCount : int.MaxValue;
			mBulkRequestMaxSize = mBulkRequestMaxSize >= MinimumBulkRequestSize ? mBulkRequestMaxSize : MinimumBulkRequestSize;
			mBulkRequestMaxSize = mBulkRequestMaxSize <= MaximumBulkRequestSize ? mBulkRequestMaxSize : MaximumBulkRequestSize;

			// rebuild request URLs
			mEndpoints.Clear();
			foreach (Uri apiBaseUrl in ApiBaseUrls) mEndpoints.AddToBack(new EndpointInfo(apiBaseUrl));
			mIsOperational = mEndpoints.Any(x => x.IsOperational);

			// initialize send operations (requests are prepared while other requests are on the line, so the number
			// of send operations must be one element greater than the number of concurrent send operations)
			int sendOperationCount = mBulkRequestMaxConcurrencyLevel + 1;
			Debug.Assert(mScheduledSendOperations.Count == 0);
			Debug.Assert(mPendingSendOperations.Count == 0);
			if (mFreeSendOperations.Count > sendOperationCount)
			{
				// too many send operations
				// => remove operations from the back of the list
				//    (the first operation may still contain messages to send)
				while (mFreeSendOperations.Count > sendOperationCount)
				{
					mFreeSendOperations.RemoveFromBack();
				}
			}
			else if (mFreeSendOperations.Count < sendOperationCount)
			{
				// too less send operations
				// => add missing operations to the back of the list
				//    (the first operation may still contain messages to send)
				for (int i = mFreeSendOperations.Count; i < sendOperationCount; i++)
				{
					mFreeSendOperations.AddToBack(new SendOperation(this));
				}
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
		AuthenticationScheme authTypes = AuthenticationSchemes;
		if (authTypes != AuthenticationScheme.None)
		{
			bool useCustomCredentials = !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
			NetworkCredential credentials = useCustomCredentials
				                                ? new NetworkCredential(Username, Password, Domain)
				                                : CredentialCache.DefaultNetworkCredentials;

			var cache = new CredentialCache();
			foreach (Uri uriPrefix in ApiBaseUrls)
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
	/// Dequeues a message to process.
	/// </summary>
	/// <returns>A message to process (<see langword="null"/> if the queue is empty).</returns>
	private LocalLogMessage DequeueMessage()
	{
		lock (mProcessingQueue)
		{
			return mProcessingQueue.Count > 0
				       ? mProcessingQueue.RemoveFromFront()
				       : null;
		}
	}

	/// <summary>
	/// Pushes the specified message back into the processing queue.
	/// </summary>
	/// <param name="message">Message to push back into the processing queue.</param>
	private void PushMessageBack(LocalLogMessage message)
	{
		lock (mProcessingQueue)
		{
			mProcessingQueue.AddToFront(message);
		}
	}

	/// <summary>
	/// Sets the operational state of the specified endpoint.
	/// </summary>
	/// <param name="endpoint">Endpoint to set.</param>
	/// <param name="isOperational">
	/// <see langword="true"/> if the endpoint is operational;<br/>
	/// otherwise, <see langword="false"/>.
	/// </param>
	private void SetEndpointOperational(EndpointInfo endpoint, bool isOperational)
	{
		endpoint.HasTriedToConnect = true;
		endpoint.IsOperational = isOperational;
		endpoint.ProbingConnection = !isOperational;
		mIsOperational = mEndpoints.Any(x => x.IsOperational);

		if (isOperational)
		{
			// endpoint is operational
			// => move it to the head of the list
			if (mEndpoints[0] != endpoint)
			{
				if (mEndpoints.Remove(endpoint))
				{
					mEndpoints.AddToFront(endpoint);
				}
			}
		}
		else
		{
			// endpoint is not operational
			// => move it to the tail of the list
			if (mEndpoints[^1] != endpoint)
			{
				if (mEndpoints.Remove(endpoint))
				{
					mEndpoints.AddToBack(endpoint);
				}
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

	#region Writing Bulk Request

	/// <summary>
	/// Writes a bulk request action for indexing a document.
	/// </summary>
	/// <param name="writer">JSON writer to use.</param>
	private void WriteBulkRequestIndexAction_ECS110(Utf8JsonWriter writer)
	{
		// write the request action document
		writer.WriteStartObject();
		writer.WriteStartObject("create");
		writer.WriteString("_index", mIndexName);
		writer.WriteEndObject();
		writer.WriteEndObject();

		// flush the writer
		writer.Flush();
	}

	/// <summary>
	/// Writes the specified log message to a JSON document complying with ECS version 1.10.
	/// </summary>
	/// <param name="writer">JSON writer to use.</param>
	/// <param name="message">Message to write.</param>
	private void WriteJsonMessage_ECS110(Utf8JsonWriter writer, LocalLogMessage message)
	{
		// determine the event severity and the name of the log level to use in the written JSON document
		GetEcsLevelAndSeverity(message, out int ecsEventSeverity, out string ecsLogLevel);

		// start a new object
		writer.WriteStartObject();

		// Path: @timestamp
		// Type: date
		// ------------------------------------------------------------------------------------------------------------------
		// Date/time when the event originated.
		// This is the date/time extracted from the event, typically representing when the event was generated by the source.
		// If the event source has no original timestamp, this value is typically populated by the first time the event was
		// received by the pipeline. Required field for all events.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-base.html#field-timestamp
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("@timestamp", message.Timestamp.UtcDateTime);

		// Path: event
		// Type: object
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteStartObject("event");

		// Path: event.severity
		// Type: long
		// ------------------------------------------------------------------------------------------------------------------
		// The numeric severity of the event according to your event source.
		// What the different severity values mean can be different between sources and use cases.
		// It’s up to the implementer to make sure severities are consistent across events from the same source.
		// The syslog severity belongs in log.syslog.severity.code. event.severity is meant to represent the severity
		// according to the event source (e.g.firewall, IDS). If the event source does not publish its own severity,
		// you may optionally copy the log.syslog.severity.code to event.severity.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-event.html#field-event-severity
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteNumber("severity", ecsEventSeverity);

		// Path: event.timezone
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// This field should be populated when the event’s timestamp does not include timezone information already
		// (e.g. default Syslog timestamps). It’s optional otherwise. Acceptable timezone formats are:
		// a canonical ID(e.g. "Europe/Amsterdam"), abbreviated(e.g. "EST") or an HH:mm differential(e.g. "-05:00").
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-event.html#field-event-timezone
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("timezone", ToTimezoneOffset(message.Timestamp.Offset));

		// end of the 'event' field
		writer.WriteEndObject();

		// Path: host
		// Type: object
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteStartObject("host");

		// Path: host.hostname
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// Hostname of the host.
		// It normally contains what the hostname command returns on the host machine.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-host.html#field-host-hostname
		// ------------------------------------------------------------------------------------------------------------------
		if (sHostname != null) writer.WriteString("hostname", sHostname);

		// Path: host.TicksNs
		// Type: long
		// ------------------------------------------------------------------------------------------------------------------
		// Tick counter of the host (custom field, shared by processes on the host, in ns).
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteNumber("TicksNs", message.HighPrecisionTimestamp);

		// end of the 'host' field
		writer.WriteEndObject();

		// Path: log
		// Type: object
		// ------------------------------------------------------------------------------------------------------------------
		// Details about the event’s logging mechanism or logging transport.
		// The log.* fields are typically populated with details about the logging mechanism used to create and/or transport
		// the event. For example, syslog details belong under log.syslog.*. The details specific to your event source are
		// typically not logged under log.*, but rather in event.* or in other ECS fields.
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteStartObject("log");

		// Path: log.level
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// Original log level of the log event.
		// If the source of the event provides a log level or textual severity, this is the one that goes in log.level.
		// If your source doesn't specify one, you may put your event transport ’s severity here (e.g.Syslog severity).
		// Some examples are warn, err, i, informational.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-log.html#field-log-level
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("level", ecsLogLevel);

		// Path: log.logger
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// The name of the logger inside an application.
		// This is usually the name of the class which initialized the logger, or can be a custom name.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-log.html#field-log-logger
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("logger", message.LogWriter.Name);

		// end of the 'log' field
		writer.WriteEndObject();

		// Path: message
		// Type: text
		// ------------------------------------------------------------------------------------------------------------------
		// For log events the message field contains the log message, optimized for viewing in a log viewer.
		// For structured logs without an original message field, other fields can be concatenated to form a human-readable
		// summary of the event. If multiple messages exist, they can be combined into one message.
		// ------------------------------------------------------------------------------------------------------------------
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-base.html#field-message
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("message", message.Text);

		// Path: organization
		// Type: object
		// ------------------------------------------------------------------------------------------------------------------
		if (mOrganizationId != null || mOrganizationName != null)
		{
			writer.WriteStartObject("organization");

			// Path: organization.id
			// Type: keyword
			// ------------------------------------------------------------------------------------------------------------------
			// Unique identifier for the organization.
			// See: https://www.elastic.co/guide/en/ecs/current/ecs-organization.html#field-organization-id
			// ------------------------------------------------------------------------------------------------------------------
			if (mOrganizationId != null) writer.WriteString("id", mOrganizationId);

			// Path: organization.name
			// Type: keyword
			// ------------------------------------------------------------------------------------------------------------------
			// Organization name.
			// See: https://www.elastic.co/guide/en/ecs/current/ecs-organization.html#field-organization-name
			// ------------------------------------------------------------------------------------------------------------------
			if (mOrganizationName != null) writer.WriteString("name", mOrganizationName);

			// end of the 'organization' field
			writer.WriteEndObject();
		}

		// Path: process
		// Type: object
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteStartObject("process");

		// Path: process.name
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// Process name.
		// Sometimes called program name or similar.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-process.html#field-process-name
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("name", message.ProcessName);

		// Path: process.pid
		// Type: long
		// ------------------------------------------------------------------------------------------------------------------
		// Process id.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-process.html#field-process-pid
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteNumber("pid", message.ProcessId);

		// Path: process.title
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// Process title.
		// The process title is sometimes the same as process name.
		// Can also be different: for example a browser setting its title to the web page currently opened.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-process.html#field-process-title
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("title", message.ApplicationName);

		// end of the 'process' field
		writer.WriteEndObject();

		// Path: tags
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// List of keywords used to tag each event.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-base.html#field-tags
		// ------------------------------------------------------------------------------------------------------------------
		LogWriterTagSet tags = message.Tags;
		int tagsCount = tags.Count;
		if (tagsCount > 0)
		{
			writer.WritePropertyName("tags");
			writer.WriteStartArray();
			for (int i = 0; i < tagsCount; i++) writer.WriteStringValue(tags[i].Name);
			writer.WriteEndArray();
		}

		// Path: ecs
		// Type: object
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteStartObject("ecs");

		// Path: ecs.version
		// Type: keyword
		// ------------------------------------------------------------------------------------------------------------------
		// ECS version this event conforms to. ecs.version is a required field and must exist in all events.
		// When querying across multiple indices — which may conform to slightly different ECS versions — this field lets
		// integrations adjust to the schema version of the events.
		// See: https://www.elastic.co/guide/en/ecs/1.10/ecs-ecs.html
		// ------------------------------------------------------------------------------------------------------------------
		writer.WriteString("version", "1.10.0");

		// end of the 'ecs' field
		writer.WriteEndObject();

		// close the top-level object
		writer.WriteEndObject();

		// flush the writer
		writer.Flush();
	}

	/// <summary>
	/// Converts the specified timezone offset to a string in the format '-hh:mm' (negative timezone offsets)
	/// respectively '+hh:mm' (positive timezone offsets).
	/// </summary>
	/// <param name="offset">Timezone offset to format.</param>
	/// <returns>The formatted timezone offset.</returns>
	private string ToTimezoneOffset(TimeSpan offset)
	{
		if (mLastTimezoneOffset == offset) return mLastTimezoneOffsetAsString;
		mLastTimezoneOffset = offset;
		mLastTimezoneOffsetAsString = offset.Ticks >= 0
			                              ? (+offset).ToString(@"\+hh\:mm")
			                              : (-offset).ToString(@"\-hh\:mm");
		return mLastTimezoneOffsetAsString;
	}

	/// <summary>
	/// Gets the severity level to put into ECS field 'event.severity' and the name of the log level
	/// to put into ECS field 'log.level'.
	/// </summary>
	/// <param name="message">Message containing to retrieve the severity id and the log level name from.</param>
	/// <param name="ecsEventSeverity">Receives the event severity to put into ECS field 'event.severity'.</param>
	/// <param name="ecsLogLevel">Receives the log level to put into ECS field 'log.level'.</param>
	private static void GetEcsLevelAndSeverity(
		LocalLogMessage message,
		out int         ecsEventSeverity,
		out string      ecsLogLevel)
	{
		// initialize the event severity (the log level id complies with the syslog level ids)
		// and the name of the log level
		ecsEventSeverity = message.LogLevel.Id;
		switch (message.LogLevel.Id)
		{
			// ReSharper disable StringLiteralTypo

			case 0:
				Debug.Assert(message.LogLevel == LogLevel.Emergency);
				ecsLogLevel = "emerg";
				break;

			case 1:
				Debug.Assert(message.LogLevel == LogLevel.Alert);
				ecsLogLevel = "alert";
				break;

			case 2:
				Debug.Assert(message.LogLevel == LogLevel.Critical);
				ecsLogLevel = "crit";
				break;

			case 3:
				Debug.Assert(message.LogLevel == LogLevel.Error);
				ecsLogLevel = "error";
				break;

			case 4:
				Debug.Assert(message.LogLevel == LogLevel.Warning);
				ecsLogLevel = "warn";
				break;

			case 5:
				Debug.Assert(message.LogLevel == LogLevel.Notice);
				ecsLogLevel = "notice";
				break;

			case 6:
				Debug.Assert(message.LogLevel == LogLevel.Informational);
				ecsLogLevel = "info";
				break;

			case 7:
				Debug.Assert(message.LogLevel == LogLevel.Debug);
				ecsLogLevel = "debug";
				break;

			case 8:
				Debug.Assert(message.LogLevel == LogLevel.Trace);
				ecsLogLevel = "trace";
				break;

			// ReSharper restore StringLiteralTypo

			default:
				// aspect log levels
				// (all aspects must have the same severity id to avoid mixing them up in an aggregated log)
				ecsEventSeverity = 9;
				ecsLogLevel = message.LogLevel.Name;
				break;
		}
	}

	#endregion
}
