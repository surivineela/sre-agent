// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;
public class CosmosDbNode : ArmResourceNode
{
    [GraphProperty("consistencyPolicy")]
    public string? ConsistencyPolicy { get; set; }

    [GraphProperty("provisioningState")]
    public string? ProvisioningState { get; set; }

    [GraphProperty("minimalTlsVersion")]
    public string? MinimalTlsVersion { get; set; }

    [GraphProperty("writeLocations")]
    public string? WriteLocations { get; set; }

    [GraphProperty("readLocations")]
    public string? ReadLocations { get; set; }

    [GraphProperty("ipRules")]
    public string? IPRules { get; set; }

    [GraphProperty("publicNetworkAccess")]
    public string? PublicNetworkAccess { get; set; }

    [GraphProperty("backupPolicy")]
    public string? BackupPolicy { get; set; }

    [GraphProperty("documentEndpoint")]
    public string? DocumentEndpoint { get; set; }

    [GraphProperty("enableAutomaticFailover")]
    public string? EnableAutomaticFailover { get; set; }

    public CosmosDbNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string? location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) { }
}

