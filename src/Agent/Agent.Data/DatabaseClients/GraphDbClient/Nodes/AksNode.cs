// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class AksNode : ArmResourceNode
{
    public AksNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string? location = null) : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
    }
}

