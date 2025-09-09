// ------------------------------------------------------------
//  Test helper for in-memory IcmScanner queue (local only)
// ------------------------------------------------------------
using System.Collections.Concurrent;

namespace Agent.Runtime.SubAgents.IcmScanner;

/// <summary>
/// In-memory queue to simplify local/manual testing of IcmScanner without full scan flow.
/// Enabled ONLY when env var SREAGENT_ENABLE_ICMSCANNER_TEST_QUEUE is set to true/1/yes.
/// Process-local, non-persistent, safe to remove.
/// </summary>
public static class IcmScannerTestQueueHelper
{
    private const string FlagName = "SREAGENT_ENABLE_ICMSCANNER_TEST_QUEUE";
    private static readonly ConcurrentQueue<IcmScannerTestItem> _queue = new();

    public sealed record IcmScannerTestItem(string IncidentId, string? OwningTeamId, bool ForceTeamSpecific);

    public static bool IsEnabled()
    {
        var v = Environment.GetEnvironmentVariable(FlagName);
        if (string.IsNullOrWhiteSpace(v)) return false;
        return v.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static bool Enqueue(string incidentId, string? owningTeamId, bool forceTeamSpecific)
    {
        if (!IsEnabled()) return false;
        if (string.IsNullOrWhiteSpace(incidentId)) return false;
        _queue.Enqueue(new IcmScannerTestItem(incidentId.Trim(), string.IsNullOrWhiteSpace(owningTeamId) ? null : owningTeamId.Trim(), forceTeamSpecific));
        return true;
    }

    public static List<IcmScannerTestItem> Drain()
    {
        var list = new List<IcmScannerTestItem>();
        if (!IsEnabled()) return list;
        while (_queue.TryDequeue(out var item))
        {
            list.Add(item);
        }
        return list;
    }

    public static int Count => _queue.Count;
}
