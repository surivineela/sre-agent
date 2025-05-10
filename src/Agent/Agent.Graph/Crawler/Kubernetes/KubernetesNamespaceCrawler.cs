// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Logging;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes
{
    public class KubernetesNamespaceCrawler : IResourceCrawler
    {
        private readonly ILogger<KubernetesNamespaceCrawler> _logger;
        private readonly IKubernetesService _k8sService;
        private readonly IGraphDatabaseClient _graphDbClient;

        public KubernetesNamespaceCrawler(ILogger<KubernetesNamespaceCrawler> logger, IKubernetesService k8sService, IGraphDatabaseClient graphDbClient)
        {
            _logger = logger;
            _k8sService = k8sService;
            _graphDbClient = graphDbClient;
        }

        public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
        {
            var nsNode = (KubernetesResourceNode)node;
            _logger.LogDebug($"Crawling Kubernetes namespace: {nsNode.GetNodeId()}");

            long startTs = DateTime.UtcNow.Ticks;

            // list all deployments
            var deployments = await _k8sService.GetDeploymentsAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {deployments.Items?.Count} deployments in namespace: {nsNode.GetNodeId()}");
            foreach (var deployment in deployments.Items)
            {
                _logger.LogDebug($"Deployment: {deployment.Name()} in namespace: {nsNode.GetNodeId()}");
                var deploymentNode = new KubernetesNamespacedResourceNode(deployment, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, deployment.Name(), "apps", Constants.KubernetesV1Version, Constants.KubernetesDeploymentType, deployment.Annotations(), deployment.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(deploymentNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), deploymentNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return deploymentNode;
            }

            // list all statefulsets
            var statefulSets = await _k8sService.GetStatefulSetsAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {statefulSets.Items?.Count} statefulsets in namespace: {nsNode.GetNodeId()}");
            foreach (var statefulSet in statefulSets.Items)
            {
                _logger.LogDebug($"StatefulSet: {statefulSet.Name()} in namespace: {nsNode.GetNodeId()}");
                var statefulSetNode = new KubernetesNamespacedResourceNode(statefulSet, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, statefulSet.Name(), "apps", Constants.KubernetesV1Version, Constants.KubernetesStatefulSetType, statefulSet.Annotations(), statefulSet.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(statefulSetNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), statefulSetNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return statefulSetNode;
            }

            // list all daemonsets
            var daemonSets = await _k8sService.GetDaemonSetsAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {daemonSets.Items?.Count} daemonsets in namespace: {nsNode.GetNodeId()}");
            foreach (var daemonSet in daemonSets.Items)
            {
                _logger.LogDebug($"DaemonSet: {daemonSet.Name()} in namespace: {nsNode.GetNodeId()}");
                var daemonSetNode = new KubernetesNamespacedResourceNode(daemonSet, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, daemonSet.Name(), "apps", Constants.KubernetesV1Version, Constants.KubernetesDaemonSetType, daemonSet.Annotations(), daemonSet.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(daemonSetNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), daemonSetNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return daemonSetNode;
            }

            // list all services
            var services = await _k8sService.GetServicesAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {services.Items?.Count} services in namespace: {nsNode.GetNodeId()}");
            foreach (var service in services.Items)
            {
                _logger.LogDebug($"Service: {service.Name()} in namespace: {nsNode.GetNodeId()}");
                var serviceNode = new KubernetesNamespacedResourceNode(service, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, service.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesServiceType, service.Annotations(), service.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(serviceNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), serviceNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return serviceNode;
            }

            // list all config maps
            var configMaps = await _k8sService.GetConfigMapsAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {configMaps.Items?.Count} config maps in namespace: {nsNode.GetNodeId()}");
            foreach (var configMap in configMaps.Items)
            {
                _logger.LogDebug($"ConfigMap: {configMap.Name()} in namespace: {nsNode.GetNodeId()}");
                var configMapNode = new KubernetesNamespacedResourceNode(configMap, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, configMap.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesConfigMapType, configMap.Annotations(), configMap.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(configMapNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), configMapNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return configMapNode;
            }

            // list all secrets
            var secrets = await _k8sService.GetSecretsAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {secrets.Items?.Count} secrets in namespace: {nsNode.GetNodeId()}");
            foreach (var secret in secrets.Items)
            {
                _logger.LogDebug($"Secret: {secret.Name()} in namespace: {nsNode.GetNodeId()}");
                var secretNode = new KubernetesNamespacedResourceNode(secret, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, secret.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesSecretType, secret.Annotations(), secret.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(secretNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), secretNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return secretNode;
            }

            // list all pvs
            var persistentVolumes = await _k8sService.GetPersistentVolumesAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {persistentVolumes.Items?.Count} persistent volumes in namespace: {nsNode.GetNodeId()}");
            foreach (var pv in persistentVolumes.Items)
            {
                _logger.LogDebug($"PersistentVolume: {pv.Name()} in namespace: {nsNode.GetNodeId()}");
                var pvNode = new KubernetesNamespacedResourceNode(pv, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, pv.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesPersistVolumeType, pv.Annotations(), pv.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(pvNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), pvNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return pvNode;
            }

            // list all pvcs
            var persistentVolumeClaims = await _k8sService.GetPersistentVolumeClaimsAsync(nsNode.ClusterResourceId, nsNode.ResourceName);
            _logger.LogDebug($"Found {persistentVolumeClaims.Items?.Count} persistent volume claims in namespace: {nsNode.GetNodeId()}");
            foreach (var pvc in persistentVolumeClaims.Items)
            {
                _logger.LogDebug($"PersistentVolumeClaim: {pvc.Name()} in namespace: {nsNode.GetNodeId()}");
                var pvcNode = new KubernetesNamespacedResourceNode(pvc, nsNode.ClusterResourceId, nsNode.ResourceName, nsNode.SubscriptionId, nsNode.ResourceGroupName, nsNode.Location, pvc.Name(), Constants.KubernetesCoreGroup, Constants.KubernetesV1Version, Constants.KubernetesPersistVolumeClaimType, pvc.Annotations(), pvc.Labels());
                await _graphDbClient.AddOrUpdateNodeAsync(pvcNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), pvcNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return pvcNode;
            }

            _logger.LogDebug($"Cleaning up stale nodes in namespace {nsNode.ResourceName} of {nsNode.ClusterResourceId} (older than {startTs})");
            var props = new Dictionary<string, string>
            {
                { "clusterResourceId", nsNode.ClusterResourceId },
                { "namespace", nsNode.ResourceName },
            };
            await CrawlerExtensions.RemoveStaleNodesWithFilter(_graphDbClient, props, startTs);
        }
    }
}

