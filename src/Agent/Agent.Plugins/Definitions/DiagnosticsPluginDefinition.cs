using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(Category = ToolCategories.Diagnostics)]
public sealed class DiagnosticsPluginDefinition
{
    private readonly IDiagnosticsPlugin _diagnosticsPlugin;

    public DiagnosticsPluginDefinition(IDiagnosticsPlugin diagnosticsPlugin)
    {
        _diagnosticsPlugin = diagnosticsPlugin;
    }

    [Description("Gets the analysis for a particular compute resource based on a particular resourceId and analysis type.")]
    public async Task<string> GetAnalysisAsync([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                               [Description("The type of analysis to be conducted that could be: Memory, CPU and Threadpool Starvation.")] AnalysisType analysisType,
                                               [Description("Required for AKS resources. Provide semicolon separated key-value pairs using '=' format: 'namespace=namespaceName;pod=podName;container=containerName'. Example: 'namespace=default;pod=my-pod;container=my-container'")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetAnalysisAsync(resourceId, analysisType, additionalProperties);
    }

    [Description("Gets the latency analysis for a particular compute resource based on a particular resourceId and analysis type.")]
    public async Task<string> GetLatencyAnalysis([Description("The resource Id.")] string resourceId,
                                                 [Description("Required for AKS resources. Provide semicolon separated key-value pairs using '=' format: 'namespace=namespaceName;pod=podName;container=containerName'. Example: 'namespace=default;pod=my-pod;container=my-container'")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetAnalysisAsync(resourceId, AnalysisType.Latency, additionalProperties);
    }

    [Description("Gets the CPU analysis for a particular compute resource for high cpu situations or situations with cpu spikes or can be independently asked for by the user. " +
        "Example 1: 'My app's CPU is extremely high - analyze to see what's going on' " +
        "Example 2: 'My app is experiencing 500s and I see a spike in CPU. Help me figure out what's going on'" +
        "Example 3: 'My app is down and I see a spike in CPU. Help me figure out what's going on' " +
        "Keywords: Deep Diagnostic CPU, High CPU, CPU Analysis." )]
    public async Task<string> GetCPUAnalysis([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                             [Description("Required for AKS resources. Provide semicolon separated key-value pairs using '=' format: 'namespace=namespaceName;pod=podName;container=containerName'. Example: 'namespace=default;pod=my-pod;container=my-container'")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetCPUAnalysisAsync(resourceId, additionalProperties);
    }

    [Description("Gets the Memory analysis for a particular compute resource for high memory situations or situations with memory spikes or can be independently asked for by the user. " +
        "Example 1: 'My app's Memory is extremely high - analyze to see what's going on' " +
        "Example 2: 'My app is experiencing 500s and I see a spike in Memory. Help me figure out what's going on'" +
        "Example 3: 'My app is down and I see a spike in Memory. Help me figure out what's going on'" +
        "Keywords: Deep Diagnostics Memory, High Memory, Memory Analysis." )]
    public async Task<string> GetMemoryAnalysis([Description("The full Azure resource ID of the resource to diagnose (format: /subscriptions/{subId}/resourceGroups/{rgName}/providers/{resourceProvider}/{resourcetype}/{resourceName}).")] string resourceId,
                                                [Description("Required for AKS resources. Provide semicolon separated key-value pairs using '=' format: 'namespace=namespaceName;pod=podName;container=containerName'. Example: 'namespace=default;pod=my-pod;container=my-container'")] string additionalProperties)
    {
        return await _diagnosticsPlugin.GetMemoryAnalysisAsync(resourceId, additionalProperties);
    }
}
