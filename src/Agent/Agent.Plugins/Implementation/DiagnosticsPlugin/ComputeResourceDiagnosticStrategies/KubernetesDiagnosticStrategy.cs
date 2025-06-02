using Agent.Plugins.Definitions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation.DiagnosticsPlugin.ComputeResourceDiagnosticStrategies;

internal sealed class KubernetesDiagnosticStrategy : ComputeResourceDiagnosticStrategyBase
{
    private readonly IDictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, Task<string>>> _analysisHandlers;

    public KubernetesDiagnosticStrategy(ILogger<DiagnosticsPlugin> logger)
        : base(logger)
    {
        _analysisHandlers = new Dictionary<AnalysisType, Func<string, ComputeResourceInfo, AnalysisType, Task<string>>>
        {
            // Register handlers.
        };
    }

    public override bool CanHandle(ComputeResourceInfo resourceInfo)
        => resourceInfo.ResourceType == ComputeResourceType.KubernetesService;

    public Task<string> PerformAnalysisAsync(string resourceId, ComputeResourceInfo resourceInfo, AnalysisType analysisType)
    {
        throw new NotImplementedException();
    }
}
