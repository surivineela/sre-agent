// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.External;
using Agent.Graph.Crawler.Kubernetes;
using Agent.Graph.Helpers;
using Agent.Graph.Interfaces;
using Azure.Core;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ArmResourceCrawlerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly AzureResourceGraphClient _graphClient;
    private readonly IArmClientFactory _armClientFactory;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly IKubernetesService _k8sService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IAuthenticationService _authenticationService;
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly IGitHubService _gitHubService;
    private readonly ArmHelper _armHelper;

    public ArmResourceCrawlerFactory(ILoggerFactory loggerFactory, AzureResourceGraphClient graphClient, IArmClientFactory armClientFactory, IGraphDatabaseClient graphDbClient,
        [FromKeyedServices("Crawler")] IKubernetesService k8sService, IHttpClientFactory httpClientFactory, IAuthenticationService authenticationService, IAzureDevOpsService azureDevOpsService,
        IGitHubService gitHubService, ArmHelper armHelper)
    {
        _loggerFactory = loggerFactory;
        _graphClient = graphClient;
        _armClientFactory = armClientFactory;
        _graphDbClient = graphDbClient;
        _authenticationService = authenticationService;
        _k8sService = k8sService;
        _armHelper = armHelper;

        _httpClientFactory = httpClientFactory;
        _azureDevOpsService = azureDevOpsService;
        _gitHubService = gitHubService;
    }

    public IResourceCrawler CreateFromNode(GraphNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        var armClient = _armClientFactory.GetCrawlerArmClient();

        if (node is ArmResourceNode armNode)
        {
            // For system managed identity the resource id is the actual resource
            if (armNode is ManagedIdentityNode)
            {
                return new ManagedIdentityCrawler(_loggerFactory.CreateLogger<ManagedIdentityCrawler>(), _graphDbClient, _graphClient, armClient);
            }

            // Filter by known resource type
            if (Constants.SubscriptionType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new SubscriptionCrawler(_loggerFactory.CreateLogger<SubscriptionCrawler>(), _graphDbClient, armClient);
            }

            if (Constants.ResourceGroupType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new ResourceGroupCrawler(_loggerFactory.CreateLogger<ResourceGroupCrawler>(), _graphDbClient, _graphClient, armClient);
            }

            if (Constants.ContainerAppEnvironmentType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new ContainerAppEnvironmentCrawler(_loggerFactory.CreateLogger<ContainerAppEnvironmentCrawler>(), _graphDbClient, _graphClient, armClient);
            }

            if (Constants.ContainerAppType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new ContainerAppCrawler(_loggerFactory.CreateLogger<ContainerAppCrawler>(), _graphDbClient, armClient, _graphClient);
            }

            if (Constants.VirtualNetworkType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new VirtualNetworkCrawler(_loggerFactory.CreateLogger<VirtualNetworkCrawler>(), _graphDbClient, armClient);
            }

            if (Constants.LoadBalancerType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new LoadBalancerCrawler(_loggerFactory.CreateLogger<LoadBalancerCrawler>(), _graphDbClient, armClient);
            }

            if (Constants.AppServiceType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                if (Constants.LogicAppKind.Equals(armNode.ResourceKind, StringComparison.OrdinalIgnoreCase))
                {
                    return new LogicAppCrawler(_loggerFactory.CreateLogger<LogicAppCrawler>(), _graphDbClient, _armHelper, armClient);
                }

                return new AppServiceCrawler(_loggerFactory.CreateLogger<AppServiceCrawler>(), _graphDbClient, armClient);
            }

            if (Constants.AppServicePlanType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new AppServicePlanCrawler(_loggerFactory.CreateLogger<AppServicePlanCrawler>(), _graphDbClient, armClient);
            }

            if (Constants.ManagedClusterType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new AzureKubernetesServiceCrawler(_loggerFactory.CreateLogger<AzureKubernetesServiceCrawler>(), _graphDbClient, _loggerFactory, armClient, _k8sService, _graphClient);
            }

            if (Constants.PostgreSqlFlexServerType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new PostgreSqlFlexServerCrawler(_loggerFactory.CreateLogger<PostgreSqlFlexServerCrawler>(), _graphDbClient, armClient);
            }

            if (Constants.ApiManagementType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new APIManagementCrawler(_loggerFactory.CreateLogger<APIManagementCrawler>(), _graphDbClient, _graphClient, armClient);
            }
            if (Constants.ApiManagementBackendType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new APIManagementBackendCrawler(_loggerFactory.CreateLogger<APIManagementBackendCrawler>(), _graphDbClient, _graphClient, armClient);
            }
            if (Constants.ApiCenterType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new APICenterCrawler(_loggerFactory.CreateLogger<APICenterCrawler>(), _graphDbClient, _graphClient, armClient, _httpClientFactory);
            }

            if (Constants.ApiConnectionType.Equals(armNode.ResourceType, StringComparison.OrdinalIgnoreCase))
            {
                return new ConnectionCrawler(_loggerFactory.CreateLogger<ConnectionCrawler>(), _graphDbClient, armClient);
            }

            return new GenericArmResourceCrawler(_loggerFactory.CreateLogger<GenericArmResourceCrawler>(), _graphDbClient, armClient);
        }
        else if (node is KubernetesResourceNode k8sNode)
        {
            if (Constants.KubernetesNamespaceType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesNamespaceCrawler(_loggerFactory.CreateLogger<KubernetesNamespaceCrawler>(), _k8sService, _graphDbClient);
            }

            if (Constants.KubernetesDeploymentType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesDeploymentCrawler(_loggerFactory.CreateLogger<KubernetesDeploymentCrawler>(), _graphDbClient, armClient, _k8sService);
            }

            if (Constants.KubernetesDaemonSetType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesDaemonSetCrawler(_loggerFactory.CreateLogger<KubernetesDaemonSetCrawler>(), _graphDbClient, armClient, _k8sService);
            }

            if (Constants.KubernetesServiceType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesServiceCrawler(_loggerFactory.CreateLogger<KubernetesServiceCrawler>(), _k8sService, _graphDbClient);
            }

            if (Constants.KubernetesStatefulSetType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesStatefulSetCrawler(_loggerFactory.CreateLogger<KubernetesStatefulSetCrawler>(), _graphDbClient, _k8sService, armClient);
            }

            if (Constants.KubernetesNodeType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesNodeCrawler(_loggerFactory.CreateLogger<KubernetesNodeCrawler>(), _graphDbClient, _k8sService);
            }

            if (Constants.KubernetesPersistentVolumeType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesPersistentVolumeCrawler(_loggerFactory.CreateLogger<KubernetesPersistentVolumeCrawler>(), _graphDbClient, _k8sService, _graphClient);
            }

            if (Constants.KubernetesPersistentVolumeClaimType.Equals(k8sNode.Kind, StringComparison.OrdinalIgnoreCase))
            {
                return new KubernetesPersistentVolumeClaimCrawler(_loggerFactory.CreateLogger<KubernetesPersistentVolumeClaimCrawler>(), _graphDbClient, _k8sService);
            }

            return new KubernetesDummyCrawler();
        }
        else if (node is SourceCodeRepoNode)
        {
            var logger = _loggerFactory.CreateLogger<SourceCodeRepoCrawler>();
            var appConfigHelper = new AppConfigurationHelper(logger, _graphDbClient, _authenticationService, armClient);
            return new SourceCodeRepoCrawler(logger, _graphDbClient, appConfigHelper, _httpClientFactory, _azureDevOpsService, _gitHubService);
        }

        throw new NotImplementedException();
    }

    public static ArmResourceNode? CreateResourceNodeFromResourceIdentifier(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return null;
        }
        var id = new ResourceIdentifier(resourceId);
        if (id is null || string.IsNullOrEmpty(id.SubscriptionId))
        {
            return null;
        }

        if (!string.IsNullOrEmpty(id.SubscriptionId) && string.IsNullOrEmpty(id.ResourceGroupName))
        {
            return new SubscriptionNode(id.SubscriptionId);
        }

        if (!string.IsNullOrEmpty(id.SubscriptionId) && !string.IsNullOrEmpty(id.ResourceGroupName) && string.Equals(id.ResourceType.Type, "resourcegroups", StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceGroupNode(id.SubscriptionId, id.ResourceGroupName);
        }

        if (string.IsNullOrEmpty(id.ResourceGroupName))
        {
            throw new Exception($"Resource identifier '{resourceId}' does not contain a valid resource group name.");
        }

        if (Constants.ContainerAppEnvironmentType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppEnvironmentNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        if (Constants.ContainerAppType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        if (Constants.AppServiceType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServiceNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name, "");
        }

        if (Constants.AppServicePlanType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServicePlanNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        if (Constants.ManagedClusterType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AksNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }
        if (Constants.PostgreSqlFlexServerType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new PostgreSqlFlexServerNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }
        if (Constants.ApiManagementType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new APIManagementNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }
        if (Constants.ApiManagementBackendType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new APIManagementBackendNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }
        if (Constants.ApiCenterType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new APICenterNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }
        if (Constants.ApiConnectionType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ConnectionNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        return new ArmResourceNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
    }

    public static KubernetesResourceNode CreateKubernetesResourceNode(IKubernetesObject? k8sObject, string? subscriptionId, string? resourceGroupName, string? location, string clusterResourceId, string? namespaceName, string resourceName, string group, string apiVersion, string kind)
    {
        if (!string.IsNullOrEmpty(namespaceName))
        {
            return new KubernetesNamespacedResourceNode(
                k8sObject,
                clusterResourceId,
                namespaceName,
                subscriptionId,
                resourceGroupName,
                location,
                resourceName,
                group,
                apiVersion,
                kind
            );
        }

        return new KubernetesResourceNode(
            k8sObject,
            clusterResourceId,
            subscriptionId,
            resourceGroupName,
            location,
            resourceName,
            group,
            apiVersion,
            kind
        );
    }
}

