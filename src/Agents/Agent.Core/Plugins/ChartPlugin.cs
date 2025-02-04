using System.ComponentModel;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.Globalization;
using ScottPlot;
using System.Text.Json;
using Agents.Core.Helpers;
using Agents.Core.Models;

namespace Agents.Core.Plugins;

public class ChartPlugin
{

    private readonly ILogger? _logger;
    private TeamsConnector _teams_Connector;

    public ChartPlugin(ILogger<ChartPlugin>? logger, TeamsConnector teams_Connector)
    {
        _logger = logger;
        _teams_Connector = teams_Connector;
    }

    [KernelFunction("plot_time_series_data")]
    [Description(
@"Generates a base64-encoded chart from time-series data and posts it to Teams.
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

description: text to accompany the chart when posting to Teams

Example usage:
plot_time_series_data
title='App Metrics'
yAxisLabel='Usage (%)'
yAxisMin='0'
yAxisMax='100'
dataPoints='2024-01-25T10:30:00|75.4|CPU Usage;2024-01-25T10:35:00|82.1|Memory Usage'
description='Showing updated usage stats.'")]
    public async Task<string> PlotTimeSeriesDataAsync(
[Description("Title for the chart, e.g. 'Application Metrics Dashboard'")] string title,
[Description("Y-Axis label, e.g. 'Usage (%)'")] string yAxisLabel,
[Description("Minimum value on the Y-axis, e.g. '0'")] string yAxisMin,
[Description("Maximum value on the Y-axis, e.g. '100'")] string yAxisMax,
[Description("Semicolon-separated data points, each 'YYYY-MM-DDTHH:MM:SS|value|seriesName'")] string dataPoints,
[Description("Short text to describe the chart when posting.")] string description)
    {
        // Parse numeric min/max from strings
        // (If blank or invalid, default to 0 or 100, etc.)
        if (!double.TryParse(yAxisMin, NumberStyles.Any, CultureInfo.InvariantCulture, out double minVal))
        {
            minVal = 0.0;
        }
        if (!double.TryParse(yAxisMax, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxVal))
        {
            maxVal = 100.0;
        }


        // Parse the data point string  
        // dataPoints is expected: "2024-01-25T10:30:00|75.4|CPU Usage;2024-01-25T10:35:00|82.1|Memory Usage"  
        var timeSeriesList = new List<TimeSeriesData>();
        if (!string.IsNullOrWhiteSpace(dataPoints))
        {
            // Split at semicolons  
            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                // Each entry: "timestamp|value|name"  
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (!DateTime.TryParse(parts[0].Trim(), out var dt)) continue;
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    continue;

                var seriesName = parts[2].Trim();

                timeSeriesList.Add(new TimeSeriesData
                {
                    Timestamp = dt,
                    Value = val,
                    Name = seriesName,
                    Unit = "" // optional  
                });
            }
        }

        if (!timeSeriesList.Any())
        {
            return "ERROR: No valid time-series data points were provided.";
        }

        // Build the ChartImageInput  
        var input = new ChartImageInput
        {
            Title = title,
            YAxisLabel = yAxisLabel,
            YAxisMin = minVal,
            YAxisMax = maxVal,
            TimeSeries = timeSeriesList
        };

        try
        {
            // Generate the chart as a base64 string  
            var base64Image = ChartHelper.GenerateChartBase64String(input);
            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return "ERROR: Chart generation returned an empty image.";
            }

            // Optionally post to Teams  
            await _teams_Connector.PostMessageAsync(new TeamsMessage(description, base64Image));

            return "Successfully generated the chart and posted to user.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate chart with ScottPlot.");
            return $"ERROR: Chart generation failed: {ex.Message}";
        }
    }

    [KernelFunction("plot_pie_chart")]
    [Description(@"Generates a pie chart from the provided data and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the pie chart.
dataPoints: Semicolon-separated items in format 'sliceLabel|value',
e.g.: 'Endpoint A|120;Endpoint B|80;Endpoint C|60'
description: A short message to include if you want to post to Teams.

Returns:
A status message, after optionally sending the chart to Teams.")]
    public async Task<string> PlotPieChartAsync(
[Description("Chart title, e.g. 'Endpoint Distribution'")] string chartTitle,
[Description("Semicolon-separated 'Label|Value' pairs for each slice.")] string dataPoints,
[Description("Optional text to describe/post with the image.")] string description)
    {
        // Parse the data points into a list of PieSlice objects
        var slices = new List<PieSlice>();
        if (!string.IsNullOrWhiteSpace(dataPoints))
        {
            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                // Each entry looks like "SliceLabel|123.45"
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;


                string label = parts[0].Trim();
                if (!double.TryParse(parts[1].Trim(),
                                     NumberStyles.Any,
                                     CultureInfo.InvariantCulture,
                                     out double sliceValue))
                {
                    continue;
                }

                slices.Add(new PieSlice
                {
                    Label = string.IsNullOrWhiteSpace(label) ? "Slice" : label,
                    Value = sliceValue
                });
            }
        }

        // If we found no valid slices, return early  
        if (!slices.Any())
        {
            return "ERROR: Could not parse any valid slice data from 'dataPoints'.";
        }

        try
        {
            // Generate the pie chart as a Base64 data URI  
            // IMPORTANT: Call the helper with the entire List<PieSlice>  
            var base64Image = ChartHelper.GeneratePieChartBase64String(slices);
            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return "ERROR: Chart generation returned an empty image.";
            }

            // Optionally post to Teams with the provided description  
            if (!string.IsNullOrWhiteSpace(description))
            {
                // _teams_Connector and TeamsMessage assumed to be defined in your class  
                await _teams_Connector.PostMessageAsync(new TeamsMessage(description, base64Image));
            }

            return "Successfully generated the pie chart.";
        }
        catch (Exception ex)
        {
            // _logger assumed to be an ILogger or similar logging interface  
            _logger?.LogError(ex, "Failed to generate pie chart with ScottPlot.");
            return $"ERROR: Pie chart generation failed: {ex.Message}";
        }
    }

    [KernelFunction("plot_bar_chart")]
    [Description(@"Generates a bar chart from the provided data and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the bar chart.
xAxisLabel: Label for the X-axis.
yAxisLabel: Label for the Y-axis.
dataPoints: Semicolon-separated items in format 'category|value',
e.g.: 'Q1|120;Q2|80;Q3|60;Q4|90'
description: A short message to include if you want to post to Teams.

Returns:
A status message, after optionally sending the chart to Teams.")]
    public async Task<string> PlotBarChartAsync(
        [Description("Chart title")] string chartTitle,
        [Description("X-axis label")] string xAxisLabel,
        [Description("Y-axis label")] string yAxisLabel,
        [Description("Semicolon-separated 'Category|Value' pairs")] string dataPoints,
        [Description("Optional text to describe/post with the image")] string description)
    {
        var barData = new List<BarData>();
        if (!string.IsNullOrWhiteSpace(dataPoints))
        {
            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string category = parts[0].Trim();
                if (!double.TryParse(parts[1].Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double value))
                {
                    continue;
                }

                barData.Add(new BarData
                {
                    Category = category,
                    Value = value
                });
            }
        }

        if (!barData.Any())
        {
            return "ERROR: Could not parse any valid bar data from 'dataPoints'.";
        }

        try
        {
            var chartInput = new BarChartInput
            {
                Title = chartTitle,
                XAxisLabel = xAxisLabel,
                YAxisLabel = yAxisLabel,
                Data = barData
            };

            var base64Image = ChartHelper.GenerateBarChartBase64String(chartInput);
            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return "ERROR: Chart generation returned an empty image.";
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                await _teams_Connector.PostMessageAsync(new TeamsMessage(description, base64Image));
            }

            return "Successfully generated the bar chart.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate bar chart with ScottPlot.");
            return $"ERROR: Bar chart generation failed: {ex.Message}";
        }
    }

    [KernelFunction("plot_scatter")]
    [Description(@"Generates a scatter plot from X-Y coordinate pairs and returns (or posts) it.
Parameters:
chartTitle: The title displayed at the top of the scatter plot.
xAxisLabel: Label for the X-axis.
yAxisLabel: Label for the Y-axis.
dataPoints: Semicolon-separated items in format 'x|y|label',
e.g.: '1.2|3.4|Point A;2.3|4.5|Point B;3.4|5.6|Point C'
description: A short message to include if you want to post to Teams.

Returns:
A status message, after optionally sending the chart to Teams.")]
    public async Task<string> PlotScatterAsync(
        [Description("Chart title")] string chartTitle,
        [Description("X-axis label")] string xAxisLabel,
        [Description("Y-axis label")] string yAxisLabel,
        [Description("Semicolon-separated 'x|y|label' coordinate pairs")] string dataPoints,
        [Description("Optional text to describe/post with the image")] string description)
    {
        var scatterData = new List<ScatterPoint>();
        if (!string.IsNullOrWhiteSpace(dataPoints))
        {
            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (!double.TryParse(parts[0].Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double x))
                {
                    continue;
                }

                if (!double.TryParse(parts[1].Trim(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out double y))
                {
                    continue;
                }

                string label = parts[2].Trim();

                scatterData.Add(new ScatterPoint
                {
                    X = x,
                    Y = y,
                    Label = label
                });
            }
        }

        if (!scatterData.Any())
        {
            return "ERROR: Could not parse any valid scatter points from 'dataPoints'.";
        }

        try
        {
            var chartInput = new ScatterChartInput
            {
                Title = chartTitle,
                XAxisLabel = xAxisLabel,
                YAxisLabel = yAxisLabel,
                Points = scatterData
            };

            var base64Image = ChartHelper.GenerateScatterPlotBase64String(chartInput);
            if (string.IsNullOrWhiteSpace(base64Image))
            {
                return "ERROR: Chart generation returned an empty image.";
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                await _teams_Connector.PostMessageAsync(new TeamsMessage(description, base64Image));
            }

            return "Successfully generated the scatter plot.";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate scatter plot with ScottPlot.");
            return $"ERROR: Scatter plot generation failed: {ex.Message}";
        }
    }

    public class BarData
    {
        public string Category { get; set; } = "";
        public double Value { get; set; }
    }

    public class BarChartInput
    {
        public string Title { get; set; } = "";
        public string XAxisLabel { get; set; } = "";
        public string YAxisLabel { get; set; } = "";
        public List<BarData> Data { get; set; } = new List<BarData>();
    }

    public class ScatterPoint
    {
        public double X { get; set; }
        public double Y { get; set; }
        public string Label { get; set; } = "";
    }

    public class ScatterChartInput
    {
        public string Title { get; set; } = "";
        public string XAxisLabel { get; set; } = "";
        public string YAxisLabel { get; set; } = "";
        public List<ScatterPoint> Points { get; set; } = new List<ScatterPoint>();
    }
}
