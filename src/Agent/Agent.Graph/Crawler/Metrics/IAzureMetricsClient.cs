// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Charts;

namespace Agent.Graph.Crawler.Metrics;
public interface IAzureMetricsClient
{
    Task<List<TimeSeriesData>> GetMetricsAsync(string resourceId, List<Metric> metrics, string filter = "");
    Task<double> GetCostAsync(string resourceId, DateTime endDate);
}
