// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Interface for Azure Application Insights analysis and monitoring operations.
    /// Provides methods for analyzing telemetry data, performance metrics, exceptions, and application health.
    /// </summary>
    public interface IAzureApplicationInsightsPlugin
    {
        /// <summary>
        /// The current thread context for the plugin, used to identify the conversation thread when sending messages or images.
        /// </summary>
        Guid? ThreadId { get; set; }

        Task<AppCorrelateTimeResult[]> CorrelateTimeSeries(
            string resourceId,
            List<AppCorrelateDataSet> dataSets,
            DateTime startTime,
            DateTime endTime);

        Task<DistributedTraceResult> GetDistributedTrace(
            string resourceId,
            string traceId,
            string? spanId,
            DateTime startTime,
            DateTime endTime);

        Task<AppListTraceResult> ListDistributedTraces(
            string resourceId,
            string[] filters,
            string table,
            DateTime startTime,
            DateTime endTime);

        Task<List<AppImpactResult>> GetImpact(
            string resourceId,
            string[] filters,
            string table,
            DateTime startTime,
            DateTime endTime);
    }
}
