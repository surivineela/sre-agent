using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;

namespace FirstPartyAgent.Core.Services
{
    public class NullableGraphDatabaseClient : IGraphDatabaseClient
    {
        public Task<bool> AddOrUpdateNodeAsync(string nodeLabel, string nodeId, string resourceType, IDictionary<string, object> properties)
        {
            return Task.FromResult(false);
        }
        public Task<bool> AddOrUpdateNodeAsync(GraphNode node)
        {
            return Task.FromResult(false);
        }
        public Task<bool> AddOrUpdateEdgeAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object> properties = null)
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
            return Task.FromResult(new ResultSet<dynamic>(null, null));
        }
        public Task<ResultSet<T>> Query<T>(string query)
        {
            return Task.FromResult(new ResultSet<T>(null, null));
        }
        public Task<string> GetNodeId(string resourceId)
        {
            return Task.FromResult(string.Empty);
        }
    }
}
