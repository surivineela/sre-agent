// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Charts;
using FirstPartyAgent.Core.Constants;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Globalization;

namespace FirstPartyAgent.Core.Plugins
{
    public class ICMChartPlugin
    {
        private readonly ICMWorkflowClient _icmWorkflowClient;
        private readonly ILogger<ICMChartPlugin> _logger;
        private readonly ITeamsClient _teamsClient;

        public ICMChartPlugin(ICMWorkflowClient icmWorkflowClient, ILogger<ICMChartPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _icmWorkflowClient = icmWorkflowClient;
            _teamsClient = teamsClient;
        }

        [KernelFunction("plot_time_series_data_in_icm")]
        [Description(
@"Generates a base64-encoded chart from time-series data and posts it in ICM incident discussion.
Used to track the service health of the resource after a mitigation action has been applied.

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
[Description("Short text to describe the chart when posting.")] string description,
[Description("Incident Id to post the plot to.")] string incidentId,
Kernel kernel)
        {
            var logMessage = $"[plot_time_series_data_in_icm][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, description: {description}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
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

                var discussionEntry = $"<p>{description}</p><img src='{base64Image}'>";

                // Post to ICM incident discussion
                discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
                await _icmWorkflowClient.PostDiscussionEntryAsync(incidentId, discussionEntry);

                return "Successfully generated the chart and posted to the ICM incident.";
            }
            catch (Exception ex)
            {
                return $"ERROR: Chart generation failed: {ex.Message}";
            }
        }
    }
}

