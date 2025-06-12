// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Agent.Graph.Services;

public class PagerDutyService : IPagerDutyService
{
    private readonly string _pagerDutyApiKey = string.Empty;
    private readonly ILogger<PagerDutyService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IncidentManagementSettings? _settings;

    public PagerDutyService(ILogger<PagerDutyService> logger, IHttpClientFactory httpClientFactory, IncidentManagementSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _settings = settings;
        if (_settings != null && _settings.Type == IncidentManagementType.PagerDuty && !string.IsNullOrEmpty(_settings.ConnectionKey))
        {
            _pagerDutyApiKey = _settings.ConnectionKey;
        }

    }

    public async Task<PagerDutyIncidentsResponse> GetIncidentsAsync(uint limit, uint offset)
    {
        _logger.LogInternalInformation("Getting PagerDuty incidents with limit: {limit}, offset: {offset}", limit, offset);
        using var client = CreateHttpClient();
        // The default time range of Listing incidents is a month, per https://developer.pagerduty.com/api-reference/9d0b4b12e36f9-list-incidents
        // Note: include%5B%5D=first_trigger_log_entries is required to get the full log entry, which contains the real incident description.
        // Note: removing the status filters as they prevent indexing incidents that will be used to derive learnings from
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/incidents?limit={limit}&offset={offset}&include%5B%5D=first_trigger_log_entries");
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var incidentsResponse = await response.Content.ReadFromJsonAsync<PagerDutyIncidentsResponse>();
            if (incidentsResponse != null)
            {
                _logger.LogInternalInformation("Successfully retrieved {count} PagerDuty incidents.", incidentsResponse.Incidents.Count);
                return incidentsResponse;
            }
            else
            {
                _logger.LogInternalError("Failed to deserialize PagerDuty incidents response.");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to get PagerDuty incidents: {errorContent}", errorContent);
        }

        throw new HttpRequestException($"Failed to get PagerDuty incidents: {response.StatusCode}");
    }

    public async Task<HttpResponseMessage> GetPagerDutyRequest(string requestPath)
    {
        using var client = CreateHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/{requestPath}");
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInternalInformation("Successfully retrieved PagerDuty request: {requestPath}", requestPath);
            return response;
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to get PagerDuty request {requestPath}. Error: {errorContent}", requestPath, errorContent);
            throw new HttpRequestException($"Failed to get PagerDuty request {requestPath}. Error: {errorContent}");
        }
    }

    // method to fetch full details of an incident by id
    public async Task<PagerDutyIncident> GetPagerDutyIncidentAsync(string incidentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(incidentId, nameof(incidentId));
        _logger.LogInternalInformation("Getting PagerDuty incident with ID: {incidentId}", incidentId);
        using var client = CreateHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/incidents/{incidentId}?include%5B%5D=first_trigger_log_entries");
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var incidentResponse = await response.Content.ReadFromJsonAsync<PagerDutyIncident>();
            if (incidentResponse != null)
            {
                _logger.LogInternalInformation("Successfully retrieved PagerDuty incident ID: {incidentId}", incidentId);
                return incidentResponse;
            }
            else
            {
                _logger.LogInternalError("Failed to deserialize PagerDuty incident response.");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to get PagerDuty incident ID: {incidentId}. Error: {errorContent}", incidentId, errorContent);
        }
        throw new HttpRequestException($"Failed to get PagerDuty incident ID: {incidentId}");
    }


    record LogEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type, // e.g. description_change_log_entry, title_change_log_entry, annotate_log_entry
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("channel")] LogEntryChannel Channel,
        [property: JsonPropertyName("agent")] LogEntryAgent? Agent
    );

    public record LogEntryAgent(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("summary")] string UserName,
        [property: JsonPropertyName("html_url")] string HtmlUrl
    );

    record LogEntryChannel(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("old_description")] string OldDescription, // present when Type is description_change_log_entry
        [property: JsonPropertyName("new_description")] string NewDescription, // present when Type is description_change_log_entry
        [property: JsonPropertyName("old_title")] string OldTitle, // present when Type is title_change_log_entry
        [property: JsonPropertyName("new_title")] string NewTitle, // present when Type is title_change_log_entry
        [property: JsonPropertyName("summary")] string Summary // present when Type is annotate_log_entry, contains the content of a note(like a discussion in icm)
    );

    record LogEntriesResponse(
        [property: JsonPropertyName("log_entries")] List<LogEntry> LogEntries
    );

    public async Task<PagerDutyIncidentLatestDetails?> GetLatestIncidentDetails(string incidentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(incidentId, nameof(incidentId));
        _logger.LogInternalInformation("Getting latest incident description for PagerDuty incident ID: {incidentId}", incidentId);
        using var client = CreateHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/incidents/{incidentId}/log_entries");

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var logEntriesResponse = await response.Content.ReadFromJsonAsync<LogEntriesResponse>();
            if (logEntriesResponse != null && logEntriesResponse.LogEntries.Count > 0)
            {
                // LogEntries are already sorted by CreatedAt in descending order by PagerDuty.
                var (Description, Title, Notes) = logEntriesResponse.LogEntries
                    .Aggregate(
                        (Description: string.Empty, Title: string.Empty, Notes: new List<PagerDutyIncidentNote>()),
                        (acc, logEntry) =>
                        {
                            var createdAt = logEntry.CreatedAt;
                            // For notify_log_entry, there's no agent field
                            var createdBy = logEntry.Agent is null ? null : new PagerDutyAgent(logEntry.Agent.Id, logEntry.Agent.UserName, logEntry.Agent.HtmlUrl);
                            
                            if (acc.Description == "" && logEntry.Type == "description_change_log_entry" && !string.IsNullOrEmpty(logEntry.Channel?.NewDescription))
                            {
                                acc.Description = logEntry.Channel.NewDescription;
                            }
                            
                            if (acc.Title == "" && logEntry.Type == "title_change_log_entry" && !string.IsNullOrEmpty(logEntry.Channel?.NewTitle))
                            {
                                acc.Title = logEntry.Channel.NewTitle;
                            }

                            if (logEntry.Type == "annotate_log_entry" && !string.IsNullOrEmpty(logEntry.Channel?.Summary))
                            {
                                var summary = logEntry.Channel.Summary;
                                acc.Notes.Add(new PagerDutyIncidentNote(logEntry.Id, summary, createdAt, createdBy));
                            }

                            return acc;
                        });

                return new PagerDutyIncidentLatestDetails(Title, Description, Notes);
            }
            else
            {
                _logger.LogInternalError("Failed to deserialize PagerDuty log entries response.");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to get PagerDuty log entries: {errorContent}", errorContent);
        }

        return null;
    }

    private HttpClient CreateHttpClient()
    {
        var client = _httpClientFactory.CreateClient("PagerDutyClient");
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", _pagerDutyApiKey);

        return client;
    }

    record PutIncidentRequest(
        [property: JsonPropertyName("incident")] Incident Incident
    );
    record Incident(
        [property: JsonPropertyName("type")] string Type, // incident_reference
        [property: JsonPropertyName("status")] string Status // allowed values: resolved, acknowledged
    );
    private static PutIncidentRequest CreateResolveIncidentRequest()
    {
        return new PutIncidentRequest(new Incident("incident_reference", "resolved"));
    }

    private static PutIncidentRequest CreateAcknowledgeIncidentRequest()
    {
        return new PutIncidentRequest(new Incident("incident_reference", "acknowledged"));
    }
    public async Task ResolveIncident(string incidentId)
    {
        if (string.IsNullOrEmpty(incidentId))
        {
            throw new ArgumentException("Incident ID cannot be null or empty.", nameof(incidentId));
        }

        if (_settings == null || _settings.Type != IncidentManagementType.PagerDuty)
        {
            throw new InvalidOperationException("PagerDuty incident management is not configured.");
        }

        if (string.IsNullOrEmpty(_pagerDutyApiKey))
        {
            throw new InvalidOperationException("PagerDuty API key is not configured.");
        }

        if (string.IsNullOrEmpty(_settings.OboUser))
        {
            throw new InvalidOperationException("PagerDuty OBO user is not configured. Cannot resolve incident.");
        }

        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add("From", _settings.OboUser);
        var request = new HttpRequestMessage(HttpMethod.Put, $"https://api.pagerduty.com/incidents/{incidentId}");
        request.Content = JsonContent.Create(CreateResolveIncidentRequest());

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInternalInformation("Successfully resolved PagerDuty incident ID: {incidentId}", incidentId);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to resolve PagerDuty incident ID: {incidentId}. Error: {errorContent}", incidentId, errorContent);
            throw new HttpRequestException($"Failed to resolve PagerDuty incident ID: {incidentId}. Error: {errorContent}");
        }
        
    }

    public async Task AcknowledgeIncident(string incidentId)
    {
        if (string.IsNullOrEmpty(incidentId))
        {
            throw new ArgumentException("Incident ID cannot be null or empty.", nameof(incidentId));
        }
        if (_settings == null || _settings.Type != IncidentManagementType.PagerDuty)
        {
            throw new InvalidOperationException("PagerDuty incident management is not configured.");
        }
        if (string.IsNullOrEmpty(_pagerDutyApiKey))
        {
            throw new InvalidOperationException("PagerDuty API key is not configured.");
        }
        if (string.IsNullOrEmpty(_settings.OboUser))
        {
            throw new InvalidOperationException("PagerDuty OBO user is not configured. Cannot acknowledge incident.");
        }
        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add("From", _settings.OboUser);
        var request = new HttpRequestMessage(HttpMethod.Put, $"https://api.pagerduty.com/incidents/{incidentId}");
        request.Content = JsonContent.Create(CreateAcknowledgeIncidentRequest());
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInternalInformation("Successfully acknowledged PagerDuty incident ID: {incidentId}", incidentId);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to acknowledge PagerDuty incident ID: {incidentId}. Error: {errorContent}", incidentId, errorContent);
            throw new HttpRequestException($"Failed to acknowledge PagerDuty incident ID: {incidentId}. Error: {errorContent}");
        }
    }
}

public class NullablePagerDutyService : IPagerDutyService
{
    public Task AcknowledgeIncident(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<PagerDutyIncidentsResponse> GetIncidentsAsync(uint limit, uint offset)
    {
        throw new NotImplementedException();
    }

    public Task<PagerDutyIncidentLatestDetails?> GetLatestIncidentDetails(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<PagerDutyIncident> GetPagerDutyIncidentAsync(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<HttpResponseMessage> GetPagerDutyRequest(string requestPath)
    {
        throw new NotImplementedException();
    }

    public Task ResolveIncident(string incidentId)
    {
        throw new NotImplementedException();
    }
}
