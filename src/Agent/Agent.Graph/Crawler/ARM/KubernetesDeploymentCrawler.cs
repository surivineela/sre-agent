using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM
{
    public class K8sDeploymentCrawler : IArmResourceCrawler
    {
        private readonly ILogger<K8sDeploymentCrawler> _logger;
        private readonly IGraphDatabaseManager _dbManager;
        private readonly IKubernetes _k8sClient;

        public K8sDeploymentCrawler(ILogger<K8sDeploymentCrawler> logger, IGraphDatabaseManager dbManager)
        {
            _logger = logger;
            _dbManager = dbManager;
            // Initialize Kubernetes client using the default configuration (e.g. from KUBECONFIG or in-cluster config)
            var config = KubernetesClientConfiguration.BuildDefaultConfig();
            _k8sClient = new Kubernetes(config);
        }

        public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode clusterNode)
        {
            _logger.LogInformation($"Crawling Kubernetes Deployments in cluster: {clusterNode.ResourceId}");

            // List deployments in all namespaces.
            var deployments = await _k8sClient.AppsV1.ListDeploymentForAllNamespacesAsync(
                allowWatchBookmarks: false,
                continueParameter: null,
                fieldSelector: null,
                labelSelector: null,
                limit: null,
                pretty: null,
                resourceVersion: null,
                resourceVersionMatch: null,
                timeoutSeconds: null,
                watch: false
            );
            foreach (var dep in deployments.Items)
            {
                // Create a unique identifier for this deployment node.
                var deploymentId = $"{clusterNode.ResourceId}/deployments/{dep.Metadata.NamespaceProperty}/{dep.Metadata.Name}";
                var depNode = new ArmResourceNode(
                    resourceType: "K8s/Deployment",
                    resourceId: deploymentId,
                    subscriptionId: clusterNode.SubscriptionId, // reusing properties from the cluster node
                    resourceGroupName: dep.Metadata.NamespaceProperty,
                    resourceName: dep.Metadata.Name);

                // Add the deployment node to the graph.
                await _dbManager.AddOrUpdateNodeAsync(depNode.GetNodeLabel(), depNode.GetNodeId(), depNode.GetResourceType(), depNode.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(clusterNode.GetNodeId(), depNode.GetNodeId(), "CONTAINS");

                // Inspect each container’s env vars for SQL connection strings.
                if (dep.Spec?.Template?.Spec?.Containers != null)
                {
                    foreach (var container in dep.Spec.Template.Spec.Containers)
                    {
                        if (container.Env != null)
                        {
                            foreach (var env in container.Env)
                            {
                                if (!string.IsNullOrEmpty(env.Value) &&
                                    env.Value.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
                                {
                                    var sqlNode = await TryLinkSqlResource(depNode, env.Value);
                                    if (sqlNode != null)
                                    {
                                        yield return sqlNode;
                                    }
                                }
                                // If the env var comes from a secret (env.ValueFrom), you might add logic to retrieve and inspect the secret here.
                            }
                        }
                    }
                }

                yield return depNode;
            }
        }

        private async Task<ArmResourceNode> TryLinkSqlResource(ArmResourceNode workloadNode, string possibleSqlResource)
        {
            try
            {
                var sqlId = new ResourceIdentifier(possibleSqlResource).ToString();
                var sqlNode = new ArmResourceNode(
                    resourceType: "Microsoft.Sql/servers",
                    resourceId: sqlId,
                    subscriptionId: workloadNode.SubscriptionId,
                    resourceGroupName: workloadNode.ResourceGroupName,
                    resourceName: workloadNode.ResourceName); // adjust as needed

                await _dbManager.AddOrUpdateNodeAsync(sqlNode.GetNodeLabel(), sqlNode.GetNodeId(), sqlNode.GetResourceType(), sqlNode.GetNodeProperties());
                await _dbManager.AddEdgeIfNotExistsAsync(workloadNode.GetNodeId(), sqlNode.GetNodeId(), "SQL_CONNECTED");
                _logger.LogInformation($"Linked workload {workloadNode.ResourceId} with SQL resource {sqlId}");
                return sqlNode;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error linking SQL resource from value: {possibleSqlResource}. Exception: {ex.Message}");
                return null;
            }
        }
    }
}
