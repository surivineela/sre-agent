// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient;
public sealed class ContainerAppEnvironmentNode : ArmResourceNode
{
    public string? VnetId { get; set; }
    public string? LbId { get; set; }

    public ContainerAppEnvironmentNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null,
        string? vnetId = null,
        string? lbId = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        VnetId = vnetId;
        LbId = lbId;
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();
        if (VnetId != null)
        {
            props.Add("vnetId", VnetId);
        }
        if (LbId != null)
        {
            props.Add("lbId", LbId);
        }

        return props;
    }
}

