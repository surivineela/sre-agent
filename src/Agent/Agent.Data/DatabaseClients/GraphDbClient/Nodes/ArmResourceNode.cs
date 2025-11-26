// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Data.DatabaseClients.Attributes;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;

namespace Agent.Data.DatabaseClients.GraphDbClient;

public class ArmResourceNode : GraphNode
{
    public string ResourceType { get; set; } = string.Empty;

    public string ResourceKind { get; set; } = string.Empty;

    [GraphProperty("resourceId")]
    public string ResourceId { get; set; } = string.Empty;

    [GraphProperty("subscriptionId")]
    public string SubscriptionId { get; set; } = string.Empty;

    [GraphProperty("resourceGroupName")]
    public string? ResourceGroupName { get; set; }

    [GraphProperty("resourceName")]
    public string? ResourceName { get; set; }

    [GraphProperty("location")]
    public string? Location { get; set; }

    [GraphJsonProperty("appHealthInfo")]
    public AppHealthInfo? AppHealthInfo { get; set; }

    [GraphJsonProperty("remarks")]
    public string? Remarks { get; set; }

    public ArmResourceNode() { }

    public ArmResourceNode(IDictionary<string, object> properties)
        : base(properties) { }

    public ArmResourceNode(string resourceType, string subscriptionId)
        : this(resourceType, string.Empty, subscriptionId, string.Empty, string.Empty) { }

    public ArmResourceNode(string resourceType, string subscriptionId, string resourceGroupName, string location)
        : this(resourceType, string.Empty, subscriptionId, resourceGroupName, string.Empty, location) { }

    public ArmResourceNode(
        string resourceType,
        string resourceId,
        string subscriptionId,
        string resourceGroupName,
        string resourceName,
        string? resourceKind = null,
        string? remarks = null,
        string? location = null,
        AppHealthInfo? appHealthInfo = null)
    {
        UpdateTs = DateTime.UtcNow.Ticks;
        ResourceKind = ResourceKindHelper.getResourceKind(resourceType, resourceKind);
        ResourceType = resourceType.ToLowerInvariant();
        ResourceId = resourceId.ToLowerInvariant();
        SubscriptionId = subscriptionId.ToLowerInvariant();
        ResourceGroupName = resourceGroupName.ToLowerInvariant();
        ResourceName = resourceName.ToLowerInvariant();
        Location = location?.NormalizeLocation() ?? string.Empty;
        AppHealthInfo = appHealthInfo ?? new AppHealthInfo();
        Remarks = remarks;
    }

    public override string GetNodeLabel()
    {
        // use full arm type to avoid potential conflict
        return ResourceType;
    }

    public override string GetNodeId()
    {
        return ResourceId;
    }

    public override string GetResourceType()
    {
        return ResourceType;
    }

    public override string GetResourceKind()
    {
        return ResourceKind;
    }

    public override void SetResourceKind(string NewResourceKind)
    {
        ResourceKind = NewResourceKind;
    }

    // Mainly for system MI
    // To be able to crawl same resource again with ManagedIdentityNode
    public override string GetHashString()
    {
        return $"{ResourceId}|{GetType()}";
    }

    public override string GetSubscriptionId()
    {
        return SubscriptionId;
    }
}

public static partial class LocationExtensions
{
    public static string NormalizeLocation(this string location)
    {
        if (string.IsNullOrEmpty(location))
        {
            return string.Empty;
        }

        return LocationNormalizationRegex().Replace(location, string.Empty).ToLowerInvariant();
    }

    [GeneratedRegex("[^a-zA-Z\\d]")]
    private static partial Regex LocationNormalizationRegex();
}
