using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Azure.ResourceManager;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Octokit;

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
                }                if (container.VolumeMounts != null)
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

            // After processing individual environment variables, scan for Individual Variables patterns (PostgreSQL environment variables)
            var allEnvVars = statefulSet.Spec.Template.Spec.Containers
                .Where(c => c.Env != null)
                .SelectMany(c => c.Env)
                .Where(env => !string.IsNullOrEmpty(env.Value))
                .ToDictionary(env => env.Name, env => env.Value);

            if (_postgresHelper.HasPostgreSqlEnvironmentVariables(allEnvVars))
            {
                ArmResourceNode postgreSqlEnvNode = null;
                try
                {
                    postgreSqlEnvNode = await _postgresHelper.GetPostgreSqlResourceFromEnvironmentVariablesAsync(
                        statefulSetNode, allEnvVars, "k8s:statefulset:environmentVariables");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error processing PostgreSQL environment variables for {statefulSetNode.GetNodeId()}: {ex.Message}");
                }

                if (postgreSqlEnvNode != null)
                {
                    yield return postgreSqlEnvNode;
                }
            }
        }

    }
}
