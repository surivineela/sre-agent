using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Azure.ResourceManager;
using k8s.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesStatefulSetCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesStatefulSetCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;
    private readonly ArmClient _armClient;
    private readonly SqlConnectionStringHelper _sqlHelper;
    private readonly PostgreSqlConnectionStringHelper _postgresHelper;

    public KubernetesStatefulSetCrawler(
        ILogger<KubernetesStatefulSetCrawler> logger,
        IGraphDatabaseClient graphDbClient,
        IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _k8sService = k8sService;
        _armClient = null;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient, _graphDbClient);
        _postgresHelper = new PostgreSqlConnectionStringHelper(logger, _armClient, _graphDbClient);
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var statefulSetNode = (KubernetesNamespacedResourceNode)node;
        _logger.LogDebug($"Crawling stateful set: {statefulSetNode.GetNodeId()}");

        var statefulSet = (V1StatefulSet)statefulSetNode.ResourceObject;
        if (statefulSet == null)
        {
            statefulSet = await _k8sService.GetStatefulSetAsync(
                statefulSetNode.ClusterResourceId,
                statefulSetNode.Namespace,
                statefulSetNode.ResourceName);
        }

        if (statefulSet == null)
        {
            yield break;
        }

        await statefulSetNode.SaveKubernetesResourceNode(_graphDbClient);

        if (statefulSet.Spec?.Template?.Spec?.Containers != null)
        {
            HashSet<string> knownVolumes = [];
            foreach (var container in statefulSet.Spec.Template.Spec.Containers)
            {
                if (container.Env != null)
                {
                    foreach (var env in container.Env)
                    {
                        var refNode = await env.TryLinkEnvReferenceAsync(statefulSetNode, _k8sService, _graphDbClient, _logger);
                        if (refNode != null)
                        {
                            yield return refNode;
                            // continue match on values
                        }

                        // chck env value
                        // match sql connection string
                        var sqlNode = await env.TryMatchAndLinkSqlResourcesAsync(statefulSetNode, _sqlHelper, _graphDbClient, "statefulset", _logger);
                        if (sqlNode != null)
                        {
                            yield return sqlNode;
                            continue;
                        }

                        // match postgresql connection string
                        var postgresNode = await env.TryMatchAndLinkPostgreSqlResourcesAsync(statefulSetNode, _postgresHelper, _graphDbClient, "statefulset", _logger);
                        if (postgresNode != null)
                        {
                            yield return postgresNode;
                            continue;
                        }

                        // match service name call
                        var serviceNode = await env.TryMatchAndLinkServiceAsync(statefulSetNode, _k8sService, _graphDbClient, _logger);
                        if (serviceNode != null)
                        {
                            yield return serviceNode;
                            continue;
                        }
                    }

                    // Process PostgreSQL environment variables for this container
                    var postgreSqlEnvNode = await container.TryProcessPostgreSqlEnvironmentVariablesAsync(
                        statefulSetNode, _postgresHelper, "k8s:statefulset:environmentVariables", _logger);
                    if (postgreSqlEnvNode != null)
                    {
                        yield return postgreSqlEnvNode;
                    }
                }
                if (container.VolumeMounts != null)
                {
                    foreach (var volumeMount in container.VolumeMounts)
                    {
                        var volume = statefulSet.Spec.Template.Spec.Volumes?.FirstOrDefault(v => v.Name == volumeMount.Name);
                        if (volume?.Name != null && !knownVolumes.Contains(volume.Name))
                        {
                            knownVolumes.Add(volume.Name);
                            var refNode = await volume.TryLinkVolumeReferenceAsync(statefulSetNode, _k8sService, _graphDbClient, _logger);
                            if (refNode != null)
                            {
                                yield return refNode;
                            }
                        }
                    }
                }
            }
        }

    }
}
