// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DatabaseClients.GraphDbClient;
public sealed class ManagedIdentityNode : ArmResourceNode
{
    public string IdentityType { set; get; }
    public string TenantId { get; set; }
    public string PrincipalId { get; set; }
    public string ClientId { get; set; }

    public const string UserAssignedManagedIdentityType = "UserAssigned";
    public const string SystemAssignedManagedIdentityType = "System";

    public ManagedIdentityNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string type,
        string location = null,
        string tenantId = null,
        string principalId = null,
        string clientId = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        IdentityType = type;
        TenantId = tenantId;
        PrincipalId = principalId;
        ClientId = clientId;
    }

    public override IDictionary<string, object> GetNodeProperties()
    {
        var props = base.GetNodeProperties();
        props.Add("identityType", IdentityType);
        if (TenantId != null)
        {
            props.Add("tenantId", TenantId);
        }
        if (PrincipalId != null)
        {
            props.Add("principalId", PrincipalId);
        }
        if (ClientId != null)
        {
            props.Add("clientId", ClientId);
        }
        return props;
    }
}

