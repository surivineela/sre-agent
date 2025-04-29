// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

// The model follows https://developer.pagerduty.com/api-reference/005299ed43553-get-an-incident
public record PagerDutyIncidentDocument(
    string Id, // Incident ID
    string HtmlUrl,
    string Status, // // Incident status: triggered, acknowledged, resolved
    DateTime CreatedAt
) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name
    public string DocumentType => "PagerDutyIncident";
    public string PartitionKey => Id; // Use incident id as partition key

    // public float[]? TitleVector { get; set; } = null;
    // public float[]? DescriptionVector { get; set; } = null;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }

}

