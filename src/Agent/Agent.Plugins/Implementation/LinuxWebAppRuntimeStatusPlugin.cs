// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class LinuxWebAppRuntimeStatusPlugin : ILinuxWebAppRuntimeStatusPlugin
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LinuxWebAppRuntimeStatusPlugin> _logger;
    private const string ApiVersion = "2023-12-01";
    private const string ArmBaseUrl = "https://management.azure.com";

    public LinuxWebAppRuntimeStatusPlugin(
        IHttpClientFactory httpClientFactory,
        ILogger<LinuxWebAppRuntimeStatusPlugin> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<string> GetLinuxWebAppRuntimeStatus(string resourceId)
    {
        try
        {
            if (!ResourceIdentifier.TryParse(resourceId, out var parsedResourceId) || parsedResourceId is null)
            {
                _logger.LogInternalError("[LinuxWebAppRuntimeStatusPlugin] Invalid resource ID provided: {ResourceId}", resourceId);
                return "Invalid resource ID provided. Azure resource IDs start with '/subscriptions/'.";
            }

            if (parsedResourceId.ResourceType != "Microsoft.Web/sites")
            {
                _logger.LogInternalError("[LinuxWebAppRuntimeStatusPlugin] Invalid resource type. Expected Microsoft.Web/sites, got: {ResourceType}", parsedResourceId.ResourceType);
                return $"Invalid resource type. Expected Microsoft.Web/sites, got: {parsedResourceId.ResourceType}";
            }

            var url = $"{ArmBaseUrl}{resourceId}/siteStatus?api-version={ApiVersion}";
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogInternalError("[LinuxWebAppRuntimeStatusPlugin] ARM API request failed with status {StatusCode}. URL: {Url}. Response: {Response}",
                    response.StatusCode, url, content);
                return $"ARM API request failed with status {response.StatusCode}. URL: {url}. Response: {response}";
            }

            return content;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[LinuxWebAppRuntimeStatusPlugin] Failed to get runtime status for resource {ResourceId}", resourceId);
            return $"Error retrieving SiteStaus: {ex.Message}";
        }
    }
}
