// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class CosmosDbNode : ArmResourceNode
{
    [GraphProperty("consistencyPolicy")]
    public string? ConsistencyPolicy { get; set; }

    public CosmosDbNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) { }
}

