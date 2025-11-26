// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesPersistentVolumeClaimCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesPersistentVolumeClaimCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;

    public KubernetesPersistentVolumeClaimCrawler(
        ILogger<KubernetesPersistentVolumeClaimCrawler> logger,
        IGraphDatabaseClient graphDbClient,
        IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _k8sService = k8sService;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var pvcNode = (KubernetesNamespacedResourceNode)node;
        _logger.LogDebug($"Crawling Kubernetes persistent volume claim: {pvcNode.GetNodeId()}");

        var pvc = (V1PersistentVolumeClaim?)pvcNode.ResourceObject;
        if (pvc == null)
        {
            pvc = await _k8sService.GetPersistentVolumeClaimAsync(pvcNode.ClusterResourceId, pvcNode.Namespace, pvcNode.ResourceName);
        }

        if (pvc == null || pvc.Spec == null)
        {
            yield break;
        }

        await pvcNode.SaveKubernetesResourceNode(_graphDbClient);

        if (pvc.Spec.VolumeName != null)
        {
            var pvNode = new KubernetesResourceNode(
                null,
                pvcNode.ClusterResourceId,
                pvcNode.SubscriptionId,
                pvcNode.ResourceGroupName,
                pvcNode.Location,
                pvc.Spec.VolumeName,
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesPersistentVolumeType,
                pvc.Annotations(),
                pvc.Labels());

            await _graphDbClient.AddOrUpdateNodeAsync(pvNode);
            var edge = new ArmResourceEdge(pvcNode.GetNodeId(), pvNode.GetNodeId(), Constants.Relationships.References);
            edge.AddReferencePersistentVolumeClaimProperties();
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return pvNode;
        }
    }
}
