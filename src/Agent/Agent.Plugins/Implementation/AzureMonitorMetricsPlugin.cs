// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Interface;
using Azure.Core;
using Azure.Monitor.Query.Models;
using Microsoft.Extensions.AI;

namespace Agent.Plugins;

public class AzureMonitorMetricsPlugin : IAzureMonitorMetricsPlugin
{
    private readonly AzureMonitorMetricsHelper _azureMonitorMetricsHelper;
    private readonly IChatClientProvider _chatClientProvider;
    public Guid? ThreadId { get; set; }

    // In-memory cache: key is "resourceType", value is List<MetricDefinition>
    private static readonly ConcurrentDictionary<string, List<MetricDefinition>> _metricDefinitionsCache = new(StringComparer.OrdinalIgnoreCase);

    public AzureMonitorMetricsPlugin(
        AzureMonitorMetricsHelper azureMonitorMetricsHelper,
        IChatClientProvider chatClientProvider)
    {
        _azureMonitorMetricsHelper = azureMonitorMetricsHelper;
        _chatClientProvider = chatClientProvider;
    }

    public async Task<List<MetricDefinition>> ListMetricsForAzureResource(string resourceId)
    {
        var resourceIdentifier = new ResourceIdentifier(resourceId);
        var cacheKey = resourceIdentifier.ResourceType.ToString().ToLowerInvariant();

        if (_metricDefinitionsCache.TryGetValue(cacheKey, out var cachedMetrics))
        {
            return cachedMetrics;
        }

        var metrics = await _azureMonitorMetricsHelper.ListMetricsAsync(resourceId);
        _metricDefinitionsCache[cacheKey] = metrics;
        return metrics;
    }

    public async Task<IReadOnlyList<MetricTimeSeriesElement>> QueryMetricValuesForAzureResource(
        string resourceId, string metricNamespace, string metricName, DateTimeOffset startTime, DateTimeOffset endTime, string dimensionFilter = "")
    {
        var duration = endTime - startTime;

        // Calculate minimum granularity to keep results under 1440 points
        var minGranularity = TimeSpan.FromTicks(duration.Ticks / 1440);

        var supportedBuckets = await GetSupportedBucketsAsync(resourceId);

        // Pick the first supported bucket >= minGranularity
        var matchingBucket = supportedBuckets.FirstOrDefault(bucket => bucket >= minGranularity);
        var roundedGranularity = matchingBucket != default
            ? matchingBucket
            : supportedBuckets.Last(); // fallback to 1 day if all buckets are smaller

        var metricsQueryResult = await _azureMonitorMetricsHelper.QueryResourceMetricAsync(
            resourceId, metricNamespace, metricName, startTime, endTime, roundedGranularity, dimensionFilter);

        return metricsQueryResult.Metrics[0].TimeSeries;
    }

    public async Task<string> GetMetricsTimeSeriesAnalysisAsync(
        string resourceId,
        string metricNamespace,
        string metricName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        string dimensionFilter = "",
        string contextualQuery = ""
    )
    {
        var timeSeriesElements = await QueryMetricValuesForAzureResource(
            resourceId,
            metricNamespace,
            metricName,
            startTime,
            endTime,
            dimensionFilter);

        string analysisPrompt = $"""
        <core_responsibilities>
        - Analyze the provided time-series data for trends, anomalies, and statistical summaries.
        - Generate a concise summary of key insights derived from the data.
        </core_responsibilities>

        <role>
        You are an expert data analyst. You have been provided with time-series data for a specific metric from an Azure resource.
        Example metrics include CPU usage, memory consumption, request counts, etc.
        The data is structured as a list of timestamped values, potentially segmented by dimensions.
        Your task is to analyze this data and provide insights such as trends, anomalies, and statistical summaries.
        Additionally, generate a concise summary of the key insights derived from the data.
        If the data is insufficient for analysis, clearly state that no meaningful insights can be derived.
        </role>

        <output_format>
        Provide your analysis in the following format, using markdown for any lists or emphasis:
        1. Summary of Key Insights:
        2. Detailed Analysis:
           - Trends: (e.g., increasing, decreasing, stable, patterns)
           - Anomalies: (e.g., spikes, drops, irregular patterns -- including timestamps / time ranges and values)
           - Statistical Summaries: (e.g., averages, medians, percentiles, depending on what is relevant for the metric)
        3. Additional Contextual Insights: (if any, based on the provided contextual query below)
        </output_format>

        <autonomy>
        There will be no additional prompts or clarifications. Use your expertise to deliver a comprehensive analysis based on the data provided.
        Do not ask for more information or prompt the user for clarification.
        </autonomy>

        <contextual_query>
        {(string.IsNullOrEmpty(contextualQuery) ? "No additional contextual query provided, provide a general analysis." : contextualQuery)}
        </contextual_query>
        """;

        var options = new ChatOptions
        {
            Temperature = (float)0.2,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { ChatOptionsExtensions.ReasoningEffortKey, ChatOptionsExtensions.MinimalReasoningEffort }
            }
        };

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, analysisPrompt),
            new ChatMessage(ChatRole.User, JsonSerializer.Serialize(timeSeriesElements, JsonSerializerOptions.Web))
        };

        var response = await _chatClientProvider.DefaultModel.GetResponseAsync(messages, options);

        return response.Text;
    }

    private async Task<List<TimeSpan>> GetSupportedBucketsAsync(string resourceId)
    {
        // Get all supported buckets for the metric
        var metricDefinition = await ListMetricsForAzureResource(resourceId);
        var supportedBuckets = metricDefinition
            .SelectMany(md => md.MetricAvailabilities)
            .Where(ma => ma.Granularity != null && ma.Granularity.HasValue && ma.Granularity != TimeSpan.Zero)
            .Select(ma => ma.Granularity!.Value)
            .Distinct()
            .ToList();

        if (!supportedBuckets.Any())
        {
            throw new InvalidOperationException("No supported buckets found for the metric.");
        }

        return supportedBuckets;
    }
}
