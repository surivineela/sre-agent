using System.Collections.ObjectModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;

namespace FirstPartyAgent.Core.Services
{
    public class NullableGraphDatabaseClient : IGraphDatabaseClient
    {
        public Task<bool> AddOrUpdateNodeAsync(string nodeLabel, string nodeId, string resourceType, IDictionary<string, object> properties, string? resourceKind)
        {
            return Task.FromResult(false);
        }

        public Task<bool> AddOrUpdateNodeAsync(GraphNode node)
        {
            return Task.FromResult(false);
        }

        public Task<bool> AddOrUpdateEdgeAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object>? properties = null)
        {
            return Task.FromResult(false);
        }

        public Task<bool> AddOrUpdateEdgeAsync(GraphEdge edge)
        {
            return Task.FromResult(false);
        }

        public Task Clear()
        {
            return Task.CompletedTask;
        }

        public Task<ResultSet<dynamic>> Query(string query)
        {
            // Option 1: Return empty collections instead of null
            return Task.FromResult(new ResultSet<dynamic>(new List<dynamic>(), new Dictionary<string, object>()));

            // Option 2: If ResultSet constructor accepts nulls, use null-forgiving operator
            // return Task.FromResult(new ResultSet<dynamic>(null!, null!));
        }

        public Task<ResultSet<T>> Query<T>(string query)
        {
            // Option 1: Return empty collections instead of null
            return Task.FromResult(new ResultSet<T>(new List<T>(), new Dictionary<string, object>()));

            // Option 2: If ResultSet constructor accepts nulls, use null-forgiving operator
            // return Task.FromResult(new ResultSet<T>(null!, null!));
        }

        public Task<string> GetNodeId(string resourceId)
        {
            return Task.FromResult(string.Empty);
        }

        public Task SoftDeleteResourceById(string resourceId)
        {
            return Task.CompletedTask;
        }

        public Task<string> SoftDeleteConnectedRepositoryByResourceId(string resourceId)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
