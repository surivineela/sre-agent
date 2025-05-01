// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Definitions;

public interface IDotnetAnalysisPlugin
{
    public Task<string> GetCPUAnalysis(string profilePath, int pid);
    public Task<string> GetGCCPUAnalysis(string profilePath, int pid);
    public Task<string> GetThreadpoolStarvationAnalysis(string profilePath, int pid);
    public Task<string> GetMemoryAnalysis(string resourceId, string dumpPath);
    public Task<string> GetDeadlockAnalysis(string dumpPath);
}
