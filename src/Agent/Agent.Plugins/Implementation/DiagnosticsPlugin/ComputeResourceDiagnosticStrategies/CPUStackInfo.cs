using System.Text.Json.Serialization;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

public sealed class FunctionNode
{
    [JsonPropertyName("FunctionName")]
    public string? FunctionName { get; set; }

    [JsonPropertyName("TimeSpent")]
    public double? TimeSpent { get; set; }

    [JsonPropertyName("ExclusiveTime")]
    public double? ExclusiveTime { get; set; }

    [JsonPropertyName("InclusiveMetricPercent")]
    public double? InclusiveMetricPercent { get; set; }

    [JsonPropertyName("childNodes")]
    public List<FunctionNode> ChildNodes { get; set; } = new();
}

public static class CPUFunctionAnalyzer
{
    private static readonly string[] SystemPrefixes =
    {
        "coreclr!", "ntdll!", "kernel32!", "kernelbase", "ntoskrnl!", "System.", "Microsoft.", "clr!", "ROOT",
        "system.private.corelib.il!", "system.threading.tasks.parallel.il!", "Thread (", "BROKEN"
    };

    public static void TraverseTree(FunctionNode node, List<FunctionNode> collection)
    {
        if (node == null) return;

        collection.Add(node);

        foreach (var child in node.ChildNodes)
            TraverseTree(child, collection);
    }

    // New method to aggregate nodes by function name
    public static Dictionary<string, FunctionNode> AggregateByFunctionName(List<FunctionNode> nodes)
    {
        var aggregatedFunctions = new Dictionary<string, FunctionNode>();

        foreach (var node in nodes)
        {
            if (string.IsNullOrEmpty(node.FunctionName)) continue;

            if (aggregatedFunctions.TryGetValue(node.FunctionName, out var existing))
            {
                // Accumulate metrics
                existing.TimeSpent = (existing.TimeSpent ?? 0) + (node.TimeSpent ?? 0);
                existing.ExclusiveTime = (existing.ExclusiveTime ?? 0) + (node.ExclusiveTime ?? 0);
                // Don't double count percentages, take the max instead
                existing.InclusiveMetricPercent = Math.Max(existing.InclusiveMetricPercent ?? 0, node.InclusiveMetricPercent ?? 0);
            }
            else
            {
                // Create a new aggregated node
                aggregatedFunctions[node.FunctionName] = new FunctionNode
                {
                    FunctionName = node.FunctionName,
                    TimeSpent = node.TimeSpent,
                    ExclusiveTime = node.ExclusiveTime,
                    InclusiveMetricPercent = node.InclusiveMetricPercent
                };
            }
        }

        return aggregatedFunctions;
    }

    public static List<FunctionNode> GetTopInclusiveMethods(List<FunctionNode> nodes, int count)
    {
        var aggregated = AggregateByFunctionName(nodes);
        return aggregated.Values
            .Where(n => n != null && n.InclusiveMetricPercent.HasValue)
            .OrderByDescending(n => n.InclusiveMetricPercent!.Value)
            .Take(count)
            .ToList();
    }

    public static List<FunctionNode> GetTopExclusiveMethods(List<FunctionNode> nodes, int count)
    {
        var aggregated = AggregateByFunctionName(nodes);
        return aggregated.Values
            .OrderByDescending(n => n.ExclusiveTime)
            .Take(count)
            .ToList();
    }

    public static List<FunctionNode> GetUserMethods(List<FunctionNode> nodes)
    {
        var aggregated = AggregateByFunctionName(nodes);
        return aggregated.Values
            .Where(n => !string.IsNullOrEmpty(n.FunctionName) &&
                        !IsSystemMethod(n.FunctionName) &&
                        !n.FunctionName.StartsWith("Process64", StringComparison.Ordinal))
            .ToList();
    }

    public static bool IsSystemMethod(string name)
    {
        return SystemPrefixes.Any(prefix => name.Contains(prefix, StringComparison.OrdinalIgnoreCase));
    }

    public static string PrintSummary(FunctionNode node)
        => $"- {node.FunctionName}\n  TimeSpent: {node.TimeSpent}, Exclusive: {node.ExclusiveTime}, Inclusive %: {node.InclusiveMetricPercent?.ToString("F3") ?? "N/A"}";

}
