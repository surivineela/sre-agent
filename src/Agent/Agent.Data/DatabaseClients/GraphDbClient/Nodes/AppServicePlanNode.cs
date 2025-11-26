// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.Attributes;

namespace Agent.Data.DatabaseClients.GraphDbClient.Nodes;

public class AppServicePlanNode : ArmResourceNode
{
    [GraphProperty("numberOfWorkers")]
    public int? NumberOfWorkers { get; set; }

    [GraphProperty("status")]
    public string? Status { get; set; }

    [GraphProperty("maximumNumberOfWOrkers")]
    public int? MaximumNumberOfWOrkers { get; set; }

    [GraphProperty("geoRegion")]
    public string? GeoRegion { get; set; }

    [GraphProperty("kind")]
    public string? Kind { get; set; }

    [GraphProperty("provisioningState")]
    public string? ProvisioningState { get; set; }

    [GraphProperty("zoneRedundant")]
    public bool? ZoneRedundant { get; set; }

    public AppServicePlanNode(
        string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string? location = null)
        : base(resourceType, resourceId, subscriptionId, resourceGroupName, resourceName, location) { }
}
