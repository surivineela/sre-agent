using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class K8sClusterCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<K8sClusterCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly IKubernetesService _k8sService;

    public K8sClusterCrawler(ILogger<K8sClusterCrawler> logger, IGraphDatabaseClient graphDbClient, ILoggerFactory loggerFactory, ArmClient armClient, IKubernetesService k8sService)
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
        await _graphDbClient.AddOrUpdateNodeAsync(clusterNode.GetNodeLabel(), clusterNode.GetNodeId(), clusterNode.GetResourceType(), clusterNode.GetNodeProperties());

        var aksNode = (AksNode)clusterNode;
        _logger.LogDebug($"Crawling Kubernetes cluster: {aksNode.GetNodeId()}");

        var namespaces = await _k8sService.GetNamespacesAsync(aksNode.ResourceId);
        _logger.LogDebug($"Found {namespaces.Items?.Count} namespaces in cluster: {aksNode.GetNodeId()}");
        foreach (var ns in namespaces)
        {
            _logger.LogDebug($"Namespace: {ns.Name()} in cluster: {aksNode.GetNodeId()}");
            // TODO: GVK are nulls
            var nsNode = new KubernetesGlobalResourceNode(ns, aksNode.ResourceId, ns.Name(), "core", "v1", "namespaces");
            await _graphDbClient.AddOrUpdateNodeAsync(nsNode.GetNodeLabel(), nsNode.GetNodeId(), nsNode.GetResourceType(), nsNode.GetNodeProperties());
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), nsNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

            yield return nsNode;
        }
    }
}
