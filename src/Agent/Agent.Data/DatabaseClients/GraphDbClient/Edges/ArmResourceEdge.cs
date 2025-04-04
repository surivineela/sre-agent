// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient
{
    public interface IArmResourceGraphEdge
    {
        public string GetRelationship();
        public string GetSourceNodeId();
        public string GetTargetNodeId();
        public IDictionary<string, object> GetEdgeProperties();
    }

    public abstract class GraphEdge : IArmResourceGraphEdge
    {
        public long UpdateTs { get; set; }

        public abstract IDictionary<string, object> GetEdgeProperties();
        public abstract string GetRelationship();
        public abstract string GetSourceNodeId();
        public abstract string GetTargetNodeId();
    }

    public class ArmResourceEdge : GraphEdge
    {
        public string Relationship { get; set; }
        public string SourceNodeId { get; set; }
        public string TargetNodeId { get; set; }
        public IDictionary<string, object> AdditionalProperties { get; }

        public ArmResourceEdge(string sourceNodeId, string targetNodeId, string relationship)
        {
            UpdateTs = DateTime.UtcNow.Ticks;
            SourceNodeId = sourceNodeId;
            TargetNodeId = targetNodeId;
            Relationship = relationship;
            AdditionalProperties = new Dictionary<string, object>();
        }

        public override string GetSourceNodeId()
        {
            return SourceNodeId;
        }

        public override string GetTargetNodeId()
        {
            return TargetNodeId;
        }

        public override IDictionary<string, object> GetEdgeProperties()
        {
            var props = new Dictionary<string, object>()
            {
                { "updateTs", UpdateTs },
            };

            foreach (var kvp in AdditionalProperties)
            {
                props.Add(kvp.Key, kvp.Value);
            }

            return props;
        }

        public override string GetRelationship()
        {
            return Relationship;
        }
    }
}
