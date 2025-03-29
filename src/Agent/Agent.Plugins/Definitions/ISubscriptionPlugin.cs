using Agent.Graph.Crawler.Legacy;

namespace Agent.Plugins
{
    public interface ISubscriptionPlugin
    {
        Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync(string? subscriptionFilter);

        Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId);
    }
}
