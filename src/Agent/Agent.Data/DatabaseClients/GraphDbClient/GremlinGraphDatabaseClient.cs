// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Logging;
using Polly;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    // workaround for Numberic types
    // https://stackoverflow.com/questions/68092798/gremlin-net-deserialize-number-property/72316108#72316108
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

    public class GremlinGraphDatabaseClient : IGraphDatabaseClient
    {
        private static GremlinClient? _gremlinClient;
        private static readonly object _lock = new object();
        private readonly GraphSettings _graphSettings;
        private readonly ILogger<GremlinGraphDatabaseClient> _logger;
        private readonly AsyncPolicy _retryPolicy;

        public GremlinGraphDatabaseClient(GraphSettings graphSettings, ILogger<GremlinGraphDatabaseClient> logger)
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

            _retryPolicy = Policy
                .Handle<Gremlin.Net.Driver.Exceptions.ResponseException>(ex => IsRetriableException(ex))
                .WaitAndRetryAsync(100,
                    sleepDurationProvider: (int retryAttempt, Exception ex, Context context) =>
                    {
                        var respEx = ex as Gremlin.Net.Driver.Exceptions.ResponseException;
                        if (respEx == null
                          || !respEx.StatusAttributes.ContainsKey("x-ms-retry-after-ms")
                          || respEx.StatusAttributes["x-ms-retry-after-ms"] == null)
                        {
                            return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                        }

                        return TimeSpan.Parse(respEx.StatusAttributes["x-ms-retry-after-ms"].ToString());
                    },
                    onRetryAsync: (ex, ts, retryCount, context) =>
                    {
                        var gremlinEx = ex as Gremlin.Net.Driver.Exceptions.ResponseException;
                        if (gremlinEx != null)
                        {
                            _logger.LogDebug($"Retry {retryCount} after {ts.TotalMilliseconds} milliseconds. Gremlin exception: {gremlinEx.StatusAttributes["x-ms-status-code"]}");
                        }
                        else
                        {
                            _logger.LogDebug($"Retry {retryCount} after {ts.TotalMilliseconds} milliseconds");
                        }

                        _logger.LogTrace($"Exception: {ex.Message}");
                        return Task.CompletedTask;
                    });
        }

        private GremlinClient CreateGremlinClient()
        {
            var gremlinServer = new GremlinServer(
                hostname: $"{_graphSettings.AccountName}.{_graphSettings.DomainSuffix}",
                port: 443,
                enableSsl: true,
                username: $"/dbs/{_graphSettings.Database}/colls/{_graphSettings.Collection}",
                password: _graphSettings.ApiKey
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
                query += $".property('{property.Key}', {getValue(property.Value)})";
            }
            query += $", addV('{nodelabel}').property(id, '{sanitizedNodeId}').property('resourceType', '{resourceType}')";
            foreach (var property in properties)
            {
                query += $".property('{property.Key}', {getValue(property.Value)})";
            }
            query += ")";

            _logger.LogTrace($"AddOrUpdateNodeAsync: query: {query}");

            try
            {
                var result = await SubmitWithRetry(query);
                return result.Count > 0;
            }
            catch (Gremlin.Net.Driver.Exceptions.ResponseException ex) when (ex.StatusAttributes["x-ms-status-code"].ToString() == "429")
            {
                _logger.LogError(ex, $"429. Query: {query}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error add/update node: {ex.Message}. Query: {query}");
                return false;
            }
        }

        public async Task<bool> AddOrUpdateEdgeAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object> properties = null)
        {
            var sanitizedSourceNodeId = GetSanitizedCosmosDBId(sourceNodeId);
            var sanitizedTargetNodeId = GetSanitizedCosmosDBId(targetNodeId);
            var edgeId = GetSanitizedCosmosDBId($"{sanitizedSourceNodeId}_{relationshipType}_{sanitizedTargetNodeId}");

            var query = $"g.V('{sanitizedSourceNodeId}').as('a').V('{sanitizedTargetNodeId}').as('b').coalesce(__.select('a').outE('{relationshipType}').where(inV().as('b')).limit(1)";
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    query += $".property('{property.Key}', {getValue(property.Value)})";
                }
            }

            query += $", __.select('a').addE('{relationshipType}').to('b').property(id, '{edgeId}')";
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    query += $".property('{property.Key}', {getValue(property.Value)})";
                }
            }
            query += ")";

            _logger.LogTrace($"AddEdgeIfNotExistsAsync: query: {query}");

            try
            {
                var result = await SubmitWithRetry(query);
                return result.Count > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error add/update edge: {ex.Message}. Query: {query}");
                return false;
            }
        }

        public async Task Clear()
        {
            string query = "g.V().drop()";
            await SubmitWithRetry(query);
        }

        public async Task<ResultSet<dynamic>> Query(string query, int maxMessageSize = 20000)
        {
            _logger.LogTrace($"Executing Gremlin query: {query}");

            try
            {
                var res = await SubmitWithRetry(query);
                int messageSize = JsonSerializer.Serialize(res).Count();
                if (maxMessageSize > 0 && messageSize > maxMessageSize)
                {
                    return new ResultSet<dynamic>(new string[] { "Too many results" }, null);
                }

                return res;
            }
            catch (Exception e)
            {
                return new ResultSet<dynamic>(new string[] { $"Exception: {e.Message}" }, null);
            }
        }

        private static string GetSanitizedCosmosDBId(string id)
        {
            return id.Replace("/", "_").Replace(":", "_").Replace(" ", "_");
        }

        private string getValue(object val)
        {
            switch (val)
            {
                case int i:
                    return i.ToString();
                case long l:
                    return l.ToString();
                // TODO: handle more types
                default:
                    return $"'{val}'";
            }
        }

        private async Task<ResultSet<dynamic>> SubmitWithRetry(string query)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _gremlinClient!.SubmitAsync<dynamic>(query);
            });
        }

        private bool IsRetriableException(Gremlin.Net.Driver.Exceptions.ResponseException ex)
        {
            if (ex.StatusAttributes is null)
            {
                return false;
            }

            // ToManyRequest
            if (ex.StatusAttributes.ContainsKey("x-ms-status-code") && ex.StatusAttributes["x-ms-status-code"].ToString() == "429")
            {
                return true;
            }

            // PreConditionFailed
            if (ex.StatusAttributes.ContainsKey("x-ms-status-code") && ex.StatusAttributes["x-ms-status-code"].ToString() == "412")
            {
                return true;
            }

            return false;
        }
    }
}
