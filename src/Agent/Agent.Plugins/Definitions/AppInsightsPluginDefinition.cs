// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Plugins.Definitions;

// [Export]
public class AppInsightsPluginDefinition
{
    private IAppInsightsPlugin _appInsightsPlugin;

    public AppInsightsPluginDefinition(IAppInsightsPlugin appInsightsPlugin)
    {
        _appInsightsPlugin = appInsightsPlugin;
    }

    [KernelFunction("make_app_insight_api_call")]
    [Description("Makes an api call to application insights")]
    public async Task<string> ExecuteAppInsightsQuery(
        string resourceId, 
       [Description("query for api call to application insights")] string queryString)
    {
        return await _appInsightsPlugin.ExecuteAppInsightsQuery(resourceId, queryString);
    }

    [KernelFunction("query_log_analytics_workspace")]
    [Description("Queries Log Analytics Workspace")]
    public async Task<string> ExecuteLogAnalyticsQuery(
        string resourceId,
       [Description("query for Log Analytics Workspace API call")] string queryString,
       [Description("Time span string (e.g. P1D, PT30m)")] string timeSpan)
    {
        return await _appInsightsPlugin.ExecuteLogAnalyticsQuery(resourceId, queryString, timeSpan);
    }
}
