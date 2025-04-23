// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Definitions;

public interface IDotnetAnalysisPlugin
{
    public Task<string> GetCPUAnalysis(string profilePath);
    public Task<string> GetGCCPUAnalysis(string profilePath);
    public Task<string> GetThreadpoolStarvationAnalysis(string profilePath);
    public Task<string> GetMemoryAnalysis(string dumpPath);
    public Task<string> GetDeadlockAnalysis(string dumpPath);
}
