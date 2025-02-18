using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Microsoft.Extensions.Logging;

public class ArmResourceCrawlerFactory
{
    private readonly ILoggerFactory _loggerFactory;

    private const string SubscriptionType = "Subscription";
    private const string ContainerAppType = "Microsoft.App/containerApps";
    private const string ContainerAppEnvironmentType = "Microsoft.App/managedEnvironments";
    private const string VirtualNetworkType = "Microsoft.Network/virtualNetworks";
    private const string LoadBalancerType = "Microsoft.Network/loadBalancers";

    // New constants for App Service Web/Function Apps and App Service Plans.
    private const string AppServiceType = "Microsoft.Web/sites";
    private const string AppServicePlanType = "Microsoft.Web/serverFarms";

    public ArmResourceCrawlerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public IArmResourceCrawler CreateFromNode(ArmResourceNode node, IGraphDatabaseManager dbManager)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (dbManager == null)
        {
            throw new ArgumentNullException(nameof(dbManager));
        }

        if (SubscriptionType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new SubscriptionCrawler(_loggerFactory.CreateLogger<SubscriptionCrawler>(), dbManager, _loggerFactory);
        }

        if (ContainerAppType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new ContainerAppCrawler(_loggerFactory.CreateLogger<ContainerAppCrawler>(), dbManager);
        }

        if (ContainerAppEnvironmentType.Equals(node.ResourceType, StringComparison.InvariantCultureIgnoreCase))
        {
            return new ContainerAppEnvironmentCrawler(_loggerFactory.CreateLogger<ContainerAppEnvironmentCrawler>(), dbManager);
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

        return new GenericArmResourceCrawler(_loggerFactory.CreateLogger<GenericArmResourceCrawler>(), dbManager);
    }
}
