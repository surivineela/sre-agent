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
    private readonly IKubernetesClientFactory _k8sClientFactory;

    public K8sClusterCrawler(ILogger<K8sClusterCrawler> logger, IGraphDatabaseClient graphDbClient, ILoggerFactory loggerFactory, ArmClient armClient, IKubernetesClientFactory k8sClientFactory)
        : base(logger, graphDbClient, armClient, false)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _k8sClientFactory = k8sClientFactory;
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
        var client = await _k8sClientFactory.CreateKubernetesClientForCrawlerAsync(aksNode.SubscriptionId, aksNode.ResourceGroupName, aksNode.ResourceName);

        var namespaces = await client.CoreV1.ListNamespaceAsync();
        foreach (var ns in namespaces)
        {
            // TODO: GVK are nulls
            var nsNode = new KubernetesGlobalResourceNode(aksNode.ResourceId, ns.Name(), "core", "v1", "namespaces");
            await _graphDbClient.AddOrUpdateNodeAsync(nsNode.GetNodeLabel(), nsNode.GetNodeId(), nsNode.GetResourceType(), nsNode.GetNodeProperties());
            var edge = new ArmResourceEdge(clusterNode.GetNodeId(), nsNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

            yield return nsNode;
        }
    }
}
