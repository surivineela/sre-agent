namespace Agent.Plugins.Models
{
    public sealed record MonitorStartResult(
        string Status,
        PeriodicMonitorInfo Info);
}
