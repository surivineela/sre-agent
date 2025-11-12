// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class PagerDutyIncidentNode : GraphNode
{
    [GraphProperty("incidentId")]
    public string? IncidentId { get; set; }

    public override string GetHashString()
    {
        return GetNodeId();
    }

    public override string GetNodeId()
    {
        return $"{GetNodeLabel()}/{IncidentId?.ToLowerInvariant()}";
    }

    public override string GetNodeLabel()
    {
        return "incidents/pagerduty";
    }

    public override string GetResourceType()
    {
        return GetNodeLabel();
    }

    public override string GetResourceKind()
    {
        return ResourceKindHelper.getResourceKind(GetNodeLabel(), null);
    }

    public override void SetResourceKind(string? NewResourceKind)
    {
        return;
    }

    public override string GetSubscriptionId()
    {
        return "";
    }
}
