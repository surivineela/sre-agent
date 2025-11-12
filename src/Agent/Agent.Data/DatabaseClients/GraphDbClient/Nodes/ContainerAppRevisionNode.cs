// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient;

public class ContainerAppRevisionNode : ArmResourceNode
{
    [GraphProperty("name")]
    public string? Name { get; set; }

    [GraphProperty("appName")]
    public string? AppName { get; set; }

    [GraphProperty("trafficWeight")]
    public int? TrafficWeight { get; set; }

    [GraphProperty("createdOn")]
    public string? CreatedOn { get; set; }

    [GraphProperty("lastActiveOn")]
    public string? LastActiveOn { get; set; }

    [GraphProperty("fqdn")]
    public string? Fqdn { get; set; }

    [GraphJsonProperty("template")]
    public string? Template { get; set; }

    [GraphProperty("isActive")]
    public bool? IsActive { get; set; }

    [GraphProperty("replicas")]
    public int? Replicas { get; set; }

    [GraphProperty("labels")]
    public string? Labels { get; set; }

    [GraphProperty("provisioningError")]
    public string? ProvisioningError { get; set; }

    [GraphProperty("healthState")]
    public string? HealthState { get; set; }

    [GraphProperty("provisioningState")]
    public string? ProvisioningState { get; set; }

    [GraphProperty("runningState")]
    public string? RunningState { get; set; }

    public ContainerAppRevisionNode() { }

    public ContainerAppRevisionNode(IDictionary<string, object> properties)
        : base(properties) { }
}
