namespace Agent.Plugins.Models
{
    public sealed record PeriodicMonitorInfo(
        string ResourceId,
        TimeSpan MonitorInterval,
        bool? LastCheckWasHealthy,
        DateTime? LastExecution);
}
