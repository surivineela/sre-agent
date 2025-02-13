// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using System.Globalization;
using ScottPlot;
using Agent.Core.Helpers;
using Agent.Core.Models.Charts;
using Agent.Core.Models;

namespace Agent.Plugins
{
    public class ChartPlugin : IChartPlugin
    {
        private readonly ILogger? _logger;
        private readonly TeamsConnector _teamsConnector;

        public ChartPlugin(ILogger<ChartPlugin>? logger, TeamsConnector teamsConnector)
        {
            _logger = logger;
            _teamsConnector = teamsConnector;
        }

        public async Task<string> PlotTimeSeriesDataAsync(
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

            var input = new ChartImageInput
            {
                Title = title,
                YAxisLabel = yAxisLabel,
                YAxisMin = minVal,
                YAxisMax = maxVal,
                TimeSeries = timeSeriesList
            };

            return await GenerateAndPostChartAsync(
                () => ChartHelper.GenerateChartBase64String(input),
                description,
                "Failed to generate chart with ScottPlot.");
        }

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

            return await GenerateAndPostChartAsync(
                () => ChartHelper.GeneratePieChartBase64String(slices),
                description,
                "Failed to generate pie chart with ScottPlot.");
        }

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

            var chartInput = new BarChartInput
            {
                Title = chartTitle,
                XAxisLabel = xAxisLabel,
                YAxisLabel = yAxisLabel,
                Data = barData
            };

            return await GenerateAndPostChartAsync(
                () => ChartHelper.GenerateBarChartBase64String(chartInput),
                description,
                "Failed to generate bar chart with ScottPlot.");
        }

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

            var chartInput = new ScatterChartInput
            {
                Title = chartTitle,
                XAxisLabel = xAxisLabel,
                YAxisLabel = yAxisLabel,
                Points = scatterData
            };

            return await GenerateAndPostChartAsync(
                () => ChartHelper.GenerateScatterPlotBase64String(chartInput),
                description,
                "Failed to generate scatter plot with ScottPlot.");
        }

        private (double min, double max) ParseAxisBounds(string yAxisMin, string yAxisMax)
        {
            double minVal = !double.TryParse(yAxisMin, NumberStyles.Any, CultureInfo.InvariantCulture, out double min) ? 0.0 : min;
            double maxVal = !double.TryParse(yAxisMax, NumberStyles.Any, CultureInfo.InvariantCulture, out double max) ? 100.0 : max;
            return (minVal, maxVal);
        }

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

        private async Task<string> GenerateAndPostChartAsync(
            Func<string> generateChart,
            string description,
            string errorContext)
        {
            try
            {
                var base64Image = generateChart();
                if (string.IsNullOrWhiteSpace(base64Image))
                {
                    return "ERROR: Chart generation returned an empty image.";
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    await _teamsConnector.PostMessageAsync(new TeamsMessage(description, base64Image));
                }

                return "Successfully generated the chart.";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, errorContext);
                return $"ERROR: Chart generation failed: {ex.Message}";
            }
        }
    }
}
