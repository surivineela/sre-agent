// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public record AzMonitorAlertDocument(
    string Id,
    string Name,
    string Severity,
    string TargetResourceType,
    string TargetResourceId,
    string SubscriptionId,
    string Status, // Alert status: New, Acknowledged, Closed
    DateTimeOffset CreatedAt) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string DocumentType => "AzMonitorAlert";

    public string PartitionKey => Id;

    public string Title => Name;

    public string Description { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }

    public int HitCount { get; set; }

    public bool UserInputRequested { get; set; } = false;

    public bool TargetResourceInputRequested { get; set; } = false;

    // Adding an optional alert rule resource ID for deduplication purposes.
    public string? AlertRuleResourceId { get; set; }
}
