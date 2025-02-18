using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class K8sClusterCrawler : IArmResourceCrawler
    {
        private readonly ILogger<K8sClusterCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly K8sDeploymentCrawler _deploymentCrawler;
        private readonly K8sDaemonSetCrawler _daemonSetCrawler;

        public K8sClusterCrawler(ILogger<K8sClusterCrawler> logger, IGraphDatabaseManager dbManager, ILoggerFactory loggerFactory)
        {
            _logger = logger;
            _dbManager = dbManager;
            _deploymentCrawler = new K8sDeploymentCrawler(loggerFactory.CreateLogger<K8sDeploymentCrawler>(), dbManager);
            _daemonSetCrawler = new K8sDaemonSetCrawler(loggerFactory.CreateLogger<K8sDaemonSetCrawler>(), dbManager);
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode clusterNode)
        {
            // Add the cluster node to the graph.
            await _dbManager.AddOrUpdateNodeAsync(clusterNode.GetNodeLabel(), clusterNode.GetNodeId(), clusterNode.GetResourceType(), clusterNode.GetNodeProperties());

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
}
