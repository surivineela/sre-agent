using Agent.Graph.Schema;

namespace Agent.Graph
{
    public class InMemoryGraphManager
    {
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
            string relationshipType)
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
    }
}
