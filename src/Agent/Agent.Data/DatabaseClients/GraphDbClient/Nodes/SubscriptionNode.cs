using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public sealed class SubscriptionNode : ArmResourceNode
{
    public string? SubscriptionName;

    public SubscriptionNode(string subscriptionId)
        : base("subscription", subscriptionId)
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
