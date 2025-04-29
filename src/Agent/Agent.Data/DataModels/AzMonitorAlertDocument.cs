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
}
