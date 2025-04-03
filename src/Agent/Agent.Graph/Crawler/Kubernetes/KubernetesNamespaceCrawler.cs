// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
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

            // list all pods
            var pods = await _k8sService.GetPodsAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {pods.Items?.Count} pods in namespace: {nsNode.GetNodeId()}");
            foreach (var pod in pods.Items)
            {
                _logger.LogDebug($"Pod: {pod.Name()} in namespace: {nsNode.GetNodeId()}");
                var podNode = new KubernetesNamespacedResourceNode(pod, nsNode.ClusterResourceId, nsNode.Name, pod.Name(), "core", "v1", "pods");
                await _graphDbClient.AddOrUpdateNodeAsync(podNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), podNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return podNode;
            }

            // list all deployments
            var deployments = await _k8sService.GetDeploymentsAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {deployments.Items?.Count} deployments in namespace: {nsNode.GetNodeId()}");
            foreach (var deployment in deployments.Items)
            {
                _logger.LogDebug($"Deployment: {deployment.Name()} in namespace: {nsNode.GetNodeId()}");
                var deploymentNode = new KubernetesNamespacedResourceNode(deployment, nsNode.ClusterResourceId, nsNode.Name, deployment.Name(), "apps", "v1", "deployments");
                await _graphDbClient.AddOrUpdateNodeAsync(deploymentNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), deploymentNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return deploymentNode;
            }

            // list all services
            var services = await _k8sService.GetServicesAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {services.Items?.Count} services in namespace: {nsNode.GetNodeId()}");
            foreach (var service in services.Items)
            {
                _logger.LogDebug($"Service: {service.Name()} in namespace: {nsNode.GetNodeId()}");
                var serviceNode = new KubernetesNamespacedResourceNode(service, nsNode.ClusterResourceId, nsNode.Name, service.Name(), "core", "v1", "services");
                await _graphDbClient.AddOrUpdateNodeAsync(serviceNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), serviceNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return serviceNode;
            }

            // list all config maps
            var configMaps = await _k8sService.GetConfigMapsAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {configMaps.Items?.Count} config maps in namespace: {nsNode.GetNodeId()}");
            foreach (var configMap in configMaps.Items)
            {
                _logger.LogDebug($"ConfigMap: {configMap.Name()} in namespace: {nsNode.GetNodeId()}");
                var configMapNode = new KubernetesNamespacedResourceNode(configMap, nsNode.ClusterResourceId, nsNode.Name, configMap.Name(), "core", "v1", "configmaps");
                await _graphDbClient.AddOrUpdateNodeAsync(configMapNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), configMapNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return configMapNode;
            }

            // list all secrets
            var secrets = await _k8sService.GetSecretsAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {secrets.Items?.Count} secrets in namespace: {nsNode.GetNodeId()}");
            foreach (var secret in secrets.Items)
            {
                _logger.LogDebug($"Secret: {secret.Name()} in namespace: {nsNode.GetNodeId()}");
                var secretNode = new KubernetesNamespacedResourceNode(secret, nsNode.ClusterResourceId, nsNode.Name, secret.Name(), "core", "v1", "secrets");
                await _graphDbClient.AddOrUpdateNodeAsync(secretNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), secretNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return secretNode;
            }

            // list all pvs
            var persistentVolumes = await _k8sService.GetPersistentVolumesAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {persistentVolumes.Items?.Count} persistent volumes in namespace: {nsNode.GetNodeId()}");
            foreach (var pv in persistentVolumes.Items)
            {
                _logger.LogDebug($"PersistentVolume: {pv.Name()} in namespace: {nsNode.GetNodeId()}");
                var pvNode = new KubernetesNamespacedResourceNode(pv, nsNode.ClusterResourceId, nsNode.Name, pv.Name(), "core", "v1", "persistentvolumes");
                await _graphDbClient.AddOrUpdateNodeAsync(pvNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), pvNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return pvNode;
            }

            // list all pvcs
            var persistentVolumeClaims = await _k8sService.GetPersistentVolumeClaimsAsync(nsNode.ClusterResourceId, nsNode.Name);
            _logger.LogDebug($"Found {persistentVolumeClaims.Items?.Count} persistent volume claims in namespace: {nsNode.GetNodeId()}");
            foreach (var pvc in persistentVolumeClaims.Items)
            {
                _logger.LogDebug($"PersistentVolumeClaim: {pvc.Name()} in namespace: {nsNode.GetNodeId()}");
                var pvcNode = new KubernetesNamespacedResourceNode(pvc, nsNode.ClusterResourceId, nsNode.Name, pvc.Name(), "core", "v1", "persistentvolumeclaims");
                await _graphDbClient.AddOrUpdateNodeAsync(pvcNode);
                var edge = new ArmResourceEdge(nsNode.GetNodeId(), pvcNode.GetNodeId(), Constants.Relationships.Contains);
                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                yield return pvcNode;
            }

            // TODO: cleanup stale nodes
        }
    }
}

