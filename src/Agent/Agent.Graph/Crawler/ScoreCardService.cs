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
                var resourceType = result["type"]?.ToString();
                if (string.IsNullOrEmpty(resourceType))
                {
                    _logger.LogWarning($"Resource type is null or empty for result: {result}");
                    continue;
                }
                var updated = false;
                if (resourceType.Equals(Constants.AzureKubernetesServiceDeploymentType, StringComparison.OrdinalIgnoreCase) ||
                    resourceType.Equals(Constants.AzureKubernetesServiceStatefulSetType, StringComparison.OrdinalIgnoreCase) ||
                    resourceType.Equals(Constants.AzureKubernetesServicePodType, StringComparison.OrdinalIgnoreCase))
                {
                    var node = CreateKubernetesResourceNodeFromDictionary(result);
                    if (node == null)
                    {
                        _logger.LogWarning($"Could not create KubernetesResourceNode from result");
                        continue;
                    }

                    updated = await UpdateScoreCardForAKSNode(node);
                    if (updated) updatedCount++;
                    continue;
                }
                var armResourceNode = CreateArmResourceNodeFromDictionary(result);
                if (armResourceNode == null)
                {
                    _logger.LogWarning($"Could not create ArmResourceNode from result");
                    continue;
                }

                updated = await UpdateScoreCard(armResourceNode);
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

    private KubernetesNamespacedResourceNode CreateKubernetesResourceNodeFromDictionary(Dictionary<string, object> result)
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

            // Extract Kubernetes specific values
            string subscriptionId = GetFirstPropertyValue(properties, "subscriptionId");
            string resourceGroupName = GetFirstPropertyValue(properties, "resourceGroupName");
            string _namespace = GetFirstPropertyValue(properties, "namespace");
            string clusterResourceId = GetFirstPropertyValue(properties, "clusterResourceId");
            string resourceName = GetFirstPropertyValue(properties, "resourceName") ?? name;
            string group = GetFirstPropertyValue(properties, "group");
            string apiVersion = GetFirstPropertyValue(properties, "apiVersion");
            string kind = GetFirstPropertyValue(properties, "kind");

            // Extract annotations and labels
            Dictionary<string, string> annotations = new Dictionary<string, string>();
            Dictionary<string, string> labels = new Dictionary<string, string>();

            foreach (var prop in properties)
            {
                if (prop.Key.StartsWith("annotation_"))
                {
                    string annotationKey = prop.Key.Substring("annotation_".Length);
                    string value = GetFirstPropertyValue(properties, prop.Key);
                    annotations[annotationKey] = value;
                }
                else if (prop.Key.StartsWith("label_"))
                {
                    string labelKey = prop.Key.Substring("label_".Length);
                    string value = GetFirstPropertyValue(properties, prop.Key);
                    labels[labelKey] = value;
                }
            }

            // Create the KubernetesResourceNode
            var kubernetesResourceNode = new KubernetesNamespacedResourceNode(
                k8sObject: null, // ResourceObject is not available during graph query
                clusterResourceId: clusterResourceId,
                @namespace: _namespace,
                subscriptionId: subscriptionId,
                resourceGroupName: resourceGroupName,
                resourceName: resourceName,
                group: group,
                apiVersion: apiVersion,
                kind: kind,
                annotations: annotations.Count > 0 ? annotations : null,
                labels: labels.Count > 0 ? labels : null
            );

            return kubernetesResourceNode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error converting dictionary to KubernetesResourceNode");
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


    private async Task<bool> UpdateScoreCardForAKSNode(KubernetesNamespacedResourceNode node)
    {

        var collector = _metricsCollector.OfType<AKSMetricsCollector>().FirstOrDefault();

        if (collector == null)
        {
            _logger.LogWarning($"No metrics collector found for resource type {node.Kind.ToLowerInvariant()}, resource name: {node.ResourceName}");
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
            _logger.LogError(ex, $"Failed to update appHealthInfo for node {node.GetNodeId()} ({node.ResourceName})");
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
                    '{Constants.AzureKubernetesServiceDeploymentType.ToLower()}',
                    '{Constants.AzureKubernetesServiceStatefulSetType.ToLower()}',
                    '{Constants.AzureKubernetesServicePodType.ToLower()}',
                ))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";
    }
}
