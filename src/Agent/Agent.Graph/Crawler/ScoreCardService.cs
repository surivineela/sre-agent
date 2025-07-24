// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Crawler.Metrics;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler;

public class ScoreCardService
{
    private readonly ILogger<ScoreCardService> _logger;
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly IEnumerable<IResourceMetricsCollector> _metricsCollector;
    private readonly IAppHealthHistoryRepository _appHealthHistoryRepository;

    public ScoreCardService(
        ILogger<ScoreCardService> logger,
        IGraphDatabaseClient graphDatabaseClient,
        IEnumerable<IResourceMetricsCollector> resourceMetricsCollectors,
        IAppHealthHistoryRepository appHealthHistoryRepository)
    {
        _logger = logger;
        _graphDatabaseClient = graphDatabaseClient;
        _metricsCollector = resourceMetricsCollectors;
        _appHealthHistoryRepository = appHealthHistoryRepository;
    }

    public async Task UpdateAllScoreCardsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting score card update for all resources");
        string nodesToUpdateQuery = GetResourceNodesToUpdateQuery();

        var queryResults = await _graphDatabaseClient.Query(nodesToUpdateQuery);
        int updatedCount = 0;

        // First, prune old health history data points (older than 24 hours)
        // this is 24 hours bc we use this health history for the daily report, we can extend this in the future
        try
        {
            var olderThan = DateTime.UtcNow.AddDays(-1);
            var (documentsUpdated, pointsRemoved) = await _appHealthHistoryRepository.PruneAppHealthHistoryAsync(olderThan);
            _logger.LogInternalInformation($"Pruned {pointsRemoved} old app health history data points from {documentsUpdated} documents");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error pruning old app health history data points");
        }

        foreach (var result in queryResults)
        {
            if (result == null)
            {
                _logger.LogInternalWarning("Null result encountered, skipping");
                continue;
            }

            try
            {
                string? resourceType = result["type"]?.ToString();
                if (string.IsNullOrEmpty(resourceType))
                {
                    _logger.LogInternalWarning($"Resource type is null or empty for result: {result}");
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
                        _logger.LogInternalWarning($"Could not create KubernetesResourceNode from result");
                        continue;
                    }

                    updated = await UpdateScoreCardForAKSNode(node);
                    if (updated) updatedCount++;
                    continue;
                }
                var armResourceNode = CreateArmResourceNodeFromDictionary(result);
                if (armResourceNode == null)
                {
                    _logger.LogInternalWarning($"Could not create ArmResourceNode from result");
                    continue;
                }

                updated = await UpdateScoreCard(armResourceNode);
                if (updated) updatedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error updating score card for node");
            }
        }

        _logger.LogInternalInformation($"Updated scorecards for {updatedCount} nodes");
    }

    private ArmResourceNode? CreateArmResourceNodeFromDictionary(Dictionary<string, object> result)
    {
        try
        {
            // Get primary fields
            string id = result["id"]?.ToString() ?? throw new Exception("ID is missing from the result.");
            string name = result["name"]?.ToString() ?? throw new Exception("Name is missing from the result.");
            string type = result["type"]?.ToString() ?? throw new Exception("Type is missing from the result.");

            var properties = result["properties"] as Dictionary<string, object>;
            if (properties == null)
            {
                _logger.LogInternalWarning($"Properties is null for node {id}");
                return null;
            }

            // Extract values, handling arrays in property values
            string resourceId = GetFirstPropertyValue(properties, "resourceId") ?? id;
            string subscriptionId = GetFirstPropertyValue(properties, "subscriptionId") ?? throw new Exception("SubscriptionId is missing");
            string resourceGroupName = GetFirstPropertyValue(properties, "resourceGroupName") ?? throw new Exception("ResourceGroupName is missing");
            string resourceName = GetFirstPropertyValue(properties, "resourceName") ?? name;
            string location = GetFirstPropertyValue(properties, "location") ?? throw new Exception("Location is missing");

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
                    string? value = GetFirstPropertyValue(properties, prop.Key);
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
            _logger.LogInternalError(ex, $"Error converting dictionary to ArmResourceNode");
            return null;
        }
    }

    private KubernetesNamespacedResourceNode? CreateKubernetesResourceNodeFromDictionary(Dictionary<string, object> result)
    {
        try
        {
            // Get primary fields
            string id = result["id"]?.ToString() ?? throw new Exception("ID is missing from the result.");
            string name = result["name"]?.ToString() ?? throw new Exception("Name is missing from the result.");
            string type = result["type"]?.ToString() ?? throw new Exception("Type is missing from the result.");

            var properties = result["properties"] as Dictionary<string, object>;
            if (properties == null)
            {
                _logger.LogInternalWarning($"Properties is null for node {id}");
                return null;
            }

            // Extract Kubernetes specific values
            string subscriptionId = GetFirstPropertyValue(properties, "subscriptionId") ?? throw new Exception("SubscriptionId is missing");
            string resourceGroupName = GetFirstPropertyValue(properties, "resourceGroupName") ?? throw new Exception("ResourceGroupName is missing");
            string location = GetFirstPropertyValue(properties, "location") ?? throw new Exception("Location is missing");
            string _namespace = GetFirstPropertyValue(properties, "namespace") ?? throw new Exception("Namespace is missing");
            string clusterResourceId = GetFirstPropertyValue(properties, "clusterResourceId") ?? throw new Exception("ClusterResourceId is missing");
            string resourceName = GetFirstPropertyValue(properties, "resourceName") ?? name;
            string group = GetFirstPropertyValue(properties, "group") ?? throw new Exception("Group is missing");
            string apiVersion = GetFirstPropertyValue(properties, "apiVersion") ?? throw new Exception("ApiVersion is missing");
            string kind = GetFirstPropertyValue(properties, "kind") ?? throw new Exception("Kind is missing");

            // Extract annotations and labels
            Dictionary<string, string> annotations = new Dictionary<string, string>();
            Dictionary<string, string> labels = new Dictionary<string, string>();

            foreach (var prop in properties)
            {
                if (prop.Key.StartsWith("annotation_"))
                {
                    string annotationKey = prop.Key.Substring("annotation_".Length);
                    string? value = GetFirstPropertyValue(properties, prop.Key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        annotations[annotationKey] = value;
                    }
                }
                else if (prop.Key.StartsWith("label_"))
                {
                    string labelKey = prop.Key.Substring("label_".Length);
                    string? value = GetFirstPropertyValue(properties, prop.Key);
                    if (!string.IsNullOrEmpty(value))
                    {
                        labels[labelKey] = value;
                    }
                }
            }

            // Create the KubernetesResourceNode
            var kubernetesResourceNode = new KubernetesNamespacedResourceNode(
                k8sObject: null, // ResourceObject is not available during graph query
                clusterResourceId: clusterResourceId,
                @namespace: _namespace,
                subscriptionId: subscriptionId,
                resourceGroupName: resourceGroupName,
                location: location,
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
            _logger.LogInternalError(ex, $"Error converting dictionary to KubernetesResourceNode");
            return null;
        }
    }

    // Helper method to extract the first value from a property that might be an array
    private string? GetFirstPropertyValue(Dictionary<string, object> properties, string key)
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
            _logger.LogInternalWarning($"No metrics collector found for resource type {node.ResourceType}, resource name: {node.ResourceName}");
            return false;
        }

        try
        {
            var appHealthInfo = await collector.CollectMetricsAsync(node);
            node.AppHealthInfo = appHealthInfo;
            await _graphDatabaseClient.AddOrUpdateNodeAsync(node);

            // Save app health information to CosmosDB history
            if (appHealthInfo != null)
            {
                try
                {
                    await _appHealthHistoryRepository.UpdateAppHealthHistoryAsync(
                        node.ResourceId,
                        node.ResourceName ?? string.Empty,
                        node.ResourceType,
                        appHealthInfo);

                    _logger.LogInternalInformation($"Updated app health history for {node.ResourceName} ({node.ResourceType})");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Failed to update app health history for resource {node.ResourceId} ({node.ResourceName})");
                    // We continue with the scorecard update even if saving history fails
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to update appHealthInfo for node {node.ResourceId} ({node.ResourceName})");
            return false;
        }
    }


    private async Task<bool> UpdateScoreCardForAKSNode(KubernetesNamespacedResourceNode node)
    {
        var collector = _metricsCollector.OfType<AKSMetricsCollector>().FirstOrDefault();

        if (collector == null)
        {
            _logger.LogInternalWarning($"No metrics collector found for resource type {node.Kind.ToLowerInvariant()}, resource name: {node.ResourceName}");
            return false;
        }

        try
        {
            var appHealthInfo = await collector.CollectMetricsAsync(node);
            node.AppHealthInfo = appHealthInfo;
            await _graphDatabaseClient.AddOrUpdateNodeAsync(node);

            // Save app health information to CosmosDB history
            if (appHealthInfo != null)
            {
                try
                {
                    await _appHealthHistoryRepository.UpdateAppHealthHistoryAsync(
                        node.GetNodeId(),
                        node.ResourceName,
                        node.Kind,
                        appHealthInfo);

                    _logger.LogInternalInformation($"Updated app health history for {node.ResourceName} ({node.Kind})");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Failed to update app health history for resource {node.GetNodeId()} ({node.ResourceName})");
                    // We continue with the scorecard update even if saving history fails
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to update appHealthInfo for node {node.GetNodeId()} ({node.ResourceName})");
            return false;
        }
    }

    private string GetResourceNodesToUpdateQuery()
    {
        // TODO: Update the query with resources that has AppHealthInfo
        // Function App vs Web App was causing issue. So removing it for now.
        return $@"g.V().has('isDeleted', false)
                .hasLabel(within(
                    '{Constants.ContainerAppType.ToLower()}',
                    '{Constants.AppServiceType.ToLower()}',
                    '{Constants.AzureRedisCacheType.ToLower()}',
                    '{Constants.AzureKubernetesServiceDeploymentType.ToLower()}',
                    '{Constants.AzureKubernetesServiceStatefulSetType.ToLower()}',
                    '{Constants.AzureKubernetesServicePodType.ToLower()}',
                    '{Constants.ApiManagementType.ToLower()}',
                ))
                .project('id', 'name', 'type', 'properties')
                .by(id())
                .by(coalesce(values('resourceName'), constant('')))
                .by(label())
                .by(valueMap())";
    }
}
