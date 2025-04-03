using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class AppServiceNode : ArmResourceNode
{
    public string? VnetId { get; set; }
    public string? MinTlsVersion { get; set; }

    public AppServiceNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null,
        string? vnetId = null,
        string? tlsVersion = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        VnetId = vnetId;
        MinTlsVersion = tlsVersion;
    }
    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();
        if (VnetId != null)
        {
            props.Add("vnetId", VnetId);
        }
        if (MinTlsVersion != null)
        {
            props.Add("minTlsVersion", MinTlsVersion);
        }
        return props;
    }
}
