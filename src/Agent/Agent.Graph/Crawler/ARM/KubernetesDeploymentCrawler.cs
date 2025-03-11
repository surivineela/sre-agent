using Agent.Data.DatabaseManagers.GraphDatabase;
using Azure.Core;
using Azure.ResourceManager;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class K8sDeploymentCrawler : IArmResourceCrawler
{
    private readonly ILogger<K8sDeploymentCrawler> _logger;
    private readonly IGraphDatabaseManager _dbManager;
    private readonly IKubernetes _k8sClient;
    private readonly ArmClient _armClient;
    private readonly SqlConnectionStringHelper _sqlHelper;

    public K8sDeploymentCrawler(ILogger<K8sDeploymentCrawler> logger, IGraphDatabaseManager dbManager, ArmClient armClient)
    {
        _logger = logger;
        _dbManager = dbManager;
        _armClient = armClient;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient);

        // Initialize Kubernetes client using the default configuration
        var config = KubernetesClientConfiguration.BuildDefaultConfig();
        _k8sClient = new Kubernetes(config);
    }

    public async IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode clusterNode)
    {
        _logger.LogDebug($"Crawling Kubernetes Deployments in cluster: {clusterNode.ResourceId}");

        V1DeploymentList deployments = null;
        try
        {
            deployments = await _k8sClient.AppsV1.ListDeploymentForAllNamespacesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving Deployments from cluster {clusterNode.ResourceId}: {ex.Message}");
            yield break;
        }

        if (deployments == null) yield break;

        foreach (var dep in deployments.Items)
        {
            ArmResourceNode depNode = null;
            try
            {
                if (dep.Namespace().Contains("gatekeeper") && dep.Name().Contains("--") && string.IsNullOrEmpty(dep.Name()))
                {
                    continue;
                }

                var deploymentId = $"{clusterNode.ResourceId}/deployments/{dep.Metadata.NamespaceProperty}{dep.Metadata.Name}";
                depNode = new ArmResourceNode(
                    resourceType: "Microsoft.ContainerService/K8sDeployment",
                    resourceId: deploymentId,
                    subscriptionId: clusterNode.SubscriptionId,
                    resourceGroupName: dep.Metadata.NamespaceProperty,
                    resourceName: dep.Metadata.Name);

                await _dbManager.AddOrUpdateNodeAsync(
                    depNode.GetNodeLabel(),
                    depNode.GetNodeId(),
                    depNode.GetResourceType(),
                    depNode.GetNodeProperties());

                var edge = new ArmResourceEdge(clusterNode.GetNodeId(), depNode.GetNodeId(), Constants.Relationships.Contains);
                await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error creating deployment node for {dep.Metadata?.Name} in namespace {dep.Metadata?.NamespaceProperty}: {ex.Message}");
                continue;
            }

            if (depNode != null && dep.Spec?.Template?.Spec?.Containers != null)
            {
                foreach (var container in dep.Spec.Template.Spec.Containers)
                {
                    if (container.Env != null)
                    {
                        foreach (var env in container.Env)
                        {
                            ArmResourceNode sqlNode = null;
                            try
                            {
                                if (!string.IsNullOrEmpty(env.Value))
                                {
                                    if (IsSqlConnectionString(env.Value))
                                    {
                                        sqlNode = await _sqlHelper.GetSqlResourceFromConnectionStringAsync(
                                            _dbManager,
                                            depNode,
                                            env.Value);

                                        if (sqlNode != null)
                                        {
                                            var properties = sqlNode.GetNodeProperties();
                                            properties["authType"] = env.Value.Contains("Authentication=Active Directory Managed Identity",
                                                StringComparison.OrdinalIgnoreCase)
                                                    ? "managedIdentity"
                                                    : "connectionString";
                                            properties["source"] = $"k8s:deployment:env:{env.Name}";

                                            await _dbManager.AddOrUpdateNodeAsync(
                                                sqlNode.GetNodeLabel(),
                                                sqlNode.GetNodeId(),
                                                sqlNode.GetResourceType(),
                                                properties);

                                            var edge = new ArmResourceEdge(depNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
                                            await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
                                        }
                                    }
                                    else if (env.Value.Contains("/Microsoft.Sql/", StringComparison.OrdinalIgnoreCase))
                                    {
                                        sqlNode = await TryLinkSqlResourceById(depNode, env.Value, env.Name);
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error processing environment variable {env.Name} in container for deployment {dep.Metadata?.Name}: {ex.Message}");
                                continue;
                            }

                            if (sqlNode != null)
                            {
                                yield return sqlNode;
                            }
                        }
                    }
                }
            }

            yield return depNode;
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

    private async Task<ArmResourceNode> TryLinkSqlResourceById(ArmResourceNode workloadNode, string possibleSqlResource, string envName)
    {
        try
        {
            var sqlId = new ResourceIdentifier(possibleSqlResource).ToString();
            var sqlNode = new ArmResourceNode(
                resourceType: "Microsoft.Sql/servers",
                resourceId: sqlId,
                subscriptionId: workloadNode.SubscriptionId,
                resourceGroupName: ExtractResourceGroupName(sqlId),
                resourceName: ExtractResourceName(sqlId));

            var properties = sqlNode.GetNodeProperties();
            properties["source"] = $"k8s:deployment:env:{envName}";
            properties["authType"] = "resourceId";

            await _dbManager.AddOrUpdateNodeAsync(
                sqlNode.GetNodeLabel(),
                sqlNode.GetNodeId(),
                sqlNode.GetResourceType(),
                properties);

            var edge = new ArmResourceEdge(workloadNode.GetNodeId(), sqlNode.GetNodeId(), Constants.Relationships.SqlConnected);
            await _dbManager.AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());

            _logger.LogDebug($"Linked workload {workloadNode.ResourceId} with SQL resource {sqlId}");
            return sqlNode;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error linking SQL resource from value: {possibleSqlResource}. Exception: {ex.Message}");
            return null;
        }
    }

    private string ExtractResourceGroupName(string resourceId)
    {
        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < segments.Length - 1; i++)
        {
            if (segments[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return segments[i + 1];
            }
        }
        return string.Empty;
    }

    private string ExtractResourceName(string resourceId)
    {
        var segments = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments[segments.Length - 1];
    }
}