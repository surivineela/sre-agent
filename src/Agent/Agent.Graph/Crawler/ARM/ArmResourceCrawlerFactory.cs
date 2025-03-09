using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

public class ArmResourceCrawlerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    public ArmResourceCrawlerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IArmResourceCrawler CreateFromNode(ArmResourceNode node, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient, ArmClient armClient)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (dbManager == null)
        {
            throw new ArgumentNullException(nameof(dbManager));
        }

        // For system managed identity the resource id is the actual resource
        if (node is ManagedIdentityNode)
        {
            return new ManagedIdentityCrawler(_loggerFactory.CreateLogger<ManagedIdentityCrawler>(), dbManager, graphClient);
        }

        // Filter by known resource type
        if (Constants.SubscriptionType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new SubscriptionCrawler(_loggerFactory.CreateLogger<SubscriptionCrawler>(), dbManager, armClient);
        }

        if (Constants.ResourceGroupType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ResourceGroupCrawler(_loggerFactory.CreateLogger<ResourceGroupCrawler>(), dbManager, graphClient);
        }

        if (Constants.ContainerAppEnvironmentType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppEnvironmentCrawler(_loggerFactory.CreateLogger<ContainerAppEnvironmentCrawler>(), dbManager, graphClient, armClient);
        }

        if (Constants.ContainerAppType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new ContainerAppCrawler(_loggerFactory.CreateLogger<ContainerAppCrawler>(), dbManager, armClient);
        }

        if (Constants.VirtualNetworkType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new VirtualNetworkCrawler(_loggerFactory.CreateLogger<VirtualNetworkCrawler>(), dbManager, armClient);
        }

        if (Constants.LoadBalancerType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new LoadBalancerCrawler(_loggerFactory.CreateLogger<LoadBalancerCrawler>(), dbManager, armClient);
        }

        if (Constants.AppServiceType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServiceARMCrawler(_loggerFactory.CreateLogger<AppServiceARMCrawler>(), dbManager, armClient);
        }

        if (Constants.AppServicePlanType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new AppServicePlanCrawler(_loggerFactory.CreateLogger<AppServicePlanCrawler>(), dbManager);
        }

        if (Constants.ManagedClusterType.Equals(node.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return new K8sClusterCrawler(_loggerFactory.CreateLogger<K8sClusterCrawler>(), dbManager, _loggerFactory, armClient);
        }

        return new GenericArmResourceCrawler(_loggerFactory.CreateLogger<GenericArmResourceCrawler>(), dbManager, armClient);
    }

    public static ArmResourceNode CreateResourceNodeFromResourceIdentifier(string resourceId)
    {
        if(string.IsNullOrEmpty(resourceId))
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
