// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins;

public class AppInsightsPlugin : IAppInsightsPlugin
{
    private readonly ArmHelper _armHelper;
    private readonly ILogger<AppInsightsPlugin> _logger;

    public AppInsightsPlugin(ArmHelper armHelper, ILogger<AppInsightsPlugin> logger)
    {
        _armHelper = armHelper;
        _logger = logger;
    }

    public async Task<string> QueryAppInsightsByAppId(string appInsightsAppId, string queryString, string? timeSpan = null, bool formatAsTsv = false)
    {
        try
        {
            if (!Guid.TryParse(appInsightsAppId, out _))
            {
                return "Error: the provided Application Insights app ID must be a GUID. You may need to look up the app ID from ARM using the full Azure resource ID.";
            }

            var results = await _armHelper.QueryAppInsightsByAppId(appInsightsAppId, queryString, timeSpan, formatAsTsv);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Application Insights query with app ID {AppInsightsAppId}", appInsightsAppId);
            return $"The Application Insights query {queryString} failed due to the exception {ex.Message}.";
        }
    }

    public async Task<string> QueryAppInsightsByResourceId(string appInsightsResourceId, string queryString, string? timeSpan = null, bool formatAsTsv = false)
    {
        try
        {
            if (!ResourceIdentifier.TryParse(appInsightsResourceId, out var parsedResourceId) || parsedResourceId is null)
            {
                return "Error: the provided Application Insights resource ID is invalid. Azure resource IDs start with '/subscriptions/'.";
            }

            if (parsedResourceId.ResourceType != "Microsoft.Insights/components")
            {
                return "Error: the provided Azure resource ID must have the 'Microsoft.Insights/components' resource type.";
            }

            var armJson = await _armHelper.GetArmResourceAsJsonAsync(appInsightsResourceId);
            using var armJsonDocument = JsonDocument.Parse(armJson);
            if (!armJsonDocument.RootElement.TryGetProperty("properties", out var properties))
            {
                return "Error: no properties could be found for the given Application Insights resource.";
            }

            if (!properties.TryGetProperty("AppId", out var appIdEl) || appIdEl.ValueKind != JsonValueKind.String)
            {
                return "Error: no AppId could be found for the given Application Insights resource.";
            }

            var results = await _armHelper.QueryAppInsightsByAppId(appIdEl.ToString(), queryString, timeSpan, formatAsTsv);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Application Insights query with resource ID {AppInsightsResourceId}", appInsightsResourceId);
            return $"The Application Insights query {queryString} failed due to the exception {ex.Message}.";
        }
    }

    public async Task<string> QueryAppInsightsByWebAppSettings(string webAppResourceId, string queryString)
    {
        try
        {
            // get instrumentation Key from web app settings
            var appSettings = await _armHelper.GetAppSettings(webAppResourceId);
            var jsonObject = JObject.Parse(appSettings);
            var instrumentationKey = GetInstrumentationKey(jsonObject["properties"]?["APPINSIGHTS_INSTRUMENTATIONKEY"]?.ToString()) ?? GetInstrumentationKey(jsonObject["properties"]?["APPLICATIONINSIGHTS_CONNECTION_STRING"]?.ToString());
            var subId = webAppResourceId.Split('/')[2];

            // use instrumentation key to single in on the correct app insights resource
            var appInsightsAppId = await _armHelper.GetAppInsightsAppIdBySubscription(subId, instrumentationKey ?? string.Empty);
            // query the correct app insights resource
            var results = await _armHelper.QueryAppInsightsByAppId(appInsightsAppId, queryString, timeSpan: null, formatAsTsv: false);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Application Insights query for web app {WebAppResourceId}", webAppResourceId);
            return $"The Application Insights query {queryString} failed due to the exception {ex.Message}.";
        }
    }

    public async Task<string> QueryLogAnalyticsByResourceId(string workspaceResourceId, string queryString, string? timeSpan, bool formatAsTsv = false)
    {
        try
        {
            if (!ResourceIdentifier.TryParse(workspaceResourceId, out var parsedResourceId) || parsedResourceId is null)
            {
                return "Error: the provided workspace resource ID is invalid. Azure resource IDs start with '/subscriptions/'.";
            }

            if (parsedResourceId.ResourceType != "Microsoft.OperationalInsights/workspaces")
            {
                return "Error: the provided Azure resource ID must have the 'Microsoft.OperationalInsights/workspaces' resource type.";
            }

            var results = await _armHelper.QueryLogAnalyticsByResourceId(workspaceResourceId, queryString, timeSpan, formatAsTsv);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Log Analytics query for workspace resource {WorkspaceId}", workspaceResourceId);
            return $"The Log Analytics query {queryString} for workspace resource {workspaceResourceId} failed due to the exception {ex.Message}.";
        }
    }

    public async Task<string> QueryLogAnalyticsByWorkspaceId(string workspaceId, string queryString, string? timeSpan, bool formatAsTsv = false)
    {
        try
        {
            if (!Guid.TryParse(workspaceId, out _))
            {
                return "Error: the provided Log Analytics workspace ID must be a GUID. You may need to look up the workspace ID from ARM using the full Azure resource ID for the workspace.";
            }

            var results = await _armHelper.QueryLogAnalyticsByWorkspaceId(workspaceId, queryString, timeSpan, formatAsTsv);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Log Analytics query for workspace {WorkspaceId}", workspaceId);
            return $"The Log Analytics query {queryString} for workspace {workspaceId} failed due to the exception {ex.Message}.";
        }
    }

    public async Task<string> QueryLogAnalyticsByWebAppDiagnosticSettings(string resourceId, string queryString, string timeSpan)
    {
        try
        {
            var results = await _armHelper.QueryLogAnalyticsByWebAppDiagnosticSettings(resourceId, queryString, timeSpan, formatAsTsv: false);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to execute Log Analytics query for workspace linked to resource {ResourceId}", resourceId);
            return $"The Log Analytics query {queryString} failed due to the exception {ex.Message}.";
        }
    }

    private string? GetInstrumentationKey(string? connectionString)
    {
        if (connectionString != null)
        {
            string[] keyValues = connectionString.Split(';');

            string instrumentationKey = string.Empty;

            foreach (var keyValue in keyValues)
            {
                string[] pair = keyValue.Split('=');
                if (pair.Length == 2 && pair[0].Trim() == "InstrumentationKey")
                {
                    instrumentationKey = pair[1];
                    break;
                }
                else if (pair.Length == 1)
                {
                    // handle case where AppInsights is deployed via ARM (just the instrumentation key as the value)
                    instrumentationKey = pair[0];
                    break;
                }
            }
            return instrumentationKey;
        }
        return null;
    }
}

