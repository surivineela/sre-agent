// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes;
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
            pod = await _k8sService.GetPodAsync(podNode.ClusterResourceId, podNode.Namespace, podNode.ResourceName);
        }

        if (pod == null)
        {
            yield break;
        }

        // Connects pod to nodes
        var nodeName = pod.Spec.NodeName;
        if (!string.IsNullOrEmpty(nodeName))
        {
            _logger.LogDebug($"Connect pod {pod.Name()} to node {nodeName}");
            var nodeNode = new KubernetesResourceNode(null, podNode.ClusterResourceId, nodeName, Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesNodeType);
            var edge = new ArmResourceEdge(podNode.GetNodeId(), nodeNode.GetNodeId(), Constants.Relationships.HostedOn);
            var edge2 = new ArmResourceEdge(nodeNode.GetNodeId(), podNode.GetNodeId(), Constants.Relationships.Hosts);

            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2);

            yield return nodeNode;
        }

        // TODO: handle pods not managed by deploymemt / daemonset / statefulset
    }
}

