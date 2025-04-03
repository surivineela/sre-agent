using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class AksNode : ArmResourceNode
{
    public AksNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null) : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        Location = location?.NormalizeLocation();
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();

        return props;
    }
}
