// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class KubernetesDeploymentCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesDeploymentCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;
    private readonly ArmClient _armClient;
    private readonly SqlConnectionStringHelper _sqlHelper;

    public KubernetesDeploymentCrawler(ILogger<KubernetesDeploymentCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient, IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient);
        _k8sService = k8sService;
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var deploymentNode = (KubernetesNamespacedResourceNode)node;
        _logger.LogDebug($"Crawling deployment: {deploymentNode.GetNodeId()}");

        var deployment = (V1Deployment)deploymentNode.ResourceObject;
        if (deployment == null)
        {
            deployment = await _k8sService.GetDeploymentAsync(
                deploymentNode.ClusterResourceId,
                deploymentNode.Namespace,
                deploymentNode.Name);
        }

        if (deployment == null)
        {
            yield break;
        }

        if (deployment.Spec?.Template?.Spec?.Containers != null)
        {
            HashSet<string> knownVolumes = [];
            foreach (var container in deployment.Spec.Template.Spec.Containers)
            {
                if (container.Env != null)
                {
                    foreach (var env in container.Env)
                    {
                        ArmResourceNode sqlNode = null;
                        KubernetesNamespacedResourceNode serviceNode = null;
                        KubernetesNamespacedResourceNode secretNode = null;
                        KubernetesNamespacedResourceNode configMapNode = null;
                        try
                        {
                            if (!string.IsNullOrEmpty(env.Value))
                            {
                                if (IsSqlConnectionString(env.Value))
                                {
                                    sqlNode = await _sqlHelper.GetSqlResourceFromConnectionStringAsync(
                                        _graphDbClient,
                                        deploymentNode,
                                        env.Value);

                                    if (sqlNode != null)
                                    {
                                        var properties = sqlNode.GetNodeProperties();
                                        properties["authType"] = env.Value.Contains("Authentication=Active Directory Managed Identity",
                                            StringComparison.OrdinalIgnoreCase)
                                                ? "managedIdentity"
                                                : "connectionString";
                                        properties["source"] = $"k8s:deployment:env:{env.Name}";

                                        await _graphDbClient.AddOrUpdateNodeAsync(sqlNode);

                                        var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                                        await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                    }
                                }
                                else if (env.Value.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
                                {
                                    sqlNode = await TryLinkSqlResourceById(deploymentNode, env.Value, env.Name);
                                }
                                else if (KubernetesHelper.TryMatchServiceUrl(env.Value, out string serviceName, out string serviceNamespace))
                                {
                                    if (string.IsNullOrEmpty(serviceNamespace))
                                    {
                                        serviceNamespace = deploymentNode.Namespace;
                                    }
                                    var service = await _k8sService.GetServiceAsync(
                                        deploymentNode.ClusterResourceId,
                                        serviceNamespace,
                                        serviceName);

                                    if (service != null)
                                    {
                                        _logger.LogDebug($"Deployment {deploymentNode.GetNodeId()} has potential service call to {serviceNamespace}/{serviceName}(Inferred from env var {env.Name}).");
                                        serviceNode = new KubernetesNamespacedResourceNode(
                                            service,
                                            deploymentNode.ClusterResourceId,
                                            serviceNamespace,
                                            serviceName,
                                            "core",
                                            "v1",
                                            "services");
                                        await _graphDbClient.AddOrUpdateNodeAsync(serviceNode);
                                        var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), serviceNode.GetNodeId(), Constants.Relationships.Connected);
                                        edge.AddNetworkEgressEdgeProperties();
                                        await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                    }
                                }
                            }
                            else if (env.ValueFrom != null)
                            {
                                if (env.ValueFrom.SecretKeyRef != null)
                                {
                                    _logger.LogDebug($"Env from secret {env.Name}. Source: {env.ValueFrom.SecretKeyRef.Name}");
                                    secretNode = new KubernetesNamespacedResourceNode(
                                        null,
                                        deploymentNode.ClusterResourceId,
                                        deploymentNode.Namespace,
                                        env.ValueFrom.SecretKeyRef.Name,
                                        "core",
                                        "v1",
                                        "secrets");
                                    await _graphDbClient.AddOrUpdateNodeAsync(secretNode);
                                    var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), secretNode.GetNodeId(), Constants.Relationships.References);
                                    edge.AddReferenceEnvProperties();
                                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                }
                                else if (env.ValueFrom.ConfigMapKeyRef != null)
                                {
                                    _logger.LogDebug($"Env from config map {env.Name}. Source: {env.ValueFrom.ConfigMapKeyRef.Name}");
                                    configMapNode = new KubernetesNamespacedResourceNode(
                                        null,
                                        deploymentNode.ClusterResourceId,
                                        deploymentNode.Namespace,
                                        env.ValueFrom.ConfigMapKeyRef.Name,
                                        "core",
                                        "v1",
                                        "configmaps");
                                    await _graphDbClient.AddOrUpdateNodeAsync(configMapNode);
                                    var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), configMapNode.GetNodeId(), Constants.Relationships.References);
                                    edge.AddReferenceEnvProperties();
                                    await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error processing environment variable {env.Name} in container for deployment {deployment.Metadata?.Name}: {ex.Message}");
                            continue;
                        }

                        if (sqlNode != null)
                        {
                            yield return sqlNode;
                        }

                        if (serviceNode != null)
                        {
                            yield return serviceNode;
                        }

                        if (secretNode != null)
                        {
                            yield return secretNode;
                        }

                        if (configMapNode != null)
                        {
                            yield return configMapNode;
                        }
                    }
                }

                if (container.VolumeMounts != null)
                {
                    foreach (var volumeMount in container.VolumeMounts)
                    {
                        var volume = deployment.Spec.Template.Spec.Volumes?.FirstOrDefault(v => v.Name == volumeMount.Name);
                        if (!knownVolumes.Contains(volume.Name))
                        {
                            knownVolumes.Add(volume.Name);
                            if (volume.Secret != null)
                            {
                                _logger.LogDebug($"Secret volume {volume.Name}. Source: {volume.Secret.SecretName}");
                                var secretNode = new KubernetesNamespacedResourceNode(
                                    null,
                                    deploymentNode.ClusterResourceId,
                                    deploymentNode.Namespace,
                                    volume.Secret.SecretName,
                                    "core",
                                    "v1",
                                    "secrets");
                                await _graphDbClient.AddOrUpdateNodeAsync(secretNode);
                                var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), secretNode.GetNodeId(), Constants.Relationships.References);
                                edge.AddReferenceVolumeMountProperties();
                                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                yield return secretNode;
                            }
                            else if (volume.ConfigMap != null)
                            {
                                _logger.LogDebug($"ConfigMap volume {volume.Name}. Source: {volume.ConfigMap.Name}");
                                var configMapNode = new KubernetesNamespacedResourceNode(
                                    null,
                                    deploymentNode.ClusterResourceId,
                                    deploymentNode.Namespace,
                                    volume.ConfigMap.Name,
                                    "core",
                                    "v1",
                                    "configmaps");
                                await _graphDbClient.AddOrUpdateNodeAsync(configMapNode);
                                var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), configMapNode.GetNodeId(), Constants.Relationships.References);
                                edge.AddReferenceVolumeMountProperties();
                                await _graphDbClient.AddOrUpdateEdgeAsync(edge);
                                yield return configMapNode;
                            }
                            // TODO: pvc
                        }
                    }
                }
            }
        }

        // connect pods
        var selector = KubernetesHelper.ConstructLabelSelector(deployment.Spec.Selector);
        var podList = new V1PodList();
        if (!string.IsNullOrEmpty(selector))
        {
            podList = await _k8sService.GetPodsAsync(deploymentNode.ClusterResourceId, deploymentNode.Namespace, selector);
        }
        foreach (var pod in podList.Items ?? new List<V1Pod>())
        {
            var podNode = new KubernetesNamespacedResourceNode(
                pod,
                deploymentNode.ClusterResourceId,
                deploymentNode.Namespace,
                pod.Name(),
                "core",
                "v1",
                "pods");
            await _graphDbClient.AddOrUpdateNodeAsync(podNode);
            var edge = new ArmResourceEdge(deploymentNode.GetNodeId(), podNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            yield return podNode;
        }
    }

    private bool IsSqlConnectionString(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        // Common SQL connection string indicators
        return value.Contains("Server=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) ||
               value.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ArmResourceNode> TryLinkSqlResourceById(GraphNode workloadNode, string possibleSqlResource, string envName)
    {
        try
        {
            var sqlId = new ResourceIdentifier(possibleSqlResource);
            var sqlNode = new ArmResourceNode(
                resourceType: "Microsoft.Sql/servers",
                resourceId: sqlId,
                subscriptionId: sqlId.SubscriptionId,
                resourceGroupName: sqlId.ResourceGroupName,
                resourceName: sqlId.Name);

            var properties = sqlNode.GetNodeProperties();
            properties["source"] = $"k8s:deployment:env:{envName}";
            properties["authType"] = "resourceId";

            await _graphDbClient.AddOrUpdateNodeAsync(sqlNode);

            var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _logger.LogDebug($"Linked workload {workloadNode.GetNodeId()} with SQL resource {sqlId}");
            return sqlNode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error linking SQL resource from value: {possibleSqlResource}. Exception: {ex.Message}");
            return null;
        }
    }
}

