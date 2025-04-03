using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Schema;
using Gremlin.Net.Driver;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Legacy
{
    public class InMemoryGraphManager : IGraphDatabaseClient
    {
        private readonly ILogger<InMemoryGraphManager> _logger;

        private readonly Dictionary<string, Node> _nodes = new();
        private readonly Dictionary<string, Edge> _edges = new();

        public bool AddOrUpdateNode(Node node)
        {
            if (_nodes.ContainsKey(node.Id))
            {
                var existingNode = _nodes[node.Id];
                existingNode.Name = node.Name;
                existingNode.Type = node.Type;
                existingNode.Properties = node.Properties;
                return false; // Node updated
            }
            else
            {
                _nodes[node.Id] = node;
                return true; // Node added
            }
        }

        public void AddUndirectedEdgeIfNotExists(Node node1, Node node2, string relationshipType)
        {
            AddDirectedEdgeIfNotExists(node1, node2, relationshipType);
            AddDirectedEdgeIfNotExists(node2, node1, relationshipType);
        }

        public bool AddDirectedEdgeIfNotExists(
            Node sourceNode,
            Node targetNode,
            string relationshipType,
            IDictionary<string, object> properties = null)
        {
            var edgeId = $"{sourceNode.Id}_{relationshipType}_{targetNode.Id}";
            if (_edges.ContainsKey(edgeId))
            {
                return false; // Edge already exists
            }
            else
            {
                var edge = new Edge
                {
                    Id = edgeId,
                    SourceNodeId = sourceNode.Id,
                    TargetNodeId = targetNode.Id,
                    RelationshipType = relationshipType,
                    Properties = properties
                };
                _edges[edgeId] = edge;
                return true; // Edge added
            }
        }

        public Node GetNode(string nodeId)
        {
            _nodes.TryGetValue(nodeId, out var node);
            return node;
        }

        public Edge GetEdge(string edgeId)
        {
            _edges.TryGetValue(edgeId, out var edge);
            return edge;
        }

        public List<Node> GetAllNodes()
        {
            return _nodes.Values.ToList();
        }

        public List<Edge> GetAllEdges()
        {
            return _edges.Values.ToList();
        }

        public Task Clear()
        {
            _edges.Clear();
            _nodes.Clear();

            return Task.CompletedTask;
        }

        public Task<bool> AddOrUpdateEdgeAsync(string sourceNodeId, string targetNodeId, string relationshipType, IDictionary<string, object> properties)
        {
            _logger.LogInformation($"Adding edge from {sourceNodeId} to {targetNodeId} with relationship type '{relationshipType}'.");
            var sourceNode = GetNode(sourceNodeId) ?? throw new ArgumentException($"Source node with ID '{sourceNodeId}' does not exist.");
            var targetNode = GetNode(targetNodeId) ?? throw new ArgumentException($"Target node with ID '{targetNodeId}' does not exist.");
            var edgeAdded = AddDirectedEdgeIfNotExists(sourceNode, targetNode, relationshipType, properties);
            _logger.LogInformation($"Edge from {sourceNodeId} to {targetNodeId} with relationship type '{relationshipType}' added: {edgeAdded}.");
            return Task.FromResult(edgeAdded);
        }

        public Task<bool> AddOrUpdateNodeAsync(string nodeLabel, string nodeId, string resourceType, IDictionary<string, object> properties)
        {
            _logger.LogInformation($"Adding or updating node with ID '{nodeId}'.");
            var node = new Node(nodeId, nodeLabel, resourceType, properties);
            var nodeAdded = AddOrUpdateNode(node);
            _logger.LogInformation($"Node with ID '{nodeId}' added or updated: {nodeAdded}.");
            return Task.FromResult(nodeAdded);
        }

        public Task<ResultSet<dynamic>> Query(string query, int maxMessageSize = 20000)
        {
            throw new NotImplementedException();
        }

        public Task<bool> AddOrUpdateNodeAsync(GraphNode node)
        {
            return AddOrUpdateNodeAsync(node.GetNodeLabel(), node.GetNodeId(), node.GetResourceType(), node.GetNodeProperties());
        }

        public Task<bool> AddOrUpdateEdgeAsync(GraphEdge edge)
        {
            return AddOrUpdateEdgeAsync(edge.GetSourceNodeId(), edge.GetTargetNodeId(), edge.GetRelationship(), edge.GetEdgeProperties());
        }
    }
}
