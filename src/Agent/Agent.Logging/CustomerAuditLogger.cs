using System.Diagnostics;

namespace Agent.Logging;

public class CustomerAuditLogger : ApplicationInsightsLogger
{
    public CustomerAuditLogger() : base()
    {
    }

    public CustomerAuditLogger(string connectionString) : base(connectionString)
    {
    }

    public void LogRequest(Activity activity)
    {
        var url = activity.TagObjects.FirstOrDefault(t => t.Key == "http.url").Value as string;
        var method = activity.TagObjects.FirstOrDefault(t => t.Key == "http.method").Value as string;
        var statusCodeObj = activity.TagObjects.FirstOrDefault(t => t.Key == "http.status_code").Value;
        var duration = activity.Duration;

        // statusCode might be int or string depending on source
        string statusCodeStr = statusCodeObj switch
        {
            string s => s,
            int i => i.ToString(),
            short s => s.ToString(),
            long l => l.ToString(),
            _ => "unknown"
        };

        base.LogRequest(method, url, statusCodeStr, duration);
    }

    public new async Task FlushAsync(CancellationToken cancellationToken)
    {
        await base.FlushAsync(cancellationToken);
    }
}
