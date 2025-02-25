using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    public interface IMonitorPlugin
    {
        MonitorStartResult StartMonitor(
           Kernel kernel,
           string resourceId,
           int intervalInSeconds);

        string UpdateMonitorInterval(
            string resourceId,
            int intervalInSeconds);

        string StopMonitor(string resourceId);

        PeriodicMonitorInfo? GetMonitorInfo(string resourceId);

        Task<string?> SummarizeMonitorActivity(
            Kernel kernel,
            string resourceId,
            string userPrompt);
    }
}
