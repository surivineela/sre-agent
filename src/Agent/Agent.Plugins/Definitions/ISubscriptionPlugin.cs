using Agent.Graph.Crawler.Legacy;
using Agent.Plugins.Models;

namespace Agent.Plugins
{
    public interface ISubscriptionPlugin
    {
        Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync();

        Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId);

        Task<IReadOnlyList<ContainerAppDescriptor>> ListContainerAppsAsync(Guid subscriptionId);

        Task<InMemoryGraphManager> BuildResourceGraphForAllSubscriptionsAsync();
    }
}
