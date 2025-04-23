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
    public async Task<string> GetCPUAnalysis(string profilePath)
    {
        return await _dotnetAnalysisPlugin.GetCPUAnalysis(profilePath);
    }

    [KernelFunction("get_gc_cpu_analysis")]
    [Description("Gets the GC CPU analysis for a given profile path")]
    public async Task<string> GetGCCPUAnalysis(string profilePath)
    {
        return await _dotnetAnalysisPlugin.GetGCCPUAnalysis(profilePath);
    }

    [KernelFunction("get_memory_analysis")]
    [Description("Gets the memory analysis for the given dump file")] 
    public async Task<string> GetMemoryAnalysis(string dumpPath)
    {
        return await _dotnetAnalysisPlugin.GetMemoryAnalysis(dumpPath);
    }
}
