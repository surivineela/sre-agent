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

    protected void LogMessage(string message, SeverityLevel severityLevel)
    {
        if (_isConfigured)
        {
            _telemetryClient.TrackTrace(message, severityLevel);
        }
    }

    protected void LogRequest(string method, string url, string statusCode, TimeSpan duration)
    {
        if (_isConfigured)
        {
            _telemetryClient.TrackRequest(new RequestTelemetry
            {
                HttpMethod = method,
                Name = $"{method} {url}",
                Url = new Uri(url),
                Timestamp = DateTimeOffset.UtcNow,
                Duration = duration,
                ResponseCode = statusCode
            });
        }
    }

    protected void LogException(Exception ex, string message)
    {
        if (_isConfigured)
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
        if (_isConfigured)
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
