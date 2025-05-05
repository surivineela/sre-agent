// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Microsoft.SemanticKernel;
using Agent.Plugins.Attributes;

namespace Agent.Plugins
{
    public class ChartPluginDefinition
    {
        private readonly IChartPlugin _chartPlugin;

        public ChartPluginDefinition(IChartPlugin chartPlugin)
        {
            _chartPlugin = chartPlugin;
        }

        [KernelFunction("plot_time_series_data")]
        [ThreadSpecific]
        [Description(
@"Generates a base64-encoded chart from time-series data.
Used whenever giving a comparison to user. eg: how many of my total monitored apps basic auth enabled

Arguments:
title: e.g. 'Application Metrics Dashboard'
yAxisLabel: e.g. 'Usage (%)'
yAxisMin: numeric, e.g. '0'
yAxisMax: numeric, e.g. '100'
dataPoints: semicolon-separated list of data points, each in the format:
'2024-01-25T10:30:00|75.4|CPU Usage'
For multiple points, separate each with a semicolon:
'2024-01-25T10:30:00|75.4|CPU Usage;2024-01-25T10:35:00|82.1|Memory Usage'
description: text to accompany the chart when posting the image")]
        public async Task<string> PlotTimeSeriesData(
            [Description("Title for the chart, e.g. 'Application Metrics Dashboard'")] string title,
            [Description("Y-Axis label, e.g. 'Usage (%)'")] string yAxisLabel,
            [Description("Minimum value on the Y-axis, e.g. '0'")] string yAxisMin,
            [Description("Maximum value on the Y-axis, e.g. '100'")] string yAxisMax,
            [Description("Semicolon-separated data points, each 'YYYY-MM-DDTHH:MM:SS|value|seriesName'")] string dataPoints,
            [Description("Short text to describe the chart when posting.")] string description)
        {
            return await _chartPlugin.PlotTimeSeriesData(title, yAxisLabel, yAxisMin, yAxisMax, dataPoints, description);
        }

        [KernelFunction("plot_pie_chart")]
        [ThreadSpecific]
        [Description(@"Generates a pie chart from the provided data and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the pie chart.
dataPoints: Semicolon-separated items in format 'sliceLabel|value',
e.g.: 'Category A|45;Category B|30;Category C|25'.
description: A short message to summarize the image.")]
        public async Task<string> PlotPieChartAsync(
            [Description("Chart title, e.g. 'Endpoint Distribution'")] string chartTitle,
            [Description("Data in format 'Label1|Value1;Label2|Value2;Label3|Value3'")] string dataPoints,
            [Description("Optional text to describe/post with the image.")] string description)
        {
            return await _chartPlugin.PlotPieChartAsync(chartTitle, dataPoints, description);
        }

        [KernelFunction("plot_bar_chart")]
        [ThreadSpecific]
        [Description(@"Generates a bar chart from the provided data and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the bar chart.
xAxisLabel: Label for the X-axis.
yAxisLabel: Label for the Y-axis.
dataPoints: Semicolon-separated items in format 'category|value',
e.g.: 'Q1|120;Q2|80;Q3|60;Q4|90'
description: A short message to summarize the image.")]
        public async Task<string> PlotBarChartAsync(
            [Description("Chart title")] string chartTitle,
            [Description("X-axis label")] string xAxisLabel,
            [Description("Y-axis label")] string yAxisLabel,
            [Description("Semicolon-separated 'Category|Value' pairs")] string dataPoints,
            [Description("Optional text to describe/post with the image")] string description)
        {
            return await _chartPlugin.PlotBarChartAsync(chartTitle, xAxisLabel, yAxisLabel, dataPoints, description);
        }

        [KernelFunction("plot_scatter")]
        [ThreadSpecific]
        [Description(@"Generates a scatter plot from X-Y coordinate pairs and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the scatter plot.
xAxisLabel: Label for the X-axis.
yAxisLabel: Label for the Y-axis.
dataPoints: Semicolon-separated items in format 'x|y|label',
e.g.: '1.2|3.4|Point A;2.3|4.5|Point B;3.4|5.6|Point C'
description: A short message to summarize the image.")]
        public async Task<string> PlotScatterAsync(
            [Description("Chart title")] string chartTitle,
            [Description("X-axis label")] string xAxisLabel,
            [Description("Y-axis label")] string yAxisLabel,
            [Description("Semicolon-separated 'x|y|label' coordinate pairs")] string dataPoints,
            [Description("Optional text to describe/post with the image")] string description)
        {
            return await _chartPlugin.PlotScatterAsync(chartTitle, xAxisLabel, yAxisLabel, dataPoints, description);
        }

        [KernelFunction("plot_heatmap")]
        [ThreadSpecific]
        [Description(@"Generates a heatmap chart from the provided data and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the heatmap.
xAxisLabel: Label for the X-axis (e.g., 'Time (hours)').
yAxisLabel: Label for the Y-axis (e.g., 'Temperature (°C)').
dataPoints: Semicolon-separated items in format 'x|y|value',
e.g.: '12:00|25|8.5;12:00|30|4.2;13:00|25|9.1'
where x is the x-axis position, y is the y-axis position, and value is the intensity.
description: A short message to summarize the chart.")]
        public async Task<string> PlotHeatmapAsync(
     [Description("Chart title")] string chartTitle,
     [Description("X-axis label")] string xAxisLabel,
     [Description("Y-axis label")] string yAxisLabel,
     [Description("Semicolon-separated 'x|y|value' triples")] string dataPoints,
     [Description("Optional text to describe/post with the chart")] string description)
        {
            return await _chartPlugin.PlotHeatmapAsync(chartTitle, xAxisLabel, yAxisLabel, dataPoints, description);
        }
    }
}
