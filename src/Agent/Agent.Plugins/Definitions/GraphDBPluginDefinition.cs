using System.ComponentModel;
using Gremlin.Net.Driver;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPluginDefinition
    {
        public IGraphDBPlugin _plugin { get; }
        public GraphDBPluginDefinition(IGraphDBPlugin graphDBPlugin)
        {
            _plugin = graphDBPlugin;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [KernelFunction("query")]
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await _plugin.Query(query);
        }

        [KernelFunction("FindAllNetworkConnectedResources")]
        [Description("Finds all resources that a particular Azure Container App connects to through network connections, such as Redis caches, databases, and other services. Useful for networking connectivity debug")]
        public async Task<string> FindAllNetworkConnectedResources(
            [Description("Azure Resource Id of the Container App, should begin with /subscriptions...., Example: /subscriptions/a058f7c6-592d-4490-887a-803e748787c0/resourcegroups/aca-sre-agent-demo/providers/microsoft.app/containerapps/iot-dashboard")]string resourceId = "")
        {
            return await _plugin.FindAllNetworkConnectedResources(resourceId);
        }
    }
} 