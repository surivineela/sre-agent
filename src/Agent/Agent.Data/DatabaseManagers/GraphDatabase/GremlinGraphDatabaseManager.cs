using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Configuration;

namespace Agent.Data.DatabaseManagers.GraphDatabase
{
    public class GremlinGraphDatabaseManager : IGraphDatabaseManager
    {
        private static GremlinClient? _gremlinClient;
        private static readonly object _lock = new object();
        private readonly IConfiguration _configuration;

        public GremlinGraphDatabaseManager(IConfiguration configuration)
        {
            _configuration = configuration;

            // Initialize the Gremlin client if it hasn't been initialized yet
            if (_gremlinClient == null)
            {
                lock (_lock)
                {
                    if (_gremlinClient == null)
                    {
                        _gremlinClient = CreateGremlinClient();
                    }
                }
            }
        }

        private GremlinClient CreateGremlinClient()
        {
            var accountName = _configuration["Azure:Gremlin:AccountName"];
            var accountKey = _configuration["Azure:Gremlin:AccountKey"];
            var database = _configuration["Azure:Gremlin:Database"];
            var collection = _configuration["Azure:Gremlin:Collection"];

            var gremlinServer = new GremlinServer(
                hostname: $"{accountName}.gremlin.cosmos.azure.com",
                port: 443,
                enableSsl: true,
                username: $"/dbs/{database}/colls/{collection}",
                password: accountKey
            );

            return new GremlinClient(
                gremlinServer,
                messageSerializer: new GraphSON2MessageSerializer()
            );
        }

        public async Task<bool> AddOrUpdateNodeAsync(string nodeId, string resourceType, IDictionary<string, object> properties)
        {
            var sanitizedNodeId = GetSanitizedCosmosDBId(nodeId);
            var query = $"g.V('{sanitizedNodeId}').fold().coalesce(unfold()";
            foreach (var property in properties)
            {
                query += $".property('{property.Key}', '{property.Value}')";
            }
            query += $", addV('node').property(id, '{sanitizedNodeId}').property('resourceType', '{resourceType}')";
            foreach (var property in properties)
            {
                query += $".property('{property.Key}', '{property.Value}')";
            }
            query += ")";

            var result = await _gremlinClient!.SubmitAsync<dynamic>(query);
            return result.Count > 0;
        }

        public async Task<bool> AddEdgeIfNotExistsAsync(string sourceNodeId, string targetNodeId, string relationshipType)
        {
            var sanitizedSourceNodeId = GetSanitizedCosmosDBId(sourceNodeId);
            var sanitizedTargetNodeId = GetSanitizedCosmosDBId(targetNodeId);
            var edgeId = GetSanitizedCosmosDBId($"{sanitizedSourceNodeId}_{relationshipType}_{sanitizedTargetNodeId}");

            var query = $"g.V('{sanitizedSourceNodeId}').as('a').V('{sanitizedTargetNodeId}').as('b').coalesce(outE('{relationshipType}').where(inV().is('b')), addE('{relationshipType}').property(id, '{edgeId}').from('a').to('b'))";

            try
            {
                var result = await _gremlinClient!.SubmitAsync<dynamic>(query);
                return result.Count > 0;
            }
            catch (Gremlin.Net.Driver.Exceptions.ResponseException ex) when (ex.StatusAttributes["x-ms-status-code"].ToString() == "409")
            {
                // Handle conflict exception (edge already exists)
                return false;
            }
        }

        private static string GetSanitizedCosmosDBId(string id)
        {
            return id.Replace("/", "_").Replace(":", "_").Replace(" ", "_");
        }
    }
}
