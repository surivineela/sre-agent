using Agent.Graph.Crawler.Legacy;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    /// <summary>
    /// Using this approach because SK does not allow interfaces to be used as kernel functions
    /// https://github.com/microsoft/semantic-kernel/issues/10323
    /// </summary>
    /// <param name="subscriptionPlugin"></param>
    public class SubscriptionPluginDefinition(ISubscriptionPlugin subscriptionPlugin)
    {
        private readonly ISubscriptionPlugin _subscriptionPlugin = subscriptionPlugin;


        [KernelFunction("list_all_subscriptions")]
        public async Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync()
        {
            return await _subscriptionPlugin.ListAllSubscriptionsAsync();
        }

        [KernelFunction("list_app_services")]
        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            return await _subscriptionPlugin.ListAppServicesAsync(subscriptionId);
        }

        [KernelFunction("build_resource_graph_for_all_subscriptions")]
        public async Task<InMemoryGraphManager> BuildResourceGraphForAllSubscriptionsAsync()
        {
            return await _subscriptionPlugin.BuildResourceGraphForAllSubscriptionsAsync();
        }
    }
}
