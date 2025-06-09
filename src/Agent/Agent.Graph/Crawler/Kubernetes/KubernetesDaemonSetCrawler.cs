using System.Linq;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Azure.Core;
using Azure.ResourceManager;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Octokit;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesDaemonSetCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesDaemonSetCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ArmClient _armClient;
    private readonly IKubernetesService _k8sService;
    private readonly SqlConnectionStringHelper _sqlHelper;
    private readonly PostgreSqlConnectionStringHelper _postgresHelper;

    public KubernetesDaemonSetCrawler(ILogger<KubernetesDaemonSetCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient, IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        var config = KubernetesClientConfiguration.BuildDefaultConfig();
        _k8sService = k8sService;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient, _graphDbClient);
        _postgresHelper = new PostgreSqlConnectionStringHelper(logger, _armClient, _graphDbClient);
    }

    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        var daemonSetNode = (KubernetesNamespacedResourceNode)node;
        _logger.LogDebug($"Crawling deployment: {daemonSetNode.GetNodeId()}");

        var daemonSet = (V1Deployment)daemonSetNode.ResourceObject;
        if (daemonSet == null)
        {
            daemonSet = await _k8sService.GetDeploymentAsync(
                daemonSetNode.ClusterResourceId,
                daemonSetNode.Namespace,
                daemonSetNode.ResourceName);
        }

        if (daemonSet == null)
        {
            yield break;
        }

        if (daemonSet.Spec?.Template?.Spec?.Containers != null)
        {
            HashSet<string> knownVolumes = [];
            foreach (var container in daemonSet.Spec.Template.Spec.Containers)
            {
                if (container.Env != null)
                {
                    foreach (var env in container.Env)
                    {
                        var refNode = await env.TryLinkEnvReferenceAsync(daemonSetNode, _k8sService, _graphDbClient, _logger);
                        if (refNode != null)
                        {
                            yield return refNode;
                            // continue match on values
                        }

                        // check env value
                        // match sql connection string
                        var sqlNode = await env.TryMatchAndLinkSqlResourcesAsync(daemonSetNode, _sqlHelper, _graphDbClient, "daemonset", _logger);
                        if (sqlNode != null)
                        {
                            yield return sqlNode;
                            continue;
                        }

                        // match postgresql connection string
                        var postgresNode = await env.TryMatchAndLinkPostgreSqlResourcesAsync(daemonSetNode, _postgresHelper, _graphDbClient, "daemonset", _logger);
                        if (postgresNode != null)
                        {
                            yield return postgresNode;
                            continue;
                        }

                        // match service name call
                        var serviceNode = await env.TryMatchAndLinkServiceAsync(daemonSetNode, _k8sService, _graphDbClient, _logger);
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
                        var volume = daemonSet.Spec.Template.Spec.Volumes?.FirstOrDefault(v => v.Name == volumeMount.Name);
                        if (!knownVolumes.Contains(volume.Name))
                        {
                            knownVolumes.Add(volume.Name);
                            var refNode = await volume.TryLinkVolumeReferenceAsync(daemonSetNode, _k8sService, _graphDbClient, _logger);
                            if (refNode != null)
                            {
                                yield return refNode;
                            }
                        }
                    }
                }
            }

            // After processing individual environment variables, scan for Individual Variables patterns (PostgreSQL environment variables)
            var allEnvVars = daemonSet.Spec.Template.Spec.Containers
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
                        daemonSetNode, allEnvVars, "k8s:daemonset:environmentVariables");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"Error processing PostgreSQL environment variables for {daemonSetNode.GetNodeId()}: {ex.Message}");
                }

                if (postgreSqlEnvNode != null)
                {
                    yield return postgreSqlEnvNode;
                }
            }
        }

    }
}
