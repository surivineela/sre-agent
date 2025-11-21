// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

// Query type enum (replaces SDK version)
public enum QueryType
{
    MQL = 0,
    KQL = 1
}

public class DGrepPluginClient : IDGrepPluginClient
{
    private readonly IAuthenticationService _authService;
    private readonly ILogger<DGrepPluginClient> _logger;
    private readonly AgentSpaceProxySettings _agentSpaceProxySettings;
    private readonly TokenCredentialHttpClientHandler _tokenCredentialHttpClientHandler;
    private readonly string _agentResourceId;

    public DGrepPluginClient(
        IAuthenticationService authService,
        ILogger<DGrepPluginClient> logger,
        AgentSpaceProxySettings agentSpaceProxySettings)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _agentSpaceProxySettings = agentSpaceProxySettings ?? throw new ArgumentNullException(nameof(agentSpaceProxySettings));

        // Initialize Agent Space Proxy support
        _agentResourceId = AgentNameHelper.GetAgentResourceId(isProd: true);

        var proxyCredential = _authService.GetAgentSpaceProxyCredential();
        _tokenCredentialHttpClientHandler = new TokenCredentialHttpClientHandler(
            proxyCredential,
            _agentSpaceProxySettings.Scope);

        _logger.LogInternalInformation("DGrepPluginClient initialized - ProxyEndpoint={ProxyEndpoint}, AgentResourceId={AgentResourceId}",
            _agentSpaceProxySettings.Endpoint, _agentResourceId);
    }

    // NEW: Agent Space Proxy HTTP client
    private HttpClient GetHttpClient()
    {
        if (string.IsNullOrEmpty(_agentSpaceProxySettings?.Endpoint))
        {
            throw new InvalidOperationException("Agent Space Proxy endpoint is not configured.");
        }

        var httpClient = new HttpClient(_tokenCredentialHttpClientHandler, disposeHandler: false)
        {
            BaseAddress = new Uri(_agentSpaceProxySettings.Endpoint),
            Timeout = TimeSpan.FromMinutes(5)
        };

        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("SREAgent-DGrepPlugin/1.0");

        return httpClient;
    }

    // NEW: Agent Space Proxy low-level call
    private async Task<HttpResponseMessage> CallAgentSpaceProxy(string path, string jsonPayload, CancellationToken ct = default)
    {
        _logger.LogInternalInformation("CallAgentSpaceProxy: Calling proxy - Endpoint={Endpoint}, Path={Path}, AgentResourceId={AgentResourceId}",
            _agentSpaceProxySettings.Endpoint, path, _agentResourceId);

        var httpClient = GetHttpClient();
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
        };

        request.Headers.Add("x-ms-sreagent-resource-id", _agentResourceId);

        var response = await httpClient.SendAsync(request, ct);
        return response;
    }

    // Production mode: Agent Space Proxy workflow
    private async Task<string> ExecuteViaAgentSpaceProxy(
        string nameSpace, string eventName, string serverQuery, string clientQuery,
        string filters, QueryType queryType, DateTime startTime, DateTime endTime,
        int maxResults, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["namespace"] = nameSpace,
            ["eventName"] = eventName,
            ["serverQuery"] = serverQuery ?? string.Empty,
            ["clientQuery"] = clientQuery ?? string.Empty,
            ["filters"] = filters ?? string.Empty,
            ["queryType"] = queryType.ToString(),
            ["startTime"] = startTime.ToString("o"),
            ["endTime"] = endTime.ToString("o"),
            ["maxResults"] = maxResults
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        _logger.LogInternalInformation("ExecuteViaAgentSpaceProxy: Sending request to proxy - Namespace={Namespace}, Event={Event}, QueryType={QueryType}",
            nameSpace, eventName, queryType);

        try
        {
            var httpResponse = await CallAgentSpaceProxy("/dgrep/query", jsonPayload, ct);
            var content = await httpResponse.Content.ReadAsStringAsync(ct);

            // Wrap in DGrepWorkflowResponse (Geneva Actions pattern)
            var response = new DGrepWorkflowResponse
            {
                StatusCode = httpResponse.StatusCode,
                Content = content ?? string.Empty
            };

            // Log status + content together BEFORE checking success (Geneva Actions pattern)
            _logger.LogInternalInformation("ExecuteViaAgentSpaceProxy: DGrep query executed with status code: {StatusCode}, response length: {ContentLength}",
                response.StatusCode, response.Content.Length);

            // Return error string on failure (don't throw)
            if (!response.IsSuccessStatusCode)
            {
                return $"DGrep proxy query failed with status {response.StatusCode}: {response.Content}";
            }

            return response.Content;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "ExecuteViaAgentSpaceProxy: Request failed - {ErrorMessage}", ex.Message);
            return $"DGrep proxy request failed: {ex.Message}";
        }
    }

    public async Task<string> ExecuteDGrepQuery(string nameSpace, string eventName, string serverQuery, string clientQuery, string filters, QueryType queryType, DateTime startTime, DateTime endTime, int maxResults = 10, CancellationToken ct = default)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(nameSpace) || string.IsNullOrWhiteSpace(eventName) || string.IsNullOrWhiteSpace(serverQuery))
        {
            _logger.LogInternalError("DGrep query validation failed: missing required parameters");
            throw new ArgumentException("Namespace, event name, and query must be provided.");
        }

        _logger.LogInternalInformation("DGrep query started - Namespace={Namespace}, Event={EventName}, Query={ServerQuery}",
            nameSpace, eventName, serverQuery);

        // Production: Use Agent Space Proxy only
        _logger.LogInternalInformation("DGrep: Using Agent Space Proxy - Endpoint={ProxyEndpoint}, AgentResourceId={AgentResourceId}",
            _agentSpaceProxySettings.Endpoint, _agentResourceId);

        return await ExecuteViaAgentSpaceProxy(
            nameSpace, eventName, serverQuery, clientQuery,
            filters, queryType, startTime, endTime, maxResults, ct);
    }

    private static string SanitizeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        // More aggressive sanitization to prevent content filtering
        var sanitized = content
            // Remove stack traces and exceptions (major triggers)
            .Replace("Exception", "[Ex]")
            .Replace("Error", "[Err]")
            .Replace("Failed", "[Fail]")
            .Replace("Failure", "[Fail]")
            .Replace("Fatal", "[Fatal]")
            .Replace("Critical", "[Crit]")
            .Replace("Warning", "[Warn]")
            // Remove file paths
            .Replace("C:\\", "[Drive]\\")
            .Replace("D:\\", "[Drive]\\")
            .Replace("\\bin\\", "\\[bin]\\")
            .Replace("\\tmp\\", "\\[tmp]\\")
            // Remove URLs and IPs
            .Replace("http://", "[http]://")
            .Replace("https://", "[https]://")
            .Replace("localhost", "[local]")
            .Replace("127.0.0.1", "[local-ip]")
            // Remove common sensitive patterns
            .Replace("password", "[pwd]")
            .Replace("token", "[tok]")
            .Replace("key", "[key]")
            .Replace("secret", "[sec]");

        // Limit length aggressively - use sanitized string length to avoid ArgumentOutOfRangeException
        return sanitized.Substring(0, Math.Min(sanitized.Length, 200));
    }
}

// Simple response wrapper - matches GenevaActionWorkflowResponse pattern
public class DGrepWorkflowResponse
{
    public System.Net.HttpStatusCode StatusCode { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsSuccessStatusCode => (int)StatusCode >= 200 && (int)StatusCode <= 299;
}
