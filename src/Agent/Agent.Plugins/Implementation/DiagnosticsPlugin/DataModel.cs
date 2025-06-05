using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin;

internal record ComputeResourceInfo(
    ComputeResourceType ResourceType,
    OSType OsType,
    Architecture Architecture,
    LanguageStack LanguageStack,
    bool Is32Bit
);

internal enum ComputeResourceType
{
    ContainerApp,
    AppService,
    KubernetesService,
    FunctionApp,
    Unknown,
}

internal enum OSType
{
    Linux,
    Windows,
    Unknown,
}

internal enum Architecture
{
    x86,
    x64,
    ARM64,
    Unknown,
}

internal enum LanguageStack
{
    Dotnet,
    // Java, Go etc.
    Unknown
}

// Strategy interface for resource-specific analyzers
internal interface IComputeResourceDiagnosticStrategy
{
    bool CanHandle(ComputeResourceInfo resourceInfo);
    Task<string> PerformAnalysisAsync(string resourceId, ComputeResourceInfo resourceInfo, AnalysisType analysisType, string additionalProperties);
}

internal abstract class ComputeResourceDiagnosticStrategyBase : IComputeResourceDiagnosticStrategy
{
    protected readonly ILogger<DiagnosticsPlugin> _logger;
    protected IDictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, string, Task<string>>> _analysisHandlers;

    public ComputeResourceDiagnosticStrategyBase(ILogger<DiagnosticsPlugin> logger)
    {
        _logger = logger;
    }

    public abstract bool CanHandle(ComputeResourceInfo resourceInfo);
    public Task<string> PerformAnalysisAsync(string resourceId, ComputeResourceInfo resourceInfo, AnalysisType analysisType, string additionalProperties)
    {
        if (_analysisHandlers.TryGetValue(analysisType, out var handler))
        {
            return handler(resourceId, resourceInfo, analysisType, additionalProperties);
        }

        else
        {
            string errorMessage = $"No analysis handler found for the specified analysis type: {analysisType} for resource id: {resourceId}.";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage);
        }
    }
}
