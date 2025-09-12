// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.LogQuery)]
public class AppInsightsPluginDefinition
{
    private readonly IAppInsightsPlugin _appInsightsPlugin;

    public AppInsightsPluginDefinition(IAppInsightsPlugin appInsightsPlugin)
    {
        _appInsightsPlugin = appInsightsPlugin;
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Queries an Application Insights given a specific Application Insights Azure resource ID. The results are formatted as tab-delimited values.")]
    public async Task<string> QueryAppInsightsByResourceId(
        [Description("Application Insights resource ID used to identify the resource. This is a string starting with '/subscriptions/' and containing the 'Microsoft.Insights/components' resource type.")] string appInsightsResourceId,
        [Description("Kusto (KQL) query for Application Insights")] string queryString,
        [Description("Optional time span string (e.g. P1D, PT30m) for the range of events to query, from the current time backwards by the given duration. Adds time range restriction in addition to the query itself.")] string? timeSpan)
    {
        return await _appInsightsPlugin.QueryAppInsightsByResourceId(appInsightsResourceId, queryString, timeSpan, formatAsTsv: true);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Queries an Application Insights given a specific Application Insights app ID. The results are formatted as tab-delimited values.")]
    public async Task<string> QueryAppInsightsByAppId(
        [Description("Application Insights app ID used to identify the resource. This is a GUID value specific to an Application Insights resource.")] string appInsightsAppId,
        [Description("Kusto (KQL) query for Application Insights")] string queryString,
        [Description("Optional time span string (e.g. P1D, PT30m) for the range of events to query, from the current time backwards by the given duration. Adds time range restriction in addition to the query itself.")] string? timeSpan)
    {
        return await _appInsightsPlugin.QueryAppInsightsByAppId(appInsightsAppId, queryString, timeSpan, formatAsTsv: true);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Queries a Log Analytics workspace given a specific Log Analytics resource ID. The results are formatted as tab-delimited values.")]
    public async Task<string> QueryLogAnalyticsByResourceId(
        [Description("Log Analytics Azure resource ID to execute a query against. This is a string starting with '/subscriptions/' and containing the 'Microsoft.OperationalInsights/workspaces' resource type.")] string workspaceResourceId,
        [Description("Kusto (KQL) query for Log Analytics")] string queryString,
        [Description("Optional time span string (e.g. P1D, PT30m) for the range of events to query, from the current time backwards by the given duration. Adds time range restriction in addition to the query itself.")] string? timeSpan)
    {
        return await _appInsightsPlugin.QueryLogAnalyticsByResourceId(workspaceResourceId, queryString, timeSpan, formatAsTsv: true);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Queries a Log Analytics workspace given a specific Log Analytics workspace ID. The results are formatted as tab-delimited values.")]
    public async Task<string> QueryLogAnalyticsByWorkspaceId(
        [Description("Log Analytics workspace ID to execute a query against. This is a GUID value specific to a Log Analytics workspace resource. It is sometimes called a customer ID.")] string workspaceId,
        [Description("Kusto (KQL) query for Log Analytics")] string queryString,
        [Description("Optional time span string (e.g. P1D, PT30m) for the range of events to query, from the current time backwards by the given duration. Adds time range restriction in addition to the query itself.")] string? timeSpan)
    {
        return await _appInsightsPlugin.QueryLogAnalyticsByWorkspaceId(workspaceId, queryString, timeSpan, formatAsTsv: true);
    }
}
