using Agent.Graph;
using Gremlin.Net.Driver;

namespace Agent.Plugins
{
    public interface ISubscriptionPlugin
    {
        Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync();

        Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId);

        Task<InMemoryGraphManager> BuildResourceGraphForAllSubscriptionsAsync();

        Task<InMemoryGraphManager> BuildMockResourceGraphForAllSubscriptionsAsync();

        Task DeleteResourceGraph();

        Task<ResultSet<dynamic>> QueryResourceGraph(string query);
    }
}
