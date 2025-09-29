using Microsoft.ApplicationInsights.DataContracts;

namespace Agent.Logging;

public class CustomerLogger : ApplicationInsightsLogger
{
    public CustomerLogger() : base()
    {
    }

    public CustomerLogger(string connectionString) : base(connectionString)
    {
    }

    public void LogMessage(string message, IDictionary<string, string>? properties = null)
    {
        base.LogMessage(message, SeverityLevel.Information, properties);
    }


    public void LogError(string message)
    {
        base.LogMessage(message, SeverityLevel.Error);
    }

    public new void LogException(Exception ex, string message)
    {
        base.LogException(ex, message);
    }

    public new void LogCustomEvent(string eventName, Dictionary<string, string> properties)
    {
        base.LogCustomEvent(eventName, properties);
    }

    public new async Task FlushAsync(CancellationToken cancellationToken)
    {
        await base.FlushAsync(cancellationToken);
    }
}
