using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;

namespace Agent.Graph.Crawler.Kubernetes;

public class KubernetesDeploymentCrawler : IResourceCrawler
{
    private readonly ILogger<KubernetesDeploymentCrawler> _logger;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;
    private readonly ArmClient _armClient;
    private readonly SqlConnectionStringHelper _sqlHelper;
    private readonly PostgreSqlConnectionStringHelper _postgresHelper;

    public KubernetesDeploymentCrawler(ILogger<KubernetesDeploymentCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient, IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient, _graphDbClient);
        _postgresHelper = new PostgreSqlConnectionStringHelper(logger, _armClient, _graphDbClient);
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
                deploymentNode.ResourceName);
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
                        var refNode = await env.TryLinkEnvReferenceAsync(deploymentNode, _k8sService, _graphDbClient, _logger);
                        if (refNode != null)
                        {
                            yield return refNode;
                            // continue match on values
                        }

                        // check env value
                        // match sql connection string
                        var sqlNode = await env.TryMatchAndLinkSqlResourcesAsync(deploymentNode, _sqlHelper, _graphDbClient, "deployment", _logger);
                        if (sqlNode != null)
                        {
                            yield return sqlNode;
                            continue;
                        }

                        // match postgresql connection string
                        var postgresNode = await env.TryMatchAndLinkPostgreSqlResourcesAsync(deploymentNode, _postgresHelper, _graphDbClient, "deployment", _logger);
                        if (postgresNode != null)
                        {
                            yield return postgresNode;
                            continue;
                        }

                        // match service name call
                        var serviceNode = await env.TryMatchAndLinkServiceAsync(deploymentNode, _k8sService, _graphDbClient, _logger);
                        if (serviceNode != null)
                        {
                            yield return serviceNode;
                            continue;
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
                            var refNode = await volume.TryLinkVolumeReferenceAsync(deploymentNode, _k8sService, _graphDbClient, _logger);
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

