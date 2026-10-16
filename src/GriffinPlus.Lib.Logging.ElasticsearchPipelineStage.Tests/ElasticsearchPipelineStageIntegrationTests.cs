///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// This file is part of the Griffin+ common library suite (https://github.com/griffinplus/dotnet-libs-logging)
// The source code is licensed under the MIT license.
///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Threading;

using Xunit;

namespace GriffinPlus.Lib.Logging.Elasticsearch.Tests;

/// <summary>
/// Integration tests targeting the <see cref="ElasticsearchPipelineStage"/> class.
/// The tests expect an Elasticsearch server listening at <c>http://127.0.0.1:9200/</c>.
/// </summary>
[Trait("Category", "Integration")]
public class ElasticsearchPipelineStageIntegrationTests
{
	private static readonly Uri[] sSetting_Server_ApiBaseUrls =
	[
		new("http://127.0.0.1:9200/"), // Elasticsearch is working on this endpoint
		new("http://127.0.0.1:9201/")  // there is no Elasticsearch server running at this endpoint
	];

	private const AuthenticationScheme Setting_Server_Authentication_Schemes          = AuthenticationScheme.Basic | AuthenticationScheme.Digest;
	private const string               Setting_Server_Authentication_Username         = "JohnDoe";
	private const string               Setting_Server_Authentication_Password         = "Secret";
	private const string               Setting_Server_Authentication_Domain           = "My-Domain";
	private const string               Setting_Server_IndexName                       = "My-Index";
	private const int                  Setting_Server_BulkRequest_MaxConcurrencyLevel = 3;
	private const int                  Setting_Server_BulkRequest_MaxMessageCount     = 1500;
	private const int                  Setting_Server_BulkRequest_MaxSize             = 10 * 1024 * 1024;
	private const string               Setting_Data_Organization_Id                   = "My-Organization-Id";
	private const string               Setting_Data_Organization_Name                 = "My-Organization-Name";
	private const int                  Setting_Stage_SendQueueSize                    = 10000;

	#region Initialize()

	/// <summary>
	/// Tests whether <see cref="ProcessingPipelineStage.Initialize"/> initializes the stage and applies the settings
	/// provided by the log configuration to the stage properties.
	/// </summary>
	[Fact]
	public void Initialize_WithConfiguration_AppliesSettings()
	{
		// Arrange
		ElasticsearchPipelineStage stage = CreateConfiguredStage();

		try
		{
			// Act
			stage.Initialize();

			// Assert
			Assert.True(stage.IsInitialized);
			Assert.False(stage.IsOperational); // no request has been sent, yet
			Assert.Equal(sSetting_Server_ApiBaseUrls, stage.ApiBaseUrls);
			Assert.Equal(Setting_Server_Authentication_Schemes, stage.AuthenticationSchemes);
			Assert.Equal(Setting_Server_Authentication_Username, stage.Username);
			Assert.Equal(Setting_Server_Authentication_Password, stage.Password);
			Assert.Equal(Setting_Server_Authentication_Domain, stage.Domain);
			Assert.Equal(Setting_Server_BulkRequest_MaxConcurrencyLevel, stage.BulkRequestMaxConcurrencyLevel);
			Assert.Equal(Setting_Server_BulkRequest_MaxMessageCount, stage.BulkRequestMaxMessageCount);
			Assert.Equal(Setting_Server_BulkRequest_MaxSize, stage.BulkRequestMaxSize);
			Assert.Equal(Setting_Server_IndexName, stage.IndexName);
			Assert.Equal(Setting_Data_Organization_Id, stage.OrganizationId);
			Assert.Equal(Setting_Data_Organization_Name, stage.OrganizationName);
			Assert.Equal(Setting_Stage_SendQueueSize, stage.SendQueueSize);
		}
		finally
		{
			if (stage.IsInitialized) stage.Shutdown();
		}
	}

	#endregion

	#region Shutdown()

	/// <summary>
	/// Tests whether <see cref="ProcessingPipelineStage.Shutdown"/> shuts down a stage that has been running for some time.
	/// </summary>
	[Fact]
	public void Shutdown_WhenInitialized_DeinitializesStage()
	{
		// Arrange
		ElasticsearchPipelineStage stage = CreateConfiguredStage();
		stage.Initialize();
		Assert.True(stage.IsInitialized);
		Thread.Sleep(1000); // let the stage run for some time

		// Act
		stage.Shutdown();

		// Assert
		Assert.False(stage.IsInitialized);
		Assert.Equal(Setting_Server_IndexName, stage.IndexName); // settings are kept after shutting down
	}

	#endregion

	#region Helpers

	/// <summary>
	/// Creates a stage whose settings are provided by a <see cref="VolatileLogConfiguration"/>.
	/// </summary>
	/// <returns>The created (uninitialized) stage.</returns>
	private static ElasticsearchPipelineStage CreateConfiguredStage()
	{
		var configuration = new VolatileLogConfiguration();
		var stage = ProcessingPipelineStage.Create<ElasticsearchPipelineStage>("Elasticsearch", configuration);

		IProcessingPipelineStageConfiguration stageSettings = configuration.ProcessingPipeline.Stages.First(x => x.Name == "Elasticsearch");
		stageSettings.SetSetting("Server.ApiBaseUrls", sSetting_Server_ApiBaseUrls, UriArrayConverter.Format, UriArrayConverter.Parse);
		stageSettings.SetSetting("Server.Authentication.Schemes", Setting_Server_Authentication_Schemes);
		stageSettings.SetSetting("Server.Authentication.Username", Setting_Server_Authentication_Username);
		stageSettings.SetSetting("Server.Authentication.Password", Setting_Server_Authentication_Password);
		stageSettings.SetSetting("Server.Authentication.Domain", Setting_Server_Authentication_Domain);
		stageSettings.SetSetting("Server.BulkRequest.MaxConcurrencyLevel", Setting_Server_BulkRequest_MaxConcurrencyLevel);
		stageSettings.SetSetting("Server.BulkRequest.MaxMessageCount", Setting_Server_BulkRequest_MaxMessageCount);
		stageSettings.SetSetting("Server.BulkRequest.MaxSize", Setting_Server_BulkRequest_MaxSize);
		stageSettings.SetSetting("Server.IndexName", Setting_Server_IndexName);
		stageSettings.SetSetting("Data.Organization.Id", Setting_Data_Organization_Id);
		stageSettings.SetSetting("Data.Organization.Name", Setting_Data_Organization_Name);
		stageSettings.SetSetting("Stage.SendQueueSize", Setting_Stage_SendQueueSize);

		return stage;
	}

	#endregion
}
