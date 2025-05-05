// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;
public class AzMonitorAlertNode : GraphNode
{
    [GraphProperty("incidentId")]
    public string IncidentId { get; set; }
    public override string GetHashString()
    {
        return GetNodeId();
    }

    public override string GetNodeId()
    {
        return $"{GetNodeLabel()}/{IncidentId.ToLowerInvariant()}";
    }

    public override string GetNodeLabel()
    {
        return "incidents/azmonitor";
    }

    public override string GetResourceType()
    {
        return GetNodeLabel();
    }

    public override string GetSubscriptionId()
    {
        return string.Empty;
    }
}
