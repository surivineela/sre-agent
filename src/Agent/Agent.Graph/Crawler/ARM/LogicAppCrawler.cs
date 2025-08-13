// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class LogicAppCrawler : AppServiceCrawler
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
        var start = DateTime.UtcNow.Ticks;

        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        var logicAppNode = (AppServiceNode)node;
        _logger.LogInternalInformation($"Crawling Logic App {logicAppNode.ResourceId}");

        var armResourceId = new ResourceIdentifier(logicAppNode.ResourceId);
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
                    logicAppNode.SubscriptionId, storageConnStr);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"Error processing azurewebJobsStorage for Logic App {logicAppNode.ResourceId}: {ex.Message}");
            }

            if (storageNode != null)
            {
                await _graphDbClient.AddOrUpdateNodeAsync(storageNode);
                var edge = new ArmResourceEdge(logicAppNode.GetNodeId(), storageNode.GetNodeId(), Constants.Relationships.Connected);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return storageNode;
            }
        }

        string? appInsightsKey = null;
        if (appSettings.TryGetValue("APPLICATIONINSIGHTS_CONNECTION_STRING", out var appInsightsConnectionString) || appSettings.TryGetValue("APPINSIGHTS_INSTRUMENTATIONKEY", out appInsightsKey))
        {
            if (!string.IsNullOrEmpty(appInsightsConnectionString))
            {
                appInsightsKey = ExtractInstrumentationKeyFromConnectionString(appInsightsConnectionString);
            }

            if (!string.IsNullOrEmpty(appInsightsKey))
            {
                var appInsightsNode = await TryAddAppInsightsNodeAsync(logicAppNode, appInsightsKey);
                if (appInsightsNode != null)
                    yield return appInsightsNode;
            }
        }

        var connectionMapping = new Dictionary<string, GraphNode>();

        var connectionsResponse = await webApp.GetWorkflowsConnectionsAsync();
        var connectionsFile = connectionsResponse?.Value?.Properties?.Files?["connections.json"];
        if (connectionsFile != null)
        {
            var connectionIds = new List<string>();

            var connections = JsonSerializer.Deserialize<LogicAppConnections>(connectionsFile, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            foreach (var entry in connections?.ManagedApiConnections ?? new Dictionary<string, ManagedApiConnection>())
            {
                var id = entry.Value.Connection?.Id;
                if (id != null)
                {
                    var resourceId = new ResourceIdentifier(id!);
                    var managedConnectionNode = new ArmResourceNode(
                        resourceType: Constants.ApiConnectionType,
                        resourceId: id,
                        subscriptionId: resourceId.SubscriptionId!,
                        resourceGroupName: resourceId.ResourceGroupName!,
                        resourceName: resourceId.Name);

                    connectionMapping.Add(entry.Key, managedConnectionNode);
                }
            }
        }

        Dictionary<string, GraphNode> inUseConnectionMapping = new Dictionary<string, GraphNode>();
        foreach (var workflowNode in await GetSiteWorkflows(logicAppNode, webApp, connectionMapping, inUseConnectionMapping))
        {
            await CrawlSiteWorkflow(webApp, workflowNode, connectionMapping, inUseConnectionMapping);

            // Since we crawled this node, we need to remove any stale edges that might exist.
            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, workflowNode, start);
        }

        foreach (var connection in inUseConnectionMapping.Values)
        {
            yield return connection;
        }
    }

    private async Task<IEnumerable<WorkflowNode>> GetSiteWorkflows(AppServiceNode logicAppNode, WebSiteResource webApp, Dictionary<string, GraphNode> connectionMapping, Dictionary<string, GraphNode> inUseConnectionMapping)
    {
        var workflowNodes = new List<WorkflowNode>();
        try
        {
            await foreach (var workflow in webApp.GetSiteWorkflows().GetAllAsync())
            {
                if (workflow.HasData)
                {
                    var workflowNode = new WorkflowNode(ParseWorkflowConfig(workflow.Data));
                    workflowNodes.Add(workflowNode);

                    await _graphDbClient.AddOrUpdateNodeAsync(workflowNode);

                    var edge = new ArmResourceEdge(logicAppNode.GetNodeId(), workflowNode.GetNodeId(), Constants.Relationships.Contains);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Error processing workflows for logic apps: {logicAppNode.ResourceId}: {ex.Message}");
        }

        return workflowNodes;
    }

    private IEnumerable<JsonElement> TraverseAllActions(JsonElement actionsElement)
    {
        if (actionsElement.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var actionProperty in actionsElement.EnumerateObject())
        {
            var action = actionProperty.Value;

            if (action.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            yield return action;

            if (action.TryGetProperty("actions", out var actions))
            {
                foreach (var nestedAction in TraverseAllActions(actions))
                {
                    yield return nestedAction;
                }
            }

            if (action.TryGetProperty("else", out var elseBranch) && elseBranch.ValueKind == JsonValueKind.Object && elseBranch.TryGetProperty("actions", out var elseActions))
            {
                foreach (var nestedAction in TraverseAllActions(elseActions))
                {
                    yield return nestedAction;
                }
            }

            if (action.TryGetProperty("cases", out var cases) && cases.ValueKind == JsonValueKind.Object)
            {
                foreach (var caseProperty in cases.EnumerateObject())
                {
                    var caseBranch = caseProperty.Value;
                    if (caseBranch.ValueKind == JsonValueKind.Object && caseBranch.TryGetProperty("actions", out var caseActions))
                    {
                        foreach (var nestedAction in TraverseAllActions(caseActions))
                        {
                            yield return nestedAction;
                        }
                    }
                }
            }

            if (action.TryGetProperty("default", out var defaultBranch) && defaultBranch.ValueKind == JsonValueKind.Object && defaultBranch.TryGetProperty("actions", out var defaultActions))
            {
                foreach (var nestedAction in TraverseAllActions(defaultActions))
                {
                    yield return nestedAction;
                }
            }

            if (action.TryGetProperty("tools", out var tools) && tools.ValueKind == JsonValueKind.Object)
            {
                foreach (var toolProperty in tools.EnumerateObject())
                {
                    var tool = toolProperty.Value;
                    if (tool.ValueKind == JsonValueKind.Object && tool.TryGetProperty("actions", out var toolActions))
                    {
                        foreach (var nestedAction in TraverseAllActions(toolActions))
                        {
                            yield return nestedAction;
                        }
                    }
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

    private async Task CrawlSiteWorkflow(WebSiteResource siteResource, WorkflowNode workflowNode, Dictionary<string, GraphNode> connectionMapping, Dictionary<string, GraphNode> inUseConnectionMapping)
     {
        var siteWorkflowResponse = siteResource.GetSiteWorkflow(workflowNode.ResourceName);
        if (!siteWorkflowResponse.HasValue)
        {
            _logger.LogInternalWarning($"Workflow {workflowNode.ResourceName} not found in Logic App {siteResource.Data.Name}");
            return;
        }
        var siteWorkflow = siteWorkflowResponse.Value;

        var files = siteWorkflow?.Data?.Properties?.Files;
        if (files == null || !files.ContainsKey("workflow.json"))
        {
            return;
        }

        var workflowFileData = files["workflow.json"];
        var workflowFile = workflowFileData?.ToString();
            if (string.IsNullOrWhiteSpace(workflowFile))
                return;

        using var doc = JsonDocument.Parse(workflowFile);
        if (doc.RootElement.TryGetProperty("definition", out var definitionElement))
        {
            if (definitionElement.TryGetProperty("actions", out var actions))
            {
                foreach (var action in TraverseAllActions(actions))
                {
                    // Find the connection reference and look it up in connectionMapping
                    var referenceName = GetReferenceName(action);
                    var connectionNode = connectionMapping.FirstOrDefault(kvp => kvp.Key == referenceName).Value;
                    if (connectionNode != null)
                    {
                        if (!inUseConnectionMapping.ContainsKey(referenceName!))
                        {
                            await _graphDbClient.AddOrUpdateNodeAsync(connectionNode);
                            var edge = new ArmResourceEdge(workflowNode.GetNodeId(), connectionNode.GetNodeId(), Constants.Relationships.Connected);
                            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                            inUseConnectionMapping[referenceName!] = connectionNode;
                        }
                    }
                }
            }
        }
    }

    private string? GetReferenceName(JsonElement operation)
    {
        if (!operation.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || !"ApiConnection".Equals(type.GetString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (operation.TryGetProperty("inputs", out var inputs) &&
            inputs.TryGetProperty("host", out var host) &&
            host.TryGetProperty("connection", out var connection) &&
            connection.TryGetProperty("referenceName", out var referenceName) &&
            referenceName.ValueKind == JsonValueKind.String)
        {
            return referenceName.GetString();
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

    public class LogicAppConnections
    {
        public Dictionary<string, ManagedApiConnection> ManagedApiConnections { get; set; } = new();
        public Dictionary<string, ServiceProviderConnection> ServiceProviderConnections { get; set; } = new();
    }

    public class ManagedApiConnection
    {
        public ApiReference? Api { get; set; }
        public Authentication? Authentication { get; set; }
        public ConnectionReference? Connection { get; set; }
        public string? ConnectionRuntimeUrl { get; set; }
    }

    public class ServiceProviderConnection
    {
        public required string DisplayName { get; set; }
        public string? ParameterSetName { get; set; }
        public required Dictionary<string, Object> ParameterValues { get; set; }
        public required ServiceProviderReference ServiceProvider { get; set; }
    }

    public class ApiReference
    {
        public string? Id { get; set; }
    }

    public class Authentication
    {
        public string? Type { get; set; }
    }

    public class ConnectionReference
    {
        public string? Id { get; set; }
    }

    public class ServiceProviderReference
    {
        public required string Id { get; set; }
    }
}
