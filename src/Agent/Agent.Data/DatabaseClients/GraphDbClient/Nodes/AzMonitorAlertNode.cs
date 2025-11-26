// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class AzMonitorAlertNode : GraphNode
{
    public const string AzMonitorAlertResourceType = "incidents/azmonitor";

    [GraphProperty("incidentId")]
    public string IncidentId { get; set; } = string.Empty;

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
        return GetResourceType();
    }

    public override string GetResourceType()
    {
        return AzMonitorAlertResourceType;
    }

    public override string GetResourceKind()
    {
        return ResourceKindHelper.getResourceKind(GetResourceType(), null);
    }

    public override void SetResourceKind(string? NewResourceKind)
    {
        return;
    }

    public override string GetSubscriptionId()
    {
        return string.Empty;
    }
}
