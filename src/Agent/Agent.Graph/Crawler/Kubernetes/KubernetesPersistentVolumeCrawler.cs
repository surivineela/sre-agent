// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Azure.Core;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesPersistentVolumeCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesPersistentVolumeCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;
    private readonly AzureResourceGraphClient _graphClient;

    public KubernetesPersistentVolumeCrawler(
        ILogger<KubernetesPersistentVolumeCrawler> logger,
        IGraphDatabaseClient graphDbClient,
        IKubernetesService k8sService,
        AzureResourceGraphClient graphClient)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _k8sService = k8sService;
        _graphClient = graphClient;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var pvNode = (KubernetesResourceNode)node;
        _logger.LogDebug($"Crawling Kubernetes persistent volume: {pvNode.GetNodeId()}");

        var pv = (V1PersistentVolume?)pvNode.ResourceObject;
        if (pv == null)
        {
            pv = await _k8sService.GetPersistentVolumeAsync(pvNode.ClusterResourceId, pvNode.ResourceName);
        }

        if (pv == null || pv.Spec == null)
        {
            yield break;
        }

        await pvNode.SaveKubernetesResourceNode(_graphDbClient);

        if (pv.Spec.Csi != null)
        {
            switch (pv.Spec.Csi.Driver)
            {
                case "disk.csi.azure.com": // https://learn.microsoft.com/en-us/azure/aks/azure-csi-disk-storage-provision#mount-disk-as-a-volume
                    await foreach (var n in HandleAzureDiskVolume(pv, pvNode))
                    {
                        yield return n;
                    }
                    break;
                case "file.csi.azure.com": // https://learn.microsoft.com/en-us/azure/aks/azure-csi-files-storage-provision#mount-file-share-as-a-persistent-volume
                    await foreach (var n in HandleAzureFileOrBlobVolume(pv, pvNode))
                    {
                        yield return n;
                    }
                    break;
                case "blob.csi.azure.com": // https://learn.microsoft.com/en-us/azure/aks/azure-csi-blob-storage-provision?tabs=mount-nfs%2Csecret#statically-provision-a-volume
                    await foreach (var n in HandleAzureFileOrBlobVolume(pv, pvNode))
                    {
                        yield return n;
                    }
                    break;
                default:
                    _logger.LogDebug($"Unsupported CSI driver: {pv.Spec.Csi.Driver}");
                    break;
            }
        }
    }

    private async IAsyncEnumerable<GraphNode> HandleAzureDiskVolume(V1PersistentVolume pv, KubernetesResourceNode pvNode)
    {
        var diskId = ResourceIdentifier.Parse(pv.Spec.Csi.VolumeHandle);
        if (diskId is null)
        {
            _logger.LogDebug($"Unrecognized volume handle format for Azure disk: {pv.Spec.Csi.VolumeHandle}. Possibly this is not auto provisioned by AKS");
            yield break;
        }
        var diskNode = new ArmResourceNode(
            resourceId: diskId!,
            resourceType: diskId.ResourceType,
            subscriptionId: diskId.SubscriptionId!,
            resourceGroupName: diskId.ResourceGroupName!,
            resourceName: diskId.Name);

        await _graphDbClient.AddOrUpdateNodeAsync(diskNode);
        var edge = new ArmResourceEdge(pvNode.GetNodeId(), diskNode.GetNodeId(), Constants.Relationships.BackedBy);
        await _graphDbClient.AddOrUpdateEdgeAsync(edge);

        yield return diskNode;
    }

    private async IAsyncEnumerable<GraphNode> HandleAzureFileOrBlobVolume(V1PersistentVolume pv, KubernetesResourceNode pvNode)
    {
        var storageAccountName = string.Empty;
        var resourceGroupName = string.Empty;

        // manual provisioned
        if (pv.Spec.Csi.VolumeAttributes.ContainsKey("storageAccount"))
        {
            storageAccountName = pv.Spec.Csi.VolumeAttributes["storageAccount"];
            if (pv.Spec.Csi.VolumeAttributes.ContainsKey("resourceGroup"))
            {
                resourceGroupName = pv.Spec.Csi.VolumeAttributes["resourceGroup"];
            }
            else
            {
                // TODO: If empty, driver uses the same resource group name as current cluster.
            }
        }
        else
        {
            var parts = pv.Spec.Csi.VolumeHandle.Split('#');
            if (parts.Length < 2)
            {
                _logger.LogDebug($"Unrecoginized volume handle format for Azure file share: {pv.Spec.Csi.VolumeHandle}. Possibly this is not auto provisioned by AKS");
                yield break;
            }

            resourceGroupName = parts[0];
            storageAccountName = parts[1];
        }

        var queryResults = await _graphClient.Query([],
            $"resources | where type =~ '{Constants.StorageType}' and name =~ '{storageAccountName}' and resourceGroup =~ '{resourceGroupName}' | project id, subscriptionId, resourceGroup, name, location");
        if (queryResults == null || queryResults.Count == 0)
        {
            _logger.LogDebug($"Unable to find storage account {storageAccountName} in resource group {resourceGroupName}");
            yield break;
        }

        var json = JsonSerializer.Deserialize<JsonElement>(queryResults.Data);
        var element = json.EnumerateArray().First();

        var storageNode = new ArmResourceNode(
            resourceId: element.GetProperty("id").GetString()!,
            resourceType: Constants.StorageType,
            subscriptionId: element.GetProperty("subscriptionId").GetString()!,
            resourceGroupName: element.GetProperty("resourceGroup").GetString()!,
            resourceName: element.GetProperty("name").GetString()!,
            location: element.GetProperty("location").GetString()!);

        await _graphDbClient.AddOrUpdateNodeAsync(storageNode);
        var edge = new ArmResourceEdge(pvNode.GetNodeId(), storageNode.GetNodeId(), Constants.Relationships.BackedBy);
        await _graphDbClient.AddOrUpdateEdgeAsync(edge);

        yield return storageNode;
    }
}
