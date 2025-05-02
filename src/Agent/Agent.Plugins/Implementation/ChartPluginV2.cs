// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Charts;
using Agent.Plugins.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using ScottPlot;

namespace Agent.Plugins
{
    /// <summary>
    /// ChartPluginV2 enhances the original ChartPlugin by saving raw chart data instead of images,
    /// allowing the front-end to render interactive charts
    /// </summary>
    public class ChartPluginV2 : IChartPlugin
    {
        private readonly ILogger? _logger;
        private readonly IAgentOutboundCommunicationService? _outboundService;

        public Guid? ThreadId { get; set; }

        public ChartPluginV2(ILogger<ChartPluginV2>? logger, IAgentOutboundCommunicationService outboundService)
        {
            _logger = logger;
            _outboundService = outboundService;
        }

        /// <summary>
        /// Creates a time series line chart with interactive data points
        /// </summary>
        public async Task<string> PlotTimeSeriesData(
            string title,
            string yAxisLabel,
            string yAxisMin,
            string yAxisMax,
            string dataPoints,
            string description)
        {
            var (minVal, maxVal) = ParseAxisBounds(yAxisMin, yAxisMax);
            var timeSeriesList = ParseTimeSeriesData(dataPoints);

            if (!timeSeriesList.Any())
            {
                return "ERROR: No valid time-series data points were provided.";
            }

            // Convert time series data to front-end friendly format
            var chartData = new
            {
                type = "line",
                title = title,
                xAxisLabel = "Time",
                yAxisLabel = yAxisLabel,
                yAxisMin = minVal,
                yAxisMax = maxVal,
                data = ConvertTimeSeriesDataToFrontendFormat(timeSeriesList)
            };

            return await SaveAndPostChartData(chartData, description);
        }

        /// <summary>
        /// Creates a pie chart with interactive data points
        /// </summary>
        public async Task<string> PlotPieChartAsync(
            string chartTitle,
            string dataPoints,
            string description)
        {
            var slices = ParsePieData(dataPoints);

            if (!slices.Any())
            {
                return "ERROR: Could not parse any valid slice data from 'dataPoints'.";
            }

            var chartData = new
            {
                type = "pie",
                title = chartTitle,
                data = slices.Select(s => new { label = s.Label, value = s.Value }).ToList()
            };

            return await SaveAndPostChartData(chartData, description);
        }

        /// <summary>
        /// Creates a bar chart with interactive data points
        /// </summary>
        public async Task<string> PlotBarChartAsync(
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description)
        {
            var barData = ParseBarData(dataPoints);

            if (!barData.Any())
            {
                return "ERROR: Could not parse any valid bar data from 'dataPoints'.";
            }

            var chartData = new
            {
                type = "bar",
                title = chartTitle,
                xAxisLabel = xAxisLabel,
                yAxisLabel = yAxisLabel,
                data = barData.Select(b => new { category = b.Category, value = b.Value }).ToList()
            };

            return await SaveAndPostChartData(chartData, description);
        }

        /// <summary>
        /// Creates a scatter plot with interactive data points
        /// </summary>
        public async Task<string> PlotScatterAsync(
            string chartTitle,
            string xAxisLabel,
            string yAxisLabel,
            string dataPoints,
            string description)
        {
            var scatterData = ParseScatterData(dataPoints);

            if (!scatterData.Any())
            {
                return "ERROR: Could not parse any valid scatter points from 'dataPoints'.";
            }

            var chartData = new
            {
                type = "scatter",
                title = chartTitle,
                xAxisLabel = xAxisLabel,
                yAxisLabel = yAxisLabel,
                data = scatterData.Select(p => new { x = p.X, y = p.Y, label = p.Label }).ToList()
            };

            return await SaveAndPostChartData(chartData, description);
        }

        /// <summary>
        /// Parses the Y-axis minimum and maximum bounds
        /// </summary>
        private (double min, double max) ParseAxisBounds(string yAxisMin, string yAxisMax)
        {
            double minVal = !double.TryParse(yAxisMin, NumberStyles.Any, CultureInfo.InvariantCulture, out double min) ? 0.0 : min;
            double maxVal = !double.TryParse(yAxisMax, NumberStyles.Any, CultureInfo.InvariantCulture, out double max) ? 100.0 : max;
            return (minVal, maxVal);
        }

        /// <summary>
        /// Parses time series data from string input
        /// </summary>
        private List<TimeSeriesData> ParseTimeSeriesData(string dataPoints)
        {
            var timeSeriesList = new List<TimeSeriesData>();
            if (string.IsNullOrWhiteSpace(dataPoints)) return timeSeriesList;

            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (!DateTime.TryParse(parts[0].Trim(), out var dt)) continue;
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double val))
                    continue;

                timeSeriesList.Add(new TimeSeriesData
                {
                    Timestamp = dt,
                    Value = val,
                    Name = parts[2].Trim(),
                    Unit = ""
                });
            }

            return timeSeriesList;
        }

        /// <summary>
        /// Parses pie chart data from string input
        /// </summary>
        private List<PieSlice> ParsePieData(string dataPoints)
        {
            var slices = new List<PieSlice>();
            if (string.IsNullOrWhiteSpace(dataPoints)) return slices;

            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string label = parts[0].Trim();
                if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double sliceValue))
                    continue;

                slices.Add(new PieSlice
                {
                    Label = string.IsNullOrWhiteSpace(label) ? "Slice" : label,
                    Value = sliceValue
                });
            }

            return slices;
        }

        /// <summary>
        /// Parses bar chart data from string input
        /// </summary>
        private List<BarData> ParseBarData(string dataPoints)
        {
            var barData = new List<BarData>();
            if (string.IsNullOrWhiteSpace(dataPoints)) return barData;

            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                    continue;

                barData.Add(new BarData
                {
                    Category = parts[0].Trim(),
                    Value = value
                });
            }

            return barData;
        }

        /// <summary>
        /// Parses scatter plot data from string input
        /// </summary>
        private List<ScatterPoint> ParseScatterData(string dataPoints)
        {
            var scatterData = new List<ScatterPoint>();
            if (string.IsNullOrWhiteSpace(dataPoints)) return scatterData;

            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (!double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double x))
                    continue;

                if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double y))
                    continue;

                scatterData.Add(new ScatterPoint
                {
                    X = x,
                    Y = y,
                    Label = parts[2].Trim()
                });
            }

            return scatterData;
        }

        /// <summary>
        /// Converts time series data to a format suitable for frontend rendering
        /// </summary>
        private List<object> ConvertTimeSeriesDataToFrontendFormat(List<TimeSeriesData> timeSeriesList)
        {
            // Group by timestamp to consolidate multiple series
            var groupedByTimestamp = timeSeriesList
                .GroupBy(ts => ts.Timestamp)
                .OrderBy(g => g.Key)
                .ToList();

            // Get unique series names
            var seriesNames = timeSeriesList
                .Select(ts => ts.Name)
                .Distinct()
                .ToList();

            // Create frontend-friendly data format
            var result = new List<object>();
            foreach (var group in groupedByTimestamp)
            {
                var dataPoint = new Dictionary<string, object>
                {
                    { "name", group.Key.ToString("yyyy-MM-dd HH:mm:ss") }
                };

                // Add value for each series
                foreach (var seriesName in seriesNames)
                {
                    var point = group.FirstOrDefault(ts => ts.Name == seriesName);
                    dataPoint[seriesName] = point?.Value ?? 0;
                }

                result.Add(dataPoint);
            }

            return result;
        }

        [KernelFunction("plot_heat_map")]
        [ThreadSpecific]
        [Description(
@"Generates a heat map visualization from the provided data.
Used for showing intensity relationships between two categorical dimensions.

Arguments:
chartTitle: Title for the heatmap, e.g. 'Server Response Times by Hour and Day'
xAxisLabel: Label for the X-axis (columns), e.g. 'Hour of Day'
yAxisLabel: Label for the Y-axis (rows), e.g. 'Day of Week'
colorLabel: Label for the color scale, e.g. 'Response Time (ms)'
dataPoints: Semicolon-separated data points in the format 'xValue|yValue|intensity',
    e.g.: 'Monday|9|45;Monday|10|78;Tuesday|9|23'
    For multiple points, each point is separated by a semicolon
description: Text to accompany the heat map chart")]
        public async Task<string> PlotHeatMapAsync(
            [Description("Chart title, e.g. 'Server Response Times'")] string chartTitle,
            [Description("X-axis label (columns)")] string xAxisLabel,
            [Description("Y-axis label (rows)")] string yAxisLabel,
            [Description("Label for the color scale")] string colorLabel,
            [Description("Semicolon-separated data in format 'xValue|yValue|intensity'")] string dataPoints,
            [Description("Optional text to describe the heat map")] string description)
        {
            var heatMapData = ParseHeatMapData(dataPoints);

            if (!heatMapData.Any())
            {
                return "ERROR: Could not parse any valid heat map data from 'dataPoints'.";
            }

            // Convert heat map data to front-end friendly format
            var chartData = new
            {
                type = "heatmap",
                title = chartTitle,
                xAxisLabel = xAxisLabel,
                yAxisLabel = yAxisLabel,
                colorLabel = colorLabel,
                data = FormatHeatMapDataForFrontend(heatMapData)
            };

            return await SaveAndPostChartData(chartData, description);
        }

        private List<HeatMapPoint> ParseHeatMapData(string dataPoints)
        {
            var heatMapData = new List<HeatMapPoint>();
            if (string.IsNullOrWhiteSpace(dataPoints)) return heatMapData;

            var entries = dataPoints.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var entry in entries)
            {
                var parts = entry.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 3) continue;

                if (!double.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double intensity))
                    continue;

                heatMapData.Add(new HeatMapPoint
                {
                    X = parts[0].Trim(),  // X category (string)
                    Y = parts[1].Trim(),  // Y category (string)
                    Value = intensity     // Intensity/value (numeric)
                });
            }

            return heatMapData;
        }

        /// <summary>
        /// Format heat map data for front-end rendering
        /// </summary>
        private object FormatHeatMapDataForFrontend(List<HeatMapPoint> heatMapData)
        {
            // Extract unique X and Y categories
            var xCategories = heatMapData.Select(p => p.X).Distinct().OrderBy(x => x).ToList();
            var yCategories = heatMapData.Select(p => p.Y).Distinct().OrderBy(y => y).ToList();

            // Create properly formatted data for the front-end
            var result = new
            {
                xCategories = xCategories,
                yCategories = yCategories,
                values = heatMapData.Select(p => new
                {
                    x = p.X,
                    y = p.Y,
                    value = p.Value
                }).ToList()
            };

            return result;
        }

        /// <summary>
        /// Represents a single data point in a heat map
        /// </summary>
        public class HeatMapPoint
        {
            /// <summary>
            /// X category (column)
            /// </summary>
            public string X { get; set; } = string.Empty;

            /// <summary>
            /// Y category (row)
            /// </summary>
            public string Y { get; set; } = string.Empty;

            /// <summary>
            /// The intensity value at coordinates (X,Y)
            /// </summary>
            public double Value { get; set; }
        };

        /// <summary>
        /// Saves and posts chart data to the thread
        /// </summary>
        private async Task<string> SaveAndPostChartData(object chartData, string description)
        {
            if (ThreadId == null)
            {
                _logger?.LogWarning("ThreadId is null while posting chart data.");
                return "ERROR: Context is null.";
            }

            try
            {
                var threadId = ThreadId.ToString();
                if (string.IsNullOrEmpty(threadId))
                {
                    return "ERROR: No thread ID available for posting the chart data.";
                }

                // Serialize chart data to JSON
                var chartDataJson = JsonSerializer.Serialize(chartData);
                _logger?.LogInformation("Posting chart data to thread {ThreadId}: {ChartData}", threadId, chartDataJson);

                // Pass chart data as a special message format that the front-end will recognize
                await _outboundService.AppendAgentImageMessage(ThreadId.Value, $"```chart-data\n{chartDataJson}\n```\n{description}");

                return $"Successfully generated the chart data, description: {description}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save and post chart data");
                return $"ERROR: Chart data processing failed: {ex.Message}";
            }
        }
    }
}
