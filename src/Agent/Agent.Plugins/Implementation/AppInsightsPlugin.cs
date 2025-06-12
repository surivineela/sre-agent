using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Net.Http;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Agent.Plugins;

public class AppInsightsPlugin : IAppInsightsPlugin
{
    private readonly ArmHelper _armHelper;

    public AppInsightsPlugin(ArmHelper armHelper)
    {
        _armHelper = armHelper;
    }

    [KernelFunction("query_app_insights")]
    [Description("Queries Application Insights")]
    public async Task<string> ExecuteAppInsightsQuery(
    [Description("resourceId of app service")] string resourceId,
    [Description("query for Application Insights API call")] string queryString)
    {
        try
        {
            // get instrumentation Key from web app settings
            var appSettings = await _armHelper.GetAppSettings(resourceId);
            var jsonObject = JObject.Parse(appSettings);
            var instrumentationKey = GetInstrumentationKey(jsonObject["properties"]?["APPINSIGHTS_INSTRUMENTATIONKEY"]?.ToString()) ?? GetInstrumentationKey(jsonObject["properties"]?["APPLICATIONINSIGHTS_CONNECTION_STRING"]?.ToString());
            var subId = resourceId.Split('/')[2];

            // use instrumentation key to single in on the correct app insights resource
            var appInsightsAppId = await _armHelper.GetAppInsightsAppIdBySubscription(subId, instrumentationKey);
            // query the correct app insights resource
            var results = await _armHelper.ExecuteAppInsightsQuery(appInsightsAppId, queryString);
            return results;
        }
        catch (Exception ex) {
            return $"The Application Insights query {queryString} failed due to the exception {ex.Message}.";
        }
    }

    [KernelFunction("query_log_analytics_workspace")]
    [Description("Queries Log Analytics Workspace")]
    public async Task<string> ExecuteLogAnalyticsQuery(
    [Description("resourceId of app service")] string resourceId,
    [Description("query for Log Analytics Workspace API call")] string queryString,
    [Description("Time span string (e.g. P1D, PT30m)")] string timeSpan)
    {
        try
        {
            var results = await _armHelper.ExecuteLogAnalyticsQuery(resourceId, queryString, timeSpan);
            return results;
        }
        catch (Exception ex)
        {
            return $"The Application Insights query {queryString} failed due to the exception {ex.Message}.";
        }
    }

    private string? GetInstrumentationKey(string? connectionString)
    {
        if (connectionString != null) {
            string[] keyValues = connectionString.Split(';');

            string instrumentationKey = null;

            foreach (var keyValue in keyValues)
            {
                string[] pair = keyValue.Split('=');
                if (pair.Length == 2 && pair[0].Trim() == "InstrumentationKey")
                {
                    instrumentationKey = pair[1];
                    break;
                }
                else if(pair.Length == 1)
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

