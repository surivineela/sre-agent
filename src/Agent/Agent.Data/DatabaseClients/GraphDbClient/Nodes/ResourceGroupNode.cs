// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient;

public sealed class ResourceGroupNode : ArmResourceNode
{
    public const string Type = "resourcegroups";

    public ResourceGroupNode(
        string subscriptionId,
        string resoureGroupName,
        string? location = null) : base(Type, subscriptionId, resoureGroupName, location ?? string.Empty)
    {
        ResourceName = resoureGroupName.ToLowerInvariant();
        ResourceId = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}";
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var properties = new Dictionary<string, object>
        {
            { "updateTs", UpdateTs },
            { "resourceId", ResourceId},
            { "subscriptionId", SubscriptionId},
            { "resourceGroupName", ResourceGroupName ?? string.Empty },
            { "isDeleted", IsDeleted}
        };

        if (!string.IsNullOrEmpty(Location))
        {
            properties.Add("location", Location);
        }

        return properties;
    }
}

