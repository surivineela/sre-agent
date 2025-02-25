// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Agent.Plugins.PeriodicMonitor;
using Microsoft.SemanticKernel;

namespace Agent.Plugins;

public class MonitorPlugin : IMonitorPlugin
{
    private readonly IPeriodicMonitor _periodicMonitor;

    public MonitorPlugin(IPeriodicMonitor periodicMonitor)
    {
        _periodicMonitor = periodicMonitor;
    }

    public MonitorStartResult StartMonitor(
        Kernel kernel,
        string resourceId,
        int intervalInSeconds)
    {
        var started = _periodicMonitor.Start(
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

    public string UpdateMonitorInterval(
        string resourceId,
        int intervalInSeconds)
    {
        var info = _periodicMonitor.UpdateFrequency(
            resourceId,
            TimeSpan.FromSeconds(intervalInSeconds));
        return info is null
            ? "Monitor not exists"
            : "Successfully updated periodic execution interval";
    }

    public string StopMonitor(string resourceId)
    {
        var info = _periodicMonitor.UpdateFrequency(
            resourceId,
            TimeSpan.MaxValue);
        return info is null
            ? "Monitor not started"
            : "Successfully stopped periodic execution";
    }

    public PeriodicMonitorInfo? GetMonitorInfo(string resourceId)
    {
        return _periodicMonitor.Get(
            resourceId);
    }

    public async Task<string?> SummarizeMonitorActivity(
        Kernel kernel,
        string resourceId,
        string userPrompt)
    {
        return await _periodicMonitor.Summarize(
            kernel,
            resourceId,
            userPrompt);
    }
}
