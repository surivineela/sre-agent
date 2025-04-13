// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Crawler.Metrics;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler;
public class ScoreCardService
{
    private readonly ILogger<ScoreCardService> _logger;
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly IEnumerable<IResourceMetricsCollector> _metricsCollector;

    public ScoreCardService(
        ILogger<ScoreCardService> logger,
        IGraphDatabaseClient graphDatabaseClient,
        IEnumerable<IResourceMetricsCollector> resourceMetricsCollectors)
    {
        _logger = logger;
        _graphDatabaseClient = graphDatabaseClient;
        _metricsCollector = resourceMetricsCollectors;
    }

    public async Task UpdateAllScoreCardsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting score card update for all resources");
        string nodesToUpdateQuery = GetResourceNodesToUpdateQuery();

        var queryResults = await _graphDatabaseClient.Query(nodesToUpdateQuery);
        int updatedCount = 0;

        foreach (var result in queryResults)
        {
            if (result == null)
            {
                _logger.LogWarning("Null result encountered, skipping");
                continue;
            }

            try
            {
                var armResourceNode = CreateArmResourceNodeFromDictionary(result);
                if (armResourceNode == null)
                {
                    _logger.LogWarning($"Could not create ArmResourceNode from result");
                    continue;
                }

                var updated = await UpdateScoreCard(armResourceNode);
                if (updated) updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating score card for node");
            }
        }

        _logger.LogInformation($"Updated scorecards for {updatedCount} nodes");
    }

    private ArmResourceNode CreateArmResourceNodeFromDictionary(Dictionary<string, object> result)
    {
        try
        {
            // Get primary fields
            string id = result["id"]?.ToString();
            string name = result["name"]?.ToString();
            string type = result["type"]?.ToString();

            var properties = result["properties"] as Dictionary<string, object>;
            if (properties == null)
            {
                _logger.LogWarning($"Properties is null for node {id}");
                return null;
            }

            // Extract values, handling arrays in property values
            string resourceId = GetFirstPropertyValue(properties, "resourceId") ?? id;
            string subscriptionId = GetFirstPropertyValue(properties, "subscriptionId");
            string resourceGroupName = GetFirstPropertyValue(properties, "resourceGroupName");
            string resourceName = GetFirstPropertyValue(properties, "resourceName") ?? name;
            string location = GetFirstPropertyValue(properties, "location");

            // Create the ArmResourceNode
            var armResourceNode = new ArmResourceNode(
                resourceType: type,
                resourceId: resourceId,
                subscriptionId: subscriptionId,
                resourceGroupName: resourceGroupName,
                resourceName: resourceName,
                location: location
            );

            // Add any additional properties
            foreach (var prop in properties)
            {
                if (!armResourceNode.GetNodeProperties().ContainsKey(prop.Key))
                {
                    string value = GetFirstPropertyValue(properties, prop.Key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        armResourceNode.GetNodeProperties()[prop.Key] = value;
                    }
                }
            }

            return armResourceNode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error converting dictionary to ArmResourceNode");
            return null;
        }
    }

    // Helper method to extract the first value from a property that might be an array
    private string GetFirstPropertyValue(Dictionary<string, object> properties, string key)
    {
        if (properties == null || !properties.ContainsKey(key) || properties[key] == null)
            return null;

        var value = properties[key];

        // If it's an array/list, get the first item
        if (value is IEnumerable enumerable && !(value is string))
        {
            foreach (var item in enumerable)
            {
                return item?.ToString();
            }
        }

        // Otherwise, just return the value as string
        return value.ToString();
    }

    private async Task<bool> UpdateScoreCard(ArmResourceNode node)
    {
        var collector = _metricsCollector.FirstOrDefault(c => c.ResourceType == node.ResourceType);

        if (collector == null)
        {
            _logger.LogWarning($"No metrics collector found for resource type {node.ResourceType}, resource name: {node.ResourceName}");
            return false;
        }
        
        try
        {
            var appHealthInfo = await collector.CollectMetricsAsync(node);
            node.AppHealthInfo = appHealthInfo;
            await _graphDatabaseClient.AddOrUpdateNodeAsync(node);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update appHealthInfo for node {node.ResourceId} ({node.ResourceName})");
            return false;
        }
    }

    private string GetResourceNodesToUpdateQuery()
    {
        // TODO: Update the query with resources that has AppHealthInfo
        // Function App vs Web App was causing issue. So removing it for now.
        return $@"g.V()
                .hasLabel(within(
                    '{Constants.ContainerAppType.ToLower()}',
                    '{Constants.AppServiceType.ToLower()}',
                    '{Constants.AzureRedisCacheType.ToLower()}',
                ))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";
    }
}
