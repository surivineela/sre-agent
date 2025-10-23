// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins;

[AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.LogQuery)]
public class MdmMetricsPluginDefinition
{
    private readonly IMdmMetricsPlugin _mdmMetricsPlugin;

    public MdmMetricsPluginDefinition(IMdmMetricsPlugin plugin)
    {
        _mdmMetricsPlugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Gets the list of MDM metric namespaces for a monitoring account from Microsoft Diagnostics Metrics (MDM).")]
    public Task<string> GetNamespacesAsync(
        [Description("MDM monitoring account identifier.")] string monitoringAccount)
    {
        return _mdmMetricsPlugin.GetNamespacesAsync(monitoringAccount);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Gets the list of MDM metric names for a monitoring account and namespace from Microsoft Diagnostics Metrics (MDM).")]
    public Task<string> GetMetricNamesAsync(
        [Description("MDM monitoring account identifier.")] string monitoringAccount,
        [Description("MDM metric namespace identifier.")] string metricNamespace)
    {
        return _mdmMetricsPlugin.GetMetricNamesAsync(monitoringAccount, metricNamespace);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Gets the dimension names for the specified MDM metric in Microsoft Diagnostics Metrics (MDM).")]
    public Task<string> GetDimensionNamesAsync(
        [Description("MDM monitoring account identifier.")] string monitoringAccount,
        [Description("MDM metric namespace identifier.")] string metricNamespace,
        [Description("MDM metric name identifier.")] string metricName)
    {
        return _mdmMetricsPlugin.GetDimensionNamesAsync(monitoringAccount, metricNamespace, metricName);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Gets the MDM metric dimension values for the requested dimension applying the provided filters from Microsoft Diagnostics Metrics (MDM).")]
    public Task<string> GetDimensionValuesAsync(
        [Description("MDM monitoring account identifier.")] string monitoringAccount,
        [Description("MDM metric namespace identifier.")] string metricNamespace,
        [Description("MDM metric name identifier.")] string metricName,
        [Description("MDM dimension name to retrieve values for.")] string dimensionName,
        [Description("JSON array of MDM dimension filter objects. Example: [{\"dimension\":\"Region\",\"values\":[\"NA\"]}].")] string dimensionFiltersJson,
        [Description("Query start time in ISO-8601 UTC format.")] string startTimeUtc,
        [Description("Query end time in ISO-8601 UTC format.")] string endTimeUtc)
    {
        return _mdmMetricsPlugin.GetDimensionValuesAsync(
            monitoringAccount,
            metricNamespace,
            metricName,
            dimensionName,
            dimensionFiltersJson,
            startTimeUtc,
            endTimeUtc);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Gets a list of multiple MDM metric time series, each with multiple sampling types, using the HTTP binary API from Microsoft Diagnostics Metrics (MDM). Requires JSON payload with query parameters.")]
    public Task<string> GetMultipleTimeSeriesAsync(
        [Description("MDM query start time in ISO-8601 UTC format.")] string startTimeUtc,
        [Description("MDM query end time in ISO-8601 UTC format.")] string endTimeUtc,
        [Description("Resolution of the returned series in minutes.")] int seriesResolutionInMinutes,
        [Description("JSON payload with MDM query parameters (sampling types, aggregation, definitions, filters). Example: { 'samplingTypes': ['Average', 'Max'], 'aggregationType': 'Automatic', 'definitions': [{ 'monitoringAccount': 'account', 'metricNamespace': 'namespace', 'metricName': 'metric' }] }")] string requestJson)
    {
        return _mdmMetricsPlugin.GetMultipleTimeSeriesAsync(startTimeUtc, endTimeUtc, seriesResolutionInMinutes, requestJson);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Retrieves MDM metric time series from Microsoft Diagnostics Metrics (MDM) using MetricReader with advanced options including dimension filters and selection clauses.")]
    public Task<string> GetTimeSeriesAsync(
        [Description("MDM monitoring account identifier.")] string monitoringAccount,
        [Description("MDM metric namespace identifier.")] string metricNamespace,
        [Description("MDM metric name identifier.")] string metricName,
        [Description("MDM query start time in ISO-8601 UTC format.")] string startTimeUtc,
        [Description("MDM query end time in ISO-8601 UTC format.")] string endTimeUtc,
        [Description("Resolution of the returned series in minutes (default 5).")] int seriesResolutionInMinutes = 5,
        [Description("Optional JSON payload describing additional MDM query parameters (sampling types, aggregation, dimensions, etc.). Example: { 'samplingTypes': ['Average'], 'aggregationType': 'Automatic', 'dimensionFilters': [{'dimension': 'Region', 'values': ['NA']}], 'outputDimensionNames': ['Region'], 'lastValueMode': false }")] 
            string requestJson = "{ \"samplingTypes\": [\"Average\"], \"aggregationType\": \"Automatic\", \"lastValueMode\": false }")
    {
        return _mdmMetricsPlugin.GetTimeSeriesAsync(
            monitoringAccount,
            metricNamespace,
            metricName,
            startTimeUtc,
            endTimeUtc,
            seriesResolutionInMinutes,
            requestJson);
    }

    [AgentTool(ToolMode.Auto)]
    [Description("Generates an AI-driven analysis of MDM metric time series, highlighting trends, anomalies, and statistical summaries.")]
    public Task<string> GetTimeSeriesAnalysisAsync(
        [Description("MDM monitoring account identifier.")] string monitoringAccount,
        [Description("MDM metric namespace identifier.")] string metricNamespace,
        [Description("MDM metric name identifier.")] string metricName,
        [Description("MDM query start time in ISO-8601 UTC format.")] string startTimeUtc,
        [Description("MDM query end time in ISO-8601 UTC format.")] string endTimeUtc,
        [Description("Resolution of the returned series in minutes (default 5).")] int seriesResolutionInMinutes = 5,
        [Description("Optional JSON payload describing additional MDM query parameters (sampling types, aggregation, dimensions, etc.). Example: { 'samplingTypes': ['Average'], 'aggregationType': 'Automatic', 'dimensionFilters': [{'dimension': 'Region', 'values': ['NA']}], 'outputDimensionNames': ['Region'], 'lastValueMode': false }")] 
            string requestJson = "{ \"samplingTypes\": [\"Average\"], \"aggregationType\": \"Automatic\", \"lastValueMode\": false }",
        [Description("Optional contextual query to guide the analysis focus.")] string? contextualQuery = null)
    {
        return _mdmMetricsPlugin.GetTimeSeriesAnalysisAsync(
            monitoringAccount,
            metricNamespace,
            metricName,
            startTimeUtc,
            endTimeUtc,
            seriesResolutionInMinutes,
            requestJson,
            contextualQuery);
    }
}
