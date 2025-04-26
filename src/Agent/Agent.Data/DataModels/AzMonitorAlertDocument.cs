// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;
public record AzMonitorAlertDocument(
    string Id,
    string HtmlUrl,
    string Status,
    DateTime CreatedAt) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string DocumentType => "AzMonitorAlert";

    public string PartitionKey => Id;

    public string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; }
}
