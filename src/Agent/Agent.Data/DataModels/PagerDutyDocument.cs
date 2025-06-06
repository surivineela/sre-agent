// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

// The model follows https://developer.pagerduty.com/api-reference/005299ed43553-get-an-incident
public record PagerDutyIncidentDocument(
    string Id, // Incident ID
    string HtmlUrl,
    string Status, // // Incident status: triggered, acknowledged, resolved
    string Priority, // e.g. P1, P2, P3 or not set
    string Urgency, // e.g. high, low
    string IncidentType, // e.g. incident, problem, maintenance
    string ImpactedServiceId, // e.g. "Microsoft Teams", "Azure Storage"
    string ImpactedServiceName, // e.g. "Microsoft Teams", "Azure Storage"
    DateTime CreatedAt
) : IIncidentDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name
    public string DocumentType => "PagerDutyIncident";
    public string Id { get; } = Id; // Use incident id as document id
    public string PartitionKey => Id; // Use incident id as partition key

    // public float[]? TitleVector { get; set; } = null;
    // public float[]? DescriptionVector { get; set; } = null;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IncidentType { get; set; } = IncidentType; // e.g. incident, problem, maintenance
    public string Status { get; set; } = Status; // e.g. triggered, acknowledged, resolved
    public string Priority { get; set; } = Priority; // e.g. P1, P2, P3 or not set
    public string ImpactedServiceId { get; set; } = ImpactedServiceId; // e.g. "Microsoft Teams", "Azure Storage"
    public string ImpactedServiceName { get; set; } = ImpactedServiceName; // e.g. "Microsoft Teams", "Azure Storage"
    public DateTime UpdatedAt { get; set; }
    public string ExtractedKnowledge { get; set; } = string.Empty;
    public List<PagerDutyIncidentNote> Notes { get; set; } = []; // Notes of the incident sorted by CreatedAt in decending order.
}

public record PagerDutyAgent(
    string Id, // e.g. PYPV3MY
    string Name, // Agent name. e.g. Yefu Wang
    string HtmlUrl // e.g. https://yefutest.pagerduty.com/users/PYPV3MY
);
// Notes are like discussions in PagerDuty. 
public record PagerDutyIncidentNote(
    string Id, // ID of the note
    string Content,
    DateTime CreatedAt,
    PagerDutyAgent? CreatedBy // for notify_log_entry, there's no created_by field.
);
