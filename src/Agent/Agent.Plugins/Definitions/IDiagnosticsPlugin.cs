namespace Agent.Plugins.Definitions;

public enum AnalysisType
{
    Memory,
    Cpu,
    ThreadpoolStarvation,
    General,
    Unknown
}

public interface IDiagnosticsPlugin
{
    Task<string> GetAnalysisAsync(string resourceId, AnalysisType analysisType, IReadOnlyDictionary<string, string> additionalProperties);
    Task<string> GetCPUAnalysisAsync(string resourceId, IReadOnlyDictionary<string, string> additionalProperties);
    Task<string> GetMemoryAnalysisAsync(string resourceId, IReadOnlyDictionary<string, string> additionalProperties);
}
