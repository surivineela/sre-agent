using DotnetAnalyzer.LatencyHelpers;

namespace DotnetAnalyzer;

internal static class AnalyzeLatencyCommand
{
    public static async Task<string> AnalyzeLatencyAsync(string artifactPath)
    {
        // Assumption: dotnet-trace is installed and available in the PATH.

        // 1. Detect deadlocks.
        string deadlockResult = await Deadlock.DetectAndDiagnoseDeadLock(artifactPath);

        // 2. TODO: Detect sync over async issues.

        return $"Deadlock Result: {deadlockResult}";
    }
}
