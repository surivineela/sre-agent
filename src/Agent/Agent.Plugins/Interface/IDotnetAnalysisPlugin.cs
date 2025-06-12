// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface;

public interface IDotnetAnalysisPlugin
{
    public Task<bool> ShouldTriggerMemoryDump(string resourceId, double spikeThreshold, double endWindowFraction, double sustainedDropLength);
    public Task<string> GetCPUAnalysis(string profilePath, int pid);
    public Task<string> GetGCCPUAnalysis(string profilePath, int pid);
    public Task<string> GetThreadpoolStarvationAnalysis(string profilePath, int pid);
    public Task<string> GetMemoryAnalysis(string resourceId, string dumpPath);
    public Task<string> GetDeadlockAnalysis(string dumpPath);
}
