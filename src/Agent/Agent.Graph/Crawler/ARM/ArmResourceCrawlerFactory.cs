using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.ARM;

public class ArmResourceCrawlerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ArmResourceCrawlerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IArmResourceCrawler CreateFromNode(ArmResourceNode node, IGraphDatabaseClient graphDbClient, AzureResourceGraphClient graphClient, ArmClient armClient)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (graphDbClient == null)
        {
            throw new ArgumentNullException(nameof(graphDbClient));
        }

        // For system managed identity the resource id is the actual resource
        if (node is ManagedIdentityNode)
        {
            return new ManagedIdentityCrawler(_loggerFactory.CreateLogger<ManagedIdentityCrawler>(), graphDbClient, graphClient, armClient);
        }

        // Filter by known resource type
        if (Constants.SubscriptionType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new SubscriptionCrawler(_loggerFactory.CreateLogger<SubscriptionCrawler>(), graphDbClient, armClient);
        }

        if (Constants.ResourceGroupType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceGroupCrawler(_loggerFactory.CreateLogger<ResourceGroupCrawler>(), graphDbClient, graphClient);
        }

        if (Constants.ContainerAppEnvironmentType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppEnvironmentCrawler(_loggerFactory.CreateLogger<ContainerAppEnvironmentCrawler>(), graphDbClient, graphClient, armClient);
        }

        if (Constants.ContainerAppType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppCrawler(_loggerFactory.CreateLogger<ContainerAppCrawler>(), graphDbClient, armClient);
        }

        if (Constants.VirtualNetworkType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new VirtualNetworkCrawler(_loggerFactory.CreateLogger<VirtualNetworkCrawler>(), graphDbClient, armClient);
        }

        if (Constants.LoadBalancerType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new LoadBalancerCrawler(_loggerFactory.CreateLogger<LoadBalancerCrawler>(), graphDbClient, armClient);
        }

        if (Constants.AppServiceType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServiceCrawler(_loggerFactory.CreateLogger<AppServiceCrawler>(), graphDbClient, armClient);
        }

        if (Constants.AppServicePlanType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServicePlanCrawler(_loggerFactory.CreateLogger<AppServicePlanCrawler>(), graphDbClient);
        }

        if (Constants.ManagedClusterType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new K8sClusterCrawler(_loggerFactory.CreateLogger<K8sClusterCrawler>(), graphDbClient, _loggerFactory, armClient);
        }

        return new GenericArmResourceCrawler(_loggerFactory.CreateLogger<GenericArmResourceCrawler>(), graphDbClient, armClient);
    }

    public static ArmResourceNode CreateResourceNodeFromResourceIdentifier(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return null;
        }
        var id = new ResourceIdentifier(resourceId);
        if (id == null || string.IsNullOrEmpty(id.SubscriptionId))
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

        if (Constants.ContainerAppEnvironmentType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppEnvironmentNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        if (Constants.AppServiceType.Equals(id.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServiceNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        return new ArmResourceNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
    }
}
