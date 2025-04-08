namespace Agent.Prometheus.Services;

public enum MetricType
{
    Counter,
    Gauge,
    Histogram,
    Summary
}

public class MetricDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public MetricType Type { get; set; } = MetricType.Gauge;
    public int ScrapeIntervalSeconds { get; set; } = 60;
    public Dictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();
    public DateTime LastUpdated { get; set; }
    public string Status { get; set; } = "active";
}

public interface IMetricsRegistry
{
    bool RegisterMetric(MetricDefinition metric);
    bool UnregisterMetric(string name);
    List<MetricDefinition> GetAllMetrics();
    MetricDefinition GetMetric(string name);
    void UpdateMetric(string name, MetricDefinition metric);
}



