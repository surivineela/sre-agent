// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Prometheus.Services;

/// <summary>
/// A service for querying Prometheus metrics.
/// </summary>
public interface IPrometheusQueryService
{
    /// <summary>
    /// Queries Prometheus for a specific metric at a given timestamp.
    /// https://prometheus.io/docs/prometheus/latest/querying/api/#instant-queries
    /// </summary>
    /// <param name="prometheusQueryEndpoint">Prometheus query endpoint</param>
    /// <param name="query">Prometheus expression query string</param>
    /// <param name="timestamp">Evaluation timestamp. Optional.</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<Response> QueryInstantAsync(string prometheusQueryEndpoint, string query, DateTime? timestamp = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries Prometheus for a specific metric over a range of time.
    /// https://prometheus.io/docs/prometheus/latest/querying/api/#range-queries
    /// </summary>
    /// <param name="prometheusQueryEndpoint">Prometheus query endpoint</param>
    /// <param name="query">Prometheus expression query string</param>
    /// <param name="start">Start timestamp, inclusive</param>
    /// <param name="end">End timestamp, inclusive</param>
    /// <param name="step">Query resolution step width in float number of seconds.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>QueryResponseDataMatrix on success</returns>
    Task<Response> QueryRangeAsync(string prometheusQueryEndpoint, string query, DateTime start, DateTime end, TimeSpan step, CancellationToken cancellationToken = default);

    Task<string> DiscoverMetricsAsync(
        string prometheusQueryEndpoint,
        string? namePattern,
        string? metricType);

    Task<string> GetMetricLabelsAsync(
        string prometheusQueryEndpoint,
        string metricName,
        string? labelName);

    Task<string> ExecutePromQLAsync(
        string prometheusQueryEndpoint,
        string query,
        string duration,
        string step,
        string? labelFilters,
        string? aggregateFunction,
        string? aggregateBy,
        int? limit,
        double? minValue);
}
