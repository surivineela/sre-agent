// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;
using ArmConstants = Agent.Graph.Crawler.ARM.Constants;

namespace Agent.Runtime.SubAgents.LocalAuthAgent;

/// <summary>
/// Represents information about a resource that needs to be remediated for local auth issues.
/// </summary>
/// <param name="ResourceId">The full Azure resource ID</param>
/// <param name="Name">The resource name</param>
/// <param name="Location">The resource location</param>
public record LocalAuthResourceInformation(string ResourceId, string Name, string Location)
{
    /// <summary>
    /// Returns the resource provider name/type from the resource ID.
    /// Sample: "microsoft.storage/storageaccounts" from "/subscriptions/fe2ef518-fe95-41c5-9264-467faa5d6182/resourceGroups/avip2-operations-agent-3p-rg/providers/Microsoft.Storage/storageAccounts/avipteststorage/overview"
    /// </summary>
    public string ResourceProviderName
    {
        get
        {
            var split = ResourceId.Split('/');
            if (split.Length >= 8)
            {
                return $"{split[6]}/{split[7]}".ToLower();
            }
            return "unknown";
        }
    }
}

/// <summary>
/// Scanner that identifies and handles resources with insecure key-based authentication enabled.
/// This scanner operates independently without requiring separate agent or factory classes.
/// </summary>
public class LocalAuthScanner(
    DurableTaskClient durableTaskClient,
    IThreadRepository threadRepository,
    ILogger<LocalAuthScanner> logger,
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IGraphDatabaseClient graphDatabaseClient,
    ArmHelper armHelper)
{
    private readonly ILogger<LocalAuthScanner> _logger = logger;
    private readonly DurableTaskClient _durableTaskClient = durableTaskClient;
    private readonly IAgentInboundCommunicationService _agentInboundCommunicationService = agentInboundCommunicationService;
    private readonly IGraphDatabaseClient _graphDatabaseClient = graphDatabaseClient;
    private readonly ArmHelper _armHelper = armHelper;
    private readonly IThreadRepository _repository = threadRepository;

    public async Task ScanAsync(CancellationToken cancellationToken)
    {
        // Do the scan itself, likely querying graph and/or ARM.
        var resourcesInViolation = await GetResourcesInViolationAsync();
        var groupedResourcesInViolation = resourcesInViolation.GroupBy(x => x.ResourceProviderName).ToList();

        foreach (var group in groupedResourcesInViolation)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInternalInformation("Cancellation requested, stopping LocalAuth scanner.");
                return;
            }

            var resourceProviderName = group.Key; // eg; "microsoft.storage/storageaccounts"
            if (group.Count() > 0)
            {
                try
                {
                    await HandleResourceGroupAsync(group, resourceProviderName);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error handling resource group {resourceProviderName}", resourceProviderName);
                }
            }
        }
    }

    private async Task HandleResourceGroupAsync(IGrouping<string, LocalAuthResourceInformation> group, string resourceProviderName)
    {
        var agentName = "LocalAuthScanner";

        // Create the thread with just the basic violation message
        (var thread, var agentContext) = await _agentInboundCommunicationService.CreateAgentThread(
            $"{agentName} for {resourceProviderName} found issues",
            "",
            AgentTypeEnum.Meta,
            ThreadSource.Conversation
        );

        _logger.LogInternalInformation($"Using Agent Framework to process resources for {agentName}");

        // Build the user message that includes the resource information that the agent can access
        var resourceDataLines = new List<string> { "RESOURCE_DATA_START" };
        foreach (var resource in group)
        {
            resourceDataLines.Add($"RESOURCE|{resource.Name}|{resource.Location}|{resource.ResourceId}");
        }
        resourceDataLines.Add("RESOURCE_DATA_END");

        var userMessage = $"Detected resources that have unsafe key-based access enabled.\n\n{string.Join("\n", resourceDataLines)}";

        var message = new ThreadMessage(
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage.Id,
            Message: userMessage,
            UserId: "",
            DisplayName: "",
            Timestamp: DateTime.UtcNow);

        await _agentInboundCommunicationService.ProcessUserMessageAsync(message);
    }

    private async Task<ICollection<LocalAuthResourceInformation>> GetResourcesInViolationAsync()
    {
        var storageStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.StorageType.ToLower(), _armHelper.GetStorageSettings);
        var cosmosStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.CosmosDbType.ToLower(), _armHelper.GetCosmosDbSettings);
        var sqlStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.AzureSQLType.ToLower(), _armHelper.GetAzureSqlServerSettings);
        var eventHubStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.EventHubType.ToLower(), _armHelper.GetEventHubSettings);
        var serviceBusStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.ServiceBusType.ToLower(), _armHelper.GetServiceBusSettings);
        var appServiceStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.AppServiceType.ToLower(), _armHelper.GetAppServiceSettings);
        var kubernetesStatusesTask = GetResourceTypeInViolationAsync(ArmConstants.AzureKubernetesServiceType.ToLower(), _armHelper.GetKubernetesSettings);

        await Task.WhenAll(storageStatusesTask, cosmosStatusesTask, sqlStatusesTask,
            eventHubStatusesTask, serviceBusStatusesTask, appServiceStatusesTask, kubernetesStatusesTask);

        var allStatuses = new[] {
            storageStatusesTask.Result
                .Where(x => x.StorageKeyEnabled == true || x.PublicContainersEnabled == true)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location)),
            cosmosStatusesTask.Result
                .Where(x => x.IsLocalAuthEnabled == false)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location)),
            sqlStatusesTask.Result
                .Where(x => x.IsAzureADOnlyAuthenticationEnabled == false && x.IsEntraAdminSet == true)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location)),
            eventHubStatusesTask.Result
                .Where(x => x.IsLocalAuthDisabled == false)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location)),
            serviceBusStatusesTask.Result
                .Where(x => x.IsLocalAuthDisabled == false)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location)),
            appServiceStatusesTask.Result
                .Where(x => x.SCMBasicAuthEnabled == true || x.FTPBasicAuthEnabled == true)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location)),
            kubernetesStatusesTask.Result
                .Where(x => x.DisableLocalAccounts == false)
                .Select(x => new LocalAuthResourceInformation(x.ResourceId, x.Name, x.Location))
        };

        return allStatuses.SelectMany(x => x).ToList();
    }

    private async Task<T> GetResourceTypeInViolationAsync<T>(string resourceType, Func<List<string>, Task<T>> resourceSettingsGetter)
    {
        const string ignoreFieldName = "notification_ignoreuntil";

        string query = $"""
        g.V().has('resourceType', '{resourceType}').has('isDeleted', false)
          .project('resourceId', '{ignoreFieldName}')
          .by(values('resourceId'))
          .by(
            coalesce(
              outE('{ArmConstants.Relationships.HasIgnoreConfig}').inV().has('isDeleted', false).values('{ignoreFieldName}'),
              constant('')
            )
          )
        """;

        var queryResults = await _graphDatabaseClient.Query(query);
        var resources = queryResults
            .Select(x => new
            {
                ResourceId = (string)x["resourceId"],
                IgnoreUntil = x[ignoreFieldName] == string.Empty ? null : DateTimeOffset.Parse(x[ignoreFieldName].ToString())
            })
            .OrderBy(x => x.ResourceId.Split("/").Last()).ToList();

        var resourcesToIgnore = resources
            .Where(x => x.IgnoreUntil != null && x.IgnoreUntil > DateTimeOffset.Now)
            .ToList();

        var resourcesToAlert = resources
            .Except(resourcesToIgnore)
            .Select(x => x.ResourceId).ToList();

        if (resourcesToIgnore.Any())
        {
            _logger.LogInternalInformation(
                $"Skipping these resources as they are tagged to be ignored: {string.Join(Environment.NewLine, resourcesToIgnore.Select(r => r.ResourceId))}"
            );
        }

        return await resourceSettingsGetter(resourcesToAlert);
    }
}
