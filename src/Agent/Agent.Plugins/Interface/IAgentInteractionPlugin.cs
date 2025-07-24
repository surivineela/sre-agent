// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Interface
{
    public interface IAgentInteractionPlugin
    {
        Task<string> ShareAgentResultAsync(string calledAgentName, string analysisSummary, string? context = null, int resultSummaryLimit = 4096);
    }
}
