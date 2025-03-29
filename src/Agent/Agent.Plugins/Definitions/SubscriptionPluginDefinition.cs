using System.ComponentModel;
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
        [Description("Gets a list of Azure subscriptions that a user has access to. Returns subscription details including ID and display name. Can optionally filter subscriptions by name using a case-insensitive partial match. Use this to find specific subscriptions or to get the subscription ID when you only know the name.")]
        public async Task<IReadOnlyList<SubscriptionDescriptor>> ListAllSubscriptionsAsync(
            [Description("Optional. Filter subscriptions by display name. Case-insensitive partial match. Example: 'prod' will match 'Production Subscription'")] string? subscriptionNameFilter = null)
        {
            return await _subscriptionPlugin.ListAllSubscriptionsAsync(subscriptionNameFilter);
        }

        [KernelFunction("list_app_services")]
        public async Task<IReadOnlyList<AppServiceDescriptor>> ListAppServicesAsync(Guid subscriptionId)
        {
            return await _subscriptionPlugin.ListAppServicesAsync(subscriptionId);
        }
    }
}
