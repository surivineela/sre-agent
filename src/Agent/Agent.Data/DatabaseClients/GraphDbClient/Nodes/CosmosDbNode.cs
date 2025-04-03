using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class CosmosDbNode : ArmResourceNode
{
    public string? ConsistencyPolicy { get; set; }

    public CosmosDbNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null,
        string? consistencyPolicy = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        ConsistencyPolicy = consistencyPolicy;
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();
        if (ConsistencyPolicy != null)
        {
            props.Add("consistencyPolicy", ConsistencyPolicy);
        }
        return props;
    }
}
