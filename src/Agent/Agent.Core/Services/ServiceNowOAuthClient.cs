// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Logging;
using Azure.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// ServiceNow API client that uses Azure API Connection's dynamicInvoke endpoint with OAuth.
/// This approach works with OAuth connections (like oauth2ServiceNow parameter set)
/// by routing calls through the ARM API, which handles token exchange automatically.
/// 
/// The client uses the agent's User-Assigned Managed Identity (UAMI) to obtain an ARM token,
/// which is then used to call the dynamicInvoke endpoint. The API Connection handles the
/// OAuth token lifecycle (refresh, etc.) for ServiceNow.
/// 
/// Connection details:
/// - SubscriptionId: from AGENT_SUBSCRIPTION_ID environment variable
/// - ResourceGroup: from AGENT_RESOURCE_GROUP environment variable  
/// - ConnectionName: from IncidentManagementSettings.ConnectionName (stored in agent config)
/// </summary>
public class ServiceNowOAuthClient : IServiceNowAPIClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ServiceNowOAuthClient> _logger;
    private readonly IAuthenticationService _authenticationService;
    private readonly IApiConnectionService _apiConnectionService;
    private readonly bool _isEnabled;
    private readonly bool _isProd;
    private readonly string _dynamicInvokeUrl;
    private readonly string _subscriptionId;
    private readonly string _resourceGroup;
    private readonly string _connectionName;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ServiceNowOAuthClient(
        HttpClient httpClient,
        ILogger<ServiceNowOAuthClient> logger,
        IncidentManagementSettings settings,
        IAuthenticationService authenticationService,
        IApiConnectionService apiConnectionService,
        IHostEnvironment hostEnvironment)
    {
        _httpClient = httpClient;
        _logger = logger;
        _authenticationService = authenticationService;
        _apiConnectionService = apiConnectionService;
        _isProd = hostEnvironment.IsProduction();
        _subscriptionId = string.Empty;
        _resourceGroup = string.Empty;
        _connectionName = string.Empty;

        if (settings.Type != IncidentManagementType.ServiceNow || string.IsNullOrEmpty(settings.ApiConnectionName))
        {
            _isEnabled = false;
            _logger.LogInternalWarning("ServiceNowOAuthClient: ServiceNow OAuth is not configured (ApiConnectionName is not set).");
            _dynamicInvokeUrl = string.Empty;
            return;
        }

        try
        {
            // Get connection details from agent runtime context and settings
            string subscriptionId = AgentNameHelper.GetSubscriptionId(_isProd);
            string resourceGroup = AgentNameHelper.GetResourceGroupName(_isProd);

            // Connection name is stored in agent configuration (set during OAuth setup)
            string connectionName = settings.ApiConnectionName ?? string.Empty;

            // Build the dynamicInvoke URL
            // Format: https://management.azure.com/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.Web/connections/{connection}/dynamicInvoke?api-version=2018-07-01-preview
            _dynamicInvokeUrl = $"https://management.azure.com/subscriptions/{subscriptionId}" +
                               $"/resourceGroups/{resourceGroup}" +
                               $"/providers/Microsoft.Web/connections/{connectionName}" +
                               "/dynamicInvoke?api-version=2018-07-01-preview";

            // Store connection details for status checks
            _subscriptionId = subscriptionId;
            _resourceGroup = resourceGroup;
            _connectionName = connectionName;

            _isEnabled = true;
            _logger.LogInternalInformation(
                "ServiceNowOAuthClient initialized. DynamicInvoke URL: {Url}",
                _dynamicInvokeUrl);
        }
        catch (Exception ex)
        {
            _isEnabled = false;
            _logger.LogInternalError(ex, "ServiceNowOAuthClient: Error initializing client. Agent context may not be available.");
            _dynamicInvokeUrl = string.Empty;
        }
    }

    private void CheckEnabled()
    {
        if (!_isEnabled)
        {
            throw new InvalidOperationException(
                "ServiceNowOAuthClient is not enabled. Check ServiceNow OAuth configuration.");
        }
    }

    /// <summary>
    /// Checks connection health by first verifying the API Connection resource status,
    /// then attempting to fetch an incident from ServiceNow.
    /// </summary>
    public async Task<ConnectionHealthResult> CheckConnectionHealthAsync()
    {
        if (!_isEnabled)
        {
            return new ConnectionHealthResult(false, "NotConfigured", "ServiceNow OAuth is not configured.");
        }

        try
        {
            _logger.LogInternalInformation("CheckConnectionHealthAsync: Checking API Connection status...");

            // Step 1: Check API Connection resource status
            var connectionStatus = await _apiConnectionService.GetConnectionStatusAsync(
                _subscriptionId,
                _resourceGroup,
                _connectionName);

            if (connectionStatus == null)
            {
                _logger.LogInternalWarning("CheckConnectionHealthAsync: API Connection not found.");
                return new ConnectionHealthResult(false, "NotFound",
                    $"API Connection '{_connectionName}' not found. Please set up ServiceNow OAuth connection.");
            }

            if (!string.Equals(connectionStatus, "Connected", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning("CheckConnectionHealthAsync: API Connection status is {Status}", connectionStatus);
                return new ConnectionHealthResult(false, connectionStatus,
                    $"API Connection is not authenticated. Status: {connectionStatus}. Please complete OAuth authorization.");
            }

            _logger.LogInternalInformation("CheckConnectionHealthAsync: API Connection is connected. Testing ServiceNow connectivity...");

            // Step 2: Try to fetch 1 incident to verify end-to-end connectivity
            var incidents = await GetIncidentsAsync(1, 0, null, null, null);

            _logger.LogInternalInformation("CheckConnectionHealthAsync: ServiceNow OAuth connection is healthy.");
            return new ConnectionHealthResult(true, "Connected");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "CheckConnectionHealthAsync: ServiceNow OAuth connection check failed.");
            return new ConnectionHealthResult(false, "Error", ex.Message);
        }
    }

    private async Task<string> GetArmAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var credential = await _authenticationService.GetArmOperationCredential();
        var tokenRequest = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
        var accessToken = await credential.GetTokenAsync(tokenRequest, cancellationToken);
        return accessToken.Token;
    }

    /// <summary>
    /// Execute a ServiceNow API call via dynamicInvoke
    /// </summary>
    private async Task<DynamicInvokeResponse<T>> ExecuteDynamicInvokeAsync<T>(
        string method,
        string path,
        object? body = null,
        Dictionary<string, string>? queries = null,
        CancellationToken cancellationToken = default) where T : class
    {
        CheckEnabled();

        var accessToken = await GetArmAccessTokenAsync(cancellationToken);

        var request = new DynamicInvokeRequest
        {
            Request = new DynamicInvokeRequestBody
            {
                Method = method.ToLowerInvariant(),
                Path = path
            }
        };

        if (queries != null && queries.Count > 0)
        {
            request.Request.Queries = queries;
        }

        if (body != null)
        {
            request.Request.Body = body;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _dynamicInvokeUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        httpRequest.Content = JsonContent.Create(request, options: JsonOptions);

        _logger.LogInternalInformation(
            "ServiceNow dynamicInvoke: {Method} {Path}",
            method.ToUpperInvariant(),
            path);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInternalError(
                "DynamicInvoke failed. Status: {Status}, Response: {Response}",
                response.StatusCode,
                responseContent);
            throw new HttpRequestException(
                $"DynamicInvoke failed with status {response.StatusCode}: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<DynamicInvokeResponse<T>>(responseContent, JsonOptions);
        if (result == null)
        {
            throw new InvalidOperationException("Failed to deserialize dynamicInvoke response.");
        }

        // Check for non-success status in the proxied response
        if (!IsSuccessStatusCode(result.Response?.StatusCode))
        {
            var errorBody = result.Response?.Body != null
                ? JsonSerializer.Serialize(result.Response.Body)
                : "No body";
            _logger.LogInternalError(
                "ServiceNow API returned error. Status: {Status}, Body: {Body}",
                result.Response?.StatusCode,
                errorBody);
            throw new HttpRequestException(
                $"ServiceNow API error: {result.Response?.StatusCode} - {errorBody}");
        }

        return result;
    }

    private static bool IsSuccessStatusCode(string? statusCode)
    {
        if (string.IsNullOrEmpty(statusCode))
            return false;

        return statusCode.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
               statusCode.Equals("Created", StringComparison.OrdinalIgnoreCase) ||
               statusCode.Equals("NoContent", StringComparison.OrdinalIgnoreCase) ||
               statusCode.Equals("Accepted", StringComparison.OrdinalIgnoreCase);
    }

    private string? GetServiceNowPriorityNumber(string? priority)
    {
        if (string.IsNullOrEmpty(priority))
            return null;

        var lowerPriority = priority.ToLower();

        if (int.TryParse(lowerPriority, out int numValue) && numValue >= 1 && numValue <= 5)
            return lowerPriority;

        return lowerPriority switch
        {
            "critical" or "1 - critical" => "1",
            "high" or "2 - high" => "2",
            "moderate" or "medium" or "3 - moderate" => "3",
            "low" or "4 - low" => "4",
            "planning" or "5 - planning" => "5",
            _ => null
        };
    }

    public async Task<ServiceNowIncident> GetIncidentAsync(string incidentSystemId)
    {
        CheckEnabled();

        if (string.IsNullOrEmpty(incidentSystemId))
        {
            throw new ArgumentException("Incident system ID cannot be null or empty.", nameof(incidentSystemId));
        }

        _logger.LogInternalInformation("GetIncidentAsync: Fetching incident {incidentSystemId}", incidentSystemId);

        // ServiceNow v2 API path
        var path = $"/api/now/v2/table/incident/{incidentSystemId}";
        var result = await ExecuteDynamicInvokeAsync<ServiceNowSingleResult<ServiceNowIncident>>(
            "get", path);

        var incident = result.Response?.Body?.Result;
        if (incident == null)
        {
            throw new InvalidOperationException($"Incident {incidentSystemId} not found.");
        }

        _logger.LogInternalInformation(
            "GetIncidentAsync: Successfully retrieved incident {incidentSystemId}",
            incidentSystemId);
        return incident;
    }

    public async Task<List<ServiceNowIncident>> GetIncidentsAsync(
        uint limit,
        uint offset,
        DateTime? lastModifiedDate,
        string? serviceId,
        string? titleContains,
        IEnumerable<string>? priorities = null)
    {
        CheckEnabled();

        var queries = new Dictionary<string, string>
        {
            { "sysparm_limit", limit.ToString() },
            { "sysparm_offset", offset.ToString() }
        };

        var queryParts = new List<string>();

        if (lastModifiedDate.HasValue)
        {
            var formattedDate = lastModifiedDate.Value.ToString("yyyy-MM-dd HH:mm:ss");
            queryParts.Add($"sys_updated_on>{formattedDate}");
        }

        if (!string.IsNullOrEmpty(serviceId))
        {
            queryParts.Add($"cmdb_ci={serviceId}");
        }

        if (!string.IsNullOrEmpty(titleContains))
        {
            queryParts.Add($"short_descriptionLIKE{titleContains}");
        }

        if (priorities != null && priorities.Any())
        {
            // Build an OR condition for multiple priorities: priority=1^ORpriority=2
            var priorityConditions = priorities
                .Select(p => GetServiceNowPriorityNumber(p))
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => $"priority={p}");

            if (priorityConditions.Any())
            {
                queryParts.Add($"({string.Join("^OR", priorityConditions)})");
            }
        }

        if (queryParts.Count > 0)
        {
            queries["sysparm_query"] = string.Join("^", queryParts);
        }

        try
        {
            var result = await ExecuteDynamicInvokeAsync<ServiceNowListResult<ServiceNowIncident>>(
                "get",
                "/api/now/v2/table/incident",
                queries: queries);

            var incidents = result.Response?.Body?.Result ?? new List<ServiceNowIncident>();
            _logger.LogInternalInformation(
                "GetIncidentsAsync: Successfully retrieved {Count} incidents",
                incidents.Count);
            return incidents;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("NotFound"))
        {
            _logger.LogInternalInformation("GetIncidentsAsync: No incidents found matching criteria");
            return new List<ServiceNowIncident>();
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
            var queries = new Dictionary<string, string>
            {
                { "sysparm_query", $"element=comments^element_id={incidentSystemId}" }
            };

            var result = await ExecuteDynamicInvokeAsync<ServiceNowListResult<ServiceNowDiscussionEntry>>(
                "get",
                "/api/now/v2/table/sys_journal_field",
                queries: queries);

            return result.Response?.Body?.Result ?? new List<ServiceNowDiscussionEntry>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "GetIncidentDiscussionEntriesAsync: Error retrieving discussion entries for incident {incidentSystemId}",
                incidentSystemId);
            return new List<ServiceNowDiscussionEntry>();
        }
    }

    public async Task<string> PostDiscussionEntryAsync(
        string incidentSystemId,
        string discussionEntry,
        bool htmlRendering = true)
    {
        CheckEnabled();

        try
        {
            var body = new
            {
                comments = discussionEntry,
                work_notes = $"Comment added by Agent: {discussionEntry}"
            };

            // Use PUT for updates (PATCH returns 404 on dynamicInvoke)
            await ExecuteDynamicInvokeAsync<ServiceNowSingleResult<ServiceNowIncident>>(
                "put",
                $"/api/now/v2/table/incident/{incidentSystemId}",
                body: body);

            return "Discussion entry posted successfully";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "PostDiscussionEntryAsync: Error posting discussion entry for incident {incidentSystemId}",
                incidentSystemId);
            throw;
        }
    }

    public async Task<string> ChangePriorityAsync(
        string incidentSystemId,
        int priority,
        string discussionEntry)
    {
        CheckEnabled();

        try
        {
            var body = new
            {
                priority = priority,
                comments = discussionEntry
            };

            await ExecuteDynamicInvokeAsync<ServiceNowSingleResult<ServiceNowIncident>>(
                "put",
                $"/api/now/v2/table/incident/{incidentSystemId}",
                body: body);

            return "Priority changed successfully";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "ChangePriorityAsync: Error changing priority for incident {incidentSystemId}",
                incidentSystemId);
            throw;
        }
    }

    public async Task<string> AcknowledgeIncidentAsync(string incidentSystemId)
    {
        CheckEnabled();

        try
        {
            var body = new
            {
                state = "2", // In Progress state
                comments = "Incident acknowledged by SRE Agent"
            };

            await ExecuteDynamicInvokeAsync<ServiceNowSingleResult<ServiceNowIncident>>(
                "put",
                $"/api/now/v2/table/incident/{incidentSystemId}",
                body: body);

            return "Incident acknowledged successfully";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "AcknowledgeIncidentAsync: Error acknowledging incident {incidentSystemId}",
                incidentSystemId);
            throw;
        }
    }

    public async Task<string> ResolveIncidentAsync(string incidentSystemId, string resolutionNotes)
    {
        CheckEnabled();

        try
        {
            var body = new
            {
                state = "6", // Resolved state
                close_code = "Solved (Permanently)",
                close_notes = resolutionNotes,
                resolved_at = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            await ExecuteDynamicInvokeAsync<ServiceNowSingleResult<ServiceNowIncident>>(
                "put",
                $"/api/now/v2/table/incident/{incidentSystemId}",
                body: body);

            _logger.LogInternalInformation(
                "ResolveIncidentAsync: Successfully resolved incident {incidentSystemId}",
                incidentSystemId);
            return "Incident resolved successfully";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "ResolveIncidentAsync: Error resolving incident {incidentSystemId}",
                incidentSystemId);
            throw;
        }
    }
}

#region DynamicInvoke DTOs

/// <summary>
/// Request body for the dynamicInvoke ARM API endpoint
/// </summary>
internal class DynamicInvokeRequest
{
    [JsonPropertyName("request")]
    public DynamicInvokeRequestBody Request { get; set; } = new();
}

internal class DynamicInvokeRequestBody
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "get";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("queries")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Queries { get; set; }

    [JsonPropertyName("body")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Body { get; set; }
}

/// <summary>
/// Response from the dynamicInvoke ARM API endpoint
/// </summary>
internal class DynamicInvokeResponse<T> where T : class
{
    [JsonPropertyName("response")]
    public DynamicInvokeResponseBody<T>? Response { get; set; }

    [JsonPropertyName("error")]
    public DynamicInvokeError? Error { get; set; }
}

internal class DynamicInvokeResponseBody<T> where T : class
{
    [JsonPropertyName("statusCode")]
    public string? StatusCode { get; set; }

    [JsonPropertyName("body")]
    public T? Body { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }
}

internal class DynamicInvokeError
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

/// <summary>
/// ServiceNow response wrapper for single result
/// </summary>
internal class ServiceNowSingleResult<T> where T : class
{
    [JsonPropertyName("result")]
    public T? Result { get; set; }
}

/// <summary>
/// ServiceNow response wrapper for list results
/// </summary>
internal class ServiceNowListResult<T> where T : class
{
    [JsonPropertyName("result")]
    public List<T>? Result { get; set; }
}

#endregion
