using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class K8sClusterCrawler : GenericArmResourceCrawler
{
    private readonly ILogger<K8sClusterCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly K8sDeploymentCrawler _deploymentCrawler;
    private readonly K8sDaemonSetCrawler _daemonSetCrawler;

    public K8sClusterCrawler(ILogger<K8sClusterCrawler> logger, IGraphDatabaseClient graphDbClient, ILoggerFactory loggerFactory, ArmClient armClient)
        : base(logger, graphDbClient, armClient, false)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _deploymentCrawler = new K8sDeploymentCrawler(loggerFactory.CreateLogger<K8sDeploymentCrawler>(), graphDbClient, armClient);
        _daemonSetCrawler = new K8sDaemonSetCrawler(loggerFactory.CreateLogger<K8sDaemonSetCrawler>(), graphDbClient, armClient);
    }

    public override async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode clusterNode)
    {
        await foreach (var n in base.Crawl(clusterNode))
        {
            yield return n;
        }

        // Add the cluster node to the graph.
        await _graphDbClient.AddOrUpdateNodeAsync(clusterNode.GetNodeLabel(), clusterNode.GetNodeId(), clusterNode.GetResourceType(), clusterNode.GetNodeProperties());

        // Crawl Deployments.
        await foreach (var dep in _deploymentCrawler.Crawl(clusterNode))
        {
            yield return dep;
        }

        // Crawl DaemonSets.
        await foreach (var ds in _daemonSetCrawler.Crawl(clusterNode))
        {
            yield return ds;
        }
    }
}
