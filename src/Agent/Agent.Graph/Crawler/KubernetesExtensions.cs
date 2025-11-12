// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Helpers;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler;

public static partial class KubernetesExtensions
{
    [GeneratedRegex("^(?:http:\\/\\/|https:\\/\\/)?(?<serviceName>[a-z](?:[a-z0-9-]*[a-z0-9])?)(?<serviceNamespace>\\.[a-z](?:[a-z0-9-]*[a-z0-9])?)?:\\d+$")]
    private static partial Regex ServiceUrlRegex();

    public static string ToSelectorString(this V1LabelSelector selectors)
    {
        if (selectors == null)
        {
            return string.Empty;
        }
        var labelSelector = new StringBuilder();
        if (selectors.MatchLabels != null && selectors.MatchLabels.Count > 0)
        {
            labelSelector.Append(string.Join(",", selectors.MatchLabels.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
        if (selectors.MatchExpressions != null && selectors.MatchExpressions.Count > 0)
        {
            foreach (var expression in selectors.MatchExpressions)
            {
                var operatorString = expression.OperatorProperty.ToString().ToLower();
                var values = string.Join(",", expression.Values);
                labelSelector.Append($"{expression.Key} {operatorString} {values}");
            }
        }

        return labelSelector.ToString();
    }

    public static string ToSelectorString(this IDictionary<string, string> selectors)
    {
        if (selectors == null)
        {
            return string.Empty;
        }
        var labelSelector = new StringBuilder();
        if (selectors.Count > 0)
        {
            labelSelector.Append(string.Join(",", selectors.Select(kvp => $"{kvp.Key}={kvp.Value}")));
        }
        return labelSelector.ToString();
    }

    public static async Task<ArmResourceNode?> TryMatchAndLinkSqlResourcesAsync(this V1EnvVar env, KubernetesNamespacedResourceNode node, SqlConnectionStringHelper sqlHelper, IGraphDatabaseClient graphDbClient, string sourceType, ILogger logger)
    {
        var val = string.Empty;
        if (env.Value != null)
        {
            val = env.Value;
        }
        // TODO: valueFrom

        ArmResourceNode? sqlNode = null;
        if (sqlHelper.IsSqlConnectionString(val))
        {
            sqlNode = await sqlHelper.GetSqlResourceFromConnectionStringAsync(
                node,
                val,
                $"k8s:{sourceType}:env",
                env.Name);
        }
        else if (val.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
        {
            sqlNode = await sqlHelper.TryLinkSqlResourceById(node, val, "k8s:deployment:env", env.Name);
        }

        return sqlNode;
    }

    public static async Task<ArmResourceNode?> TryMatchAndLinkPostgreSqlResourcesAsync(
        this V1EnvVar env,
        KubernetesNamespacedResourceNode node,
        PostgreSqlConnectionStringHelper postgresHelper,
        IGraphDatabaseClient graphDbClient,
        string sourceType,
        ILogger logger)
    {
        var val = string.Empty;
        if (env.Value != null)
        {
            val = env.Value;
        }

        ArmResourceNode? postgresNode = null;
        if (postgresHelper.IsPostgreSqlConnectionString(val))
        {
            postgresNode = await postgresHelper.GetPostgreSqlResourceFromConnectionStringAsync(
                node,
                val,
                $"k8s:{sourceType}:env",
                env.Name);
        }
        else if (val.Contains("/Microsoft.DBforPostgreSQL/", StringComparison.OrdinalIgnoreCase))
        {
            postgresNode = await postgresHelper.TryLinkPostgreSqlResourceById(node, val, $"k8s:{sourceType}:env", env.Name);
        }

        return postgresNode;
    }

    // try to extract service name and service namespace if the env value is a service url
    public async static Task<KubernetesNamespacedResourceNode?> TryMatchAndLinkServiceAsync(this V1EnvVar env, KubernetesNamespacedResourceNode node, IKubernetesService k8sService, IGraphDatabaseClient graphDbClient, ILogger logger)
    {
        var serviceName = string.Empty;
        var serviceNamespace = string.Empty;

        var val = string.Empty;
        if (env.Value != null)
        {
            val = env.Value;
        }
        // TODO: valueFrom

        var match = ServiceUrlRegex().Match(val);
        if (!match.Success)
        {
            return null;
        }

        serviceName = match.Groups["serviceName"].Value;
        serviceNamespace = match.Groups["serviceNamespace"].Value.TrimStart('.');
        if (string.IsNullOrEmpty(serviceNamespace))
        {
            serviceNamespace = node.Namespace;
        }

        var service = await k8sService.GetServiceAsync(node.ClusterResourceId, serviceNamespace, serviceName);
        if (service != null)
        {
            logger.LogDebug($"Deployment {node.GetNodeId()} has potential service call to {serviceNamespace}/{serviceName}(Inferred from env var {env.Name}).");

            var serviceNode = new KubernetesNamespacedResourceNode(
                service,
                node.ClusterResourceId,
                serviceNamespace,
                node.SubscriptionId,
                node.ResourceGroupName,
                 node.Location,
                serviceName,
                "core",
                Constants.KubernetesV1Version,
                "services");

            await graphDbClient.AddOrUpdateNodeAsync(serviceNode);
            var edge = new ArmResourceEdge(node.GetNodeId(), serviceNode.GetNodeId(), Constants.Relationships.Connected);
            edge.AddNetworkEgressEdgeProperties();
            await graphDbClient.AddOrUpdateEdgeAsync(edge);

            return serviceNode;
        }

        return null;
    }

    public async static Task<KubernetesNamespacedResourceNode?> TryLinkEnvReferenceAsync(this V1EnvVar env, KubernetesNamespacedResourceNode node, IKubernetesService k8sService, IGraphDatabaseClient graphDbClient, ILogger logger)
    {
        if (env.ValueFrom == null)
        {
            return null;
        }

        if (env.ValueFrom.SecretKeyRef != null)
        {
            logger.LogDebug($"Env from secret {env.Name}. Source: {env.ValueFrom.SecretKeyRef.Name}");
            var secretNode = new KubernetesNamespacedResourceNode(
                null,
                node.ClusterResourceId,
                node.Namespace,
                node.SubscriptionId,
                node.ResourceGroupName,
                 node.Location,
                env.ValueFrom.SecretKeyRef.Name,
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesSecretType);
            await graphDbClient.AddOrUpdateNodeAsync(secretNode);
            var edge = new ArmResourceEdge(node.GetNodeId(), secretNode.GetNodeId(), Constants.Relationships.References);
            edge.AddReferenceEnvProperties();
            await graphDbClient.AddOrUpdateEdgeAsync(edge);

            return secretNode;
        }
        else if (env.ValueFrom.ConfigMapKeyRef != null)
        {
            logger.LogDebug($"Env from config map {env.Name}. Source: {env.ValueFrom.ConfigMapKeyRef.Name}");
            var configMapNode = new KubernetesNamespacedResourceNode(
                null,
                node.ClusterResourceId,
                node.Namespace,
                node.SubscriptionId,
                node.ResourceGroupName,
                 node.Location,
                env.ValueFrom.ConfigMapKeyRef.Name,
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesConfigMapType);
            await graphDbClient.AddOrUpdateNodeAsync(configMapNode);
            var edge = new ArmResourceEdge(node.GetNodeId(), configMapNode.GetNodeId(), Constants.Relationships.References);
            edge.AddReferenceEnvProperties();
            await graphDbClient.AddOrUpdateEdgeAsync(edge);

            return configMapNode;
        }

        return null;
    }

    public async static Task<KubernetesNamespacedResourceNode?> TryLinkVolumeReferenceAsync(this V1Volume volume, KubernetesNamespacedResourceNode node, IKubernetesService k8sService, IGraphDatabaseClient graphDbClient, ILogger logger)
    {
        if (volume.Secret != null)
        {
            logger.LogDebug($"Secret volume {volume.Name}. Source: {volume.Secret.SecretName}");
            var secretNode = new KubernetesNamespacedResourceNode(
                null,
                node.ClusterResourceId,
                node.Namespace,
                node.SubscriptionId,
                node.ResourceGroupName,
                 node.Location,
                volume.Secret.SecretName,
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesSecretType);
            await graphDbClient.AddOrUpdateNodeAsync(secretNode);
            var edge = new ArmResourceEdge(node.GetNodeId(), secretNode.GetNodeId(), Constants.Relationships.References);
            edge.AddReferenceVolumeMountProperties();
            await graphDbClient.AddOrUpdateEdgeAsync(edge);

            return secretNode;
        }
        else if (volume.ConfigMap != null)
        {
            logger.LogDebug($"ConfigMap volume {volume.Name}. Source: {volume.ConfigMap.Name}");
            var configMapNode = new KubernetesNamespacedResourceNode(
                null,
                node.ClusterResourceId,
                node.Namespace,
                node.SubscriptionId,
                node.ResourceGroupName,
                node.Location,
                volume.ConfigMap.Name,
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesConfigMapType);
            await graphDbClient.AddOrUpdateNodeAsync(configMapNode);
            var edge = new ArmResourceEdge(node.GetNodeId(), configMapNode.GetNodeId(), Constants.Relationships.References);
            edge.AddReferenceVolumeMountProperties();
            await graphDbClient.AddOrUpdateEdgeAsync(edge);

            return configMapNode;
        }
        else if (volume.PersistentVolumeClaim != null)
        {
            logger.LogDebug($"Persistent volume {volume.Name}.");
            var pvcNode = new KubernetesNamespacedResourceNode(
                null,
                node.ClusterResourceId,
                node.Namespace,
                node.SubscriptionId,
                node.ResourceGroupName,
                node.Location,
                volume.PersistentVolumeClaim.ClaimName,
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesPersistentVolumeClaimType);

            await graphDbClient.AddOrUpdateNodeAsync(pvcNode);
            var edge = new ArmResourceEdge(node.GetNodeId(), pvcNode.GetNodeId(), Constants.Relationships.References);
            edge.AddReferenceVolumeMountProperties();
            await graphDbClient.AddOrUpdateEdgeAsync(edge);

            return pvcNode;
        }

        return null;
    }

    public async static Task SaveKubernetesResourceNode(this KubernetesResourceNode node, IGraphDatabaseClient graphDbClient)
    {
        await graphDbClient.AddOrUpdateNodeAsync(node);
        GraphNode pNode;
        if (node is KubernetesNamespacedResourceNode namespacedNode)
        {
            pNode = ArmResourceCrawlerFactory.CreateKubernetesResourceNode(
            k8sObject: null,
            subscriptionId: namespacedNode.SubscriptionId,
            resourceGroupName: namespacedNode.ResourceGroupName,
            location: namespacedNode.Location,
            clusterResourceId: namespacedNode.ClusterResourceId,
            namespaceName: null,
            resourceName: namespacedNode.Namespace,
            group: Constants.KubernetesCoreGroup,
            apiVersion: Constants.KubernetesV1Version,
            kind: Constants.KubernetesNamespaceType
        );
        }
        else
        {
            pNode = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(node.ClusterResourceId) ?? throw new Exception("Failed to create resource node from resource identifier");
        }

        var edge = new ArmResourceEdge(pNode.GetNodeId(), node.GetNodeId(), Constants.Relationships.Contains);
        await graphDbClient.AddOrUpdateEdgeAsync(edge);
    }

    public static async Task<ArmResourceNode?> TryProcessPostgreSqlEnvironmentVariablesAsync(
        this V1Container container,
        GraphNode parentNode,
        PostgreSqlConnectionStringHelper postgresHelper,
        string sourceType,
        ILogger logger)
    {
        if (container.Env == null)
        {
            return null;
        }

        var containerEnvVars = container.Env
            .Where(env => !string.IsNullOrEmpty(env.Value))
            .ToDictionary(env => env.Name, env => env.Value);

        if (postgresHelper.HasPostgreSqlEnvironmentVariables(containerEnvVars))
        {
            try
            {
                return await postgresHelper.GetPostgreSqlResourceFromEnvironmentVariablesAsync(
                    parentNode, containerEnvVars, sourceType);
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning($"Error processing PostgreSQL environment variables for container in {parentNode.GetNodeId()}: {ex.Message}");
            }
        }

        return null;
    }

    public static async Task<ArmResourceNode?> TryProcessPostgreSqlEnvironmentVariablesAsync(
        this Azure.ResourceManager.AppContainers.Models.ContainerAppContainer container,
        GraphNode parentNode,
        PostgreSqlConnectionStringHelper postgresHelper,
        string sourceType,
        ILogger logger)
    {
        if (container.Env == null)
        {
            return null;
        }

        var containerEnvVars = container.Env
            .Where(env => !string.IsNullOrEmpty(env.Value))
            .ToDictionary(env => env.Name, env => env.Value);

        if (postgresHelper.HasPostgreSqlEnvironmentVariables(containerEnvVars))
        {
            try
            {
                return await postgresHelper.GetPostgreSqlResourceFromEnvironmentVariablesAsync(
                    parentNode, containerEnvVars, sourceType);
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning($"Error processing PostgreSQL environment variables for container in {parentNode.GetNodeId()}: {ex.Message}");
            }
        }

        return null;
    }
}
