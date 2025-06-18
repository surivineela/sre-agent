using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Azure.ResourceManager;
using Gremlin.Net.Process.Traversal;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesNodeCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesNodeCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;
    private readonly ArmClient _armClient;

    public KubernetesNodeCrawler(
        ILogger<KubernetesNodeCrawler> logger,
        IGraphDatabaseClient graphDbClient,
        IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _k8sService = k8sService;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var nodeNode = (KubernetesResourceNode)node;
        _logger.LogDebug($"Crawling Kubernetes node: {nodeNode.GetNodeId()}");

        var aksNode = (V1Node)nodeNode.ResourceObject;
        if (aksNode == null)
        {
            aksNode = await _k8sService.GetNodeAsync(nodeNode.ClusterResourceId, nodeNode.ResourceName);
        }

        if (aksNode == null)
        {
            yield break;
        }

        await nodeNode.SaveKubernetesResourceNode(_graphDbClient);

        // TODO: add node properties
        yield break;
    }
}
