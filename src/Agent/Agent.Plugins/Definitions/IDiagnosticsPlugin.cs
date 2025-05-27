namespace Agent.Plugins.Definitions;

public enum AnalysisType
{
    Memory,
    Cpu,
    ThreadpoolStarvation,
    Unknown
}

public interface IDiagnosticsPlugin
{
    Task<string> GetAnalysisAsync(string resourceId, AnalysisType analysisType, IReadOnlyDictionary<string, string> additionalProperties);
}
