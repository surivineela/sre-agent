// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Runtime.Services;

public record PagerDutyIncident(
    [property: JsonPropertyName("id") ] string IncidentId, // PagerDuty incident ID
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("html_url")] string HtmlUrl,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("created_at")] DateTime CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTime UpdatedAt,
    [property: JsonPropertyName("status")] string Status
);

public record PagerDutyIncidentsResponse(
    [property: JsonPropertyName("incidents")] List<PagerDutyIncident> Incidents
);

public interface IPagerDutyService
{
    /// <summary>
    /// List all incidents. limit and offset are used for pagination.
    /// </summary>
    /// <returns></returns>
    Task<PagerDutyIncidentsResponse> GetIncidentsAsync(uint limit, uint offset);

    /// <summary>
    /// Get the latest incident description from PagerDuty.
    /// Note the default get incident API returns description of the incident when it was created, not the latest description.
    /// </summary>
    Task<string?> GetLatestIncidentDescription(string incidentId);
}
