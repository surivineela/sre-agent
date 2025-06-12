using System.Diagnostics;
using OpenTelemetry;

namespace Agent.Logging;

public class CustomerAuditTraceFilteringProcessor : BaseProcessor<Activity>
{
    private readonly CustomerAuditLogger _customerAuditLogger;
    private readonly Func<Activity, bool> _filter;

    public CustomerAuditTraceFilteringProcessor(CustomerAuditLogger customerAuditLogger)
    {
        var crawlSubscriptions = GetCrawlSubscriptions();

        _customerAuditLogger = customerAuditLogger;
        _filter = activity =>
        {
            var url = activity?.Tags.FirstOrDefault(t => t.Key == "http.url").Value;

            var status = url is string u && crawlSubscriptions.Any(s => u.Contains(s));
            return status;
        };
    }

    public override void OnEnd(Activity activity)
    {
        if (_filter(activity))
        {
            _customerAuditLogger.LogRequest(activity);

            base.OnEnd(activity);
        }
    }

    private List<string> GetCrawlSubscriptions()
    {
        var crawlRoots = GetCrawlRoots();

        return crawlRoots
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Split('/', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[0] == "subscriptions")
            .Select(parts => $"{parts[0]}/{parts[1]}")
            .Distinct()
            .ToList();
    }

    private string GetCrawlRoots()
    {
        return Environment.GetEnvironmentVariable("AppSettings__Core__Azure__Crawler__CrawlRoots") ?? string.Empty;
    }
}
