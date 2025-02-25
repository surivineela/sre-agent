using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.PeriodicMonitor
{
    public interface IPeriodicMonitor
    {
        PeriodicMonitorInfo? Get(string resourceId);
        Task<string?> Summarize(Kernel kernel, string resourceId, string userPrompt);
        bool Start(Kernel kernel, string resourceId, TimeSpan interval, out PeriodicMonitorInfo info);
        PeriodicMonitorInfo? UpdateFrequency(string resourceId, TimeSpan interval);
    }
}
