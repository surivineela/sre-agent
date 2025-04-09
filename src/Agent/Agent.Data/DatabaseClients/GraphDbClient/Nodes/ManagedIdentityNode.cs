// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public sealed class ManagedIdentityNode : ArmResourceNode
{
    [GraphProperty("identityType")]
    public string IdentityType { set; get; }
    [GraphProperty("tenantId")]
    public string TenantId { get; set; }
    [GraphProperty("principalId")]
    public string PrincipalId { get; set; }
    [GraphProperty("clientId")]
    public string ClientId { get; set; }

    public const string UserAssignedManagedIdentityType = "UserAssigned";
    public const string SystemAssignedManagedIdentityType = "System";

    public ManagedIdentityNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string type,
        string location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        IdentityType = type;
    }
}

