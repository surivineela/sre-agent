using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPlugin : IGraphDBPlugin
    {
        public IGraphDatabaseClient GraphDbClient { get; }
        public ILogger<GraphDBPlugin> _logger { get; }
        public GraphDBPlugin(IGraphDatabaseClient graphDbClient, ILogger<GraphDBPlugin> logger)
        {
            GraphDbClient = graphDbClient;
            _logger = logger;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [KernelFunction("query")]
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await GraphDbClient.Query(query);
        }
    }
} 