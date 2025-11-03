// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Combined analysis result
    /// </summary>
    public sealed record MetricsAnalysisResult(
        string DirectAnalysis,
        string StatisticalAnalysis,
        string VisualizationAnalysis,
        string CombinedAnalysis);
}
