// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Threading;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Plugins.Kusto;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SREAgent.Incidents.IcM.Interface;
using Newtonsoft.Json;
using IICMAPIClient = Agent.Core.Services.IICMAPIClient;

namespace Agent.Runtime.Services
{
    public interface ISmartFilterService
    {
        Task<List<FilterRecommendation>> GetFilterRecommendations(string owningTeamId, string? incidentType = null);
    }

    public class SmartFilterService : ISmartFilterService
    {
        private readonly IICMAPIClient _icmApiClient;
        private readonly IChatClientProvider _chatClientProvider;
        private readonly ILogger<SmartFilterService> _logger;


        public SmartFilterService(
            IICMAPIClient icmApiClient,
            IChatClientProvider chatClientProvider,
            ILogger<SmartFilterService> logger)
        {
            _icmApiClient = icmApiClient;
            _chatClientProvider = chatClientProvider;
            _logger = logger;
        }

        public async Task<List<FilterRecommendation>> GetFilterRecommendations(string owningTeamId, string? incidentType = null)
        {
            _logger.LogInternalInformation("GetFilterRecommendations: Invoked for OwningTeamId: {OwningTeamId}, IncidentType: {IncidentType}", owningTeamId, incidentType ?? "All");

            try
            {
                var incidents = await _icmApiClient.GetIncidentsAsync(
                    100,
                    0,
                    DateTime.UtcNow.AddDays(-90),
                    null,
                    null,
                    owningTeamId,
                    incidentType,
                    null,
                    null,
                    null,
                    null
                );

                // only take incidents that haven't already been processed by SREAgent
                var unprocessedIncidents = incidents.Where(incident => !incident.Tags.Any(tag => tag.StartsWith("SREAgent"))).ToList();

                if (unprocessedIncidents == null || unprocessedIncidents.Count == 0)
                {
                    _logger.LogInternalWarning("GetHandlerRecommendations: No incidents found for OwningTeamId within the past 90 days: {OwningTeamId}", owningTeamId);
                    return new List<FilterRecommendation>();
                }

                _logger.LogInternalInformation("GetHandlerRecommendations: Retrieved {Count} incidents from ICM API", unprocessedIncidents.Count);

                var incidentSnapshots = unprocessedIncidents.Select(i => new IncidentSnapshot()
                {
                    IncidentId = i.Id.ToString(),
                    Title = i.Title,
                    OwningTeamId = i.OwningTeamId.ToString(),
                    OwningTeamName = i.OwningTeamName,
                    IncidentType = i.Type,
                    MonitorId = i.MonitorId,
                    Severity = i.Severity.ToString()
                }).ToList();

                // Generate handler recommendations using OpenAI
                var recommendations = await GenerateFilterRecommendations(incidentSnapshots, owningTeamId);

                _logger.LogInternalInformation("GetFilterRecommendations: Generated {Count} filter recommendations", recommendations.Count);

                return recommendations.OrderByDescending(r => r.Count).ToList().GetRange(0, Math.Min(5, recommendations.Count()));
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetFilterRecommendations: Error generating filter recommendations for OwningTeamId {OwningTeamId}", owningTeamId);
                throw;
            }
        }

        private async Task<List<FilterRecommendation>> GenerateFilterRecommendations(List<IncidentSnapshot> incidents, string owningTeamId)
        {
            _logger.LogInternalInformation("GenerateFilterRecommendations: Generating recommendations for {Count} incidents", incidents.Count);

            var systemPrompt = GetSystemPrompt(incidents, owningTeamId);

            try
            {
                var response = await _chatClientProvider.SmallFastModel.GetResponseAsync(
                    new ChatMessage(ChatRole.System, systemPrompt),
                    new ChatOptions
                    {
                        Temperature = 0.1f,
                        ResponseFormat = ChatResponseFormat.Json
                    }
                );

                var responseText = response.Messages.LastOrDefault()?.Text ?? string.Empty;

                if (string.IsNullOrEmpty(responseText))
                {
                    _logger.LogInternalError("GenerateFilterRecommendations: Received empty response from chat client");
                    return new List<FilterRecommendation>();
                }

                _logger.LogInternalInformation("GenerateFilterRecommendations: Received response from chat client");

                // Deserialize the response into filter recommendationsp
                var result = JsonConvert.DeserializeObject<FilterRecommendationsResponse>(responseText);

                if (result?.Filters == null || result.Filters.Count == 0)
                {
                    _logger.LogInternalWarning("GenerateFilterRecommendations: No filters were recommended by the model");
                    return new List<FilterRecommendation>();
                }

                return result.Filters;
            }
            catch (JsonException jsonEx)
            {
                _logger.LogInternalError(jsonEx, "GenerateFilterRecommendations: Failed to deserialize filter recommendations");
                throw new InvalidOperationException("Failed to deserialize filter recommendations.", jsonEx);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GenerateFilterRecommendations: Error generating handler recommendations");
                throw;
            }
        }

        private string GetSystemPrompt(List<IncidentSnapshot> incidents, string owningTeamId)
        {
            var incidentSummary = JsonConvert.SerializeObject(incidents, Formatting.Indented);

            // TODO: Replace with actual system prompt
            return $@"
                You are an AI assistant specialized in analyzing incident patterns and recommending incident filters.

                Incident Data:
                {incidentSummary}

                Your task is to analyze these incidents and recommend incident filters that would effectively manage similar incidents in the future.

                For each recommended filter, provide the following information:
                - FilterName: A descriptive name for the filter
                - TitleContains: (optional) A string pattern that should appear in the incident title to match this filter.
                  TitleContains cannot be a Regex expression. Set to null if not applicable. TitleContains cannot be just one word.
                - Severity: (optional) The severity level this filter should target. Set to null if not applicable.
                - IncidentType: The type of incidents this filter should manage
                - Count: The number of incidents that fall under this filter

                Return your response as a JSON object with the following structure:
                {{
                    ""Filters"": [
                        {{
                            ""FilterName"": ""example-filter"",
                            ""TitleContains"": ""error pattern"",
                            ""Severity"": ""3"",
                            ""IncidentType"": ""Alert"",
                            ""Count"": ""Count""
                        }}
                    ]
                }}
                
                Focus on identifying common patterns and groupings that would benefit from automated handling.
                The filter TitleContains has a higher priority over the Severity filter. 
                If you capture a large majority of incidents through TitleContains, then see if you can further filter the
                remaining incidents into decent-sized groups by Severity. If the number of incidents filtered by TitleContains is already
                low, such as less than 10, the set the optional parameter (e.g. Severity) to null.
                Never filter only by Severity and IncidentType. There should always be a value for TitleContains.
                The TitleContains value should be at least 3 words. It should be reasonably at least a phrase.
                For example, instead of 'DataPlane Resource', use 'DataPlane Resource Provision Failure'. If you can extend the TitleContains substring while maintaining
                the approximate number of incidents that fall under that filter, do so. 
                FilterName should be concise yet descriptive of the incidents it handles. Do not include spaces in FilterName; use hyphens or underscores instead.  
                ";
        }

        // ensure correct incident count of suggested filters
        private static List<FilterRecommendation> MatchHandlers(List<IncidentSnapshot> incidents, List<FilterRecommendation> handlers)
        {
            foreach (FilterRecommendation handler in handlers)
            {
                string? titleContains = handler.TitleContains;
                string? severity = handler.Severity;
                string incidentType = handler.IncidentType;

                IEnumerable<IncidentSnapshot> filteredIncidents = incidents.Where((incident) =>
                {
                    if (!string.IsNullOrWhiteSpace(titleContains) && !incident.Title.Contains(titleContains))
                    {
                        return false;
                    }
                    if (!string.IsNullOrWhiteSpace(severity) && incident.Severity != severity)
                    {
                        return false;
                    }
                    if (incident.IncidentType != incidentType)
                    {
                        return false;
                    }
                    return true;
                });
                handler.Count = filteredIncidents.Count();
            }

            // Sort recommendations by Count in descending order
            return handlers.OrderByDescending(h => h.Count).ToList();
        }
    }

    // Model classes for Kusto results
    public class IncidentSnapshot
    {
        public string IncidentId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string OwningTeamId { get; set; } = string.Empty;
        public string OwningTeamName { get; set; } = string.Empty;
        public string IncidentType { get; set; } = string.Empty;
        public string MonitorId { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
    }

    // Model class for handler recommendations
    public class FilterRecommendation
    {
        public string FilterName { get; set; } = string.Empty;
        public string? TitleContains { get; set; }
        public string? Severity { get; set; }
        public string IncidentType { get; set; } = string.Empty;
        public int Count { get; set; } = 0;
    }

    // Response wrapper for deserialization
    public class FilterRecommendationsResponse
    {
        public List<FilterRecommendation> Filters { get; set; } = new List<FilterRecommendation>();
    }
}
