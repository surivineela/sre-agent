// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Graph.Interfaces;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Graph.Services;

public class PagerDutyService : IPagerDutyService
{
    private readonly string _pagerDutyApiKey = string.Empty;
    private readonly ILogger<PagerDutyService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IncidentManagementSettings? _settings;
    private IncidentManagementSettings _current;
    private readonly Container _container;


    public PagerDutyService(ILogger<PagerDutyService> logger, IHttpClientFactory httpClientFactory, IOptionsMonitor<IncidentManagementSettings> monitor, CosmosClient cosmosClient, CosmosDBSettings cosmosDbSettings)
    {
        _current = monitor.CurrentValue;
        monitor.OnChange(newConfig =>
        {
            _current = newConfig;
            // Optionally log or re-initialize internal caches
        });
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        _settings = _current;
        if (_settings != null && _settings.Type == IncidentManagementType.PagerDuty && !string.IsNullOrEmpty(_settings.ConnectionKey))
        {
            _pagerDutyApiKey = _settings.ConnectionKey;
        }

        _container = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
    }

    public async Task<IEnumerable<PagerDutyIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? since, string? impactServiceId, string? priority, string? titleContains, string? urgency, IEnumerable<string>? statuses)
    {
        _logger.LogInternalInformation("Getting PagerDuty incidents with limit: {limit}, offset: {offset}", limit, offset);

        var incidents = new List<PagerDutyIncident>();

        if (limit == 0 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        if (since.HasValue && since.Value < DateTime.UtcNow.AddDays(-180))
        {
            throw new ArgumentOutOfRangeException(nameof(since), "Since must be within the last 180 days.");
        }

        // The default time range of Listing incidents is a month, per https://developer.pagerduty.com/api-reference/9d0b4b12e36f9-list-incidents
        // Note: include%5B%5D=first_trigger_log_entries is required to get the full log entry, which contains the real incident description.
        // Note: removing the status filters as they prevent indexing incidents that will be used to derive learnings from
        var defaultStartTime = DateTime.UtcNow.AddDays(-90);

        var queryParams = new List<KeyValuePair<string, string?>> {
            new KeyValuePair<string, string?>("limit", limit.ToString()),
            new KeyValuePair<string, string?>("offset", offset.ToString()),
            new KeyValuePair<string, string?>("include[]", "first_trigger_log_entries"),
            new KeyValuePair<string, string?>("since", since.GetValueOrDefault(defaultStartTime).ToString("o")),
            new KeyValuePair<string, string?>("until", DateTime.UtcNow.AddMinutes(30).ToString("o")),
            new KeyValuePair<string, string?>("sort_by","created_at:desc")
        };

        //If impactServiceId is provided(could be name or id), we need to translate it to service ID via /services API.
        if (!string.IsNullOrEmpty(impactServiceId))
        {
            var servicesResponse = await GetPagerDutyRequest("services");
            var services = await servicesResponse.Content.ReadFromJsonAsync<PDServicesResponse>();
            var service = services?.Services.FirstOrDefault(s => impactServiceId.Equals(s.Name, StringComparison.OrdinalIgnoreCase) || impactServiceId.Equals(s.Id, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(service?.Id))
            {
                var availableServices = string.Join(",", services?.Services.Select(s => s.Name) ?? []);
                _logger.LogInternalWarning($"Cannot find {impactServiceId} in {availableServices}");
            }
            else
            {
                queryParams.Add(new KeyValuePair<string, string?>("service_ids[]", service.Id));
            }
        }

        if (!string.IsNullOrEmpty(urgency))
        {
            queryParams.Add(new KeyValuePair<string, string?>("urgencies[]", urgency));
        }

        if (statuses is not null && statuses.Any())
        {
            queryParams.AddRange(statuses.Select(status => new KeyValuePair<string, string?>("status[]", status)));
        }

        var allIncidents = await GetIncidentsAsyncInternal(queryParams);

        //For some properties that cannot be filtered via API query parameters, we filter in-memory.
        return allIncidents.Where(incident =>
        {
            bool isMatch = true;
            if (!string.IsNullOrEmpty(priority))
            {
                isMatch = isMatch && priority.Equals(incident.Priority?.Summary, StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrEmpty(titleContains))
            {
                isMatch = isMatch && (incident.Title?.Contains(titleContains, StringComparison.OrdinalIgnoreCase) == true);
            }
            return isMatch;
        });
    }

    private async Task<IEnumerable<PagerDutyIncident>> GetIncidentsAsyncInternal(IEnumerable<KeyValuePair<string, string?>> queryParams)
    {
        string apiPath = QueryHelpers.AddQueryString("https://api.pagerduty.com/incidents", queryParams);

        var request = new HttpRequestMessage(HttpMethod.Get, apiPath);
        _logger.LogInternalInformation("PagerDuty incidents request URL: {url}", apiPath);

        using var client = CreateHttpClient();
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            try
            {
                var incidentsResponse = await response.Content.ReadFromJsonAsync<PagerDutyIncidentsResponse>();
                if (incidentsResponse is not null)
                {
                    _logger.LogInternalInformation("Successfully retrieved {count} PagerDuty incidents.", incidentsResponse.Incidents.Count);
                    return incidentsResponse.Incidents;
                }
                else
                {
                    string responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInternalError($"Failed to deserialize PagerDuty incidents response. Response : {responseContent}");
                    return Enumerable.Empty<PagerDutyIncident>();
                }
            }
            catch (Exception ex)
            {
                string responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Failed to deserialize PagerDuty incidents response. Message : {ex.Message}. Response : {responseContent}");
                throw;
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to get PagerDuty incidents: {errorContent}, url: {url}", errorContent, apiPath);
            throw new HttpRequestException($"Failed to get PagerDuty incidents: {response.StatusCode}");
        }
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
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/incidents/{incidentId}?include%5B%5D=body");
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var incidentResponse = await response.Content.ReadFromJsonAsync<PagerDutyIncidentApiResult>();
            if (incidentResponse != null)
            {
                _logger.LogInternalInformation("Successfully retrieved PagerDuty incident ID: {incidentId}", incidentId);
                return incidentResponse.Incident;
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

    private void RunBasicValidations(string incidentId)
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
            throw new InvalidOperationException("PagerDuty OBO user is not configured.");
        }
    }

    record PutIncidentRequest(
        [property: JsonPropertyName("incident")] Incident Incident
    );
    record Incident(
        [property: JsonPropertyName("type")] string Type, // incident_reference
        [property: JsonPropertyName("status")] string Status // allowed values: resolved, acknowledged
    );

    record IncidentNote(
        [property: JsonPropertyName("content")] string Content
    );

    record PostIncidentNoteRequest(
        [property: JsonPropertyName("note")] IncidentNote Note
    );

    private static PutIncidentRequest CreateResolveIncidentRequest()
    {
        return new PutIncidentRequest(new Incident("incident_reference", "resolved"));
    }

    private static PutIncidentRequest CreateAcknowledgeIncidentRequest()
    {
        return new PutIncidentRequest(new Incident("incident_reference", "acknowledged"));
    }

    public async Task AddNoteToIncident(string incidentId, string note)
    {
        RunBasicValidations(incidentId);
        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add("From", _settings?.OboUser);
        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api.pagerduty.com/incidents/{incidentId}/notes");
        request.Content = JsonContent.Create(new PostIncidentNoteRequest(new IncidentNote($"Comment added by SREAgent: {note}")));

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInternalInformation("Successfully posted note to PagerDuty incident ID: {incidentId}", incidentId);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to post note to PagerDuty incident ID: {incidentId}. Error: {errorContent}", incidentId, errorContent);
            throw new HttpRequestException($"Failed to post note to PagerDuty incident ID: {incidentId}. Error: {errorContent}");
        }

    }

    public async Task ResolveIncident(string incidentId)
    {
        RunBasicValidations(incidentId);

        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add("From", _settings?.OboUser);
        var request = new HttpRequestMessage(HttpMethod.Put, $"https://api.pagerduty.com/incidents/{incidentId}");
        request.Content = JsonContent.Create(CreateResolveIncidentRequest());

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            _logger.LogInternalInformation("Successfully resolved PagerDuty incident ID: {incidentId}", incidentId);
            var incidentDocument = await GetDocumentAsync<PagerDutyIncidentDocument>(incidentId, incidentId);
            if (incidentDocument != null)
            {
                // do not update UpdatedAt value so that incident gets captured in next scanner iteration
                incidentDocument.Status = "resolved";
                incidentDocument.ResolvedAt = DateTime.UtcNow;
                incidentDocument.Tags.Add("SREAgent_Resolved");
                await _container.UpsertItemAsync(incidentDocument, new PartitionKey(incidentDocument.PartitionKey));
                _logger.LogInternalInformation("Successfully updated PagerDuty incident document ID: {incidentId} in Cosmos DB", incidentId);
            }
            else
            {
                var incident = await GetPagerDutyIncidentAsync(incidentId);
                var updatedDoc = new PagerDutyIncidentDocument(
                    incident.IncidentId,
                    incident.HtmlUrl,
                    "resolved",
                    incident.Priority?.ToString() ?? string.Empty,
                    incident.Urgency ?? string.Empty,
                    incident.IncidentType?.ToString() ?? string.Empty,
                    incident.ImpactedService?.Id ?? string.Empty,
                    incident.ImpactedService?.Summary ?? string.Empty,
                    DateTime.UtcNow);

                updatedDoc.Tags.Add("SREAgent_Resolved");
                updatedDoc.ResolvedAt = DateTime.UtcNow;

                _ = await _container.CreateItemAsync<PagerDutyIncidentDocument>(updatedDoc, new PartitionKey(updatedDoc.PartitionKey));
                _logger.LogInternalWarning("PagerDuty incident document ID: {incidentId} not found in Cosmos DB", incidentId);
            }
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
        RunBasicValidations(incidentId);

        using var client = CreateHttpClient();
        client.DefaultRequestHeaders.Add("From", _settings?.OboUser);
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

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _container.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }
}

public class NullablePagerDutyService : IPagerDutyService
{
    public Task AcknowledgeIncident(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<PagerDutyIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? since, string? impactServiceId, string? priority, string? titleContains, string? urgency, IEnumerable<string>? statuses)
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

    public Task AddNoteToIncident(string incidentId, string note)
    {
        throw new NotImplementedException();
    }
}
