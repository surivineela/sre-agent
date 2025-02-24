using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Schema;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;

namespace Agent.Graph
{
    // An adapter that allows a InMemoryGraphManager to be used as a IGraphDatabaseManager
    public class InMemoryGraphDatabaseManagerAdapter : IGraphDatabaseManager
    {
        private readonly ILogger<InMemoryGraphDatabaseManagerAdapter> _logger;
        private readonly InMemoryGraphManager _inMemoryGraphManager;

        public InMemoryGraphDatabaseManagerAdapter(ILogger<InMemoryGraphDatabaseManagerAdapter> logger, InMemoryGraphManager inMemoryGraphManager)
        {
            _logger = logger;
            _inMemoryGraphManager = inMemoryGraphManager;
        }

        public Task<bool> AddEdgeIfNotExistsAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object> properties)
        {
            _logger.LogInformation($"Adding edge from {sourceNodeId} to {targetNodeId} with relationship type '{relationshipType}'.");
            var sourceNode = _inMemoryGraphManager.GetNode(sourceNodeId) ?? throw new ArgumentException($"Source node with ID '{sourceNodeId}' does not exist.");
            var targetNode = _inMemoryGraphManager.GetNode(targetNodeId) ?? throw new ArgumentException($"Target node with ID '{targetNodeId}' does not exist.");
            var edgeAdded = _inMemoryGraphManager.AddDirectedEdgeIfNotExists(sourceNode, targetNode, relationshipType, properties);
            _logger.LogInformation($"Edge from {sourceNodeId} to {targetNodeId} with relationship type '{relationshipType}' added: {edgeAdded}.");
            return Task.FromResult(edgeAdded);
        }

        public Task<bool> AddOrUpdateNodeAsync(string nodeLabel, string nodeId, string resourceType, IDictionary<string, object> properties)
        {
            _logger.LogInformation($"Adding or updating node with ID '{nodeId}'.");
            var node = new Node(nodeId, nodeLabel, resourceType, properties);
            var nodeAdded = _inMemoryGraphManager.AddOrUpdateNode(node);
            _logger.LogInformation($"Node with ID '{nodeId}' added or updated: {nodeAdded}.");
            return Task.FromResult(nodeAdded);
        }

        public Task Clear()
        {
            _inMemoryGraphManager.Clear();
            return Task.CompletedTask;
        }

        public Task<ResultSet<dynamic>> Query(string query)
        {
            throw new NotImplementedException();
        }
    }
}