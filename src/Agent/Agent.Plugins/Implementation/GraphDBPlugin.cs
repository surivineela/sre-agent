using System.ComponentModel;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class GraphDBPlugin : IGraphDBPlugin
    {
        public IGraphDatabaseManager GraphDatabaseManager { get; }
        public ILogger<GraphDBPlugin> _logger { get; }
        public GraphDBPlugin(IGraphDatabaseManager graphDatabaseManager, ILogger<GraphDBPlugin> logger)
        {
            GraphDatabaseManager = graphDatabaseManager;
            _logger = logger;
        }

        /// <summary>
        /// When implementing this in prod, we need to give this agent a read-only user
        /// </summary>
        [KernelFunction("query")]
        [Description("Run a generic query against the graph database. Do NOT perform any write operations.")]
        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await GraphDatabaseManager.Query(query);
        }
    }
} 