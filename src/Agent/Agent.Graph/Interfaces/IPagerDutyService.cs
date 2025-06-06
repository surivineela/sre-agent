// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Data.DataModels;

namespace Agent.Graph.Interfaces;

public record LogEntryChannel(
    [property: JsonPropertyName("type")] string Type, // e.g. web_trigger
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("subject")] string Subject,
    // This is the real description of the incident.
    [property: JsonPropertyName("details")] string Details
);
public record FirstTriggerLogEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("type")] string Type, // e.g. trigger_log_entry
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("channel")] LogEntryChannel? Channel
);

public record Priority(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("summary")] string Summary // e.g. P1, P2, P3
);

public record PDIncidentType(
    [property: JsonPropertyName("name")] string Name
);

public record ImpactedService(
    [property: JsonPropertyName("id")] string Id, // e.g. PHULJQ6
    [property: JsonPropertyName("summary")] string Summary, // e.g. Default Service
    [property: JsonPropertyName("type")] string Type // e.g. service_reference
);

public record PagerDutyIncident(
    [property: JsonPropertyName("id") ] string IncidentId, // PagerDuty incident ID
    // For whatever reason, the description is always the same as the title.
    // The real title is in first_trigger_log_entry and you need to pass "include[]=first_trigger_log_entries" in the query string 
    // to get the full log entry. If you don't pass it, the first_trigger_log_entry is NOT null but doesn't contain the real title.
    // Good job PagerDuty for creating such a confusing API.
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("status")] string Status,
    // The first trigger log entry of the incident. It is used to get the incident description when the incident is created.
    // The description of the incident can only be retrieved from the first trigger log entry.
    // include[]=first_trigger_log_entries parameter is required to get the full log entry.
    [property: JsonPropertyName("first_trigger_log_entry")] FirstTriggerLogEntry FirstTriggerLogEntry,
    [property: JsonPropertyName("priority")] Priority? Priority,
    [property: JsonPropertyName("urgency")] string? Urgency,
    [property: JsonPropertyName("incident_type")] PDIncidentType? IncidentType, // e.g. incident, problem, maintenance
    [property: JsonPropertyName("service")] ImpactedService? ImpactedService // e.g. "Microsoft Teams", "Azure Storage"
);

public record PagerDutyIncidentsResponse(
    [property: JsonPropertyName("incidents")] List<PagerDutyIncident> Incidents
);

public record PagerDutyIncidentLatestDetails(
    string LatestTitle, // It could be empty if the incident title is not updated.
    string LatestDescription, // It could be empty if the incident description is not updated.
    List<PagerDutyIncidentNote> Notes
);

public interface IPagerDutyService
{
    /// <summary>
    /// List all incidents. limit and offset are used for pagination.
    /// </summary>
    /// <returns></returns>
    Task<PagerDutyIncidentsResponse> GetIncidentsAsync(uint limit, uint offset);

    Task<PagerDutyIncident> GetPagerDutyIncidentAsync(string incidentId);

    /// <summary>
    /// Get the latest incident description from PagerDuty.
    /// Note the default get incident API returns description of the incident when it was created, not the latest description.
    /// </summary>
    Task<PagerDutyIncidentLatestDetails?> GetLatestIncidentDetails(string incidentId);

    /// <summary>
    /// Resolve an incident in PagerDuty.
    /// Resolving an already resolved incident will throw an error.
    /// </summary>
    /// <param name="incidentId"></param>
    /// <returns></returns>
    Task ResolveIncident(string incidentId);
}
