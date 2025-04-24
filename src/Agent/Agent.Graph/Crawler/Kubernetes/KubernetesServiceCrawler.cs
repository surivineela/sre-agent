// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Gremlin.Net.Process.Traversal;

namespace Agent.Graph.Crawler.Kubernetes;
public class KubernetesServiceCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesServiceCrawler> _logger;
    private readonly IKubernetesService _k8sService;
    private readonly IGraphDatabaseClient _graphDbClient;
    public KubernetesServiceCrawler(ILogger<KubernetesServiceCrawler> logger, IKubernetesService k8sService, IGraphDatabaseClient graphDbClient)
    {
        _logger = logger;
        _k8sService = k8sService;
        _graphDbClient = graphDbClient;
    }
    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var serviceNode = (KubernetesNamespacedResourceNode)node;
        _logger.LogDebug($"Crawling Kubernetes service: {serviceNode.GetNodeId()}");

        var service = (V1Service)serviceNode.ResourceObject;
        if (service == null)
        {
            service = await _k8sService.GetServiceAsync(serviceNode.ClusterResourceId, serviceNode.Namespace, serviceNode.ResourceName);
        }

        if (service == null)
        {
            yield break;
        }

        // Connects pods
        var selector = service.Spec.Selector.ToSelectorString();
        var podList = new V1PodList();
        if (!string.IsNullOrEmpty(selector))
        {
            podList = await _k8sService.GetPodsAsync(serviceNode.ClusterResourceId, serviceNode.Namespace, selector);
        }

        _logger.LogDebug($"Found {podList.Items?.Count} backend pods for service: {serviceNode.GetNodeId()}");
        foreach (var pod in podList.Items ?? new List<V1Pod>())
        {
            _logger.LogDebug($"Pod: {pod.Name()} for service: {serviceNode.GetNodeId()}");
            var podNode = new KubernetesNamespacedResourceNode(pod, serviceNode.ClusterResourceId, serviceNode.Namespace, pod.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesPodType);
            await _graphDbClient.AddOrUpdateNodeAsync(podNode);
            var edge = new ArmResourceEdge(serviceNode.GetNodeId(), podNode.GetNodeId(), Constants.Relationships.BackedBy);
            edge.AddNetworkIngressEdgeProperties();
            bool ready = false;
            if (pod.Status?.Conditions != null)
            {

                foreach (var condition in pod.Status.Conditions)
                {
                    if (condition.Type == "Ready" && condition.Status == "True")
                    {
                        ready = true;
                        break;
                    }
                }
            }

            if (ready)
            {
                edge.AddBackendStatusReadyProperties();
            }
            else
            {
                edge.AddBackendStatusNotReadyProperties();
            }

            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return podNode;
        }
        yield break;
    }
}

