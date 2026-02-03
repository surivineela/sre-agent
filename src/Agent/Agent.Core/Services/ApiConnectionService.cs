// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Service for checking Azure API Connection (Microsoft.Web/connections) status.
/// Used by ServiceNowOAuthClient to verify connection health.
/// 
/// Note: API Connection creation, deletion, and role assignment are now handled
/// by the frontend using the user's ARM credentials via the Azure portal's ARM proxy.
/// </summary>
public class ApiConnectionService : IApiConnectionService
{
    private readonly ILogger<ApiConnectionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TokenCredential _credential;

    public ApiConnectionService(
        ILogger<ApiConnectionService> logger,
        IHttpClientFactory httpClientFactory,
        TokenCredential credential)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    /// <inheritdoc/>
    public async Task<string?> GetConnectionStatusAsync(
        string subscriptionId,
        string resourceGroupName,
        string connectionName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionResourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/connections/{connectionName}";
            var apiVersion = "2018-07-01-preview";

            var httpClient = _httpClientFactory.CreateClient();
            var token = await _credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                cancellationToken);

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var getUrl = $"https://management.azure.com{connectionResourceId}?api-version={apiVersion}";
            var response = await httpClient.GetAsync(getUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInternalWarning(
                    "Failed to get API Connection status for {ConnectionName}. Status: {StatusCode}",
                    connectionName, response.StatusCode);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var connectionResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (connectionResponse.TryGetProperty("properties", out var properties) &&
                properties.TryGetProperty("statuses", out var statuses) &&
                statuses.GetArrayLength() > 0)
            {
                var firstStatus = statuses[0];
                if (firstStatus.TryGetProperty("status", out var statusElement))
                {
                    var status = statusElement.GetString();
                    _logger.LogInternalInformation(
                        "API Connection {ConnectionName} status: {Status}",
                        connectionName, status);
                    return status;
                }
            }

            _logger.LogInternalWarning(
                "Could not determine status for API Connection: {ConnectionName}",
                connectionName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting API Connection status: {ConnectionName}", connectionName);
            return null;
        }
    }
}
