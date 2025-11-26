// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Extensions;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ConnectionCrawler : GenericArmResourceCrawler
{
    private readonly ILogger _logger;
    private readonly IGraphDatabaseClient _graphDbClient;

    public ConnectionCrawler(ILogger logger, IGraphDatabaseClient dbManager, ArmClient armClient)
        : base(logger, dbManager, armClient, false)
    {
        _logger = logger;
        _graphDbClient = dbManager;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        await foreach (var n in base.Crawl(node))
        {
            yield return n;
        }

        if (node is ConnectionNode connectionNode)
        {
            var id = new ResourceIdentifier(connectionNode.ResourceId);
            try
            {
                var response = await _armClient.GetGenericResource(id).GetAsync();
                var properties = response.Value.Data.Properties.ToObjectFromJson<ApiConnectionPropertiesDefinition>();

                connectionNode.ConnectorId = properties?.Api?.Id;
                await _graphDbClient.AddOrUpdateNodeAsync(connectionNode);

                var connectedResource = await FindConnectedResourceFromProperties(properties, node.GetSubscriptionId());
                if (connectedResource != null)
                {
                    await _graphDbClient.AddOrUpdateNodeAsync(connectedResource);

                    var edge = new ArmResourceEdge(connectionNode.GetNodeId(), connectedResource.GetNodeId(), Constants.Relationships.Connected);
                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                }

                _logger.LogInternalInformation($"Crawled ARM resource: {connectionNode.ResourceId}");
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == (int)HttpStatusCode.Unauthorized)
                {
                    _logger.LogDebug($"Agent MI does not have permission on {connectionNode.ResourceId}");
                }
                else if (ex.Status == (int)HttpStatusCode.BadRequest)
                {
                    if (ex.ErrorCode == "NoRegisteredProviderFound")
                    {
                        _logger.LogDebug($"No registered provider found: {connectionNode.ResourceId}, {ex}");
                    }
                    else
                    {
                        _logger.LogInternalWarning($"Failed to get resource: {connectionNode.ResourceId}, {ex}");
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogInternalWarning($"Invalid operation for resource: {connectionNode.ResourceId}, {ex}");
            }
        }
    }

    private async Task<GraphNode?> FindConnectedResourceFromProperties(ApiConnectionPropertiesDefinition? properties, string subscriptionId)
    {
        var id = properties?.Api?.Id;
        if (!string.IsNullOrEmpty(id))
        {
            if (id.EndsWith("/managedApis/azuretables", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["keyBasedAuth"], "storageaccount");
                var resourceName = ArmHelper.TryParseStorageAccountFromNameOrEndpoint(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.StorageType, resourceName);
            }

            if (id.EndsWith("/managedApis/azureblob", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["keyBasedAuth"], "accountName");
                var resourceName = ArmHelper.TryParseStorageAccountFromNameOrEndpoint(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.StorageType, resourceName);
            }

            if (id.EndsWith("/managedApis/azurequeues", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["keyBasedAuth"], "storageaccount");
                var resourceName = ArmHelper.TryParseStorageAccountFromNameOrEndpoint(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.StorageType, resourceName);
            }

            if (id.EndsWith("/managedApis/azurefile", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameter(properties, "accountName");
                var resourceName = ArmHelper.TryParseStorageAccountFromNameOrEndpoint(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.StorageType, resourceName);
            }

            if (id.EndsWith("/managedApis/servicebus", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["managedIdentityAuth", "aadAuth", "CertOauth"], "namespaceEndpoint");
                var resourceName = ArmHelper.TryParseFirstSubdomainFromHttpsUrl(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.ServiceBusType, resourceName);
            }

            if (id.EndsWith("/managedApis/eventhubs", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["tokenBasedAuth", "managedIdentityAuth"], "namespaceEndpoint");
                var resourceName = ArmHelper.TryParseFirstSubdomainFromHttpsUrl(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.EventHubType, resourceName);
            }

            if (id.EndsWith("/managedApis/documentdb", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["keyBasedAuth"], "databaseAccount");
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.CosmosDbType, value);
            }

            if (id.EndsWith("/managedApis/keyvault", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["CertOauth", "oauthMI", "oauthDefault"], "vaultName");
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.KeyVaultType, value);
            }

            if (id.EndsWith("/managedApis/azureeventgridpublish", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameter(properties, "endpoint");
                var resourceName = ArmHelper.TryParseFirstSubdomainFromHttpsUrl(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.EventGridTopicType, resourceName);
            }

            if (id.EndsWith("/managedApis/sqldw", StringComparison.OrdinalIgnoreCase))
            {
                var value = GetStringValueFromParameterSet(properties, ["sqlAuthentication"], "server");
                var resourceName = ArmHelper.TryParseSynapseWorkspaceFromEndpoint(value);
                return await _armClient.FindGenericArmResource(subscriptionId, ResourceKindHelper.SynapseWorkspaceType, resourceName);
            }
        }

        return null;
    }

    private string? GetStringValueFromParameterSet(ApiConnectionPropertiesDefinition? properties, string[] parameterValueSetNames, string parameterName)
    {
        if (parameterValueSetNames.IndexOf(parameterValueSetName => properties?.ParameterValueSet?.Name == parameterValueSetName) >= 0)
        {
            if (properties?.ParameterValueSet?.Values?.TryGetValue(parameterName, out var parameter) == true && parameter?.Value is JsonElement element)
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    return null;
                }

                return element.GetString();
            }
        }

        return null;
    }

    private string? GetStringValueFromParameter(ApiConnectionPropertiesDefinition? properties, string parameterName)
    {
        if (properties?.ParameterValues?.TryGetValue(parameterName, out var parameter) == true && parameter is JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return element.GetString();
        }

        return null;
    }
}

public class ApiConnectionPropertiesDefinition
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the overall status.
    /// </summary>
    [JsonPropertyName("overallStatus")]
    public string? OverallStatus { get; set; }

    /// <summary>
    /// Gets or sets the connection statuses.
    /// </summary>
    [JsonPropertyName("statuses")]
    public List<object>? Statuses { get; set; }

    /// <summary>
    /// Gets or sets the created time.
    /// </summary>
    [JsonPropertyName("createdTime")]
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// Gets or sets the changed time.
    /// </summary>
    [JsonPropertyName("changedTime")]
    public DateTime? ChangedTime { get; set; }

    /// <summary>
    /// Gets or sets the api reference.
    /// </summary>
    [JsonPropertyName("api")]
    public ApiReference? Api { get; set; }

    [JsonPropertyName("parameterValueSet")]
    public ParameterValueSet? ParameterValueSet { get; set; }

    [JsonPropertyName("parameterValues")]
    public Dictionary<string, object>? ParameterValues { get; set; }
}

public class ApiReference
{
    /// <summary>
    /// Gets or sets the name of the API.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class ParameterValueSet
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("values")]
    public Dictionary<string, ParameterValue>? Values { get; set; }
}

public class ParameterValue
{
    [JsonPropertyName("value")]
    public object? Value { get; set; }
}
