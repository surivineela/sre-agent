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
        var pod = podList.Items?.FirstOrDefault();
        if (pod == null)
        {
            _logger.LogDebug($"No pods found for service: {serviceNode.GetNodeId()}");
            yield break;
        }
        switch (pod.OwnerReferences().FirstOrDefault()?.Kind.ToLowerInvariant())
        {
            case "replicaset":
                var replicaSet = await _k8sService.GetReplicaSetAsync(serviceNode.ClusterResourceId, serviceNode.Namespace, pod.OwnerReferences().FirstOrDefault()?.Name);
                if (replicaSet == null)
                {
                    _logger.LogDebug($"No replicaset found for service: {serviceNode.GetNodeId()}");
                    yield break;
                }

                var deploymentNode = new KubernetesNamespacedResourceNode(pod, serviceNode.ClusterResourceId, serviceNode.Namespace, serviceNode.SubscriptionId, serviceNode.ResourceGroupName, replicaSet.OwnerReferences().FirstOrDefault()?.Name, Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesDeploymentType);
                await _graphDbClient.AddOrUpdateNodeAsync(deploymentNode);
                var edge = new ArmResourceEdge(serviceNode.GetNodeId(), deploymentNode.GetNodeId(), Constants.Relationships.Connected);
                edge.AddNetworkIngressEdgeProperties();
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);

                var edge1 = new ArmResourceEdge(deploymentNode.GetNodeId(), serviceNode.GetNodeId(), Constants.Relationships.Linked);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge1);
                _logger.LogDebug($"Found deployment {replicaSet.OwnerReferences().FirstOrDefault()?.Name} for service: {serviceNode.GetNodeId()}");
                break;
            case "statefulset":
                var statefulSetNode = new KubernetesNamespacedResourceNode(pod, serviceNode.ClusterResourceId, serviceNode.Namespace, serviceNode.SubscriptionId, serviceNode.ResourceGroupName, pod.OwnerReferences().FirstOrDefault()?.Name, Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesStatefulSetType);
                await _graphDbClient.AddOrUpdateNodeAsync(statefulSetNode);
                var edge2 = new ArmResourceEdge(serviceNode.GetNodeId(), statefulSetNode.GetNodeId(), Constants.Relationships.Connected);
                edge2.AddNetworkIngressEdgeProperties();
                await _graphDbClient.AddOrUpdateEdgeAsync(edge2);
                var edge3 = new ArmResourceEdge(statefulSetNode.GetNodeId(), serviceNode.GetNodeId(), Constants.Relationships.Linked);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge3);
                _logger.LogDebug($"Found statefulset {pod.OwnerReferences().FirstOrDefault()?.Name} for service: {serviceNode.GetNodeId()}");
                break;
        }

        yield break;
    }
}

