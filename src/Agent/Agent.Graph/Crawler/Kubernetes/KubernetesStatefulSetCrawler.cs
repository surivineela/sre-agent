using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
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

    public KubernetesStatefulSetCrawler(
        ILogger<KubernetesStatefulSetCrawler> logger,
        IGraphDatabaseClient graphDbClient,
        IKubernetesService k8sService)
    {
        _logger = logger;
        _graphDbClient = graphDbClient;
        _k8sService = k8sService;
        _sqlHelper = new SqlConnectionStringHelper(logger, _armClient, _graphDbClient);
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

                        // match service name call
                        var serviceNode = await env.TryMatchAndLinkServiceAsync(statefulSetNode, _k8sService, _graphDbClient, _logger);
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
                        var volume = statefulSet.Spec.Template.Spec.Volumes?.FirstOrDefault(v => v.Name == volumeMount.Name);
                        if (!knownVolumes.Contains(volume.Name))
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

        // connect pods
        var selector = statefulSet.Spec.Selector.ToSelectorString();
        var podList = new V1PodList();
        if (!string.IsNullOrEmpty(selector))
        {
            podList = await _k8sService.GetPodsAsync(statefulSetNode.ClusterResourceId, statefulSetNode.Namespace, selector);
        }
        foreach (var pod in podList.Items ?? new List<V1Pod>())
        {
            var podNode = new KubernetesNamespacedResourceNode(
                pod,
                statefulSetNode.ClusterResourceId,
                statefulSetNode.Namespace,
                statefulSetNode.SubscriptionId,
                statefulSetNode.ResourceGroupName,
                pod.Name(),
                Constants.KubernetesCoreGroup,
                Constants.KubernetesV1Version,
                Constants.KubernetesPodType);
            await _graphDbClient.AddOrUpdateNodeAsync(podNode);
            var edge = new ArmResourceEdge(statefulSetNode.GetNodeId(), podNode.GetNodeId(), Constants.Relationships.Contains);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);
            var edge2 = new ArmResourceEdge(podNode.GetNodeId(), statefulSetNode.GetNodeId(), Constants.Relationships.OwnedBy);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge2);

            yield return podNode;
        }
    }
}
