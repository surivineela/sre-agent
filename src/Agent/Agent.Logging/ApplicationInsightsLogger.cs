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
            _telemetryClient.TrackEvent(eventName, properties);
        }
    }

    protected virtual async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_isConfigured && _telemetryClient != null)
        {
            await _telemetryClient.FlushAsync(cancellationToken);
        }
    }
}
