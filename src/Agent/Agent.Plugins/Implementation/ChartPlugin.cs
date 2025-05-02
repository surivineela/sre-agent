// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Charts;
using Microsoft.Extensions.Logging;
using ScottPlot;

namespace Agent.Plugins
{
    // Chat plugin can be used to generate various types of charts and post them to Teams.
    public class ChartPlugin : IChartPlugin
    {
        private readonly ILogger? _logger;
        private readonly IAgentOutboundCommunicationService? _outboundService;

        public Guid? ThreadId { get; set; }

        public ChartPlugin(ILogger<ChartPlugin>? logger, IAgentOutboundCommunicationService outboundService)
        {
            _logger = logger;
            _outboundService = outboundService;
        }

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

        // adding this method to directly get Pie chart image
        // plot pie chart doesn't work because it calls a team message not agent message when plotting
        public string GetPieChartBase64Image(string chartTitle,
            string dataPoints,
            string description)
        {
            var slices = ParsePieData(dataPoints);

            if (!slices.Any())
            {
                return "ERROR: Could not parse any valid slice data from 'dataPoints'.";
            }

            return ChartHelper.GeneratePieChartBase64String(slices); ;
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
            if (ThreadId == null)
            {
                _logger?.LogWarning("ThreadId is null while posting the chart.");
                return "ERROR: Context is null.";
            }
            try
            {
                var base64Image = generateChart();
                if (string.IsNullOrWhiteSpace(base64Image))
                {
                    return "ERROR: Chart generation returned an empty image.";
                }

                var threadId = ThreadId.ToString();
                if (string.IsNullOrEmpty(threadId))
                {
                    return "ERROR: No thread ID available for posting the chart.";
                }

                _logger?.LogInformation("Posting chart to thread {ThreadId}, base64 image: {base64Image}", threadId, base64Image);
                // if the base64Image doesn't contains the prefix, add it
                if (!base64Image.StartsWith("data:image/png;base64,"))
                {
                    base64Image = $"data:image/png;base64,{base64Image}";
                }
                await _outboundService.AppendAgentImageMessage(ThreadId.Value, $"![Chart Graph]({base64Image})\r");

                return $"Successfully generated the chart, image description: {description}";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, errorContext);
                return $"ERROR: Chart generation failed: {ex.Message}";
            }
        }

        public Task<string> PlotHeatMapAsync(string chartTitle, string xAxisLabel, string yAxisLabel, string colorLabel, string dataPoints, string description)
        {
            throw new NotImplementedException();
        }
    }
}
