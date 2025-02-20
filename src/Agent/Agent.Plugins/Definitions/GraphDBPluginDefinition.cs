using System.ComponentModel;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
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
    }
} 