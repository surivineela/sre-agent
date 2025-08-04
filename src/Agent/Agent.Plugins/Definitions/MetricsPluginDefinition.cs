// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Attributes;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins;

// [Export]
[AgentToolPlugin(Category = ToolCategories.Monitoring)]
public class MetricsPluginDefinition
{
    private readonly IMetricsPlugin _metricsPlugin;

    public MetricsPluginDefinition(IMetricsPlugin metricsPlugin)
    {
        _metricsPlugin = metricsPlugin;
    }

    [Submit202(ExecuteMethodName = nameof(GetWebAppCpuMetrics))]
    [KernelFunction("start_get_webapp_cpu_metrics")]
    [Description("Start a background task to get the average CPU utilization metrics of a specific WebApp instance at per minute granularity" +
        " for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy")]
    public string StartGetWebAppCpuMetrics(
        [Description("The resource ID of the WebApp resource.")] string resourceId)
    {
        return $"The operation to get cpu metrics has been started for WebApp: {resourceId}";
    }

    [KernelFunction("get_webapp_cpu_metrics")]
    [Description("Get the average CPU utilization metrics of a specific WebApp instance at per minute granularity" +
                 " for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy")]
    [AgentTool(ToolMode.Auto)]
    public async Task<IReadOnlyList<CpuTimeSeriesData>> GetWebAppCpuMetrics(
        [Description("The resource ID of the WebApp resource.")] string resourceId)
    {
        return await _metricsPlugin.GetWebAppCpuMetrics(resourceId);
    }

    [KernelFunction("get_success_request_volume")]
    [Description("Get the 2XX request volume of a specific resource at per minute granularity")]
    [AgentTool(ToolMode.Auto)]
    public async Task<IReadOnlyList<SuccessfulRequestVolumeTimeSeriesData>> GetSuccessfulRequestVolumeAsync(
        [Description("The resource ID of the WebApp resource.")] string resourceId)
    {
        return await _metricsPlugin.GetSuccessfulRequestVolumeAsync(resourceId);
    }

    [KernelFunction("get_functionapp_request_availability")]
    [Description("Get the request availability of a specific FunctionApp (DO NOT CALL FOR FLEX or CONSUMPTION SKU) at per minute granularity" +
    " for the past 30 minutes, FunctionApp is healthy if all data points are at least 99.9 availability")]
    [AgentTool(ToolMode.Auto)]
    public async Task<IReadOnlyList<RequestAvailabilitySeriesData>> GetFunctionAppRequestAvailability(
        [Description("The resource ID of the FunctionApp resource.")] string resourceId)
    {
        return await _metricsPlugin.GetFunctionAppRequestAvailability(resourceId);
    }

    [KernelFunction("get_webapp_and_functionapp_memory_metrics")]
    [Description("Get the average memory utilization metrics of a specific WebApp or FunctionApp instance at per minute granularity" +
    " for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% memory utilization.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<IReadOnlyList<MemoryTimeSeriesData>> GetMemoryMetrics(
        [Description("The resource ID of the WebApp or FunctionApp resource.")] string resourceId)
    {
        return await _metricsPlugin.GetMemoryMetrics(resourceId);
    }

    [Submit202(ExecuteMethodName = nameof(GetMemoryMetrics))]
    [KernelFunction("start_get_webapp_and_functionapp_memory_metrics")]
    [Description("Start a background operation to get the average memory utilization metrics of a specific WebApp or FunctionApp instance at per minute granularity" +
        " for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% memory utilization.")]
    public string StartGetMemoryMetrics(
        [Description("The resource ID of the WebApp or FunctionApp resource.")] string resourceId)
    {
        return $"The operation to get cpu metrics has been started for AppService: {resourceId}";
    }

    [KernelFunction("get_webapp_thread_metrics")]
    [Description("Get the average thread count metrics of a web app")]
    [AgentTool(ToolMode.Auto)]
    public async Task<IReadOnlyList<ThreadTimeSeriesData>> GetThreadMetrics(
        [Description("The resource ID of the web app service resource.")] string resourceId)
    {
        return await _metricsPlugin.GetThreadMetrics(resourceId);
    }
}
