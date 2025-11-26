// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public interface IResourceGraphNode
{
    public string? GetNodeLabel();
    public string GetNodeId();
    public string? GetResourceType();
    public string? GetResourceKind();
    public IDictionary<string, object> GetNodeProperties();
}
