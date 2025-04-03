// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;
public class KubernetesPodCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesPodCrawler> _logger;
    private readonly IKubernetesService _k8sService;
    private readonly IGraphDatabaseClient _graphDbClient;
    public KubernetesPodCrawler(ILogger<KubernetesPodCrawler> logger, IKubernetesService k8sService, IGraphDatabaseClient graphDbClient)
    {
        _logger = logger;
        _k8sService = k8sService;
        _graphDbClient = graphDbClient;
    }
    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var podNode = (KubernetesNamespacedResourceNode)node;
        _logger.LogDebug($"Crawling Kubernetes pod: {podNode.GetNodeId()}");

        var pod = (V1Pod)podNode.ResourceObject;
        if (pod == null)
        {
            pod = await _k8sService.GetPodAsync(podNode.ClusterResourceId, podNode.Namespace, podNode.Name);
        }

        // TODO: handle pods not managed by deploymemt / daemonset / statefulset

        yield break;
    }
}

