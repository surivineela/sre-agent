using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace Agent.Logging;

public abstract class ApplicationInsightsLogger
{
    protected readonly TelemetryClient? _telemetryClient;
    protected readonly bool _isConfigured;

    protected ApplicationInsightsLogger()
    {
        _isConfigured = false;
        _telemetryClient = null;
    }

    protected ApplicationInsightsLogger(string connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            _isConfigured = false;
            _telemetryClient = null;
        }
        else
        {
            var config = TelemetryConfiguration.CreateDefault();
            config.ConnectionString = connectionString;
            _telemetryClient = new TelemetryClient(config);
            _isConfigured = true;
        }
    }

    protected void LogMessage(string message, SeverityLevel severityLevel, IDictionary<string, string>? properties = null)
    {
        if (_isConfigured && _telemetryClient != null)
        {
            AlignOperationIdWithTraceId(properties);
            _telemetryClient.TrackTrace(message, severityLevel, properties);
        }
    }

    protected void LogRequest(string method, string url, string statusCode, TimeSpan duration)
    {
        if (_isConfigured && _telemetryClient != null)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _telemetryClient.TrackRequest(new RequestTelemetry
            {
                HttpMethod = method,
                Name = $"{method} {url}",
                Url = new Uri(url),
                Timestamp = DateTimeOffset.UtcNow,
                Duration = duration,
                ResponseCode = statusCode
            });
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    protected void LogException(Exception ex, string message)
    {
        if (_isConfigured && _telemetryClient != null)
        {
            _telemetryClient.TrackException(ex);
            if (!string.IsNullOrEmpty(message))
            {
                _telemetryClient.TrackTrace(message, SeverityLevel.Error);
            }
        }
    }

    protected void LogCustomEvent(string eventName, Dictionary<string, string> properties)
    {
        if (_isConfigured && _telemetryClient != null)
        {
            AlignOperationIdWithTraceId(properties);
            _telemetryClient.TrackEvent(eventName, properties);
        }
    }

    protected void LogDependency(DependencyTelemetry dependency, bool isStartOfTrace = false)
    {
        if (_isConfigured && _telemetryClient != null)
        {
            AlignOperationIdWithTraceId(dependency.Properties, isStartOfTrace);
            _telemetryClient.TrackDependency(dependency);
        }
    }

    protected virtual async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_isConfigured && _telemetryClient != null)
        {
            await _telemetryClient.FlushAsync(cancellationToken);
        }
    }

    private void AlignOperationIdWithTraceId(IDictionary<string, string>? properties, bool isStartOfTrace = false)
    {
        // FCP UI uses operation id to group logs. This requires customer logger to have same operation id across single trace.
        // Currently this gets regenerated every time a new System.Diagnostics.Activity is created.
        // This means a new operation id is created even if an internal activity is started.
        // For now, we manually set the operation id back to the current customer activity operation id
        if (properties != null && properties.TryGetValue("TraceId", out var traceId))
        {
            var context = _telemetryClient!.Context.Operation;
            if (!string.IsNullOrEmpty(traceId) && traceId != context.Id)
            {
                context.Id = traceId;
            }

            if (isStartOfTrace)
            {
                context.ParentId = traceId;
            }
            else
            {
                // parent id will be auto set to trace id if not set.
                properties.TryGetValue("SpanId", out var spanId);
                context.ParentId = spanId;
            }
        }
    }
}
