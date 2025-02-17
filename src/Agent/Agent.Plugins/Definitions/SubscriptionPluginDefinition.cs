using Agent.Graph;
using Gremlin.Net.Driver;
using Microsoft.SemanticKernel;
using System.ComponentModel;

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

        [KernelFunction("build_mock_resource_graph_for_all_subscriptions")]
        public async Task<InMemoryGraphManager> BuildMockResourceGraphForAllSubscriptionsAsync()
        {
            return await _subscriptionPlugin.BuildMockResourceGraphForAllSubscriptionsAsync();
        }

        [KernelFunction("delete_resource_graph")]
        public async Task DeleteResourceGraph()
        {
            await _subscriptionPlugin.DeleteResourceGraph();
        }

        [KernelFunction("query_resource_graph")]
        [Description(@"In 10 queries or less iteratively starting from g.V().limit(10), try to build the query you need by understanding the schema of the graph.
        Never query more than 10 vertices or edges at a time.
        Traverse one edge at a time.
        Every time you reach new vertices, list the possible edge labels from that vertex e.g. g.V().outE().label().dedup()
        To check the resource type of a node, do e.g. g.V().has('resourceType', ""WebApp"")
        Don't directly use node ids in your queries")]
        public async Task<ResultSet<dynamic>> QueryResourceGraph(string query)
        {
            return await _subscriptionPlugin.QueryResourceGraph(query);
        }
    }
}
