using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using Agent.Logging;

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
