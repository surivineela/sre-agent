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
    // public float[]? TitleVector { get; set; } = null;
    // public float[]? DescriptionVector { get; set; } = null;
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string DocumentType { get; } = "PagerDutyIncident";
    public string Id { get; } = Id; // Use the incident id as the document id
    public string PartitionKey => Id; // Use incident id as partition key
    public DateTime CreatedAt { get; } = CreatedAt;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string ImpactedServiceId { get; set; } = ImpactedServiceId;
    public string ImpactedServiceName { get; set; } = ImpactedServiceName;
    public string Status { get; set; } = Status;
    public string IncidentType { get; set; } = IncidentType;
    public string Priority { get; set; } = Priority;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExtractedKnowledge { get; set; } = string.Empty;
    public List<PagerDutyIncidentNote> Notes { get; set; } = []; // Notes of the incident sorted by CreatedAt in decending order.
    public DateTime? ResolvedAt { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public string GeneralSummary { get; set; } = string.Empty;

    public List<string> Tags = new List<string>();
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
