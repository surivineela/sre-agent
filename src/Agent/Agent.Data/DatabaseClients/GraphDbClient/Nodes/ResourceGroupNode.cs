using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public sealed class ResourceGroupNode : ArmResourceNode
{

    public ResourceGroupNode(
        string subscriptionId,
        string resoureGroupName,
        string location = null) : base("resourcegroup", subscriptionId, resoureGroupName, location)
    {
        ResourceName = resoureGroupName.ToLowerInvariant();
        ResourceId = $"/subscriptions/{SubscriptionId}/resourcegroups/{ResourceGroupName}";
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var properties = new Dictionary<string, object>
        {
            { "updateTs", UpdateTs },
            { "resourceId", ResourceId },
            { "subscriptionId", SubscriptionId },
            { "resourceGroupName", ResourceGroupName },
        };

        if (!string.IsNullOrEmpty(Location))
        {
            properties.Add("location", Location);
        }

        return properties;
    }
}
