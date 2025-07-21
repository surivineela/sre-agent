// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Interfaces;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesNodeCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesNodeCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;

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
