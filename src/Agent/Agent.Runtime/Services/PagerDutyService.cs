// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Agent.Runtime.Services;

public class PagerDutyService(ILogger<PagerDutyService> logger, IHttpClientFactory httpClientFactory) : IPagerDutyService
{

    private readonly static string PagerDutyApiKey = Environment.GetEnvironmentVariable("PAGERDUTY_API_KEY") ?? "";
    public async Task<PagerDutyIncidentsResponse> GetIncidentsAsync(uint limit, uint offset)
    {
        logger.LogInformation("Getting PagerDuty incidents with limit: {limit}, offset: {offset}", limit, offset);
        using var client = CreateHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/incidents?limit={limit}&offset={offset}");
        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var incidentsResponse = await response.Content.ReadFromJsonAsync<PagerDutyIncidentsResponse>();
            if (incidentsResponse != null)
            {
                logger.LogInformation("Successfully retrieved {count} PagerDuty incidents.", incidentsResponse.Incidents.Count);
                return incidentsResponse;
            }
            else
            {
                logger.LogError("Failed to deserialize PagerDuty incidents response.");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Failed to get PagerDuty incidents: {errorContent}", errorContent);
        }

        throw new HttpRequestException($"Failed to get PagerDuty incidents: {response.StatusCode}");
    }


    record LogEntry(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("created_at")] DateTime CreatedAt,
        [property: JsonPropertyName("channel")] LogEntryChannel Channel
    );

    record LogEntryChannel(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("old_description")] string OldDescription,
        [property: JsonPropertyName("new_description")] string NewDescription
    );

    record LogEntriesResponse(
        [property: JsonPropertyName("log_entries")] List<LogEntry> LogEntries
    );

    public async Task<string?> GetLatestIncidentDescription(string incidentId)
    {
        ArgumentException.ThrowIfNullOrEmpty(incidentId, nameof(incidentId));
        logger.LogInformation("Getting latest incident description for PagerDuty incident ID: {incidentId}", incidentId);
        using var client = CreateHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.pagerduty.com/incidents/{incidentId}/log_entries");

        var response = await client.SendAsync(request);
        if (response.IsSuccessStatusCode)
        {
            var logEntriesResponse = await response.Content.ReadFromJsonAsync<LogEntriesResponse>();
            if (logEntriesResponse != null && logEntriesResponse.LogEntries.Count > 0)
            {
                var latestLogEntry = logEntriesResponse.LogEntries.Where(x => x.Type == "description_change_log_entry").OrderByDescending(x => x.CreatedAt).FirstOrDefault();
                if (latestLogEntry?.Channel.NewDescription != null)
                {
                    logger.LogInformation("Successfully retrieved latest incident description: {description}", latestLogEntry.Channel.NewDescription);
                    return latestLogEntry.Channel.NewDescription;
                }
                else
                {
                    logger.LogInformation("No description change log entry found for incident ID: {incidentId}", incidentId);
                }
            }
            else
            {
                logger.LogError("Failed to deserialize PagerDuty log entries response.");
            }
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            logger.LogError("Failed to get PagerDuty log entries: {errorContent}", errorContent);
        }

        return null;
    }

    private HttpClient CreateHttpClient()
    {
        var client = httpClientFactory.CreateClient("PagerDutyClient");
        client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Token", PagerDutyApiKey);

        return client;
    }
}
