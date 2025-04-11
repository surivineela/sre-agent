// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient;
public sealed class SubscriptionNode : ArmResourceNode
{
    public const string Type = "subscriptions";
    public string? SubscriptionName;

    public SubscriptionNode(string subscriptionId)
        : base(Type, subscriptionId)
    {
        ResourceName = subscriptionId.ToLowerInvariant();
        ResourceId = $"/subscriptions/{SubscriptionId}";
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        return new Dictionary<string, object>
        {
            { "updateTs", UpdateTs },
            { "resourceId", ResourceId },
            { "subscriptionId", SubscriptionId },
            { "subscriptionName", SubscriptionName }
        };
    }
}

