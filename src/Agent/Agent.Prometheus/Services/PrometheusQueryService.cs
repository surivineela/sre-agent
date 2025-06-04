using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Interfaces;
using Agent.Logging;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Prometheus.Services;

public class PrometheusQueryService(ILogger<PrometheusQueryService> logger, IHttpClientFactory httpClientFactory, IAuthenticationService authService) : IPrometheusQueryService
{

    private static readonly JsonSerializerOptions options = new()
    {
        Converters =
            {
                new MetricItemConverter()
            }
    };

    public async Task<Response> QueryInstantAsync(string prometheusQueryEndpoint, string query, DateTime? timestamp = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prometheusQueryEndpoint, nameof(prometheusQueryEndpoint));
        ArgumentException.ThrowIfNullOrEmpty(query, nameof(query));

        using var client = await CreateHttpClientAsync();
        var dict = new Dictionary<string, string>
        {
            { "query", query },
        };
        if (timestamp.HasValue)
        {
            dict["time"] = timestamp.Value.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{prometheusQueryEndpoint.TrimEnd('/')}/api/v1/query")
        {
            Content = new FormUrlEncodedContent(dict),
        };

        var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            return DeserializeInstantQueryResponse(content);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInternalError("Failed to query Prometheus: {errorContent}", errorContent);
            throw new HttpRequestException($"Failed to query Prometheus: {response.StatusCode} - {errorContent}");
        }
    }

    public async Task<string> DiscoverMetricsAsync(
        string prometheusQueryEndpoint,
        string? namePattern,
        string? metricType)
    {
        const int maxMetrics = 100;

        try
        {
            using var client = await CreateHttpClientAsync();
            var response = await client.GetStringAsync($"{prometheusQueryEndpoint}/api/v1/label/__name__/values");
            var data = JsonDocument.Parse(response);

            if (data.RootElement.GetProperty("status").GetString() != "success")
            {
                return "Failed to retrieve metrics list";
            }

            var metrics = data.RootElement.GetProperty("data").EnumerateArray()
                .Select(m => m.GetString()!)
                .Where(m => namePattern == null || MatchesPattern(m, namePattern))
                .ToArray();

            var metricsSb = new StringBuilder();

            if (metrics.Length > maxMetrics)
            {
                metricsSb.AppendLine($"Retrieved {metrics.Length} metrics for regex {namePattern}. Showing only first {maxMetrics} metrics. Please query again with better filters if you need complete list.");
                metrics = metrics
                    .Take(maxMetrics)
                    .ToArray();
            }

            foreach (var metric in metrics)
            {
                metricsSb.AppendLine(metric);
            }

            return metricsSb.ToString();
        }
        catch (Exception ex)
        {
            return $"Metrics discovery failed: {ex.Message}";
        }
    }

    private static bool MatchesPattern(
        string text,
        string pattern)
    {
        var regex = pattern.Replace("*", ".*").Replace("?", ".");
        return Regex.IsMatch(text, $"^{regex}$", RegexOptions.IgnoreCase);
    }

    public async Task<string> GetMetricLabelsAsync(
        string prometheusQueryEndpoint,
        string metricName,
        string? labelName)
    {
        const int maxLabels = 100;

        try
        {
            using var client = await CreateHttpClientAsync();

            var apiUrl = string.IsNullOrEmpty(labelName)
                // Get all label names for the metric
                ? $"{prometheusQueryEndpoint}/api/v1/labels?match[]={Uri.EscapeDataString(metricName)}"
                // Get values for specific label
                : $"{prometheusQueryEndpoint}/api/v1/label/{labelName}/values?match[]={Uri.EscapeDataString(metricName)}";

            var response = await client.GetStringAsync(apiUrl);
            var data = JsonDocument.Parse(response);

            if (data.RootElement.GetProperty("status").GetString() != "success")
            {
                return "Failed to retrieve label information";
            }

            var labels = data.RootElement.GetProperty("data").EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray();

            var labelsSb = new StringBuilder();

            if (labels.Length > maxLabels)
            {
                labelsSb.AppendLine($"Retrieved {labels.Length} labels for selector {labelName}. Showing only first {maxLabels} labels. Please query again with better filters if you need complete list.");
                labels = labels
                    .Take(maxLabels)
                    .ToArray();
            }

            foreach (var label in labels)
            {
                labelsSb.AppendLine(label);
            }

            return labelsSb.ToString();
        }
        catch (Exception ex)
        {
            return $"Label discovery failed: {ex.Message}";
        }
    }

    public async Task<string> ExecutePromQLAsync(
        string prometheusQueryEndpoint,
        string query,
        string duration,
        string step,
        string? labelFilters,
        string? aggregateFunction,
        string? aggregateBy,
        int? limit,
        double? minValue)
    {
        try
        {
            // Build the enhanced query
            var enhancedQuery = BuildEnhancedQuery(query, labelFilters, aggregateFunction, aggregateBy, limit);

            // Determine query type and build URL
            string apiUrl;
            if (duration == "now")
            {
                // Instant query
                apiUrl = $"{prometheusQueryEndpoint}/api/v1/query?query={Uri.EscapeDataString(enhancedQuery)}";
            }
            else
            {
                // Range query
                var endTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var startTime = endTime - ParseDurationToSeconds(duration);
                apiUrl = $"{prometheusQueryEndpoint}/api/v1/query_range?query={Uri.EscapeDataString(enhancedQuery)}" +
                        $"&start={startTime}&end={endTime}&step={step}";
            }

            using var client = await CreateHttpClientAsync();
            var response = await client.GetStringAsync(apiUrl);

            // Parse and format response
            var data = JsonDocument.Parse(response);

            if (data.RootElement.GetProperty("status").GetString() != "success")
            {
                return $"Query failed: {data.RootElement.GetProperty("error").GetString()}";
            }

            var result = data.RootElement.GetProperty("data").GetProperty("result");

            return FormatAsCsv(result, minValue);
        }
        catch (Exception ex)
        {
            return $"PromQL query failed: {ex.Message}";
        }
    }

    private static string BuildEnhancedQuery(
        string query,
        string? labelFilters,
        string? aggregateFunction,
        string? aggregateBy,
        int? limit)
    {
        var enhancedQuery = query;

        // Add label filters
        if (!string.IsNullOrEmpty(labelFilters))
        {
            var filters = labelFilters.Split(',')
                .Select(f => f.Trim())
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => f.Contains('=') ? f : $"{f}=~\".*\""); // Add regex match if no operator

            if (query.Contains('{'))
            {
                // Insert filters into existing label selector
                enhancedQuery = query.Replace("}", $",{string.Join(",", filters)}}}");
            }
            else
            {
                // Add label selector
                enhancedQuery = $"{query}{{{string.Join(",", filters)}}}";
            }
        }

        // Apply aggregation
        if (!string.IsNullOrEmpty(aggregateFunction))
        {
            if (!string.IsNullOrEmpty(aggregateBy))
            {
                enhancedQuery = $"{aggregateFunction} by ({aggregateBy}) ({enhancedQuery})";
            }
            else
            {
                enhancedQuery = $"{aggregateFunction}({enhancedQuery})";
            }
        }

        // Apply limit using topk if specified
        if (limit.HasValue && string.IsNullOrEmpty(aggregateFunction))
        {
            enhancedQuery = $"topk({limit.Value}, {enhancedQuery})";
        }

        return enhancedQuery;
    }

    private static long ParseDurationToSeconds(string duration)
    {
        var unit = duration[^1];
        var value = int.Parse(duration[..^1]);

        return unit switch
        {
            's' => value,
            'm' => value * 60,
            'h' => value * 3600,
            'd' => value * 86400,
            _ => throw new ArgumentException($"Unsupported duration unit: {unit}")
        };
    }

    private static string FormatAsCsv(
        JsonElement result,
        double? minValue)
    {
        var output = new List<string> { "metric,labels,value,timestamp" };

        foreach (var series in result.EnumerateArray())
        {
            var metric = series.GetProperty("metric");
            var metricName = metric.TryGetProperty("__name__", out var name) ? name.GetString() : "unknown";

            var labels = string.Join(";", metric.EnumerateObject()
                .Where(p => p.Name != "__name__")
                .Select(p => $"{p.Name}={p.Value.GetString()}"));

            if (series.TryGetProperty("value", out var value))
            {
                var val = double.Parse(value[1].GetString()!);
                if (minValue == null || val >= minValue)
                {
                    output.Add($"{metricName},\"{labels}\",{val},{value[0].GetInt64()}");
                }
            }
            else if (series.TryGetProperty("values", out var values))
            {
                foreach (var valuePoint in values.EnumerateArray())
                {
                    var val = double.Parse(valuePoint[1].GetString()!);
                    if (minValue == null || val >= minValue)
                    {
                        output.Add($"{metricName},\"{labels}\",{val},{valuePoint[0].GetInt64()}");
                    }
                }
            }
        }

        return string.Join("\n", output);
    }

    public async Task<Response> QueryRangeAsync(string prometheusQueryEndpoint, string query, DateTime start, DateTime end, TimeSpan step, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(prometheusQueryEndpoint, nameof(prometheusQueryEndpoint));
        ArgumentException.ThrowIfNullOrEmpty(query, nameof(query));

        if (step.TotalSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(step), "Step must be greater than zero.");
        }

        using var client = await CreateHttpClientAsync();
        var dict = new Dictionary<string, string>
        {
            { "query", query },
            { "start", start.ToString("o", System.Globalization.CultureInfo.InvariantCulture) },
            { "end", end.ToString("o", System.Globalization.CultureInfo.InvariantCulture) },
            { "step", step.TotalSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture) },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{prometheusQueryEndpoint.TrimEnd('/')}/api/v1/query_range")
        {
            Content = new FormUrlEncodedContent(dict),
        };

        var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return DeserializeRangeQueryResponse(content);
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogInternalError("Failed to query Prometheus: {errorContent}", errorContent);
            throw new HttpRequestException($"Failed to query Prometheus: {response.StatusCode} - {errorContent}");
        }
    }

    private T? TryDeserialize<T>(string content)
    {
        try
        {
            var response = JsonSerializer.Deserialize<T>(content, options);
            if (response is not null)
            {
                return response;
            }
        }
        catch (Exception ex)
        {
            logger.LogInternalInformation(ex, "Failed to deserialize response: {content}", content);
        }
        return default;
    }

    private Response DeserializeRangeQueryResponse(string content)
    {
        var matrixResponse = TryDeserialize<SuccessMatrixResponse>(content);
        if (matrixResponse is not null)
        {
            return matrixResponse;
        }

        var errorResponse = TryDeserialize<ErrorResponse>(content);
        if (errorResponse is not null)
        {
            return errorResponse;
        }

        throw new JsonException($"Failed to deserialize response: {content}");
    }

    private Response DeserializeInstantQueryResponse(string content)
    {
        var vectorResponse = TryDeserialize<SuccessVectorResponse>(content);
        if (vectorResponse is not null)
        {
            return vectorResponse;
        }

        var matrixResponse = TryDeserialize<SuccessMatrixResponse>(content);
        if (matrixResponse is not null)
        {
            return matrixResponse;
        }

        var errorResponse = TryDeserialize<ErrorResponse>(content);
        if (errorResponse is not null)
        {
            return errorResponse;
        }

        throw new JsonException($"Failed to deserialize response: {content}");
    }

    private async Task<HttpClient> CreateHttpClientAsync()
    {
        var client = httpClientFactory.CreateClient();
        var token = await authService.GetCrawlerCredential().GetTokenAsync(new TokenRequestContext(["https://prometheus.monitor.azure.com//.default"]), default);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }
}
