
namespace Agent.Prometheus.Services;

public interface IGremlinMetricsService
{
    void StartMetricsCollection();
    Task<long> ExecuteCountQuery(string query);
    Task<Dictionary<string, long>> ExecuteGroupCountQuery(string query);
    Task<List<string>> ExecuteDeduplicationQuery(string query);
    Task ExecuteCustomMetricCollection(MetricDefinition metric);
}
