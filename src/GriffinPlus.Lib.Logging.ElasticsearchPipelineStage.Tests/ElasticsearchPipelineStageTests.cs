///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using Xunit;

namespace GriffinPlus.Lib.Logging.Elasticsearch
{

	/// <summary>
	/// Unit tests targeting the <see cref="ElasticsearchPipelineStage"/> class.
	/// </summary>
	public class ElasticsearchPipelineStageTests
	{
		private readonly Uri[] mSetting_Server_ApiBaseUrls =
		{
			new Uri("http://127.0.0.1:9200/"), // Elasticsearch is working on this endpoint
			new Uri("http://127.0.0.1:9201/")  // there is no Elasticsearch server running at this endpoint
		};

		private readonly AuthenticationScheme mSetting_Server_Authentication_Schemes      = AuthenticationScheme.Basic | AuthenticationScheme.Digest;
		private readonly string               mSetting_Server_Authentication_Username     = "JohnDoe";
		private readonly string               mSetting_Server_Authentication_Password     = "Secret";
		private readonly string               mSetting_Server_Authentication_Domain       = "My-Domain";
		private readonly string               mSetting_Server_IndexName                   = "My-Index";
		private readonly int                  mSetting_Server_BulkRequest_MaxMessageCount = 1500;
		private readonly int                  mSetting_Server_BulkRequest_MaxSize         = 10 * 1024 * 1024;
		private readonly string               mSetting_Data_Organization_Id               = "My-Organization-Id";
		private readonly string               mSetting_Data_Organization_Name             = "My-Organization-Name";
		private readonly int                  mSetting_Stage_SendQueueSize                = 10000;

		#region Construction

		/// <summary>
		/// Tests creating a new instance using <see cref="ElasticsearchPipelineStage"/>.
		/// </summary>
		[Fact]
		public void Create_Default()
		{
			var stage = new ElasticsearchPipelineStage("Elasticsearch");

			// check common stage properties
			// ------------------------------------------------------------------------------------
			Assert.False(stage.IsInitialized);
			Assert.False(stage.IsDefaultStage);
			Assert.Equal("Elasticsearch", stage.Name);
			Assert.Empty(stage.NextStages);

			// check setting 'Server.ApiBaseUrls'
			// ------------------------------------------------------------------------------------
			var expectedApiEndpoints = new[] { new Uri("http://127.0.0.1:9200/") };
			var endpointsSetting = stage.Settings.GetSetting("Server.ApiBaseUrls", UriArrayToString, StringToUriArray);
			Assert.Equal(expectedApiEndpoints, endpointsSetting.DefaultValue);
			Assert.Equal(expectedApiEndpoints, endpointsSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(1, stage.ApiBaseUrls.Count);
			Assert.Equal(expectedApiEndpoints, stage.ApiBaseUrls);

			// check setting 'Server.Authentication.Schemes'
			// ------------------------------------------------------------------------------------
			var expectedAuthenticationSchemes = AuthenticationScheme.PasswordBased;
			var authenticationSchemesSetting = stage.Settings.GetSetting<AuthenticationScheme>("Server.Authentication.Schemes");
			Assert.Equal(expectedAuthenticationSchemes, authenticationSchemesSetting.DefaultValue);
			Assert.Equal(expectedAuthenticationSchemes, authenticationSchemesSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedAuthenticationSchemes, stage.AuthenticationSchemes);

			// check setting 'Server.Authentication.Username'
			// ------------------------------------------------------------------------------------
			string expectedUsername = "";
			var usernameSetting = stage.Settings.GetSetting<string>("Server.Authentication.Username");
			Assert.Equal(expectedUsername, usernameSetting.DefaultValue);
			Assert.Equal(expectedUsername, usernameSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedUsername, stage.Username);

			// check setting 'Server.Authentication.Password'
			// ------------------------------------------------------------------------------------
			string expectedPassword = "";
			var passwordSetting = stage.Settings.GetSetting<string>("Server.Authentication.Password");
			Assert.Equal(expectedPassword, passwordSetting.DefaultValue);
			Assert.Equal(expectedPassword, passwordSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedPassword, stage.Password);

			// check setting 'Server.Authentication.Domain'
			// ------------------------------------------------------------------------------------
			string expectedDomain = "";
			var domainSetting = stage.Settings.GetSetting<string>("Server.Authentication.Domain");
			Assert.Equal(expectedDomain, domainSetting.DefaultValue);
			Assert.Equal(expectedDomain, domainSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedDomain, stage.Domain);

			// check setting 'Server.BulkRequest.MaxMessageCount'
			// ------------------------------------------------------------------------------------
			int expectedMaxMessageCount = 1000;
			var maxMessageCountSetting = stage.Settings.GetSetting<int>("Server.BulkRequest.MaxMessageCount");
			Assert.Equal(expectedMaxMessageCount, maxMessageCountSetting.DefaultValue);
			Assert.Equal(expectedMaxMessageCount, maxMessageCountSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedMaxMessageCount, stage.BulkRequestMaxMessageCount);

			// check setting 'Server.BulkRequest.MaxSize'
			// ------------------------------------------------------------------------------------
			int expectedMaxSize = 5 * 1024 * 1024;
			var maxSizeSetting = stage.Settings.GetSetting<int>("Server.BulkRequest.MaxSize");
			Assert.Equal(expectedMaxSize, maxSizeSetting.DefaultValue);
			Assert.Equal(expectedMaxSize, maxSizeSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedMaxSize, stage.BulkRequestMaxSize);

			// check setting 'Server.IndexName'
			// ------------------------------------------------------------------------------------
			string expectedIndexName = "logs";
			var indexNameSetting = stage.Settings.GetSetting<string>("Server.IndexName");
			Assert.Equal(expectedIndexName, indexNameSetting.DefaultValue);
			Assert.Equal(expectedIndexName, indexNameSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedIndexName, stage.IndexName);

			// check setting 'Data.Organization.Id'
			// ------------------------------------------------------------------------------------
			string expectedOrganizationId = "griffin.plus";
			var organizationIdSetting = stage.Settings.GetSetting<string>("Data.Organization.Id");
			Assert.Equal(expectedOrganizationId, organizationIdSetting.DefaultValue);
			Assert.Equal(expectedOrganizationId, organizationIdSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedOrganizationId, stage.OrganizationId);

			// check setting 'Data.Organization.Name'
			// ------------------------------------------------------------------------------------
			string expectedOrganizationName = "Griffin+";
			var organizationNameSetting = stage.Settings.GetSetting<string>("Data.Organization.Name");
			Assert.Equal(expectedOrganizationName, organizationNameSetting.DefaultValue);
			Assert.Equal(expectedOrganizationName, organizationNameSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedOrganizationName, stage.OrganizationName);

			// check setting 'Server.SendQueueSize'
			// ------------------------------------------------------------------------------------
			int expectedSendQueueSize = 50000;
			var sendQueueSizeSetting = stage.Settings.GetSetting<int>("Stage.SendQueueSize");
			Assert.Equal(expectedSendQueueSize, sendQueueSizeSetting.DefaultValue);
			Assert.Equal(expectedSendQueueSize, sendQueueSizeSetting.Value);

			// the corresponding stage property should reflect the default setting
			Assert.Equal(expectedSendQueueSize, stage.SendQueueSize);

			// the stage should be operational at start
			// ------------------------------------------------------------------------------------
			Assert.True(stage.IsOperational);
		}

		#endregion

		#region Initialize and Shutdown

		/// <summary>
		/// Initializes the stage, lets the stage run for some time and shuts it down at the end.
		/// </summary>
		[Fact]
		public void InitializeAndShutdown()
		{
			var stage = new ElasticsearchPipelineStage("Elasticsearch");

			// let the configuration provide the appropriate Elasticsearch endpoints
			var configuration = new VolatileLogConfiguration();
			var stageSettings = configuration.ProcessingPipeline.Stages.AddNew("Elasticsearch");
			stageSettings.SetSetting("Server.ApiBaseUrls", mSetting_Server_ApiBaseUrls, UriArrayToString, StringToUriArray);
			stageSettings.SetSetting("Server.Authentication.Schemes", mSetting_Server_Authentication_Schemes);
			stageSettings.SetSetting("Server.Authentication.Username", mSetting_Server_Authentication_Username);
			stageSettings.SetSetting("Server.Authentication.Password", mSetting_Server_Authentication_Password);
			stageSettings.SetSetting("Server.Authentication.Domain", mSetting_Server_Authentication_Domain);
			stageSettings.SetSetting("Server.BulkRequest.MaxMessageCount", mSetting_Server_BulkRequest_MaxMessageCount);
			stageSettings.SetSetting("Server.BulkRequest.MaxSize", mSetting_Server_BulkRequest_MaxSize);
			stageSettings.SetSetting("Server.IndexName", mSetting_Server_IndexName);
			stageSettings.SetSetting("Data.Organization.Id", mSetting_Data_Organization_Id);
			stageSettings.SetSetting("Data.Organization.Name", mSetting_Data_Organization_Name);
			stageSettings.SetSetting("Stage.SendQueueSize", mSetting_Stage_SendQueueSize);
			stage.Settings = configuration.ProcessingPipeline.Stages.First(x => x.Name == "Elasticsearch");

			// initialize the stage
			((IProcessingPipelineStage)stage).Initialize();
			Assert.True(stage.IsInitialized);
			Assert.True(stage.IsOperational);
			Assert.Equal(mSetting_Server_ApiBaseUrls, stage.ApiBaseUrls);
			Assert.Equal(mSetting_Server_Authentication_Schemes, stage.AuthenticationSchemes);
			Assert.Equal(mSetting_Server_Authentication_Username, stage.Username);
			Assert.Equal(mSetting_Server_Authentication_Password, stage.Password);
			Assert.Equal(mSetting_Server_Authentication_Domain, stage.Domain);
			Assert.Equal(mSetting_Server_BulkRequest_MaxMessageCount, stage.BulkRequestMaxMessageCount);
			Assert.Equal(mSetting_Server_BulkRequest_MaxSize, stage.BulkRequestMaxSize);
			Assert.Equal(mSetting_Server_IndexName, stage.IndexName);
			Assert.Equal(mSetting_Data_Organization_Id, stage.OrganizationId);
			Assert.Equal(mSetting_Data_Organization_Name, stage.OrganizationName);
			Assert.Equal(mSetting_Stage_SendQueueSize, stage.SendQueueSize);

			// wait for some time before shutting down
			Thread.Sleep(2000);

			// shut the stage down
			((IProcessingPipelineStage)stage).Shutdown();
			Assert.False(stage.IsInitialized);
		}

		#endregion

		#region Helpers

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
	}

}
