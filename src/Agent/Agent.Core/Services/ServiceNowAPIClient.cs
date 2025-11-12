using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using static System.Net.WebRequestMethods;

namespace Agent.Core.Services
{
    public class ServiceNowAPIClient : IServiceNowAPIClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ServiceNowAPIClient> _logger;
        private readonly ServiceNowAPISettings? _settings;
        private readonly string? _apiBaseUrl;
        private readonly bool isEnabled;

        public ServiceNowAPIClient(
            HttpClient httpClient,
            ILogger<ServiceNowAPIClient> logger,
            IncidentManagementSettings settings)
        {
            _httpClient = httpClient;
            _logger = logger;
            if (settings.Type != IncidentManagementType.ServiceNow)
            {
                isEnabled = false;
                _logger.LogInternalWarning("ServiceNowAPIClient: ServiceNow is not configured, should use NullableServiceNowAPIClient instead.");
                return;
            }
            try
            {
                if (string.IsNullOrEmpty(settings.ConnectionKey))
                {
                    throw new InvalidOperationException("ServiceNow connection key is not configured.");
                }

                _settings = JsonSerializer.Deserialize<ServiceNowAPISettings>(settings.ConnectionKey, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                            ?? throw new InvalidOperationException("ServiceNow settings are not properly configured.");
                _settings.Endpoint = settings.ConnectionUrl ?? throw new InvalidOperationException("ServiceNow connection URL is not configured.");
                _apiBaseUrl = $"{_settings.Endpoint}/api/now/v1";

                var authToken = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.Username}:{_settings.Password}"));
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                isEnabled = true;
            }
            catch (Exception ex)
            {
                isEnabled = false;
                _logger.LogInternalError(ex, "ServiceNowAPIClient: Error initializing ServiceNow API client. Check your configuration.");
            }
        }

        private void CheckEnabled()
        {
            if (!isEnabled)
            {
                throw new InvalidOperationException("ServiceNowAPIClient is not enabled. Check the configuration or use NullableServiceNowAPIClient instead.");
            }
        }

        private string? GetServiceNowPriorityNumber(string? priority)
        {
            if (string.IsNullOrEmpty(priority))
                return null;

            var lowerPriority = priority.ToLower();

            // If it's already a number, validate and return it
            if (int.TryParse(lowerPriority, out int numValue) && numValue >= 1 && numValue <= 5)
                return lowerPriority;

            // Convert using enum-based approach for consistency
            var serviceNowPriority = lowerPriority switch
            {
                "critical" or "1 - critical" => ServiceNowIncidentPriority.Critical,
                "high" or "2 - high" => ServiceNowIncidentPriority.High,
                "moderate" or "medium" or "3 - moderate" => ServiceNowIncidentPriority.Moderate,
                "low" or "4 - low" => ServiceNowIncidentPriority.Low,
                "planning" or "5 - planning" => ServiceNowIncidentPriority.Planning,
                _ => (ServiceNowIncidentPriority?)null
            };

            return serviceNowPriority?.ToString("D"); // Returns numeric value (1, 2, 3, 4, 5)
        }

        public async Task<ServiceNowIncident> GetIncidentAsync(string incidentSystemId)
        {
            CheckEnabled();

            if (string.IsNullOrEmpty(incidentSystemId))
            {
                throw new ArgumentException("Incident system ID cannot be null or empty.", nameof(incidentSystemId));
            }

            _logger.LogInternalInformation("GetIncidentAsync: Fetching incident {incidentSystemId}", incidentSystemId);
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ServiceNowResponse<ServiceNowIncident>>($"{_apiBaseUrl}/table/incident/{incidentSystemId}");
                if (response == null || response.Result == null)
                {
                    throw new Exception($"Failed to get incident {incidentSystemId}");
                }

                _logger.LogInternalInformation("GetIncidentAsync: Successfully retrieved incident {incidentSystemId}", incidentSystemId);
                return response.Result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIncidentAsync: Error retrieving incident {incidentSystemId}", incidentSystemId);
                throw;
            }
        }

        public async Task<List<ServiceNowIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? serviceId, string? titleContains, string? priority = null)
        {
            CheckEnabled();
            var queryParams = new Dictionary<string, string>
            {
                { "sysparm_limit", limit.ToString() },
                { "sysparm_offset", offset.ToString() }
            };

            // Build sysparm_query parts
            var sysParamQueryParts = new List<string>();

            if (lastModifiedDate.HasValue)
            {
                var formattedDate = lastModifiedDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
                sysParamQueryParts.Add($"sys_updated_on>{formattedDate}");
            }

            if (!string.IsNullOrEmpty(serviceId))
            {
                sysParamQueryParts.Add($"cmdb_ci={serviceId}");
            }

            if (!string.IsNullOrEmpty(titleContains))
            {
                sysParamQueryParts.Add($"short_descriptionLIKE{titleContains}");
            }

            if (!string.IsNullOrEmpty(priority))
            {
                var numericPriority = GetServiceNowPriorityNumber(priority);
                if (!string.IsNullOrEmpty(numericPriority))
                {
                    sysParamQueryParts.Add($"priority={numericPriority}");
                }
            }

            if (sysParamQueryParts.Count > 0)
            {
                var sysparmQuery = string.Join("^", sysParamQueryParts);
                queryParams.Add("sysparm_query", sysparmQuery);
            }

            var queryString = string.Join("&", queryParams.Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}"));
            var url = $"{_apiBaseUrl}/table/incident?{queryString}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInternalInformation("GetIncidentsAsync: No incidents found matching criteria");
                    return new List<ServiceNowIncident>();
                }
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<ServiceNowListResponse<ServiceNowIncident>>(content, options);

                if (result == null || result.Result == null)
                {
                    return new List<ServiceNowIncident>();
                }

                return result.Result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIncidentsAsync: Error retrieving incidents");
                return new List<ServiceNowIncident>();
            }
        }


        public async Task<List<ServiceNowDiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentSystemId)
        {
            CheckEnabled();
            try
            {
                var url = $"{_apiBaseUrl}/table/sys_journal_field?sysparm_query=element=comments^element_id={incidentSystemId}";
                var response = await _httpClient.GetFromJsonAsync<ServiceNowListResponse<ServiceNowDiscussionEntry>>(url);
                if (response == null || response.Result == null)
                {
                    return new List<ServiceNowDiscussionEntry>();
                }

                return response.Result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIncidentDiscussionEntriesAsync: Error retrieving discussion entries for incident {incidentSystemId}", incidentSystemId);
                return new List<ServiceNowDiscussionEntry>();
            }
        }

        public async Task<string> PostDiscussionEntryAsync(string incidentSystemId, string discussionEntry, bool htmlRendering = true)
        {
            CheckEnabled();
            try
            {
                var payload = new
                {
                    comments = discussionEntry,
                    work_notes = $"Comment added by Agent: {discussionEntry}"
                };

                var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/table/incident/{incidentSystemId}", payload);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "PostDiscussionEntryAsync: Error posting discussion entry for incident {incidentSystemId}", incidentSystemId);
                throw;
            }
        }

        public async Task<string> ChangePriorityAsync(string incidentSystemId, int priority, string discussionEntry)
        {
            CheckEnabled();
            try
            {
                var payload = new
                {
                    priority = priority,
                    comments = discussionEntry
                };

                var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/table/incident/{incidentSystemId}", payload);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ChangePriorityAsync: Error changing priority for incident {incidentSystemId}", incidentSystemId);
                throw;
            }
        }

        public async Task<string> AcknowledgeIncidentAsync(string incidentSystemId)
        {
            CheckEnabled();
            try
            {
                if (_settings == null)
                {
                    throw new InvalidOperationException("ServiceNow settings are not configured.");
                }

                // Always use the username from settings
                string contactAlias = _settings.Username;

                var payload = new
                {
                    state = "2", // In Progress state in ServiceNow
                    assigned_to = contactAlias,
                    comments = $"Incident acknowledged by {contactAlias}"
                };

                var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/table/incident/{incidentSystemId}", payload);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "AcknowledgeIncidentAsync: Error acknowledging incident {incidentSystemId}", incidentSystemId);
                throw;
            }
        }

        public async Task<string> ResolveIncidentAsync(string incidentSystemId, string resolutionNotes)
        {
            CheckEnabled();
            try
            {
                var payload = new
                {
                    state = "6", // Resolved state in ServiceNow
                    close_code = "Resolved by caller",
                    close_notes = resolutionNotes,
                    resolved_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var response = await _httpClient.PutAsJsonAsync($"{_apiBaseUrl}/table/incident/{incidentSystemId}", payload);
                response.EnsureSuccessStatusCode();

                _logger.LogInternalInformation("ResolveIncidentAsync: Successfully resolved incident {incidentSystemId}", incidentSystemId);
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ResolveIncidentAsync: Error resolving incident {incidentSystemId}", incidentSystemId);
                throw;
            }
        }
    }

    // Null implementation for when ServiceNow is not configured
    public class NullableServiceNowAPIClient : IServiceNowAPIClient
    {
        public Task<ServiceNowIncident> GetIncidentAsync(string incidentId) =>
            Task.FromResult(new ServiceNowIncident());

        public Task<List<ServiceNowIncident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? serviceId, string? titleContains, string? priority = null) =>
            Task.FromResult(new List<ServiceNowIncident>());

        public Task<List<ServiceNowDiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId) =>
            Task.FromResult(new List<ServiceNowDiscussionEntry>());

        public Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true) =>
            Task.FromResult(string.Empty);

        public Task<string> ChangePriorityAsync(string incidentId, int priority, string discussionEntry) =>
            Task.FromResult(string.Empty);

        public Task<string> AcknowledgeIncidentAsync(string incidentId) =>
            Task.FromResult(string.Empty);

        public Task<string> ResolveIncidentAsync(string incidentId, string resolutionNotes) =>
            Task.FromResult(string.Empty);
    }
}
