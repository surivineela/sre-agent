using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Azure.Core;
using Microsoft.Extensions.Logging;

public class ArmResourceCrawlerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    private const string SubscriptionType = "Subscription";
    private const string ResourceGroupType = "ResourceGroup";
    private const string ContainerAppType = "Microsoft.App/containerApps";
    private const string ContainerAppEnvironmentType = "Microsoft.App/managedEnvironments";
    private const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
    private const string LoadBalancerType = "Microsoft.Network/loadBalancers";
    private const string ManagedClusterType = "Microsoft.ContainerService/managedClusters";
    private const string UserAssignedManagedIdentityType = "Microsoft.ManagedIdentity/userAssignedIdentities";

    // New constants for App Service Web/Function Apps and App Service Plans.
    private const string AppServiceType = "Microsoft.Web/sites";
    private const string AppServicePlanType = "Microsoft.Web/serverFarms";

    public ArmResourceCrawlerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IArmResourceCrawler CreateFromNode(ArmResourceNode node, IGraphDatabaseManager dbManager, AzureResourceGraphClient graphClient)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (dbManager == null)
        {
            throw new ArgumentNullException(nameof(dbManager));
        }

        // Filter by known node type
        if (node is SubscriptionNode)
        {
            return new SubscriptionCrawler(_loggerFactory.CreateLogger<SubscriptionCrawler>(), dbManager, _loggerFactory);
        }

        if (node is ResourceGroupNode)
        {
            return new ResourceGroupCrawler(_loggerFactory.CreateLogger<ResourceGroupCrawler>(), dbManager, graphClient);
        }

        if (node is ContainerAppEnvironmentNode)
        {
            return new ContainerAppEnvironmentCrawler(_loggerFactory.CreateLogger<ContainerAppEnvironmentCrawler>(), dbManager, graphClient);
        }

        if (node is ManagedIdentityNode)
        {
            return new ManagedIdentityCrawler(_loggerFactory.CreateLogger<ManagedIdentityCrawler>(), dbManager, graphClient);
        }

        // Filter by known resource type
        if (ContainerAppType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new ContainerAppCrawler(_loggerFactory.CreateLogger<ContainerAppCrawler>(), dbManager);
        }

        if (VirtualNetworkType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new VirtualNetworkCrawler(_loggerFactory.CreateLogger<VirtualNetworkCrawler>(), dbManager);
        }

        if (LoadBalancerType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new LoadBalancerCrawler(_loggerFactory.CreateLogger<LoadBalancerCrawler>(), dbManager);
        }

        if (AppServiceType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new AppServiceARMCrawler(_loggerFactory.CreateLogger<AppServiceARMCrawler>(), dbManager);
        }

        if (AppServicePlanType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new AppServicePlanCrawler(_loggerFactory.CreateLogger<AppServicePlanCrawler>(), dbManager);
        }

        if (ManagedClusterType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new K8sClusterCrawler(_loggerFactory.CreateLogger<K8sClusterCrawler>(), dbManager, _loggerFactory);
        }

        return new GenericArmResourceCrawler(_loggerFactory.CreateLogger<GenericArmResourceCrawler>(), dbManager);
    }

    public static ArmResourceNode CreateResourceNodeFromResourceIdentifier(string resourceId)
    {
        var id = new ResourceIdentifier(resourceId);
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (!string.IsNullOrEmpty(id.SubscriptionId) && string.IsNullOrEmpty(id.ResourceGroupName))
        {
            return new SubscriptionNode(id.SubscriptionId);
        }

        if (!string.IsNullOrEmpty(id.SubscriptionId) && !string.IsNullOrEmpty(id.ResourceGroupName) && string.IsNullOrEmpty(id.ResourceType))
        {
            return new ResourceGroupNode(id.SubscriptionId, id.ResourceGroupName);
        }

        if (ContainerAppEnvironmentType.Equals(id.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new ContainerAppEnvironmentNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
        }

        return new ArmResourceNode(id.ResourceType, id.ToString(), id.SubscriptionId, id.ResourceGroupName, id.Name);
    }
}
