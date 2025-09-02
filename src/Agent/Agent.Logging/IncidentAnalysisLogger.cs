using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;

namespace Agent.Logging;
public class IncidentAnalysisLogger : ApplicationInsightsLogger
{
    public IncidentAnalysisLogger() : base()
    {
    }

    public IncidentAnalysisLogger(string connectionString) : base(connectionString)
    {
    }

    public void LogMessage(string message)
    {
        base.LogMessage(message, SeverityLevel.Information);
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
