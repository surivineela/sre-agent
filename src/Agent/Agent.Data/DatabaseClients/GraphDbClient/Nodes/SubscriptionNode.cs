// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public sealed class SubscriptionNode : ArmResourceNode
{
    public const string Type = "subscriptions";

    [GraphProperty("subscriptionName")]
    public string? SubscriptionName { get; private set; }

    public SubscriptionNode(
        string subscriptionId)
        : base(Type, subscriptionId)
    {
        ResourceName = subscriptionId.ToLowerInvariant();
        ResourceId = $"/subscriptions/{SubscriptionId}";
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var properties = base.GetNodeProperties();
        // Ensure subscriptionName falls back to subscriptionId if empty
        properties["subscriptionName"] = string.IsNullOrEmpty(SubscriptionName) ? SubscriptionId : SubscriptionName;
        return properties;
    }

    public void UpdateSubscriptionName(string subscriptionName)
    {
        SubscriptionName = subscriptionName;
    }
}

