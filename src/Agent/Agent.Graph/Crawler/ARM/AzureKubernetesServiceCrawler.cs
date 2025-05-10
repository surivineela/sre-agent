// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class AzureKubernetesServiceCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<AzureKubernetesServiceCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly IKubernetesService _k8sService;

    public AzureKubernetesServiceCrawler(ILogger<AzureKubernetesServiceCrawler> logger, IGraphDatabaseClient graphDbClient, ILoggerFactory loggerFactory, ArmClient armClient, IKubernetesService k8sService)
        : base(logger, graphDbClient, armClient, false)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _k8sService = k8sService;
    }

    public override async IAsyncEnumerable<GraphNode> Crawl(GraphNode clusterNode)
    {
        await foreach (var n in base.Crawl(clusterNode))
        {
            yield return n;
        }

        // Add the cluster node to the graph.
        await _graphDbClient.AddOrUpdateNodeAsync(clusterNode);

        var aksNode = (AksNode)clusterNode;
        _logger.LogDebug($"Crawling Kubernetes cluster: {aksNode.GetNodeId()}");

        var namespaces = await _k8sService.GetNamespacesAsync(aksNode.ResourceId);
        _logger.LogDebug($"Found {namespaces.Items?.Count} namespaces in cluster: {aksNode.GetNodeId()}");
        foreach (var ns in namespaces)
        {
            _logger.LogDebug($"Namespace: {ns.Name()} in cluster: {aksNode.GetNodeId()}");
            // TODO: GVK are nulls
            var nsNode = new KubernetesResourceNode(ns, aksNode.ResourceId, aksNode.SubscriptionId, aksNode.ResourceGroupName, aksNode.Location, ns.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesNamespaceType, ns.Annotations(), ns.Labels());
            await _graphDbClient.AddOrUpdateNodeAsync(nsNode);
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), nsNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return nsNode;
        }

        // non-namespaced resources
        // nodes
        var nodes = await _k8sService.GetNodesAsync(aksNode.ResourceId);
        foreach (var node in nodes)
        {
            _logger.LogDebug($"Node: {node.Name()} in cluster: {aksNode.GetNodeId()}");
            var nodeNode = new KubernetesResourceNode(node, aksNode.ResourceId, aksNode.SubscriptionId, aksNode.ResourceGroupName, aksNode.Location, node.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesNodeType, node.Annotations(), node.Labels());
            await _graphDbClient.AddOrUpdateNodeAsync(nodeNode);
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), nodeNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return nodeNode;
        }
    }
}

