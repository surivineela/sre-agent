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
    Task<string> GetAnalysisAsync(string resourceId, AnalysisType analysisType, string additionalProperties);
    Task<string> GetCPUAnalysisAsync(string resourceId, string additionalProperties);
    Task<string> GetMemoryAnalysisAsync(string resourceId, string additionalProperties);
}
