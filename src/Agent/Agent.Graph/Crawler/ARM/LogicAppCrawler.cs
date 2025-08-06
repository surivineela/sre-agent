// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class LogicAppCrawler: AppServiceCrawler
{
    private readonly ILogger<LogicAppCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmHelper _armHelper;
    
    public LogicAppCrawler(ILogger<LogicAppCrawler> logger, IGraphDatabaseClient graphDbClient, ArmHelper armHelper, ArmClient armClient): base(logger, graphDbClient, armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armHelper = armHelper;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var appServiceNode = (AppServiceNode)node;
        _logger.LogInternalInformation($"Crawling Logic App {appServiceNode.ResourceId}");

        var armResourceId = new ResourceIdentifier(appServiceNode.ResourceId);
        var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(armResourceId.SubscriptionId, armResourceId.ResourceGroupName);
        var resourceGroup = _armClient.GetResourceGroupResource(resourceGroupId);
        var siteResponse = await resourceGroup.GetWebSiteAsync(armResourceId.Name);
        var webApp = siteResponse.Value;

        var appSettingsResponse = await webApp.GetApplicationSettingsAsync();
        var appSettings = appSettingsResponse.Value.Properties;


        if (appSettings.TryGetValue("AzureWebJobsStorage", out var storageConnStr) && !string.IsNullOrEmpty(storageConnStr))
        {
            ArmResourceNode? storageNode = null;
            try
            {
                var storageHelper = new StorageAccountConnectionStringHelper(_logger, _armClient);
                storageNode = await storageHelper.GetStorageAccountResourceFromSettingAsync(
                    appServiceNode.SubscriptionId, storageConnStr);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"Error processing azurewebJobsStorage for Logic App {appServiceNode.ResourceId}: {ex.Message}");
            }

            if (storageNode != null)
            {
                await _graphDbClient.AddOrUpdateNodeAsync(storageNode);
                var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), storageNode.GetNodeId(), Constants.Relationships.Connected);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return storageNode;
            }
        }
        string? appInsightsKey = null;
        if (appSettings.TryGetValue("APPLICATIONINSIGHTS_CONNECTION_STRING", out var appInsightsConnectionString) || appSettings.TryGetValue("APPINSIGHTS_INSTRUMENTATIONKEY", out appInsightsKey))
        {
            if(!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                appInsightsKey = ExtractInstrumentationKeyFromConnectionString(appInsightsConnectionString);
            }

            if(!string.IsNullOrEmpty(appInsightsKey))
            {
                var appInsightsNode = await TryAddAppInsightsNodeAsync(appServiceNode, appInsightsKey);
                if (appInsightsNode != null)
                    yield return appInsightsNode;
            }
        }
        AsyncPageable<SiteWorkflowResource>? workflows = null;
        try
        {
            workflows = webApp.GetSiteWorkflows().GetAllAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error processing workflows for logic apps: {appServiceNode.ResourceId}: {ex.Message}");
        }

        if(workflows != null)
        {
            await foreach (var workflow in workflows)
            {
                if (workflow.HasData)
                {
                    var workflowNode = new WorkflowNode(ParseWorkflowConfig(workflow.Data));
                    await _graphDbClient.AddOrUpdateNodeAsync(workflowNode);

                    var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), workflowNode.GetNodeId(), Constants.Relationships.Contains);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                    yield return workflowNode;
                }
            }
        }
    }

    private async Task<ArmResourceNode?> TryAddAppInsightsNodeAsync(
        AppServiceNode appServiceNode,
        string instrumentationKey)
    {
        if (string.IsNullOrEmpty(instrumentationKey))
            return null;

        try
        {
            var appInsightsResource = await _armHelper.GetAppInsightsResourceByInstrumentationKeyAsync(
                appServiceNode.SubscriptionId, instrumentationKey);

            if (appInsightsResource != null)
            {
                var appInsightsNode = new ArmResourceNode(
                    resourceType: Constants.ApplicationInsightsType,
                    resourceId: appInsightsResource.id,
                    subscriptionId: appServiceNode.SubscriptionId,
                    resourceGroupName: ArmHelper.ExtractResourceGroupNameFromId(appInsightsResource.id)!,
                    resourceName: appInsightsResource.name,
                    location: appInsightsResource.location
                );

                await _graphDbClient.AddOrUpdateNodeAsync(appInsightsNode);
                var edge = new ArmResourceEdge(appServiceNode.GetNodeId(), appInsightsNode.GetNodeId(), Constants.Relationships.Connected);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                return appInsightsNode;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error processing Application Insights for Logic App {appServiceNode.ResourceId}: {ex.Message}");
        }

        return null;
    }

    private static string? ExtractInstrumentationKeyFromConnectionString(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var kvp = part.Split('=', 2);
            if (kvp.Length == 2 && kvp[0].Trim().Equals("InstrumentationKey", StringComparison.OrdinalIgnoreCase))
            {
                return kvp[1].Trim();
            }
        }
        return null;
    }

    private WorkflowNode.Workflow ParseWorkflowConfig(WorkflowEnvelopeData data)
    {
        if (data == null)
        {
            return new WorkflowNode.Workflow
            {
                Id = "unknown",
                Name = "unknown",
                SubscriptionId = "unknown",
                ResourceGroupName = "unknown",
                Location = "unknown",
            };
        }

        try
        {
            var resourceIdentifier = new ResourceIdentifier(data.Id!);
            var nameSplit = data.Name.Split('/');
            var workflowName = data.Name;
            if (nameSplit.Length > 1)
            {
                workflowName = nameSplit.Last();
            }
            var workflow = new WorkflowNode.Workflow
            {
                Id = data.Id!,
                SubscriptionId = resourceIdentifier.SubscriptionId ?? throw new InvalidOperationException("SubscriptionId cannot be null"),
                ResourceGroupName = resourceIdentifier.ResourceGroupName!,
                Location = data.Location ?? string.Empty,
                Name = workflowName,
            };

            return workflow;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to parse function config for {data.Name}: {ex.Message}");
            return new WorkflowNode.Workflow
            {
                Id = "unknown",
                Name = data.Name ?? "unknown",
                SubscriptionId = "unknown",
                ResourceGroupName = "unknown",
                Location = "unknown",
            };
        }
    }
}
