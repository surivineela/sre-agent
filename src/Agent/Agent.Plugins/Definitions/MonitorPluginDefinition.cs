using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Core.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public class MonitorPluginDefinition
    {
        private readonly IMonitorPlugin _monitorPlugin;

        public MonitorPluginDefinition(IMonitorPlugin monitorPlugin)
        {
            _monitorPlugin = monitorPlugin;
        }

        [KernelFunction("start_monitor_appservice")]
        [Description("To start monitoring an app service periodically in the background and notify user once found unhealthy or security holes.")]
        public MonitorStartResult StartMonitor(
            Kernel kernel,
            [Description("The resource ID of the app service resource.")]
            string resourceId,
            [Description("The interval/frequency in seconds between each healthiness inspection.")]
            int intervalInSeconds)
        {
            return _monitorPlugin.StartMonitor(
                kernel: kernel,
                resourceId: resourceId,
                intervalInSeconds: intervalInSeconds);
        }

        [KernelFunction("update_monitor_appservice_interval")]
        [Description("To update the periodic execution interval of an existing monitor, this function can also restart a stopped monitor.")]
        public string UpdateMonitorInterval(
            [Description("The resource ID of the app service resource.")]
            string resourceId,
            [Description("The interval/frequency in seconds between each healthiness inspection.")]
            int intervalInSeconds)
        {
            return _monitorPlugin.UpdateMonitorInterval(
                resourceId: resourceId,
                intervalInSeconds: intervalInSeconds);
        }

        [KernelFunction("stop_monitor_appservice")]
        [Description("To stop monitoring an app service periodically in the background.")]
        public string StopMonitor(
            [Description("The resource ID of the app service resource.")]
            string resourceId)
        {
            return _monitorPlugin.StopMonitor(resourceId);
        }

        [KernelFunction("get_monitor_appservice")]
        [Description("To get the monitor info, including last execution time, result, and execution interval. Returns null if not found")]
        public PeriodicMonitorInfo? GetMonitorInfo(
            [Description("The resource ID of the app service resource.")]
            string resourceId)
        {
            return _monitorPlugin.GetMonitorInfo(resourceId);
        }

        [KernelFunction("summarize_monitor_appservice")]
        [Description("Summarize past activity of the monitor, returns null if moonitor is not found.")]
        public async Task<string?> SummarizeMonitorActivity(
            Kernel kernel,
            [Description("The resource ID of the app service resource.")]
            string resourceId,
            [Description("The prompt of the summary info, it should describe what kind of summary user wants")]
            string userPrompt)
        {
            return await _monitorPlugin.SummarizeMonitorActivity(
                kernel: kernel,
                resourceId: resourceId,
                userPrompt: userPrompt);
        }
    }
} 