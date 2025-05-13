// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Interfaces;
using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Azure.Monitor.Query;
using Agent.Logging;
using Agent.Core.Helpers;
using Microsoft.AspNetCore.Connections.Features;

namespace Agent.Core.Services;
public class LogQueryService : ILogQueryService
{
    private readonly ILogger<LogQueryService> _logger;
    private readonly IAuthenticationService _authService;
    private readonly IHttpClientFactory _httpClientFactory;

    public LogQueryService(IHttpClientFactory httpClientFactory, IAuthenticationService authService, ILogger<LogQueryService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authService = authService;
        _logger = logger;
    }

    public async Task<IEnumerable<QueryInfo>> GetSavedQueriesForSubscriptionAsync(string subscriptionId)
    {
        List<QueryInfo> allQueries = new();

        try
        {
            // Get all query packs for the subscription
            string queryPacksUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.OperationalInsights/queryPacks?api-version=2025-02-01";
            
            var queryPacks = await GetQueryPacksAsync(queryPacksUrl);
            _logger.LogInternalInformation($"Found {queryPacks.Count} query packs in subscription {subscriptionId}");
            
            // For each query pack, get its queries
            foreach (var queryPack in queryPacks)
            {
                string queryPackId = queryPack.Id;
                string queryPackName = queryPack.Name;
                
                _logger.LogInternalInformation($"Getting queries for query pack: {queryPackName}");
                
                // Get queries for this query pack
                string queriesUrl = $"https://management.azure.com{queryPackId}/queries?api-version=2025-02-01";
                var queries = await GetQueriesForQueryPackAsync(queriesUrl);
                
                _logger.LogInternalInformation($"Found {queries.Count} queries in query pack {queryPackName}");
                
                // Process each query as needed
                foreach (var query in queries)
                {
                    _logger.LogDebug($"Query: {query.Name}, ID: {query.Id}");
                    allQueries.Add(query);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error getting queries for subscription {subscriptionId}");
            throw;
        }

        return allQueries;
    }

    public async Task<string> ExecuteLogQueryAsync(string subscriptionId, string queryText, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        if (string.IsNullOrEmpty(subscriptionId))
        {
            _logger.LogInternalWarning("subscriptionId cannot be null or empty!");
            return string.Empty;
        }

        try
        {
            _logger.LogInternalInformation($"Finding workspaces in subscription {subscriptionId} to execute query");

            List<string> workspaceIds = await GetWorkspaceIdsInSubscriptionAsync(subscriptionId);
            
            if (!workspaceIds.Any())
            {
                _logger.LogInternalWarning($"No Log Analytics workspaces found in subscription {subscriptionId}");
                return "No workspaces found";
            }

            var credentials = _authService.GetLogAnalyticsCredential();
            var client = new LogsQueryClient(credentials);
            
            // Try each workspace until we get a successful result
            foreach (var workspaceId in workspaceIds)
            {
                try
                {
                    _logger.LogInternalInformation($"Executing query against workspace {workspaceId}");
                    var response = await client.QueryWorkspaceAsync(
                        workspaceId,
                        queryText,
                        new QueryTimeRange(startTime, endTime));
                    
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                    return JsonSerializer.Serialize(response.Value, jsonOptions);
                }
                catch (Exception ex)
                {
                    // Log error but continue to next workspace
                    string errorMessage = $"Failed to query workspace {workspaceId}: {ex.Message}";
                    _logger.LogInternalWarning(ex, errorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error executing log query: {ex.Message}");
        }

        // If we get here, all workspaces failed
        return $"Failed to execute query on all workspaces.";
    }

    private async Task<List<QueryPackInfo>> GetQueryPacksAsync(string url)
    {
        var queryPacks = new List<QueryPackInfo>();
        
        using (var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation))
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                var response = await httpClient.SendAsync(request);
                
                _logger.LogInternalInformation($"QueryPacks API response status: {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // This helps with casing mismatches
                    };
                    
                    var queryPacksResponse = JsonSerializer.Deserialize<QueryPacksResponse>(content, options);
                    
                    if (queryPacksResponse?.Value != null)
                    {
                        _logger.LogInternalInformation($"Successfully deserialized {queryPacksResponse.Value.Count} query packs");
                        queryPacks.AddRange(queryPacksResponse.Value);
                    }
                    else
                    {
                        _logger.LogInternalWarning("Deserialized response successfully but Value property is null or empty");
                    }
                    
                    string nextLink = queryPacksResponse?.NextLink;
                    while (!string.IsNullOrEmpty(nextLink))
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, nextLink);
                        
                        response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        
                        content = await response.Content.ReadAsStringAsync();
                        _logger.LogDebug($"NextLink raw response: {content}");
                        
                        queryPacksResponse = JsonSerializer.Deserialize<QueryPacksResponse>(content, options);
                        
                        if (queryPacksResponse?.Value != null)
                        {
                            queryPacks.AddRange(queryPacksResponse.Value);
                        }
                        
                        nextLink = queryPacksResponse?.NextLink;
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogInternalError(jsonEx, $"JSON deserialization error: {jsonEx.Message}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogInternalError(httpEx, $"HTTP request error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting query packs: {ex.Message}");
            }
        }
        
        return queryPacks;
    }

    private async Task<List<QueryInfo>> GetQueriesForQueryPackAsync(string url)
    {
        var queries = new List<QueryInfo>();
        
        using (var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation))
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                
                var response = await httpClient.SendAsync(request);
                
                _logger.LogInternalInformation($"Queries API response status: {response.StatusCode}");
                
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true // This helps with casing mismatches
                    };
                    
                    var queriesResponse = JsonSerializer.Deserialize<QueriesResponse>(content, options);
                    
                    if (queriesResponse?.Value != null)
                    {
                        _logger.LogInternalInformation($"Successfully deserialized {queriesResponse.Value.Count} queries");
                        queries.AddRange(queriesResponse.Value);
                    }
                    else
                    {
                        _logger.LogInternalWarning("Deserialized queries response successfully but Value property is null or empty");
                    }
                    
                    string nextLink = queriesResponse?.NextLink;
                    while (!string.IsNullOrEmpty(nextLink))
                    {
                        request = new HttpRequestMessage(HttpMethod.Get, nextLink);
                        
                        response = await httpClient.SendAsync(request);
                        response.EnsureSuccessStatusCode();
                        
                        content = await response.Content.ReadAsStringAsync();
                        _logger.LogDebug($"NextLink raw response: {content}");
                        
                        queriesResponse = JsonSerializer.Deserialize<QueriesResponse>(content, options);
                        
                        if (queriesResponse?.Value != null)
                        {
                            queries.AddRange(queriesResponse.Value);
                        }
                        
                        nextLink = queriesResponse?.NextLink;
                    }
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogInternalError(jsonEx, $"JSON deserialization error: {jsonEx.Message}");
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogInternalError(httpEx, $"HTTP request error: {httpEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting queries: {ex.Message}");
            }
        }
        
        return queries;
    }

    private async Task<List<string>> GetWorkspaceIdsInSubscriptionAsync(string subscriptionId)
    {
        List<string> workspaceIds = new();
        string url = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.OperationalInsights/workspaces?api-version=2021-06-01";
        
        using (var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation))
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var workspacesResponse = JsonSerializer.Deserialize<WorkspacesResponse>(content, options);
                
                if (workspacesResponse?.Value != null)
                {
                    _logger.LogInternalInformation($"Found {workspacesResponse.Value.Count} Log Analytics workspaces");
                    foreach (var workspace in workspacesResponse.Value)
                    {
                        // Extract the customerId (actual workspace ID for querying)
                        if (workspace.Properties?.CustomerId != null)
                        {
                            workspaceIds.Add(workspace.Properties.CustomerId);
                            _logger.LogDebug($"Found workspace: {workspace.Name}, ID: {workspace.Properties.CustomerId}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error getting workspaces: {ex.Message}");
            }
        }
        
        return workspaceIds;
    }
}

#region QueryPack Models

public class QueryPacksResponse
{
    public List<QueryPackInfo> Value { get; set; }
    
    [JsonPropertyName("nextLink")]
    public string NextLink { get; set; }
}

public class QueryPackInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public Properties Properties { get; set; }
}

public class Properties
{
    public string QueryPackId { get; set; }
    public string ProvisioningState { get; set; }
}

public class QueriesResponse
{
    public List<QueryInfo> Value { get; set; }
    
    [JsonPropertyName("nextLink")]
    public string NextLink { get; set; }
}

public class QueryInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public QueryProperties Properties { get; set; }
}

public class QueryProperties
{
    public string Id { get; set; }
    public string DisplayName { get; set; }
    public string Description { get; set; }
    public string Body { get; set; }
    public Tags Tags { get; set; }
}

public class Tags
{
    public List<string> Labels { get; set; } = new List<string>();
}

public class WorkspacesResponse
{
    public List<WorkspaceInfo> Value { get; set; }
    
    [JsonPropertyName("nextLink")]
    public string NextLink { get; set; }
}

public class WorkspaceInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public WorkspaceProperties Properties { get; set; }
}

public class WorkspaceProperties
{
    public string CustomerId { get; set; }
    public string ProvisioningState { get; set; }
}

#endregion
