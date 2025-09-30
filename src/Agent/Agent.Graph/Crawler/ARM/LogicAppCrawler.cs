// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Extensions;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using static Agent.Graph.Crawler.ARM.LogicAppCrawler;

namespace Agent.Graph.Crawler.ARM;

public class LogicAppCrawler : AppServiceCrawler
{
    private readonly ILogger<LogicAppCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmHelper _armHelper;

    public LogicAppCrawler(ILogger<LogicAppCrawler> logger, IGraphDatabaseClient graphDbClient, ArmHelper armHelper, ArmClient armClient) : base(logger, graphDbClient, armClient)
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
        var appSettings = new Dictionary<string, string>(appSettingsResponse.Value.Properties, StringComparer.OrdinalIgnoreCase);


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

        var logicAppConnections = await GetConnectionsWithAppSettingsResolved(webApp, appSettings);

        var graphAccumulatorDictionary = new GraphAccumulatorDictionary();

        foreach (var workflowNode in await GetSiteWorkflows(logicAppNode, webApp))
        {
            if (logicAppConnections == null)
            {
                // Skip crawling workflows as we couldn't fetch logic app connections
                _logger.LogInternalWarning($"Unable to load connections for Logic App {logicAppNode.ResourceId}, skipping workflow connection crawling");
                break;
            }

            await CrawlSiteWorkflow(webApp, workflowNode, logicAppConnections, graphAccumulatorDictionary);

            // Since we crawled this node, we need to remove any stale edges that might exist.
            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, workflowNode, start);
        }

        foreach (var managedApiConnectionNode in graphAccumulatorDictionary.GetNodesByKind(NodeType.ManagedApiConnection))
        {
            yield return managedApiConnectionNode;
        }

        foreach (var serviceProviderResourceNode in graphAccumulatorDictionary.GetNodesByKind(NodeType.ServiceProviderResource))
        {
            yield return serviceProviderResourceNode;
        }

        foreach (var apiManagementResourceNode in graphAccumulatorDictionary.GetNodesByKind(NodeType.ApiManagementResource))
        {
            yield return apiManagementResourceNode;
        }

        foreach (var functionResourceNode in graphAccumulatorDictionary.GetNodesByKind(NodeType.FunctionResource))
        {
            yield return functionResourceNode;
        }
    }

    private async Task<IEnumerable<WorkflowNode>> GetSiteWorkflows(AppServiceNode logicAppNode, WebSiteResource webApp)
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

    public static IEnumerable<(string, JsonElement)> TraverseAllActions(JsonElement actionsElement)
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

            yield return (actionProperty.Name, action);

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

    private async Task CrawlSiteWorkflow(WebSiteResource siteResource, WorkflowNode workflowNode, LogicAppConnections connections, GraphAccumulatorDictionary graphAccumulatorDictionary)
    {
        var start = DateTime.UtcNow.Ticks;

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
            if (definitionElement.TryGetProperty("actions", out var actionsElement))
            {
                foreach (var (name, action) in TraverseAllActions(actionsElement))
                {
                    await CrawlWorkflowOperation(action, siteResource, workflowNode, connections, graphAccumulatorDictionary, start, Constants.Relationships.UsesAction);
                }
            }

            if (definitionElement.TryGetProperty("triggers", out var triggersElement))
            {
                foreach (var trigger in triggersElement.EnumerateObject())
                {
                    var triggerDefinition = trigger.Value;
                    if (triggerDefinition.ValueKind != JsonValueKind.Object)
                        continue;

                    await CrawlWorkflowOperation(triggerDefinition, siteResource, workflowNode, connections, graphAccumulatorDictionary, start, Constants.Relationships.UsesTrigger);
                }
            }
        }
    }

    private async Task CrawlWorkflowOperation(JsonElement action,
        WebSiteResource siteResource,
        WorkflowNode workflowNode,
        LogicAppConnections connections,
        GraphAccumulatorDictionary graphAccumulatorDictionary,
        long start,
        string edgeRelationship)
    {
        var operationType = GetOperationType(action);

        if (operationType != "ApiConnection" &&
            operationType != "ServiceProvider" &&
            operationType != "ApiManagement" &&
            operationType != "Function")
        {
            return;
        }

        var connectionReferenceName = GetConnectionReferenceName(operationType, action);

        if (string.IsNullOrEmpty(connectionReferenceName))
        {
            _logger.LogInternalWarning($"Action {operationType} in workflow {workflowNode.ResourceName} does not have a valid connection reference name.");
            return;
        }

        if (operationType == "ServiceProvider" && connections.ServiceProviderConnections.TryGetValue(connectionReferenceName, out var serviceProviderConnection))
        {
            var existingConnectionNode = graphAccumulatorDictionary.GetNode(NodeType.ServiceProviderConnection, connectionReferenceName);
            if (existingConnectionNode != null)
            {
                var existingEdge = graphAccumulatorDictionary.GetEdge(workflowNode.GetNodeId(), existingConnectionNode.GetNodeId());

                if (existingEdge != null)
                {
                    // Update edge Relationship
                    var newRelationship = GetUpdatedEdgeRelationship(existingEdge.Relationship, edgeRelationship);
                    await _graphDbClient.ReplaceEdgeRelationshipAsync(existingEdge, newRelationship);
                }
                return;
            }

            var serviceProviderConnectionNode = new ArmResourceNode(
                resourceType: Constants.ServiceProviderConnectionType,
                resourceId: $"{Constants.ServiceProviderConnectionType}/{siteResource.Data.Id}/{connectionReferenceName}",
                subscriptionId: workflowNode.SubscriptionId,
                resourceGroupName: workflowNode.ResourceGroupName!,
                resourceName: connectionReferenceName,
                location: workflowNode.Location ?? string.Empty
            );
            await _graphDbClient.AddOrUpdateNodeAsync(serviceProviderConnectionNode);
            graphAccumulatorDictionary.AddNode(NodeType.ServiceProviderConnection, connectionReferenceName, serviceProviderConnectionNode);

            var edge = new ArmResourceEdge(
                workflowNode.GetNodeId(),
                serviceProviderConnectionNode.GetNodeId(),
                edgeRelationship
            );
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            graphAccumulatorDictionary.AddEdge(edge);


            // Find the corresponding ServiceProviderResourceNode
            var serviceProviderResourceNode = await CrawlServiceProviderConnection(
                    serviceProviderConnection,
                    connectionReferenceName,
                    serviceProviderConnectionNode);

            if (serviceProviderResourceNode != null)
            {
                graphAccumulatorDictionary.AddNode(NodeType.ServiceProviderResource, connectionReferenceName, serviceProviderResourceNode);
            }
            // Remove any stale edges for the service provider connection node
            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, serviceProviderConnectionNode, start);
        }
        else if (operationType == "ApiConnection" && connections.ManagedApiConnections.TryGetValue(connectionReferenceName, out var managedConnection))
        {
            var existingConnectionNode = (ConnectionNode?)graphAccumulatorDictionary.GetNode(NodeType.ManagedApiConnection, connectionReferenceName);
            if (existingConnectionNode != null)
            {
                var existingEdge = graphAccumulatorDictionary.GetEdge(workflowNode.GetNodeId(), existingConnectionNode.GetNodeId());

                if (existingEdge != null)
                {
                    // Update edge label
                    var newRelationship = GetUpdatedEdgeRelationship(existingEdge.Relationship, edgeRelationship);
                    await _graphDbClient.ReplaceEdgeRelationshipAsync(existingEdge, newRelationship);
                }
                return;
            }

            var connectionId = managedConnection.Connection?.Id;
            if (!string.IsNullOrEmpty(connectionId))
            {
                var resourceId = new ResourceIdentifier(connectionId);
                var managedApiConnectionNode = new ConnectionNode(
                    resourceType: Constants.ApiConnectionType,
                    resourceId: connectionId,
                    subscriptionId: resourceId.SubscriptionId!,
                    resourceGroupName: resourceId.ResourceGroupName!,
                    resourceName: resourceId.Name
                );
                await _graphDbClient.AddOrUpdateNodeAsync(managedApiConnectionNode);
                graphAccumulatorDictionary.AddNode(NodeType.ManagedApiConnection, connectionReferenceName, managedApiConnectionNode);

                var edge = new ArmResourceEdge(
                    workflowNode.GetNodeId(),
                    managedApiConnectionNode.GetNodeId(),
                    edgeRelationship
                );
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                graphAccumulatorDictionary.AddEdge(edge);
            }
        }
        else if (operationType == "ApiManagement" && connections.ApiManagementConnections.TryGetValue(connectionReferenceName, out var apiManagementConnection))
        {
            var existingConnectionNode = graphAccumulatorDictionary.GetNode(NodeType.ApiManagementConnection, connectionReferenceName);
            if (existingConnectionNode != null)
            {
                var existingEdge = graphAccumulatorDictionary.GetEdge(workflowNode.GetNodeId(), existingConnectionNode.GetNodeId());

                if (existingEdge != null)
                {
                    // Update edge label
                    var newRelationship = GetUpdatedEdgeRelationship(existingEdge.Relationship, edgeRelationship);
                    await _graphDbClient.ReplaceEdgeRelationshipAsync(existingEdge, newRelationship);
                }
                return;
            }

            var apimConnectionNode = new ArmResourceNode(
                resourceType: Constants.ApiManagementConnectionType,
                resourceId: $"{Constants.ApiManagementConnectionType}/{siteResource.Data.Id}/{connectionReferenceName}",
                subscriptionId: workflowNode.SubscriptionId,
                resourceGroupName: workflowNode.ResourceGroupName!,
                resourceName: connectionReferenceName,
                location: workflowNode.Location ?? string.Empty);
            await _graphDbClient.AddOrUpdateNodeAsync(apimConnectionNode);
            graphAccumulatorDictionary.AddNode(NodeType.ApiManagementConnection, connectionReferenceName, apimConnectionNode);

            var edge = new ArmResourceEdge(
                workflowNode.GetNodeId(),
                apimConnectionNode.GetNodeId(),
                edgeRelationship
            );
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            graphAccumulatorDictionary.AddEdge(edge);

            // Find the corresponding ApiManagementResourceNode
            var apimResourceNode = await CrawlApiManagementConnection(
                    apiManagementConnection,
                    connectionReferenceName,
                    apimConnectionNode);

            if (apimResourceNode != null)
            {
                graphAccumulatorDictionary.AddNode(NodeType.ApiManagementResource, connectionReferenceName, apimResourceNode);
            }

            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, apimConnectionNode, start);
        }
        else if (operationType == "Function" && connections.FunctionConnections.TryGetValue(connectionReferenceName, out var functionConnection))
        {
            var existingNode = graphAccumulatorDictionary.GetNode(NodeType.FunctionConnection, connectionReferenceName);
            if (existingNode != null)
            {
                var existingEdge = graphAccumulatorDictionary.GetEdge(workflowNode.GetNodeId(), existingNode.GetNodeId());

                if (existingEdge != null)
                {
                    // Update edge label
                    var newRelationship = GetUpdatedEdgeRelationship(existingEdge.Relationship, edgeRelationship);
                    await _graphDbClient.ReplaceEdgeRelationshipAsync(existingEdge, newRelationship);
                }
                return;
            }

            var functionConnectionNode = new ArmResourceNode(
                resourceType: Constants.FunctionConnectionType,
                resourceId: $"{Constants.FunctionConnectionType}/{siteResource.Data.Id}/{connectionReferenceName}",
                subscriptionId: workflowNode.SubscriptionId,
                resourceGroupName: workflowNode.ResourceGroupName!,
                resourceName: connectionReferenceName,
                location: workflowNode.Location ?? string.Empty);
            await _graphDbClient.AddOrUpdateNodeAsync(functionConnectionNode);
            graphAccumulatorDictionary.AddNode(NodeType.FunctionConnection, connectionReferenceName, functionConnectionNode);

            var edge = new ArmResourceEdge(
                workflowNode.GetNodeId(),
                functionConnectionNode.GetNodeId(),
                edgeRelationship
            );
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            graphAccumulatorDictionary.AddEdge(edge);

            // Find the corresponding FunctionResourceNode
            var functionResourceNode = await CrawlFunctionConnection(
                functionConnection,
                connectionReferenceName,
                functionConnectionNode);

            if (functionResourceNode != null)
            {
                graphAccumulatorDictionary.AddNode(NodeType.FunctionResource, connectionReferenceName, functionResourceNode);
            }

            // Remove any stale edges for the service provider connection node
            await CrawlerExtensions.RemoveStaleEdgeForNode(_graphDbClient, functionConnectionNode, start);
        }
    }

    private async Task<ArmResourceNode?> CrawlServiceProviderConnection(ServiceProviderConnection serviceProviderConnection,
        string connectionReference,
        ArmResourceNode workflowConnectionNode)
    {
        var serviceProviderResourceNode = await GetServiceProviderResourceNode(
            serviceProviderConnection,
            workflowConnectionNode.SubscriptionId);

        if (serviceProviderResourceNode == null)
            return null;

        // Add or update the service provider resource node
        await _graphDbClient.AddOrUpdateNodeAsync(serviceProviderResourceNode);

        // Create edge from workflow connection to service provider resource
        var connectionToResourceEdge = new ArmResourceEdge(
            workflowConnectionNode.GetNodeId(),
            serviceProviderResourceNode.GetNodeId(),
            Constants.Relationships.Uses);
        await _graphDbClient.AddOrUpdateEdgeAsync(connectionToResourceEdge);

        return serviceProviderResourceNode;
    }

    private async Task<ArmResourceNode?> CrawlApiManagementConnection(ApiManagementConnection apiManagementConnection,
        string connectionReferenceName,
        ArmResourceNode apiManagementConnectionNode)
    {
        var baseUrl = apiManagementConnection.BaseUrl;

        if (string.IsNullOrEmpty(baseUrl))
        {
            _logger.LogInternalWarning($"Api Management connection {connectionReferenceName} does not have a valid base URL.");
            return null;
        }

        var resourceName = ArmHelper.TryParseFirstSubdomainFromHttpsUrl(baseUrl);
        var apiManagementResourceNode = await _armClient.FindGenericArmResource(apiManagementConnectionNode.SubscriptionId, "Microsoft.ApiManagement/service", resourceName);

        if (apiManagementResourceNode == null)
            return null;

        await _graphDbClient.AddOrUpdateNodeAsync(apiManagementResourceNode);

        // Create edge from connection node to API Management resource
        var connectionToResourceEdge = new ArmResourceEdge(
            apiManagementConnectionNode.GetNodeId(),
            apiManagementResourceNode.GetNodeId(),
            Constants.Relationships.Uses);
        await _graphDbClient.AddOrUpdateEdgeAsync(connectionToResourceEdge);

        return apiManagementResourceNode;
    }

    private async Task<ArmResourceNode?> CrawlFunctionConnection(FunctionConnection functionConnection,
        string connectionReferenceName,
        ArmResourceNode functionConnectionNode)
    {
        var triggerUrl = functionConnection.TriggerUrl;

        if (string.IsNullOrEmpty(triggerUrl))
        {
            _logger.LogInternalWarning($"Function connection {connectionReferenceName} does not have a valid trigger URL.");
            return null;
        }

        var resourceName = ArmHelper.TryParseFirstSubdomainFromHttpsUrl(functionConnection.TriggerUrl);
        var functionResourceNode = await _armClient.FindGenericArmResource(functionConnectionNode.SubscriptionId, Constants.AppServiceType, resourceName);

        if (functionResourceNode == null)
            return null;

        await _graphDbClient.AddOrUpdateNodeAsync(functionResourceNode);

        // Create edge from connection Node to Function resource
        var connectionToResourceEdge = new ArmResourceEdge(
            functionConnectionNode.GetNodeId(),
            functionResourceNode.GetNodeId(),
            Constants.Relationships.Uses);
        await _graphDbClient.AddOrUpdateEdgeAsync(connectionToResourceEdge);

        return functionResourceNode;
    }

    private string GetUpdatedEdgeRelationship(string existingRelationship, string newRelationship)
    {
        if (string.IsNullOrEmpty(existingRelationship))
            return newRelationship;

        if (existingRelationship.Equals(newRelationship, StringComparison.OrdinalIgnoreCase))
            return existingRelationship; // No change needed if they're the same

        // If one is Trigger and one is Action, return "UsesTriggerAction"
        if (existingRelationship.Equals(Constants.Relationships.UsesTrigger, StringComparison.OrdinalIgnoreCase) && newRelationship.Equals(Constants.Relationships.UsesAction, StringComparison.OrdinalIgnoreCase) ||
            existingRelationship.Equals(Constants.Relationships.UsesAction, StringComparison.OrdinalIgnoreCase) && newRelationship.Equals(Constants.Relationships.UsesTrigger, StringComparison.OrdinalIgnoreCase))
        {
            return Constants.Relationships.UsesTriggerAction;
        }

        return existingRelationship;
    }

    private string? GetOperationType(JsonElement action)
    {
        if (action.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String)
        {
            return type.GetString();
        }

        return null;
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

    private async Task<LogicAppConnections?> GetConnectionsWithAppSettingsResolved(WebSiteResource webApp, IDictionary<string, string> appSettings)
    {
        var connectionsResponse = await webApp.GetWorkflowsConnectionsAsync();
        var connectionsFile = connectionsResponse?.Value?.Properties?.Files?["connections.json"];

        if (connectionsFile == null)
            return null;

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        LogicAppConnections? logicAppConnections = null;
        try
        {
            logicAppConnections = JsonSerializer.Deserialize<LogicAppConnections>(connectionsFile, options);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to deserialize Logic App connections JSON for {webApp}: {ex.Message}");
            return null;
        }

        ResolveSettings(appSettings, logicAppConnections);

        return logicAppConnections;
    }

    private void ResolveSettings(IDictionary<string, string> appSettings, LogicAppConnections? logicAppConnections)
    {
        if (logicAppConnections?.ServiceProviderConnections != null)
        {
            foreach (var kvp in logicAppConnections.ServiceProviderConnections)
            {
                if (kvp.Value.ParameterValues != null)
                {
                    foreach (var (key, value) in kvp.Value.ParameterValues)
                    {
                        if (value is JsonElement element && (element.ValueKind != JsonValueKind.String))
                            continue;

                        var strValue = value?.ToString();

                        if (!string.IsNullOrEmpty(strValue) &&
                            strValue.StartsWith("@appsetting('", StringComparison.OrdinalIgnoreCase) &&
                            strValue.EndsWith("')", StringComparison.OrdinalIgnoreCase))
                        {
                            var appSettingKey = strValue.Substring(13, strValue.Length - 15);
                            if (appSettings.TryGetValue(appSettingKey, out var resolved))
                            {
                                kvp.Value.ParameterValues[key] = resolved;
                            }
                            else
                            {
                                _logger.LogInternalWarning($"App setting not found in connection lookup for key: {appSettingKey}");
                                kvp.Value.ParameterValues.Remove(key);
                            }
                        }
                    }
                }
            }
        }
    }

    private async Task<ArmResourceNode?> GetServiceProviderResourceNode(ServiceProviderConnection connection, string subscriptionId)
    {
        // Checking if we support the service provider currently
        if (!ServiceProviderConnectorRegistry.Entries.TryGetValue(connection.ServiceProvider.Id, out var registryEntry))
            return null;

        ArmResourceNode? resourceNode = null;

        foreach (var param in registryEntry.ConnectionParameters)
        {
            if (connection?.ParameterValues != null && connection.ParameterValues.TryGetValue(param, out var paramValue))
            {
                var resourceName = registryEntry!.Parser.Parse((string)paramValue, param);
                resourceNode = await _armClient.FindGenericArmResource(subscriptionId, registryEntry!.ResourceType, resourceName);
                break;
            }
        }

        return resourceNode;
    }

    private string? GetManagedApiConnectionReferenceName(JsonElement operation)
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

    private string? GetServiceProviderReferenceName(JsonElement operation)
    {
        if (!operation.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || !"ServiceProvider".Equals(type.GetString(), StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (operation.TryGetProperty("inputs", out var inputs) &&
            inputs.TryGetProperty("serviceProviderConfiguration", out var host) &&
            host.TryGetProperty("connectionName", out var referenceName) &&
            referenceName.ValueKind == JsonValueKind.String)
        {
            return referenceName.GetString();
        }

        return null;
    }

    private string? GetApiManagementConnectionReferenceName(JsonElement operation)
    {
        if (operation.TryGetProperty("inputs", out var inputs) &&
            inputs.TryGetProperty("apiManagement", out var host) &&
            host.TryGetProperty("connection", out var referenceName) &&
            referenceName.ValueKind == JsonValueKind.String)
        {
            return referenceName.GetString();
        }

        return null;
    }

    private string? GetFunctionConnectionReferenceName(JsonElement operation)
    {
        if (operation.TryGetProperty("inputs", out var inputs) &&
            inputs.TryGetProperty("function", out var host) &&
            host.TryGetProperty("connectionName", out var referenceName) &&
            referenceName.ValueKind == JsonValueKind.String)
        {
            return referenceName.GetString();
        }

        return null;
    }

    private string? GetConnectionReferenceName(string operationType, JsonElement operation)
    {
        switch (operationType)
        {
            case "ApiConnection":
                return GetManagedApiConnectionReferenceName(operation);
            case "ServiceProvider":
                return GetServiceProviderReferenceName(operation);
            case "ApiManagement":
                return GetApiManagementConnectionReferenceName(operation);
            case "Function":
                return GetFunctionConnectionReferenceName(operation);
            default:
                return null;
        }
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
        public Dictionary<string, ApiManagementConnection> ApiManagementConnections { get; set; } = new();
        public Dictionary<string, FunctionConnection> FunctionConnections { get; set; } = new();
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

    public class ApiManagementConnection
    {
        public string? ApiId { get; set; }
        public string? BaseUrl { get; set; }
        public string? DisplayName { get; set; }
        public string? SubscriptionKey { get; set; }
    }

    public class FunctionConnection
    {
        public FunctionAuthentication? Authentication { get; set; }
        public string? FunctionName { get; set; }
        public FunctionReference? Function { get; set; }
        public string? TriggerUrl { get; set; }
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

    public class FunctionAuthentication
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Value { get; set; }
    }

    public class FunctionReference
    {
        public string? Id { get; set; }
    }

    private enum NodeType
    {
        ManagedApiConnection,      // Logic App managed API connection node
        ServiceProviderResource,   // ARM resource from a Service Provider connection
        ServiceProviderConnection, // Logic App Service Provider connection node
        ApiManagementConnection,   // Logic App APIM connection node
        ApiManagementResource,     // API Management service resource
        FunctionConnection,        // Logic App Function connection node
        FunctionResource           // Function App resource
    }

    private sealed class NodeAccumulatorDictionary
    {
        private readonly Dictionary<(NodeType Type, string ReferenceName), ArmResourceNode> _nodes = new();

        public bool Add(NodeType type, string referenceName, ArmResourceNode node) =>
            _nodes.TryAdd((type, referenceName), node);

        public IEnumerable<ArmResourceNode> GetByKind(NodeType type) =>
            _nodes.Where(kv => kv.Key.Type == type).Select(kv => kv.Value);

        public ArmResourceNode? Get(NodeType type, string referenceName) =>
        _nodes.TryGetValue((type, referenceName), out var node) ? node : null;
    }

    private sealed class EdgeAccumulatorDictionary
    {
        private readonly Dictionary<(string SourceNodeId, string TargetNodeId), ArmResourceEdge> _edges = new();

        public bool Add(ArmResourceEdge edge) =>
            _edges.TryAdd((edge.SourceNodeId, edge.TargetNodeId), edge);

        public ArmResourceEdge? Get(string sourceNodeId, string targetNodeId) =>
            _edges.TryGetValue((sourceNodeId, targetNodeId), out var edge) ? edge : null;
    }

    private sealed class GraphAccumulatorDictionary
    {
        private readonly NodeAccumulatorDictionary _nodeAccumulator = new();
        private readonly EdgeAccumulatorDictionary _edgeAccumulator = new();

        public bool AddNode(NodeType type, string referenceName, ArmResourceNode node) =>
            _nodeAccumulator.Add(type, referenceName, node);

        public IEnumerable<ArmResourceNode> GetNodesByKind(NodeType type) =>
            _nodeAccumulator.GetByKind(type);

        public ArmResourceNode? GetNode(NodeType type, string referenceName) => 
            _nodeAccumulator.Get(type, referenceName);

        public bool AddEdge(ArmResourceEdge edge) =>
            _edgeAccumulator.Add(edge);

        public ArmResourceEdge? GetEdge(string sourceNodeId, string targetNodeId) => 
            _edgeAccumulator.Get(sourceNodeId, targetNodeId);
    }
}

internal interface IServiceProviderConnectorParser
{
    /// <summary>
    /// Parses a connection parameter (e.g., connection string or endpoint) and returns the resource name or ID.
    /// </summary>
    string? Parse(string parameterValue, string parameterName);
}

internal sealed class ServiceProviderConnectorRegistryEntry
{
    public IReadOnlyList<string> ConnectionParameters { get; init; }
    public IServiceProviderConnectorParser Parser { get; init; }
    public string ResourceType { get; init; }

    public ServiceProviderConnectorRegistryEntry(IReadOnlyList<string> connectionParameters, IServiceProviderConnectorParser parserType, string resourceType)
    {
        ConnectionParameters = connectionParameters;
        Parser = parserType;
        ResourceType = resourceType;
    }
}

internal class StorageAccountParser : IServiceProviderConnectorParser
{
    public string? Parse(string parameterValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
            return null;

        if (string.Equals(parameterName, "connectionString", StringComparison.OrdinalIgnoreCase))
            return ArmHelper.TryParseStorageAccountFromConnectionString(parameterValue);

        if (string.Equals(parameterName, "blobStorageEndpoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameterName, "tableStorageEndpoint", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameterName, "queueServiceUri", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(parameterName, "storageAccountUri", StringComparison.OrdinalIgnoreCase))
        {
            return ArmHelper.TryParseStorageAccountFromNameOrEndpoint(parameterValue);
        }

        return null;
    }
}

internal class ServiceBusParser : IServiceProviderConnectorParser
{
    public string? Parse(string parameterValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
            return null;

        if (string.Equals(parameterName, "connectionString", StringComparison.OrdinalIgnoreCase))
            return ArmHelper.TryParseServiceBusFromConnectionString(parameterValue);

        if (string.Equals(parameterName, "fullyQualifiedNamespace", StringComparison.OrdinalIgnoreCase))
            return ArmHelper.TryParseFirstSubdomainFromHttpsUrl(parameterValue);

        return null;
    }
}

internal class SqlServerParser : IServiceProviderConnectorParser
{
    public string? Parse(string parameterValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
            return null;

        // SQL only has connection string parameter
        if (string.Equals(parameterName, "connectionString", StringComparison.OrdinalIgnoreCase))
            return ArmHelper.TryParseSQLServerFromConnectionString(parameterValue);

        return null;
    }
}

internal class CosmosDBParser : IServiceProviderConnectorParser
{
    public string? Parse(string parameterValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
            return null;

        if (string.Equals(parameterName, "connectionString", StringComparison.OrdinalIgnoreCase))
            return ArmHelper.TryParseCosmosDbFromConnectionString(parameterValue);

        return null;
    }
}

// Generic Endpoint parser for service provider connections that only has an endpoint as a parameter
internal class EndpointSubdomainParser : IServiceProviderConnectorParser
{
    public string? Parse(string parameterValue, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(parameterValue))
            return null;

        return ArmHelper.TryParseFirstSubdomainFromHttpsUrl(parameterValue);
    }
}

internal static class ServiceProviderConnectorRegistry
{
    public static readonly Dictionary<string, ServiceProviderConnectorRegistryEntry> Entries = new(StringComparer.OrdinalIgnoreCase)
    {
        ["/serviceProviders/AzureBlob"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString", "blobStorageEndpoint" },
            new StorageAccountParser(),
            "Microsoft.Storage/storageAccounts"
        ),
        ["/serviceProviders/azureTables"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString", "tableStorageEndpoint" },
            new StorageAccountParser(),
            "Microsoft.Storage/storageAccounts"
        ),
        ["/serviceProviders/azurequeues"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString", "queueServiceUri" },
            new StorageAccountParser(),
            "Microsoft.Storage/storageAccounts"
        ),
        ["/serviceProviders/AzureFile"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString", "storageAccountUri" },
            new StorageAccountParser(),
            "Microsoft.Storage/storageAccounts"
        ),
        ["/serviceProviders/serviceBus"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString", "fullyQualifiedNamespace" },
            new ServiceBusParser(),
            "Microsoft.ServiceBus/namespaces"
        ),
        ["/serviceProviders/eventHub"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString", "fullyQualifiedNamespace" },
            new ServiceBusParser(), // Reusing ServiceBusParser for Event Hubs as they share similar format
            "Microsoft.EventHub/namespaces"
        ),
        ["/serviceProviders/sql"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString" },
            new SqlServerParser(),
            "Microsoft.Sql/servers"
        ),
        ["/serviceProviders/AzureCosmosDB"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "connectionString" },
            new CosmosDBParser(),
            "Microsoft.DocumentDb/databaseAccounts"
        ),
        ["/serviceProviders/keyVault"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "VaultUri" },
            new EndpointSubdomainParser(),
            "Microsoft.KeyVault/vaults"
        ),
        ["/serviceProviders/eventGridPublisher"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "topicEndpoint" },
            new EndpointSubdomainParser(),
            "Microsoft.EventGrid/topics"
        ),
        ["/serviceProviders/azureaisearch"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "searchServiceEndpoint" },
            new EndpointSubdomainParser(),
            "Microsoft.Search/searchServices"
        ),
        ["/serviceProviders/openai"] = new ServiceProviderConnectorRegistryEntry(
            new[] { "openAIEndpoint" },
            new EndpointSubdomainParser(),
            "Microsoft.CognitiveServices/accounts"
        )
    };
}
