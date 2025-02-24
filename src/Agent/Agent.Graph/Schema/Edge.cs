namespace Agent.Graph.Schema
{
    public class Edge
    {
        public string Id { get; set; }
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public string RelationshipType { get; set; }
        public IDictionary<string, object> Properties { get; set; }
    }
}
