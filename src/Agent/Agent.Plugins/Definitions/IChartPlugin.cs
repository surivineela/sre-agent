// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Attributes;
using Microsoft.SemanticKernel;

namespace Agent.Plugins
{
    public interface IChartPlugin
    {
        public Guid? ThreadId { get; set; }

        Task<string> PlotTimeSeriesData(
            string title,
            string yAxisLabel,
            string yAxisMin,
            string yAxisMax,
            string dataPoints,
            string description);

        Task<string> PlotPieChartAsync(
            string chartTitle,
            string dataPoints,
            string description);

        Task<string> PlotBarChartAsync(
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description);

        Task<string> PlotScatterAsync(
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description);

        Task<string> PlotHeatmapAsync(
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description);
    }
}

