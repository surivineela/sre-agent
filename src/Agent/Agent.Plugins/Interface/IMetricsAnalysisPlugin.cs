// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Plugin for comprehensive metrics analysis
    /// </summary>
    public interface IMetricsAnalysisPlugin
    {
        /// <summary>
        /// Generates dimension filters using LLM based on symptoms, metric name, and available dimensions
        /// </summary>
        /// <param name="symptoms">Description of the symptoms being investigated</param>
        /// <param name="resourceDetails">Details of the impacted resource including resource id, region, and other properties</param>
        /// <param name="metricName">Name of the metric to generate filters for</param>
        /// <param name="dimensions">Array of available dimension names for the metric</param>
        /// <returns>Array of suggested dimension filters</returns>
        Task<DimensionFilter[]> GenerateFiltersAsync(
            string symptoms,
            string resourceDetails,
            string metricName,
            string[] dimensions);

        /// <summary>
        /// Analyzes metrics data using multiple approaches: direct LLM, statistical/ML, and visualization
        /// </summary>
        /// <param name="symptoms">Description of the symptoms being investigated</param>
        /// <param name="timeSeries">Array of time series data to analyze</param>
        /// <returns>Comprehensive analysis result with factual observations</returns>
        Task<MetricsAnalysisResult> AnalyzeMetricsAsync(
            string symptoms,
            TimeSeries[] timeSeries);
    }
}
