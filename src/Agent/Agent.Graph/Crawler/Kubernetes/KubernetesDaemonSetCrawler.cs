using System.Linq;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
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

    public KubernetesDaemonSetCrawler(ILogger<KubernetesDaemonSetCrawler> logger, IGraphDatabaseClient graphDbClient, ArmClient armClient, IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _armClient = armClient;
        var config = KubernetesClientConfiguration.BuildDefaultConfig();
        _k8sService = k8sService;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient, _graphDbClient);
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

                        // chck env value
                        // match sql connection string
                        var sqlNode = await env.TryMatchAndLinkSqlResourcesAsync(daemonSetNode, _sqlHelper, _graphDbClient, "daemonset", _logger);
                        if (sqlNode != null)
                        {
                            yield return sqlNode;
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
                }

                if (container.VolumeMounts != null)
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
        }

        // connect pods
        var selector = daemonSet.Spec.Selector.ToSelectorString();
        var podList = new V1PodList();
        if (!string.IsNullOrEmpty(selector))
        {
            podList = await _k8sService.GetPodsAsync(daemonSetNode.ClusterResourceId, daemonSetNode.Namespace, selector);
        }
        foreach (var pod in podList.Items ?? new List<V1Pod>())
        {
            var podNode = new KubernetesNamespacedResourceNode(
                pod,
                daemonSetNode.ClusterResourceId,
                daemonSetNode.Namespace,
                daemonSetNode.SubscriptionId,
                daemonSetNode.ResourceGroupName,
                pod.Name(),
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesPodType);
            await _graphDbClient.AddOrUpdateNodeAsync(podNode);
            var edge = new ArmResourceEdge(daemonSetNode.GetNodeId(), podNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            yield return podNode;
        }
    }
}
