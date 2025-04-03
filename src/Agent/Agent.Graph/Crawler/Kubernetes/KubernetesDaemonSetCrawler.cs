// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using k8s;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class K8sDaemonSetCrawler : IResourceCrawler
{
    private readonly ILogger<K8sDaemonSetCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly IKubernetes _k8sClient;

    public K8sDaemonSetCrawler(ILogger<K8sDaemonSetCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        var config = KubernetesClientConfiguration.BuildDefaultConfig();
        _k8sClient = new Kubernetes(config);
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var clusterNode = (AksNode)node;
        _logger.LogDebug($"Crawling Kubernetes DaemonSets in cluster: {clusterNode.ResourceId}");

        var daemonSets = await _k8sClient.AppsV1.ListDaemonSetForAllNamespacesAsync(
            allowWatchBookmarks: false,
            continueParameter: null,
            fieldSelector: null,
            labelSelector: null,
            limit: null,
            pretty: null,
            resourceVersion: null,
            resourceVersionMatch: null,
            timeoutSeconds: null,
            watch: false
        );

        foreach (var ds in daemonSets.Items)
        {
            var dsId = $"{clusterNode.ResourceId}/daemonsets/{ds.Metadata.NamespaceProperty}{ds.Metadata.Name}";
            var dsNode = new ArmResourceNode(
                resourceType: "Microsoft.ContainerService/DaemonSet",
                resourceId: dsId,
                subscriptionId: clusterNode.SubscriptionId,
                resourceGroupName: ds.Metadata.NamespaceProperty,
                resourceName: ds.Metadata.Name);

            await _graphDbClient.AddOrUpdateNodeAsync(dsNode);

            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), dsNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            if (ds.Spec?.Template?.Spec?.Containers != null)
            {
                foreach (var container in ds.Spec.Template.Spec.Containers)
                {
                    if (container.Env != null)
                    {
                        foreach (var env in container.Env)
                        {
                            if (!string.IsNullOrEmpty(env.Value) &&
                                env.Value.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
                            {
                                var sqlNode = await TryLinkSqlResource(dsNode, env.Value);
                                if (sqlNode != null)
                                {
                                    yield return sqlNode;
                                }
                            }
                        }
                    }
                }
            }

            yield return dsNode;
        }
    }

    private async Task<ArmResourceNode> TryLinkSqlResource(ArmResourceNode workloadNode, string possibleSqlResource)
    {
        try
        {
            var sqlId = new ResourceIdentifier(possibleSqlResource).ToString();
            var sqlNode = new ArmResourceNode(
                resourceType: "Microsoft.Sql/servers",
                resourceId: sqlId,
                subscriptionId: workloadNode.SubscriptionId,
                resourceGroupName: workloadNode.ResourceGroupName,
                resourceName: workloadNode.ResourceName);

            await _graphDbClient.AddOrUpdateNodeAsync(sqlNode);

            var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked workload {workloadNode.ResourceId} with SQL resource {sqlId}");
            return sqlNode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error linking SQL resource from value: {possibleSqlResource}. Exception: {ex.Message}");
            return null;
        }
    }
}

