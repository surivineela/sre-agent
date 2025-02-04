using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OperationalAgentCore;

public class MonitorPlugin
{
    [KernelFunction("start_monitor_appservice")]
    [Description("To start monitoring an app service periodically in the background and notify user once found unhealthy or security holes.")]
    public MonitorStartResult StartMonitor(
        Kernel kernel,
        [Description("The resource ID of the app service resource.")]
        string resourceId,
        [Description("The interval/frequency in seconds between each healthiness inspection.")]
        int intervalInSeconds)
    {
        var started = PeriodicMonitor.Start(
            kernel,
            resourceId,
            TimeSpan.FromSeconds(intervalInSeconds),
            out var info);
        return new MonitorStartResult(
            Status: started
            ? "New monitor is started"
            : "Monitor for this resource was already started",
            Info: info);
    }

    [KernelFunction("update_monitor_appservice_interval")]
    [Description("To update the periodic execution interval of an existing monitor, this function can also restart a stopped monitor.")]
    public string UpdateMonitorInterval(
        [Description("The resource ID of the app service resource.")]
        string resourceId,
        [Description("The interval/frequency in seconds between each healthiness inspection.")]
        int intervalInSeconds)
    {
        var info = PeriodicMonitor.UpdateFrequency(
            resourceId,
            TimeSpan.FromSeconds(intervalInSeconds));
        return info is null
            ? "Monitor not exists"
            : "Successfully updated periodic execution interval";
    }

    [KernelFunction("stop_monitor_appservice")]
    [Description("To stop monitoring an app service periodically in the background.")]
    public string StopMonitor(
        [Description("The resource ID of the app service resource.")]
        string resourceId)
    {
        var info = PeriodicMonitor.UpdateFrequency(
            resourceId,
            TimeSpan.MaxValue);
        return info is null
            ? "Monitor not started"
            : "Successfully stopped periodic execution";
    }

    [KernelFunction("get_monitor_appservice")]
    [Description("To get the monitor info, including last execution time, result, and execution interval. Returns null if not found")]
    public PeriodicMonitorInfo? GetMonitorInfo(
        [Description("The resource ID of the app service resource.")]
        string resourceId)
    {
        return PeriodicMonitor.Get(
            resourceId);
    }

    [KernelFunction("summarize_monitor_appservice")]
    [Description("Summarize past activity of the monitor, returns null if moonitor is not found.")]
    public async Task<string?> GetMonitorInfo(
        Kernel kernel,
        [Description("The resource ID of the app service resource.")]
        string resourceId,
        [Description("The prompt of the summary info, it should describe what kind of summary user wants")]
        string userPrompt)
    {
        return await PeriodicMonitor.Summarize(
            kernel,
            resourceId,
            userPrompt);
    }
}

public sealed record MonitorStartResult(
    string Status,
    PeriodicMonitorInfo Info);
