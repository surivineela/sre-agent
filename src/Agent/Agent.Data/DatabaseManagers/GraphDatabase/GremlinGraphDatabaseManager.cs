using Agent.Core.Configuration;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.AccessControl;
using System.Text.Json;

namespace Agent.Data.DatabaseManagers.GraphDatabase
{
    public class CustomGraphSON2Reader : GraphSON2Reader
    {
        public override dynamic ToObject(JsonElement graphSon) =>
            graphSon.ValueKind switch
            {
                // numbers
                JsonValueKind.Number when graphSon.TryGetInt32(out var intValue) => intValue,
                JsonValueKind.Number when graphSon.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when graphSon.TryGetDecimal(out var decimalValue) => decimalValue,


                _ => base.ToObject(graphSon)
            };
    }

    public class GremlinGraphDatabaseManager : IGraphDatabaseManager
    {
        private static GremlinClient? _gremlinClient;
        private static readonly object _lock = new object();
        private readonly GraphSettings _graphSettings;
        private readonly ILogger<GremlinGraphDatabaseManager> _logger;

        public GremlinGraphDatabaseManager(GraphSettings graphSettings, ILogger<GremlinGraphDatabaseManager> logger)
        {
            _graphSettings = graphSettings;
            _logger = logger;

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
            var accountName = _graphSettings.AccountName;
            var accountKey = _graphSettings.ApiKey;
            var database = _graphSettings.Database;
            var collection = _graphSettings.Collection;

            var gremlinServer = new GremlinServer(
                hostname: $"{accountName}.gremlin.cosmos.azure.com",
                port: 443,
                enableSsl: true,
                username: $"/dbs/{database}/colls/{collection}",
                password: accountKey
            );

            return new GremlinClient(
                gremlinServer,
                messageSerializer: new GraphSON2MessageSerializer(new CustomGraphSON2Reader())
            );
        }

        public async Task<bool> AddOrUpdateNodeAsync(string nodelabel, string nodeId, string resourceType, IDictionary<string, object> properties)
        {
            var sanitizedNodeId = GetSanitizedCosmosDBId(nodeId);
            var query = $"g.V('{sanitizedNodeId}').fold().coalesce(unfold()";
            foreach (var property in properties)
            {
                query += $".property('{property.Key}', '{property.Value}')";
            }
            query += $", addV('{nodelabel}').property(id, '{sanitizedNodeId}').property('resourceType', '{resourceType}')";
            foreach (var property in properties)
            {
                query += $".property('{property.Key}', '{property.Value}')";
            }
            query += ")";

            _logger.LogTrace($"AddOrUpdateNodeAsync: query: {query}");

            var result = await _gremlinClient!.SubmitAsync<dynamic>(query);
            return result.Count > 0;
        }

        public async Task<bool> AddEdgeIfNotExistsAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object> properties = null)
        {
            var sanitizedSourceNodeId = GetSanitizedCosmosDBId(sourceNodeId);
            var sanitizedTargetNodeId = GetSanitizedCosmosDBId(targetNodeId);
            var edgeId = GetSanitizedCosmosDBId($"{sanitizedSourceNodeId}_{relationshipType}_{sanitizedTargetNodeId}");

            var query = $"g.V('{sanitizedSourceNodeId}').as('a').V('{sanitizedTargetNodeId}').as('b').coalesce(outE('{relationshipType}').where(inV().is('b')), addE('{relationshipType}').property(id, '{edgeId}')";

            if (properties != null) {
                foreach (var property in properties)
                {
                    query += $".property('{property.Key}', '{property.Value}')";
                }
            }

            query += ".from('a').to('b'))";

            _logger.LogTrace($"AddEdgeIfNotExistsAsync: query: {query}");

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

        public async Task Clear()
        {
            string query = "g.V().drop()";
            await _gremlinClient!.SubmitAsync<dynamic>(query);
        }

        public async Task<ResultSet<dynamic>> Query(string query)
        {
            _logger.LogDebug($"Executing Gremlin query: {query}");

            try
            {
                var res = await _gremlinClient!.SubmitAsync<dynamic>(query);
                int messageSize = JsonSerializer.Serialize(res).Count();
                if (messageSize > 20000)
                {
                    return new ResultSet<dynamic>(new string[] { "Too many results" }, null);
                }

                return res;
            } catch (Exception e)
            {
                return new ResultSet<dynamic>(new string[] { $"Exception: {e.Message}" }, null);
            }
        }

        private static string GetSanitizedCosmosDBId(string id)
        {
            return id.Replace("/", "_").Replace(":", "_").Replace(" ", "_");
        }
    }
}
