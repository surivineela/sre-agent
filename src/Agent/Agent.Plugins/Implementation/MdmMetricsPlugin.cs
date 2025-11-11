// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Cloud.Metrics.Client;
using Microsoft.Cloud.Metrics.Client.Configuration;
using Microsoft.Cloud.Metrics.Client.Metrics;
using Microsoft.Cloud.Metrics.Client.Query;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Agent.Framework;
using DimensionFilter = Microsoft.Cloud.Metrics.Client.Metrics.DimensionFilter;

namespace Agent.Plugins;

public class MdmMetricsPlugin : IMdmMetricsPlugin
{
    private const string DefaultTimeSeriesRequestJson = "{ \"samplingTypes\": [\"Count\"], \"aggregationType\": \"Automatic\", \"lastValueMode\": false }";

    private readonly MdmMetricsSettings _settings;
    private readonly ILogger<MdmMetricsPlugin> _logger;
    private readonly IAccessTokenProvider? _tokenProvider;
    private readonly IChatClientProvider _chatClientProvider;

    public MdmMetricsPlugin(
        MdmMetricsSettings settings,
        ILogger<MdmMetricsPlugin> logger,
        IAuthenticationService authenticationService,
        IHostEnvironment hostEnvironment,
        IChatClientProvider chatClientProvider)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenProvider = hostEnvironment.IsDevelopment() ? null : new MdmMetricsAccessTokenProvider(settings, logger, authenticationService, hostEnvironment);
        _chatClientProvider = chatClientProvider ?? throw new ArgumentNullException(nameof(chatClientProvider));
    }

    /// <summary>
    /// Gets the list of MDM metric namespaces for a monitoring account from Microsoft Diagnostics Metrics (MDM).
    /// </summary>
    /// <param name="monitoringAccount">MDM monitoring account identifier.</param>
    /// <returns>A JSON payload containing the available namespaces.</returns>
    public async Task<string> GetNamespacesAsync(string monitoringAccount)
    {
        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetNamespacesAsync] - monitoringAccount: {monitoringAccount}");

        if (string.IsNullOrWhiteSpace(monitoringAccount))
        {
            throw new ArgumentException("Monitoring account must be provided.", nameof(monitoringAccount));
        }

        var connectionInfo = new ConnectionInfo(_tokenProvider);
        var reader = new MetricReader(connectionInfo);

        var namespaces = await reader.GetNamespacesAsync(monitoringAccount);

        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetNamespacesAsync] - Result retrieved successfully");
        return System.Text.Json.JsonSerializer.Serialize(namespaces);
    }

    /// <summary>
    /// Gets the list of MDM metric for a monitoring account and namespace from Microsoft Diagnostics Metrics (MDM).
    /// </summary>
    /// <param name="monitoringAccount">MDM monitoring account identifier.</param>
    /// <param name="metricNamespace">MDM metric namespace identifier.</param>
    /// <returns>A JSON payload containing the metric names.</returns>
    public async Task<MetricDefinition[]> GetMetricAsync(string monitoringAccount, string metricNamespace)
    {
        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetMetricAsync] - monitoringAccount: {monitoringAccount}, metricNamespace: {metricNamespace}");

        if (string.IsNullOrWhiteSpace(monitoringAccount))
        {
            throw new ArgumentException("Monitoring account must be provided.", nameof(monitoringAccount));
        }

        if (string.IsNullOrWhiteSpace(metricNamespace))
        {
            throw new ArgumentException("Metric namespace must be provided.", nameof(metricNamespace));
        }

        // Use MetricConfigurationManager to get all metrics in the namespace
        var connectionInfo = new ConnectionInfo(_tokenProvider);

        // Get the monitoring account using MonitoringAccountConfigurationManager
        var accountManager = new MonitoringAccountConfigurationManager(connectionInfo);
        var account = await accountManager.GetAsync(monitoringAccount).ConfigureAwait(false);

        // Get all metric configurations in the namespace
        var metricManager = new MetricConfigurationManager(connectionInfo);
        var metricConfigs = await metricManager.GetMultipleAsync(account, metricNamespace, returnEmptyConfig: false).ConfigureAwait(false);
        var metrics = metricConfigs.Select(c => new MetricDefinition
        (
            c.Name,
            c.Description
        )).ToArray();

        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetMetricAsync] - Result retrieved successfully");
        return metrics;
    }

    /// <summary>
    /// Gets the dimension names for the specified MDM metric in Microsoft Diagnostics Metrics (MDM).
    /// </summary>
    /// <param name="monitoringAccount">MDM monitoring account identifier.</param>
    /// <param name="metricNamespace">MDM metric namespace identifier.</param>
    /// <param name="metricName">MDM metric name identifier.</param>
    /// <returns>A JSON payload containing the dimension names.</returns>
    public async Task<string[]> GetDimensionNamesAsync(string monitoringAccount, string metricNamespace, string metricName)
    {
        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetDimensionNamesAsync] - monitoringAccount: {monitoringAccount}, metricNamespace: {metricNamespace}, metricName: {metricName}");

        if (string.IsNullOrWhiteSpace(monitoringAccount))
        {
            throw new ArgumentException("Monitoring account must be provided.", nameof(monitoringAccount));
        }

        if (string.IsNullOrWhiteSpace(metricNamespace))
        {
            throw new ArgumentException("Metric namespace must be provided.", nameof(metricNamespace));
        }

        if (string.IsNullOrWhiteSpace(metricName))
        {
            throw new ArgumentException("Metric name must be provided.", nameof(metricName));
        }

        // Use MetricConfigurationManager to get configuration
        var connectionInfo = new ConnectionInfo(_tokenProvider);

        // Get the monitoring account using MonitoringAccountConfigurationManager
        var accountManager = new MonitoringAccountConfigurationManager(connectionInfo);
        var account = await accountManager.GetAsync(monitoringAccount).ConfigureAwait(false);

        // Get the metric configuration using MetricConfigurationManager
        var metricManager = new MetricConfigurationManager(connectionInfo);
        var metricConfig = await metricManager.GetAsync(account, metricNamespace, metricName).ConfigureAwait(false);

        var rawConfig = metricConfig as RawMetricConfiguration;
        if (rawConfig != null && !string.IsNullOrWhiteSpace(rawConfig.Name))
        {
            _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetDimensionNamesAsync] - Result retrieved successfully");
            return rawConfig.Preaggregations.SelectMany(agg => agg.Dimensions).ToArray();
        }
        else
        {
            return [];
        }
    }

    /// <summary>
    /// Gets the MDM metric dimension values for the requested dimension applying the provided filters from Microsoft Diagnostics Metrics (MDM).
    /// </summary>
    /// <param name="monitoringAccount">MDM monitoring account identifier.</param>
    /// <param name="metricNamespace">MDM metric namespace identifier.</param>
    /// <param name="metricName">MDM metric name identifier.</param>
    /// <param name="dimensionName">MDM dimension name to retrieve values for.</param>
    /// <param name="dimensionFiltersJson">JSON array of MDM dimension filter objects. Example: [{"dimension":"Region","values":["NA"]}].</param>
    /// <param name="startTimeUtc">Query start time in ISO-8601 UTC format.</param>
    /// <param name="endTimeUtc">Query end time in ISO-8601 UTC format.</param>
    /// <returns>A JSON payload containing the dimension values.</returns>
    public async Task<string> GetDimensionValuesAsync(
        string monitoringAccount,
        string metricNamespace,
        string metricName,
        string dimensionName,
        string dimensionFiltersJson,
        string startTimeUtc,
        string endTimeUtc)
    {
        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetDimensionValuesAsync] - monitoringAccount: {monitoringAccount}, metricNamespace: {metricNamespace}, metricName: {metricName}");

        var filterPayloads = ParseDimensionFilters(dimensionFiltersJson);
        var start = ParseUtc(startTimeUtc);
        var end = ParseUtc(endTimeUtc);

        if (string.IsNullOrWhiteSpace(dimensionName))
        {
            throw new ArgumentException("Dimension name must be provided.", nameof(dimensionName));
        }

        var normalizedStart = NormalizeToMinute(start);
        var normalizedEnd = NormalizeToMinute(end);

        // Create ConnectionInfo with managed identity token provider
        var connectionInfo = new ConnectionInfo(_tokenProvider);
        var reader = new MetricReader(connectionInfo);

        // Create the metric identifier
        var metricId = new Microsoft.Online.Metrics.Serialization.Configuration.MetricIdentifier(
            monitoringAccount,
            metricNamespace,
            metricName);

        // Build dimension filters following the sample pattern
        var dimensionFilters = new List<DimensionFilter>();

        // Add existing filters from the request
        foreach (var filter in filterPayloads)
        {
            if (filter.Values.Count == 0)
            {
                // Empty values means include all values for this dimension (wildcard)
                dimensionFilters.Add(DimensionFilter.CreateIncludeFilter(filter.Name));
            }
            else
            {
                // Specific values to filter on
                dimensionFilters.Add(DimensionFilter.CreateIncludeFilter(filter.Name, filter.Values.ToArray()));
            }
        }

        // Add the requested dimension as a wildcard to retrieve all its values
        dimensionFilters.Add(DimensionFilter.CreateIncludeFilter(dimensionName));

        // Get known time series definitions using the SDK method
        // This returns all time series combinations that match the dimension filters
        var knownTimeSeriesDefinitions = await reader.GetKnownTimeSeriesDefinitionsAsync(
            metricId,
            dimensionFilters,
            normalizedStart,
            normalizedEnd,
            newCombinationsOnly: false).ConfigureAwait(false);

        // Extract unique values for the requested dimension
        var uniqueValues = new HashSet<string>();
        foreach (var timeSeriesDef in knownTimeSeriesDefinitions)
        {
            if (timeSeriesDef.DimensionCombination != null)
            {
                // DimensionCombination is IReadOnlyList<KeyValuePair<string, string>>
                foreach (var kvp in timeSeriesDef.DimensionCombination)
                {
                    if (kvp.Key.Equals(dimensionName, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(kvp.Value))
                    {
                        uniqueValues.Add(kvp.Value);
                        break; // Found the dimension, move to next time series
                    }
                }
            }
        }

        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetDimensionValuesAsync] - Result retrieved successfully");
        return System.Text.Json.JsonSerializer.Serialize(uniqueValues.OrderBy(v => v).ToList());
    }

    /// <summary>
    /// Gets a list of multiple MDM metric time series, each with multiple sampling types, using the HTTP binary API from Microsoft Diagnostics Metrics (MDM).
    /// </summary>
    /// <param name="startTimeUtc">MDM query start time in ISO-8601 UTC format.</param>
    /// <param name="endTimeUtc">MDM query end time in ISO-8601 UTC format.</param>
    /// <param name="seriesResolutionInMinutes">Resolution of the returned series in minutes.</param>
    /// <param name="requestJson">JSON payload with additional MDM query parameters (sampling types, aggregation, definitions, filters). Example: { 'samplingTypes': ['Average', 'Max'], 'aggregationType': 'Automatic', 'definitions': [{ 'monitoringAccount': 'account', 'metricNamespace': 'namespace', 'metricName': 'metric' }] }.</param>
    /// <returns>A JSON payload containing the requested time series.</returns>
    public async Task<string> GetMultipleTimeSeriesAsync(string startTimeUtc, string endTimeUtc, int seriesResolutionInMinutes, string requestJson)
    {
        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetMultipleTimeSeriesAsync] - Invoked");

        if (string.IsNullOrWhiteSpace(requestJson))
        {
            throw new ArgumentException("Request JSON must be provided.", nameof(requestJson));
        }

        var request = System.Text.Json.JsonSerializer.Deserialize<MultipleTimeSeriesRequest>(requestJson)
                      ?? throw new InvalidOperationException("Unable to deserialize multiple time-series request payload.");

        var start = ParseUtc(startTimeUtc);
        var end = ParseUtc(endTimeUtc);

        if (seriesResolutionInMinutes < 1)
        {
            throw new ArgumentException("seriesResolutionInMinutes must be >= 1", nameof(seriesResolutionInMinutes));
        }

        if (start > end)
        {
            throw new ArgumentException($"startTimeUtc [{start}] must be <= endTimeUtc [{end}]");
        }

        var normalizedStart = NormalizeToMinute(start);
        var normalizedEnd = NormalizeToMinute(end);

        request.StartTimeUtc = normalizedStart;
        request.EndTimeUtc = normalizedEnd;

        var minResolutionMinutes = CalculateMinimumResolutionMinutes(normalizedStart, normalizedEnd);
        if (seriesResolutionInMinutes < minResolutionMinutes)
        {
            seriesResolutionInMinutes = minResolutionMinutes;
        }

        request.SeriesResolutionInMinutes = seriesResolutionInMinutes;

        if (request.Definitions == null || request.Definitions.Count == 0)
        {
            throw new ArgumentException("At least one metric definition must be supplied.");
        }

        // Parse sampling types
        Microsoft.Cloud.Metrics.Client.Metrics.SamplingType[] samplingTypes;
        if (request.SamplingTypes != null && request.SamplingTypes.Length > 0)
        {
            var samplingTypesList = new List<Microsoft.Cloud.Metrics.Client.Metrics.SamplingType>();
            foreach (var st in request.SamplingTypes)
            {
                var samplingType = st.ToLowerInvariant() switch
                {
                    "average" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Average,
                    "min" or "minimum" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Min,
                    "max" or "maximum" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Max,
                    "count" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Count,
                    "sum" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Sum,
                    _ => throw new ArgumentException($"Invalid sampling type: {st}. Valid values are: Average, Min, Max, Count, Sum")
                };
                samplingTypesList.Add(samplingType);
            }
            samplingTypes = samplingTypesList.ToArray();
        }
        else
        {
            samplingTypes = new[] { Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Average };
        }

        // Parse aggregation type - currently only Automatic is supported
        var aggregationType = Microsoft.Cloud.Metrics.Client.Query.AggregationType.Automatic;
        if (!string.IsNullOrWhiteSpace(request.AggregationType) &&
            !request.AggregationType.Equals("Automatic", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid aggregation type: {request.AggregationType}. Only 'Automatic' is currently supported.");
        }

        // Create ConnectionInfo with managed identity token provider
        var connectionInfo = new ConnectionInfo(_tokenProvider);
        var reader = new MetricReader(connectionInfo);

        // Build TimeSeriesDefinition list - just the metric identifiers
        var definitions = new List<Microsoft.Cloud.Metrics.Client.TimeSeriesDefinition<Microsoft.Online.Metrics.Serialization.Configuration.MetricIdentifier>>();

        foreach (var def in request.Definitions)
        {
            if (string.IsNullOrWhiteSpace(def.MonitoringAccount) ||
                string.IsNullOrWhiteSpace(def.MetricNamespace) ||
                string.IsNullOrWhiteSpace(def.MetricName))
            {
                throw new ArgumentException("Each definition must include monitoringAccount, metricNamespace, and metricName.");
            }

            var metricId = new Microsoft.Online.Metrics.Serialization.Configuration.MetricIdentifier(
                def.MonitoringAccount,
                def.MetricNamespace,
                def.MetricName);

            // TimeSeriesDefinition just needs the metric identifier
            // Time range, sampling types, etc. are passed as parameters to GetMultipleTimeSeriesAsync
            var timeSeriesDef = new Microsoft.Cloud.Metrics.Client.TimeSeriesDefinition<Microsoft.Online.Metrics.Serialization.Configuration.MetricIdentifier>(metricId);

            definitions.Add(timeSeriesDef);
        }

        // Use MetricReader.GetMultipleTimeSeriesAsync from the SDK
        var result = await reader.GetMultipleTimeSeriesAsync(
            normalizedStart,
            normalizedEnd,
            samplingTypes,
            definitions,
            request.SeriesResolutionInMinutes,
            aggregationType).ConfigureAwait(false);

        _logger.LogInternalInformation($"[MdmMetricsPlugin] [GetMultipleTimeSeriesAsync] - Result retrieved successfully");
        // Serialize the result using Newtonsoft.Json (what the SDK uses)
        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }

    /// <summary>
    /// Retrieves MDM metric time series from Microsoft Diagnostics Metrics (MDM) using MetricReader with advanced options including dimension filters and selection clauses.
    /// </summary>
    /// <param name="startTimeUtc">MDM query start time in ISO-8601 UTC format.</param>
    /// <param name="endTimeUtc">MDM query end time in ISO-8601 UTC format.</param>
    /// <param name="seriesResolutionInMinutes">Resolution of the returned series in minutes.</param>
    /// <param name="requestJson">JSON payload describing the MDM query (sampling types, aggregation, dimensions, etc.). Example: { 'monitoringAccount': 'account', 'metricNamespace': 'namespace', 'metricName': 'metric', 'samplingTypes': ['Average'], 'aggregationType': 'Automatic', 'dimensionFilters': [{'dimension': 'Region', 'values': ['NA']}], 'outputDimensionNames': ['Region'], 'lastValueMode': false }.</param>
    /// <returns>A JSON payload containing the requested time series.</returns>
    public async Task<IQueryResultListV3> GetTimeSeriesAsync(
        string monitoringAccount,
        string metricNamespace,
        string metricName,
        string startTimeUtc,
        string endTimeUtc,
        int seriesResolutionInMinutes = 5,
        string? requestJson = null)
    {
        _logger.LogInternalInformation("[MdmMetricsPlugin] [GetTimeSeriesAsync] - Invoked");

        var context = await ExecuteTimeSeriesQueryAsync(
            monitoringAccount,
            metricNamespace,
            metricName,
            startTimeUtc,
            endTimeUtc,
            seriesResolutionInMinutes,
            requestJson).ConfigureAwait(false);

        _logger.LogInternalInformation("[MdmMetricsPlugin] [GetTimeSeriesAsync] - Result retrieved successfully");
        return context.Result;
    }

    public async Task<string> GetTimeSeriesAnalysisAsync(
        string monitoringAccount,
        string metricNamespace,
        string metricName,
        string startTimeUtc,
        string endTimeUtc,
        int seriesResolutionInMinutes = 5,
        string? requestJson = null,
        string? contextualQuery = null)
    {
        _logger.LogInternalInformation("[MdmMetricsPlugin] [GetTimeSeriesAnalysisAsync] - Invoked");

        var context = await ExecuteTimeSeriesQueryAsync(
            monitoringAccount,
            metricNamespace,
            metricName,
            startTimeUtc,
            endTimeUtc,
            seriesResolutionInMinutes,
            requestJson).ConfigureAwait(false);
        var payload = BuildAnalysisPayload(context);

        string analysisPrompt = $"""
        <core_responsibilities>
        - Analyze the provided MDM time-series data for trends, anomalies, and statistical summaries.
        - Generate a concise summary of key insights derived from the data.
        </core_responsibilities>

        <role>
        You are an expert SRE data analyst working with Microsoft Diagnostics Metrics (MDM) telemetry.
        The payload includes metric metadata and bounded time-series points prepared for downstream reasoning.
        Identify meaningful trends, call out anomalies with timestamps, and provide relevant statistical context.
        If the dataset is insufficient for analysis, explicitly state the limitation.
        </role>

        <output_format>
        Provide your analysis in the following format, using markdown for any lists or emphasis:
        1. Summary of Key Insights:
        2. Detailed Analysis:
           - Trends: (direction, patterns, or stability)
           - Anomalies: (spikes, drops, irregular patterns with timestamps and values)
           - Statistical Summaries: (averages, percentiles, extrema, or other relevant aggregates)
        3. Additional Contextual Insights: (if any, based on the contextual query below)
        </output_format>

        <autonomy>
        You will not receive follow-up questions. Use only the provided data to produce the analysis.
        Do not fabricate data points; prefer concise, accurate explanations over speculation.
        </autonomy>

        <contextual_query>
        {(string.IsNullOrWhiteSpace(contextualQuery) ? "No additional contextual query provided, provide a general analysis." : contextualQuery)}
        </contextual_query>
        """;

        var options = new ChatOptions
        {
            Temperature = 0.2f,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { ChatOptionsExtensions.ReasoningEffortKey, ChatOptionsExtensions.MinimalReasoningEffort }
            }
        };

        var payloadJson = System.Text.Json.JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, analysisPrompt),
            new ChatMessage(ChatRole.User, payloadJson)
        };

        var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(messages, options).ConfigureAwait(false);

        _logger.LogInternalInformation("[MdmMetricsPlugin] [GetTimeSeriesAnalysisAsync] - Analysis generated successfully");
        return response.Text;
    }

    private static int CalculateMinimumResolutionMinutes(DateTime startInclusive, DateTime endInclusive)
    {
        var duration = endInclusive - startInclusive;
        if (duration <= TimeSpan.Zero)
        {
            return 1;
        }

        var minResolution = (int)Math.Ceiling(duration.TotalMinutes / 1440d);
        return Math.Max(1, minResolution);
    }

    private async Task<TimeSeriesQueryContext> ExecuteTimeSeriesQueryAsync(
        string monitoringAccount,
        string metricNamespace,
        string metricName,
        string startTimeUtc,
        string endTimeUtc,
        int seriesResolutionInMinutes,
        string? requestJson)
    {
        requestJson = string.IsNullOrWhiteSpace(requestJson)
            ? DefaultTimeSeriesRequestJson
            : requestJson;

        var request = System.Text.Json.JsonSerializer.Deserialize<TimeSeriesRequest>(requestJson)
                      ?? throw new InvalidOperationException("Unable to deserialize time-series request payload.");

        request.MonitoringAccount = monitoringAccount;
        request.MetricNamespace = metricNamespace;
        request.MetricName = metricName;

        var start = ParseUtc(startTimeUtc);
        var end = ParseUtc(endTimeUtc);

        if (seriesResolutionInMinutes < 1)
        {
            throw new ArgumentException("seriesResolutionInMinutes must be >= 1", nameof(seriesResolutionInMinutes));
        }

        if (start > end)
        {
            throw new ArgumentException($"startTimeUtc [{start}] must be <= endTimeUtc [{end}]");
        }

        request.StartTimeUtc = start;
        request.EndTimeUtc = end;
        request.SeriesResolutionInMinutes = seriesResolutionInMinutes;
        request.Normalize();

        var minResolutionMinutes = CalculateMinimumResolutionMinutes(request.StartTimeUtc, request.EndTimeUtc);
        if (request.SeriesResolutionInMinutes < minResolutionMinutes)
        {
            request.SeriesResolutionInMinutes = minResolutionMinutes;
        }

        if (string.IsNullOrWhiteSpace(request.MonitoringAccount))
        {
            throw new ArgumentException("Monitoring account is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MetricNamespace))
        {
            throw new ArgumentException("Metric namespace is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MetricName))
        {
            throw new ArgumentException("Metric name is required.");
        }

        var connectionInfo = new ConnectionInfo(_tokenProvider);
        var reader = new MetricReader(connectionInfo);

        var metricId = new Microsoft.Online.Metrics.Serialization.Configuration.MetricIdentifier(
            request.MonitoringAccount,
            request.MetricNamespace,
            request.MetricName);

        List<Microsoft.Cloud.Metrics.Client.Metrics.SamplingType> samplingTypes;
        if (request.SamplingTypes is { Length: > 0 })
        {
            samplingTypes = new List<Microsoft.Cloud.Metrics.Client.Metrics.SamplingType>(request.SamplingTypes.Length);
            foreach (var st in request.SamplingTypes)
            {
                var samplingType = st.ToLowerInvariant() switch
                {
                    "average" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Average,
                    "min" or "minimum" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Min,
                    "max" or "maximum" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Max,
                    "count" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Count,
                    "sum" => Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Sum,
                    _ => throw new ArgumentException($"Invalid sampling type: {st}. Valid values are: Average, Min, Max, Count, Sum")
                };
                samplingTypes.Add(samplingType);
            }
        }
        else
        {
            samplingTypes = new List<Microsoft.Cloud.Metrics.Client.Metrics.SamplingType>
            {
                Microsoft.Cloud.Metrics.Client.Metrics.SamplingType.Average
            };
        }

        var dimensionFilters = request.DimensionFilters?
            .Where(df => !string.IsNullOrWhiteSpace(df.Dimension ?? df.Name))
            .Select(df => new Microsoft.Cloud.Metrics.Client.Metrics.DimensionFilter(
                df.Dimension ?? df.Name ?? string.Empty,
                df.Values?.ToArray() ?? Array.Empty<string>(),
                isExcludeFilter: false))
            .ToList() ?? new List<Microsoft.Cloud.Metrics.Client.Metrics.DimensionFilter>();

        var aggregationType = Microsoft.Cloud.Metrics.Client.Query.AggregationType.Automatic;
        if (!string.IsNullOrWhiteSpace(request.AggregationType) &&
            !request.AggregationType.Equals("Automatic", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid aggregation type: {request.AggregationType}. Only 'Automatic' is currently supported.");
        }

        var result = await reader.GetTimeSeriesAsync(
            metricId,
            dimensionFilters,
            request.StartTimeUtc,
            request.EndTimeUtc,
            samplingTypes,
            selectionClause: null,
            aggregationType,
            request.SeriesResolutionInMinutes,
            traceId: Guid.NewGuid(),
            outputDimensionNames: request.OutputDimensionNames?.ToList(),
            lastValueMode: request.LastValueMode ?? false).ConfigureAwait(false);

        return new TimeSeriesQueryContext(request, samplingTypes, result);
    }

    private static MdmTimeSeriesAnalysisPayload BuildAnalysisPayload(TimeSeriesQueryContext context)
    {
        var rawJson = JsonConvert.SerializeObject(context.Result, Formatting.None);
        JsonElement rawResponse;
        using (var document = JsonDocument.Parse(rawJson))
        {
            rawResponse = document.RootElement.Clone();
        }

        var dimensionFilters = context.Request.DimensionFilters?
            .Where(df => !string.IsNullOrWhiteSpace(df.Dimension ?? df.Name))
            .Select(df => new MdmDimensionFilterSummary(
                Name: df.Dimension ?? df.Name ?? string.Empty,
                Values: (df.Values ?? new List<string>()).Where(static v => !string.IsNullOrWhiteSpace(v)).ToArray()))
            .Where(summary => !string.IsNullOrWhiteSpace(summary.Name))
            .ToArray() ?? Array.Empty<MdmDimensionFilterSummary>();

        var samplingTypeNames = context.SamplingTypes
            .Select(st => st.ToString())
            .ToArray();

        return new MdmTimeSeriesAnalysisPayload(
            Metric: new MdmMetricMetadata(
                MonitoringAccount: context.Request.MonitoringAccount,
                MetricNamespace: context.Request.MetricNamespace,
                MetricName: context.Request.MetricName,
                OutputDimensions: context.Request.OutputDimensionNames ?? Array.Empty<string>(),
                DimensionFilters: dimensionFilters),
            TimeRange: new MdmTimeRangeMetadata(
                StartTimeUtc: context.Request.StartTimeUtc,
                EndTimeUtc: context.Request.EndTimeUtc,
                SeriesResolutionInMinutes: context.Request.SeriesResolutionInMinutes),
            SamplingTypes: samplingTypeNames,
            RawResponse: rawResponse);
    }

    private sealed record TimeSeriesQueryContext(
        TimeSeriesRequest Request,
        IReadOnlyList<Microsoft.Cloud.Metrics.Client.Metrics.SamplingType> SamplingTypes,
        Microsoft.Cloud.Metrics.Client.Query.IQueryResultListV3 Result);

    private sealed record MdmTimeSeriesAnalysisPayload(
        MdmMetricMetadata Metric,
        MdmTimeRangeMetadata TimeRange,
        IReadOnlyList<string> SamplingTypes,
        JsonElement RawResponse);

    private sealed record MdmMetricMetadata(
        string? MonitoringAccount,
        string? MetricNamespace,
        string? MetricName,
        IReadOnlyList<string> OutputDimensions,
        IReadOnlyList<MdmDimensionFilterSummary> DimensionFilters);

    private sealed record MdmTimeRangeMetadata(
        DateTime StartTimeUtc,
        DateTime EndTimeUtc,
        int SeriesResolutionInMinutes);

    private sealed record MdmDimensionFilterSummary(
        string Name,
        IReadOnlyList<string> Values);

    public static string SerializeQueryResult(IQueryResultListV3 result)
    {
        // Serialize IQueryResultListV3 result using Newtonsoft.Json (what the SDK uses)
        var json = JsonConvert.SerializeObject(result, Formatting.Indented);
        return json;
    }

    private static IReadOnlyList<DimensionFilterDescriptor> ParseDimensionFilters(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<DimensionFilterDescriptor>();
        }

        var payload = System.Text.Json.JsonSerializer.Deserialize<List<DimensionFilterPayload>>(json);
        if (payload == null)
        {
            return Array.Empty<DimensionFilterDescriptor>();
        }

        return payload
            .Select(p => new DimensionFilterDescriptor(
                p.Dimension ?? p.Name ?? string.Empty,
                p.Values ?? new List<string>()))
            .ToList();
    }

    private static DateTime ParseUtc(string value)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        throw new ArgumentException($"Invalid UTC date time: {value}");
    }

    private static DateTime NormalizeToMinute(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);

    private static long ToUnixMilliseconds(DateTime utc) =>
        new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private static string EscapeTwice(string value) =>
        Uri.EscapeDataString(Uri.EscapeDataString(value));
}
