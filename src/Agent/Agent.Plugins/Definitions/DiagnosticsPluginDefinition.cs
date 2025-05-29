using System.ComponentModel;
using Agent.Framework;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public sealed class DiagnosticsPluginDefinition
{
    private readonly IDiagnosticsPlugin _diagnosticsPlugin;

    public DiagnosticsPluginDefinition(IDiagnosticsPlugin diagnosticsPlugin)
    {
        _diagnosticsPlugin = diagnosticsPlugin;
    }

    [KernelFunction("get_analysis")]
    [Description("Gets the diagnostic analysis for a particular compute resource based on a particular resourceId and analysis type.")]
    public async Task<string> GetAnalysisAsync([Description("The resource Id.")] string resourceId,
                                               [Description("The type of analysis to be conducted that could be: Memory, CPU and Threadpool Starvation.")] AnalysisType analysisType,
                                               [Description("Additional properties for the analysis such as for AKS' resource identification.")] IReadOnlyDictionary<string, string> additionalProperties)
    {
        return await _diagnosticsPlugin.GetAnalysisAsync(resourceId, analysisType, additionalProperties);
    }

    [KernelFunction("get_cpu_analysis")]
    [Description("Gets the CPU diagnostic analysis for a particular compute resource.")]
    public async Task<string> GetCPUAnalysis([Description("The resource Id.")] string resourceId)
    {
        return await _diagnosticsPlugin.GetAnalysisAsync(resourceId, AnalysisType.Cpu, new Dictionary<string, string>());
    }
}
