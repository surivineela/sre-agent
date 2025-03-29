// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Graph.Schema
{
    public class ApplicationGraph
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public SimpleNode EntryPoint { get; set; }
        public List<SimpleNode> Nodes { get; set; } = new List<SimpleNode>();
    }

    public class SimpleNode
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string ResourceId { get; set; }

        public SimpleNode(Node node)
        {
            Id = node.Id;
            Name = node.Name;
            Type = node.Type;
            ResourceId = node.Properties.TryGetValue("resourceId", out var resourceId) 
                ? ((IEnumerable<object>)resourceId).First().ToString()
                : string.Empty;
        }
    }
}