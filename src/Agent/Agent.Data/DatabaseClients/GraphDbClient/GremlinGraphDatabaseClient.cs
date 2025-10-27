// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Logging;
using Gremlin.Net.Driver;
using Gremlin.Net.Driver.Exceptions;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Logging;
using Polly;

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    // workaround for Numberic types
    // https://stackoverflow.com/questions/68092798/gremlin-net-deserialize-number-property/72316108#72316108
    public class CustomGraphSON2Reader : GraphSON2Reader
    {
        public override dynamic? ToObject(JsonElement graphSon) =>
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
        private Lazy<Task<GremlinClient>> _gremlinClient;
        private readonly ILogger<GremlinGraphDatabaseClient> _logger;
        private readonly AsyncPolicy _retryPolicy;
        private readonly IAuthenticationService _authenticationService;
        private readonly GraphSettings _graphSettings;
        private CancellationTokenSource _tokenExpiryCts = new CancellationTokenSource();

        private const string DefaultGraphName = "g";
        public string GraphName { get; set; } = DefaultGraphName;

        public GremlinGraphDatabaseClient(GremlinClient client, ILogger<GremlinGraphDatabaseClient> logger, IAuthenticationService authenticationService)
        {
            _logger = logger;
            _graphSettings = new GraphSettings();
            _gremlinClient = new Lazy<Task<GremlinClient>>(Task.FromResult(client));
            _retryPolicy = BuildRetryPolicy();
            _authenticationService = authenticationService;
        }

        public GremlinGraphDatabaseClient(GraphSettings graphSettings, ILogger<GremlinGraphDatabaseClient> logger, IAuthenticationService authenticationService)
        {
            _logger = logger;
            _graphSettings = graphSettings;
            _gremlinClient = new Lazy<Task<GremlinClient>>(async () => await CreateGremlinClient(graphSettings), LazyThreadSafetyMode.ExecutionAndPublication);
            _retryPolicy = BuildRetryPolicy();
            _authenticationService = authenticationService;
        }

        private AsyncPolicy BuildRetryPolicy()
        {
            return Policy
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

                        TimeSpan retryDuration;
                        if (respEx.StatusAttributes != null &&
                            respEx.StatusAttributes.TryGetValue("x-ms-retry-after-ms", out var value) &&
                            value != null &&
                            int.TryParse(value.ToString(), out int retryMs))
                        {
                            retryDuration = TimeSpan.FromMilliseconds(retryMs);
                        }
                        else
                        {
                            retryDuration = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)); // default
                        }

                        if (retryDuration.TotalMilliseconds <= 0)
                        {
                            return TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
                        }
                        return retryDuration;
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

        private async Task<GremlinClient> CreateGremlinClient(GraphSettings graphSettings)
        {
            var hostname = $"{graphSettings.AccountName}.{graphSettings.DomainSuffix}";
            var port = 443;
            var username = $"/dbs/{graphSettings.Database}/colls/{graphSettings.Collection}";
            string password;

            if (string.IsNullOrEmpty(graphSettings.ApiKey))
            {
                _logger.LogInternalInformation("Using Managed Identity Authentication for Gremlin");
                var cred = _authenticationService.GetGraphDbCredential();
                var token = await cred.GetTokenAsync(
                    new Azure.Core.TokenRequestContext(
                        [Constants.CosmosDbOboTokenScope]
                    ),

                    CancellationToken.None
                );

                password = token.Token;
                var tokenExpiration = token.ExpiresOn.UtcDateTime;

                // refresh token 10 minutes before expiration
                _tokenExpiryCts = new CancellationTokenSource();
                _tokenExpiryCts.CancelAfter(tokenExpiration - DateTime.UtcNow - TimeSpan.FromMinutes(10));
            }
            else
            {
                _logger.LogInternalInformation("Using API Key Authentication for Gremlin");
                password = graphSettings.ApiKey;
            }

            return CreateGremlinClient(hostname, port, username, password);
        }

        private GremlinClient CreateGremlinClient(string hostname, int port, string username, string password)
        {
            var gremlinServer = new GremlinServer(
                hostname: hostname,
                port: port,
                enableSsl: true,
                username: username,
                password: password
            );

            var random = new Random();

            var policy = Policy.Handle<Exception>()
                .WaitAndRetry(retryCount: 5,
                    sleepDurationProvider: retryAttempt =>
                    {
                        var delay = TimeSpan.FromSeconds(Math.Max(30, Math.Pow(2, retryAttempt)));
                        var jitter = TimeSpan.FromMilliseconds(random.Next(0, 1000));
                        return delay.Add(jitter);
                    },
                    onRetry: (exception, timeSpan, context) =>
                    {
                        _logger.LogInternalWarning($"Error connecting to Gremlin server. Retrying in {timeSpan.TotalSeconds} seconds. Exception: {exception.Message}");
                    });

            return policy.Execute(() =>
                {
                    return new GremlinClient(
                        gremlinServer,
                        messageSerializer: new GraphSON2MessageSerializer(new CustomGraphSON2Reader())
                    );
                });
        }

        private async Task<GremlinClient> GetGremlinClientValue()
        {
            try
            {
                _tokenExpiryCts.Token.ThrowIfCancellationRequested();
                return await _gremlinClient.Value;
            }
            catch (Exception ex)
            {
                if (ex is OperationCanceledException)
                {
                    _logger.LogInternalInformation("Recreating Gremlin client due to token expiration.");
                }
                else
                {
                    _logger.LogInternalError(ex, "Recreating Gremlin client due to exception.");
                }

                _gremlinClient = new Lazy<Task<GremlinClient>>(() => CreateGremlinClient(_graphSettings), LazyThreadSafetyMode.ExecutionAndPublication);
                return await _gremlinClient.Value;
            }
        }

        public async Task<bool> AddOrUpdateNodeAsync(string nodelabel, string nodeId, string resourceType, IDictionary<string, object> properties, string? resourceKind)
        {
            var sanitizedNodeId = GetSanitizedCosmosDBId(nodeId);

            // NOTE: This is a temporary workaround to avoid the crawler from overwriting these properties
            if (properties.ContainsKey("appHealthInfo") &&
                (properties["appHealthInfo"] == null ||
                 properties["appHealthInfo"].ToString() == "null" ||
                 string.IsNullOrEmpty(properties["appHealthInfo"].ToString())))
            {
                properties.Remove("appHealthInfo");
            }

            if (properties.ContainsKey("remarks") &&
                (properties["remarks"] == null ||
                 properties["remarks"].ToString() == "null" ||
                 string.IsNullOrEmpty(properties["remarks"].ToString())))
            {
                properties.Remove("remarks");
            }

            var query = $"g.V('{sanitizedNodeId}').fold().coalesce(unfold()";
            foreach (var property in properties)
            {
                query += $".property('{property.Key}', {getValue(property.Value)})";
            }
            query += $", addV('{nodelabel}').property(id, '{sanitizedNodeId}').property('resourceType', '{resourceType}').property('resourceKind', '{resourceKind}')";
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
                _logger.LogInternalError(ex, $"429. Query: {query}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error add/update node: {ex.Message}. Query: {query}");
                return false;
            }
        }

        public async Task<bool> AddOrUpdateEdgeAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object>? properties = null)
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
                _logger.LogInternalError(ex, $"Error add/update edge: {ex.Message}. Query: {query}");
                return false;
            }
        }

        public async Task Clear()
        {
            string query = "g.V().drop()";
            await SubmitWithRetry(query);
        }

        public async Task<ResultSet<dynamic>> Query(string query)
        {
            return await Query<dynamic>(query);
        }

        public async Task<ResultSet<T>> Query<T>(string query)
        {
            _logger.LogTrace($"Executing Gremlin query: {query}");

            try
            {
                return await SubmitWithRetry<T>(query);
            }
            catch (Exception e)
            {
                _logger.LogInternalError(e, $"Exception: {e.Message}. Query: {query}");

                return new ResultSet<T>([], new Dictionary<string, object>());
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
                case bool b:
                    return b ? "true" : "false";
                default:
                    return $"'{val}'";
            }
        }

        private async Task<ResultSet<dynamic>> SubmitWithRetry(string query)
        {
            return await SubmitWithRetry<dynamic>(query);
        }

        private async Task<ResultSet<T>> SubmitWithRetry<T>(string query)
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                if (this.GraphName == DefaultGraphName)
                {
                    return await (await GetGremlinClientValue()).SubmitAsync<T>(query);
                }
                else
                {
                    // Lets us override which graph we are querying.
                    // For example, if we have a graph called "gfunctionsBadFlexApps" that we want this client to query, but all the queries are still written using "g".
                    // Then we can use ArgsAliases to redirect queries on "g" to "gfunctionsBadFlexApps".
                    var msgBuilder = Gremlin.Net.Driver.Messages.RequestMessage.Build(Tokens.OpsEval)
                        .AddArgument(Tokens.ArgsAliases, new Dictionary<string, string> { { "g", this.GraphName } })
                        .AddArgument(Tokens.ArgsGremlin, query);

                    return await (await GetGremlinClientValue()).SubmitAsync<T>(msgBuilder.Create());
                }
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

        public Task<bool> AddOrUpdateNodeAsync(GraphNode node)
        {
            return AddOrUpdateNodeAsync(node.GetNodeLabel(), node.GetNodeId() ?? string.Empty, node.GetResourceType() ?? string.Empty, node.GetNodeProperties(), node.GetResourceKind());
        }

        public Task<bool> AddOrUpdateEdgeAsync(GraphEdge edge)
        {
            return AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
        }

        public async Task<string> GetNodeId(string resourceId)
        {
            var result = await Query($"g.V().has('isDeleted', false).has('resourceId', '{resourceId.ToLowerInvariant()}').limit(1)");
            if (result is not null && result.Count == 1)
            {
                return result.First()["id"].ToString() ?? string.Empty;
            }
            return "";
        }

        public async Task SoftDeleteResourceById(string resourceId)
        {
            var query = $"g.V().has('resourceId', '{resourceId.ToLowerInvariant()}').property('isDeleted', true).property('updateTs', {DateTime.UtcNow.Ticks})";

            _logger.LogTrace($"SoftDeleteNode: query: {query}");

            try
            {
                var result = await SubmitWithRetry(query);
                if (result.Count == 0)
                {
                    _logger.LogInternalWarning($"Soft delete node {resourceId} did not find any matching node.");
                }
                else
                {
                    _logger.LogInternalInformation($"Soft deleted node {resourceId} successfully.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error soft deleting node: {ex.Message}. Query: {query}");
            }
        }

        public async Task<string> SoftDeleteConnectedRepositoryByResourceId(string resourceId)
        {
            try
            {
                // Current timestamp in .NET ticks
                long updateTs = DateTime.UtcNow.Ticks;

                // Gremlin query to soft-delete the connected repository
                string query = $@"
                g.V().has('id', '{resourceId}')
                     .has('isDeleted', false)
                     .outE('SERVES_CODE')
                     .inV()
                     .has('isDeleted', false)
                     .property('isDeleted', true)
                     .property('updateTs', {updateTs})";

                var result = await SubmitWithRetry(query);
                if (result == null || result?.Count == 0)
                {
                    string message = $"Soft delete connected repository for resource {resourceId} did not find any matching repository.";
                    _logger.LogInternalWarning(message);
                    return message;
                }

                else
                {
                    string message = $"Soft deleted connected repository for resource {resourceId} successfully.";
                    return message;
                }
            }

            catch (ResponseException ex)
            {
                _logger.LogInternalError(ex, $"Gremlin query failed while trying to delete the source repository for resource: {resourceId}");
                throw;
            }
        }

        public async Task ReplaceEdgeRelationshipAsync(ArmResourceEdge edge, string newRelationship)
        {
            if (string.Equals(edge.Relationship, newRelationship, StringComparison.OrdinalIgnoreCase))
            {
                return; // no change
            }

            var sourceSan = GetSanitizedCosmosDBId(edge.SourceNodeId);
            var targetSan = GetSanitizedCosmosDBId(edge.TargetNodeId);
            var oldEdgeId = GetSanitizedCosmosDBId($"{sourceSan}_{edge.Relationship}_{targetSan}");

            try
            {
                await Query($"g.E('{oldEdgeId}').drop()");
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"Failed dropping old edge {oldEdgeId}: {ex.Message}");
            }

            edge.Relationship = newRelationship;
            edge.UpdateTs = DateTime.UtcNow.Ticks;

            await AddOrUpdateEdgeAsync(edge);
        }
    }
}
