using System.ComponentModel;
using Agent.Framework;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin]
public sealed class DiagnosticsPluginDefinition
{
    private readonly IDiagnosticsPlugin _diagnosticsPlugin;

    public DiagnosticsPluginDefinition(IDiagnosticsPlugin diagnosticsPlugin)
    {
        _diagnosticsPlugin = diagnosticsPlugin;
    }

    [Description("Gets the compute resource details for a particular compute resource based on a particular resourceId including OS, Resource Type, Language Stack etc.")]
    public async Task<string> GetComputeResourceDetailsAsync([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                                             [Description("Required for AKS resources. Provide semicolon separated key-value pairs: 'namespace':'namespaceName', 'pod':'podName', 'container':'containerName'.")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetComputeResourceDetailsAsync(resourceId, additionalProperties);
    }

    [Description("Gets the diagnostic analysis for a particular compute resource based on a particular resourceId and analysis type.")]
    public async Task<string> GetAnalysisAsync([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                               [Description("The type of analysis to be conducted that could be: Memory, CPU and Threadpool Starvation.")] AnalysisType analysisType,
                                               [Description("Required for AKS resources. Provide semicolon separated key-value pairs: 'namespace':'namespaceName', 'pod':'podName', 'container':'containerName'.")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetAnalysisAsync(resourceId, analysisType, additionalProperties);
    }

    [Description("Gets the diagnostic latency analysis for a particular compute resource based on a particular resourceId and analysis type.")]
    public async Task<string> GetLatencyAnalysis([Description("The resource Id.")] string resourceId,
                                               [Description("Additional properties for the analysis such as for AKS' resource identification.")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetAnalysisAsync(resourceId, AnalysisType.Latency, additionalProperties);
    }

    [Description("Gets the CPU diagnostic analysis for a particular compute resource for high cpu situations or situations with cpu spikes.")]
    public async Task<string> GetCPUAnalysis([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                             [Description("Required for AKS resources. Provide semicolon separated key-value pairs: 'namespace' (pod namespace), 'pod' (pod name), 'container' (container name within the pod).")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetCPUAnalysisAsync(resourceId, additionalProperties);
    }

    [Description("Gets the Memory diagnostic analysis for a particular compute resource for High Memory situations and memory spikes.")]
    public async Task<string> GetMemoryAnalysis([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                                [Description("Required for AKS resources. Provide semicolon separated key-value pairs: 'namespace' (pod namespace), 'pod' (pod name), 'container' (container name within the pod).")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetMemoryAnalysisAsync(resourceId, additionalProperties);
    }
}
