using System.ComponentModel;
using System.Text.Json;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;

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
        public async Task<string> FindAllNetworkConnectedResources(string resourceId = "")
        {
            try
            {
                string vertexFilter = string.IsNullOrEmpty(resourceId)
                    ? "hasLabel('microsoft.app/containerapps')"
                    : $"hasId('{resourceId.ToLower().Replace("/", "_")}')"; // Replacing "/" with "_" as graph IDs use underscores

                string query = $@"
    g.V().{vertexFilter}
      .outE('USES_REDIS')
      .project('from', 'to', 'label', 'connection_details', 'properties')
      .by(outV().values('resourceId'))
      .by(inV().values('resourceId'))
      .by(label())
      .by(
        __.project('protocol', 'port', 'description', 'auth_mechanism')
        .by(constant('SSL/TLS'))
        .by(constant(6380))
        .by(constant('The container app connects to Redis cache over port 6380 (SSL/TLS encrypted) using an environment variable REDIS_HOST'))
        .by(constant('Access key authentication'))
      )
      .by(valueMap())
";

                var results = await GraphDbClient.Query(query);
                return JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding network connected resources");
                return $"Error finding network connected resources: {ex.Message}";
            }
        }

        public async Task AddSourceCodeNodeToContainerAppNodeAsync(string resourceId, string repoUrl)
        {
            try
            {
                var containerAppNodeId = resourceId.ToLower().Replace("/", "_");
                string vertexFilter = $"hasId('{containerAppNodeId}')"; // Replacing "/" with "_" as graph IDs use underscores

                string query = $@"
                    g.V().{vertexFilter}";

                var containerAppNodeResults = await GraphDbClient.Query(query);
                if (!containerAppNodeResults.Any())
                {
                    return;
                }

                // Check if the source code node exists, create it if it doesn't
                string sourceCodeNodeId = repoUrl.ToLower().Replace("/", "_");
                string checkSourceCodeNodeQuery = $"g.V('{sourceCodeNodeId}').hasLabel('microsoft.source/repository')";
                var sourceCodeNodeResults = await GraphDbClient.Query(checkSourceCodeNodeQuery);
                
                if (!sourceCodeNodeResults.Any())
                {
                    var properties = new Dictionary<string, object>
                    {
                        { "resourceId", repoUrl },
                        { "subscriptionId", "githubrepo-sub" },
                        { "resourceGroupName", "githubrepo-rg" },
                        { "resourceName", sourceCodeNodeId },
                        { "updateTs", DateTime.UtcNow.Ticks }
                    };
                            
                    await GraphDbClient.AddOrUpdateNodeAsync("microsoft.source/repository", sourceCodeNodeId, "microsoft.source/repository", properties);
                }

                await GraphDbClient.AddOrUpdateEdgeAsync(containerAppNodeId, sourceCodeNodeId, Constants.Relationships.ServesCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding network connected resources");
            }
        }

        public async Task<List<string>> GetContainerAppsWithNodesWithoutSourceCodeNodesAsync()
        {
            var queryResults = await GraphDbClient.Query(@"
                g.V().has('resourceType', 'microsoft.app/containerapps')
                .not(outE().hasLabel('SERVES_CODE').inV().has('resourceType', 'microsoft.source/repository'))
                .values('resourceId')");

            var resources = queryResults.Select(x => (string)x).OrderBy(resourceId => resourceId.Split("/").Last()).ToList();

            return resources;
        }
    }
} 