// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Runtime.SubAgents.LocalAuthAgent;

public class LocalAuthScanner : SimpleResourceSubAgentScannerBase<LocalAuthAgent, LocalAuthAgentInput, LocalAuthAgentActivity, LocalAuthAgentActivityInput>
{
    public LocalAuthScanner(
        DurableTaskClient durableTaskClient,
        IThreadRepository threadRepository,
        LocalAuthAgentFactory LocalAuthAgentFactory,
        ILogger<LocalAuthAgent> logger,
        IAgentInboundCommunicationService agentInboundCommunicationService,
        IGraphDatabaseClient graphDatabaseClient,
        ArmHelper armHelper)
        : base(durableTaskClient, LocalAuthAgentFactory, logger, agentInboundCommunicationService, graphDatabaseClient, armHelper)
    {

    }

    protected override TimeSpan RunInterval => TimeSpan.FromMinutes(3);

    protected override string MessageWhenFoundResourcesInViolation => """
                    Hi there! I found some resources that have key-based access enabled;
                    this is generally not recommended - it is not as secure as using Entra-based authentication.
                    Preparing details...  
                    """;

    protected override LocalAuthAgentActivityInput GenerateActivityInput(IEnumerable<SimpleResourceSubAgentResourceInformation> Resources)
    {
        return new LocalAuthAgentActivityInput(
                LocalAuthSetLocalAuthSupport: FeatureState.Disabled,
                Resources.ToList()
        );
    }

    protected override async Task<ICollection<SimpleResourceSubAgentResourceInformation>> GetResourcesInViolationAsync()
    {

        var storageStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.StorageType.ToLower(), _armHelper.GetStorageSettings);
        var cosmosStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.CosmosDbType.ToLower(), _armHelper.GetCosmosDbSettings);
        var sqlStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.AzureSQLType.ToLower(), _armHelper.GetAzureSqlServerSettings);
        var eventHubStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.EventHubType.ToLower(), _armHelper.GetEventHubSettings);
        var serviceBusStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.ServiceBusType.ToLower(), _armHelper.GetServiceBusSettings);
        var appServiceStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.AppServiceType.ToLower(), _armHelper.GetAppServiceSettings);


        await Task.WhenAll(storageStatusesTask, cosmosStatusesTask, sqlStatusesTask,
            eventHubStatusesTask, serviceBusStatusesTask);

        var allStatuses = new[] {
            storageStatusesTask.Result
                .Where(x => x.StorageKeyEnabled == true || x.PublicContainersEnabled == true)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                ,
            cosmosStatusesTask.Result
                .Where(x => x.IsLocalAuthEnabled == false)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                ,
            sqlStatusesTask.Result
                .Where(x => x.IsAzureADOnlyAuthenticationEnabled == false && x.IsEntraAdminSet == true)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                ,
            eventHubStatusesTask.Result
                .Where(x => x.IsLocalAuthDisabled == false)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                ,
            serviceBusStatusesTask.Result
                .Where(x => x.IsLocalAuthDisabled == false)
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
                ,
            appServiceStatusesTask.Result
                .Where(x => x.SCMBasicAuthEnabled == true || x.FTPBasicAuthEnabled == true) 
                .Select(x => new SimpleResourceSubAgentResourceInformation(x.ResourceId, x.Name, x.Location))
        };

        return allStatuses.SelectMany(x => x).ToList();
    }

    private async Task<T> GetResourceTypeInViolationAsync<T>(string resourceType, Func<List<string>, Task<T>> resourceSettingsGetter)
    {
        string query = $"g.V().has('resourceType', '{resourceType}').values('resourceId')";
        var queryResults = await _graphDatabaseClient.Query(query);
        var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();
        return await resourceSettingsGetter(resources);
    }
}
