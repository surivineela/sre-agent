// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public sealed class ContainerAppEnvironmentNode : ArmResourceNode
{
    [GraphProperty("internal")]
    public bool? Internal { get; set; }

    [GraphProperty("staticIp")]
    public string? StaticIp { get; set; }

    [GraphProperty("logdestination")]
    public string? LogDestination { get; set; }

    [GraphProperty("zoneRedundant")]
    public bool? ZoneRedundant { get; set; }

    [GraphProperty("customDomain")]
    public string? CustomDomain { get; set; }

    public List<WorkloadProfileName> WorkloadProfiles { get; set; }

    [GraphProperty("infrastructureResourceGroup")]
    public string? InfrastructureResourceGroup { get; set; }

    [GraphProperty("publicNetworkAccess")]
    public string? PublicNetworkAccess { get; set; }

    [GraphProperty("infrastructureSubnetId")]
    public string? InfrastructureSubnetId { get; set; }

    [GraphProperty("lbId")]
    public string? LbId { get; set; }

    public class WorkloadProfileName
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int? MinimumCount { get; set; }
        public int? MaximumCount { get; set; }
    }

    public ContainerAppEnvironmentNode(string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string? location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location)
    {
        WorkloadProfiles = new List<WorkloadProfileName>();
    }
}

