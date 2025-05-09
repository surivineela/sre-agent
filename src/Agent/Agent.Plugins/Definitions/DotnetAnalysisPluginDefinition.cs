// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions;

public sealed class DotnetAnalysisPluginDefinition
{
    private readonly IDotnetAnalysisPlugin _dotnetAnalysisPlugin;

    public DotnetAnalysisPluginDefinition(IDotnetAnalysisPlugin dotnetAnalysisPlugin)
    {
        _dotnetAnalysisPlugin = dotnetAnalysisPlugin;
    }

    [KernelFunction("get_cpu_analysis")]
    [Description("Gets the CPU analysis for a given profile path")]
    public async Task<string> GetCPUAnalysis([Description("Path to the profile")] string profilePath,
                                             [Description("Process Id of the process")] int pid)
    {
        return await _dotnetAnalysisPlugin.GetCPUAnalysis(profilePath, pid);
    }

    [KernelFunction("get_gc_cpu_analysis")]
    [Description("Gets the GC CPU analysis for a given profile path")]
    public async Task<string> GetGCCPUAnalysis(string profilePath, int pid)
    {
        return await _dotnetAnalysisPlugin.GetGCCPUAnalysis(profilePath, pid);
    }

    [KernelFunction("get_memory_analysis")]
    [Description("Gets the memory analysis for the given dump file")] 
    public async Task<string> GetMemoryAnalysis(string resourceId, string dumpPath)
    {
        return await _dotnetAnalysisPlugin.GetMemoryAnalysis(resourceId, dumpPath);
    }

    [KernelFunction("should_trigger_memory_dump")]
    [Description("Decides if a memory dump should be triggered based on the slope of the memory time series data.")] 
    public async Task<bool> ShouldTriggerMemoryDump(string resourceId)
    {
        return await _dotnetAnalysisPlugin.ShouldTriggerMemoryDump(resourceId, 0.2, 0.1, 3);
    }

}
