// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public sealed class ResourceGroupNode : ArmResourceNode
{
    public const string Type = "resourcegroups";

    public ResourceGroupNode(
        string subscriptionId,
        string resoureGroupName,
        string? location = null)
        : base(
            resourceType: Type,
            subscriptionId: subscriptionId,
            resourceGroupName: resoureGroupName,
            location: location ?? string.Empty)
    {
        ResourceName = resoureGroupName.ToLowerInvariant();
        ResourceId = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}";
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var properties = base.GetNodeProperties();
        // Ensure resourceGroupName is never null
        properties["resourceGroupName"] = ResourceGroupName ?? string.Empty;
        return properties;
    }
}

