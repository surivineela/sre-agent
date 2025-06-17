using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Logging;
using Agent.Prometheus.Extensions;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace Agent.Prometheus.Services;

public class GremlinMetricsService : IGremlinMetricsService, IDisposable
{
    private readonly IMetricsRegistry _metricsRegistry;
    private readonly ILogger<GremlinMetricsService> _logger;
    private CancellationTokenSource _cancellationTokenSource;
    private readonly ConcurrentDictionary<string, Task> _metricTasks = new();

    // Core metrics
    private readonly Gauge _vertexCountGauge;
    private readonly Gauge _edgeCountGauge;
    private readonly Gauge _queryLatencyGauge;
    private readonly Counter _errorsCounter;
    private readonly Gauge _resourceTypeCountGauge;
    private readonly Gauge _edgeTypeCountGauge;
    private readonly Gauge _vertexPropertyCountGauge;
    private readonly Gauge _edgePropertyCountGauge;
    private readonly IGraphDatabaseClient _graphDatabaseClient;
    private readonly IRemoteWriteService _remoteWriteService;
    private readonly string _agentName;

    private readonly Gauge _appHealthGauge;
    private readonly Gauge _appAvailabilityGauge;
    private readonly Gauge _appTransactionsGauge;
    private readonly Gauge _appCostsGauge;
    private readonly Gauge _appAvgLatencyInMsGauge;
    private readonly Gauge _appAvgMemoryUsageGauge;
    private readonly Gauge _appAvgCpuUsageGauge;

    public GremlinMetricsService(
        IMetricsRegistry metricsRegistry,
        ILogger<GremlinMetricsService> logger,
        IGraphDatabaseClient graphDatabaseClient,
        IRemoteWriteService remoteWriteService,
        IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            _agentName = "SREAgentTest";
        }
        else
        {
            _agentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? throw new ArgumentNullException("AGENT_NAME", "Environment variable AGENT_NAME is not set.");
        }
        _metricsRegistry = metricsRegistry;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();

        _graphDatabaseClient = graphDatabaseClient ?? throw new ArgumentNullException(nameof(graphDatabaseClient));
        _remoteWriteService = remoteWriteService ?? throw new ArgumentNullException(nameof(remoteWriteService));
        // Register static labels for all metrics so that metrics can be filtered by agent name
        // This is useful for multiple agents sharing the same Prometheus instance
        var staticLabels = new Dictionary<string, string>
        {
            { "agent_name", _agentName },
        };
        Metrics.DefaultRegistry.SetStaticLabels(staticLabels);
        // Define core Prometheus metrics
        _vertexCountGauge = Metrics.CreateGauge("gremlin_vertex_count", "Total number of vertices");
        _edgeCountGauge = Metrics.CreateGauge("gremlin_edge_count", "Total number of edges");
        _queryLatencyGauge = Metrics.CreateGauge("gremlin_query_latency_seconds", "Latency of Gremlin queries in seconds", new GaugeConfiguration
        {
            LabelNames = new[] { "query_type" }
        });
        _errorsCounter = Metrics.CreateCounter("gremlin_query_errors_total", "Total number of Gremlin query errors", new CounterConfiguration
        {
            LabelNames = new[] { "query_type" }
        });

        // Resource type metrics
        _resourceTypeCountGauge = Metrics.CreateGauge("gremlin_resource_type_count", "Count of resources by type", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type" }
        });

        // Edge type metrics
        _edgeTypeCountGauge = Metrics.CreateGauge("gremlin_edge_type_count", "Count of edges by type", new GaugeConfiguration
        {
            LabelNames = new[] { "edge_type" }
        });

        // Property metrics
        _vertexPropertyCountGauge = Metrics.CreateGauge("gremlin_vertex_property_count", "Count of vertex properties", new GaugeConfiguration
        {
            LabelNames = new[] { "property" }
        });

        _edgePropertyCountGauge = Metrics.CreateGauge("gremlin_edge_property_count", "Count of edge properties", new GaugeConfiguration
        {
            LabelNames = new[] { "property" }
        });

        _appHealthGauge = Metrics.CreateGauge("app_group_health", "App health status", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });

        _appAvailabilityGauge = Metrics.CreateGauge("app_group_availability", "App availability status", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });

        _appAvgCpuUsageGauge = Metrics.CreateGauge("app_group_avg_cpu_usage", "App average CPU usage", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });

        _appAvgLatencyInMsGauge = Metrics.CreateGauge("app_group_avg_latency_in_ms", "App average latency in milliseconds", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });

        _appAvgMemoryUsageGauge = Metrics.CreateGauge("app_group_avg_memory_usage", "App average memory usage", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });

        _appCostsGauge = Metrics.CreateGauge("app_group_costs", "App costs", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });

        _appTransactionsGauge = Metrics.CreateGauge("app_group_transactions", "App transactions", new GaugeConfiguration
        {
            LabelNames = new[] { "resource_type", "resource_id", "subscription_id", "location" }
        });
    }

    private bool ShouldBeExported(string metricName)
    {
        // Check if the metric name is in the list of core metrics.
        // Please update this if new core metrics are added that are not prefixed with "gremlin_"
        if (metricName.StartsWith("gremlin_") || metricName.StartsWith("app_group_"))
        {
            return true;
        }
        // Check if the metric name is in the list of exported metrics
        var metric = _metricsRegistry.GetMetric(metricName);
        if (metric.Name == metricName && metric.Status == "active")
        {
            return true;
        }
        return false;
    }

    public void StartMetricsCollection()
    {
        // Core metrics collection
        Task.Run(async () => await CollectCoreMetrics(_cancellationTokenSource.Token));

        // Resource type metrics
        Task.Run(async () => await CollectResourceTypeMetrics(_cancellationTokenSource.Token));

        // Edge type metrics
        Task.Run(async () => await CollectEdgeTypeMetrics(_cancellationTokenSource.Token));

        // Property metrics
        Task.Run(async () => await CollectPropertyMetrics(_cancellationTokenSource.Token));

        // Custom metrics from registry
        Task.Run(async () => await ProcessRegisteredMetrics(_cancellationTokenSource.Token));

        // Remote write metrics to Azure Managed Prometheus
        Task.Run(async () => await RemoteWriteMetricsAsync(_cancellationTokenSource.Token));

        // App group metrics collection
        Task.Run(async () => await CollectAppGroupMetrics(_cancellationTokenSource.Token));
    }

    // Export metrics in Text format and remote write to Azure Managed Prometheus(Azure Monitor Workspace)
    private async Task RemoteWriteMetricsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var stream = new MemoryStream();
                await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(stream, cancellationToken);
                stream.Position = 0;

                using var reader = new StreamReader(stream);
                var exportedMetrics = await reader.ReadToEndAsync(cancellationToken);
                if (string.IsNullOrEmpty(exportedMetrics))
                {
                    _logger.LogInternalWarning("No metrics to write to remote storage.");
                    await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                    continue;
                }

                _logger.LogDebug("Exported metrics in Text: {Metrics}", exportedMetrics);

                var metricsFamilies = PrometheusTextParser.Parse(exportedMetrics);
                _logger.LogInternalInformation("Parsed metrics n families: {count}", metricsFamilies.Count);
                var filteredMetricsFamilies = metricsFamilies.Where(m => ShouldBeExported(m.Name)).ToList();
                _logger.LogInternalInformation("Filtered metrics families: {count}", filteredMetricsFamilies.Count);
                var writeRequest = filteredMetricsFamilies.ToWriteRequest(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

                var succeeded = await _remoteWriteService.RemoteWriteAsync(writeRequest);

                if (!succeeded)
                {
                    _logger.LogInternalError("Failed to remote write metrics to Azure Managed Prometheus.");
                }
                else
                {
                    _logger.LogInternalInformation("Successfully remote wrote metrics to Azure Managed Prometheus.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Error writing metrics to remote storage");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
        }
    }

    record AppGroupMetricsItem(
        [property: JsonPropertyName("resourceType")] string ResourceType,
        [property: JsonPropertyName("resourceId")] string ResourceId,
        [property: JsonPropertyName("subscriptionId")] string SubscriptionId,
        [property: JsonPropertyName("location")] string Location,
        [property: JsonPropertyName("appHealthInfo")] string AppHealthInfo
    );


    private async Task CollectAppGroupMetrics(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting App group metrics collection");
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var appGroups = await _graphDatabaseClient.Query<Dictionary<string, object>>("g.V().has('isDeleted', false).has('resourceType').has('resourceId').has('subscriptionId').has('location').has('appHealthInfo').project('resourceType','resourceId', 'subscriptionId', 'location', 'appHealthInfo').by(values('resourceType')).by(values('resourceId')).by(values('subscriptionId')).by(values('location')).by(values('appHealthInfo'))");
                foreach (var app in appGroups)
                {
                    if (app is not null)
                    {
                        var resourceType = app.TryGetValue("resourceType", out var type) ? type.ToString() : null;
                        var resourceId = app.TryGetValue("resourceId", out var id) ? id.ToString() : null;
                        var subscriptionId = app.TryGetValue("subscriptionId", out var subId) ? subId.ToString() : null;
                        var location = app.TryGetValue("location", out var loc) ? loc.ToString() : null;
                        var appHealthInfoJson = app.TryGetValue("appHealthInfo", out var healthInfo) ? healthInfo.ToString() : null;

                        if (resourceType == null || resourceId == null || subscriptionId == null || location == null || appHealthInfoJson == null)
                        {
                            _logger.LogInternalWarning("App group metrics is missing required fields: {ResourceType}, {ResourceId}, {SubscriptionId}, {Location}, {AppHealthInfo}", resourceType, resourceId, subscriptionId, location, appHealthInfoJson);
                            continue;
                        }

                        var appHealthInfo = JsonSerializer.Deserialize<AppHealthInfo>(appHealthInfoJson);
                        if (appHealthInfo is not null && appHealthInfo.IsActive)
                        {
                            if (appHealthInfo.Costs.HasValue)
                            {
                                double cost = appHealthInfo.Costs.Value;
                                _appCostsGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(cost);
                            }

                            if (appHealthInfo.AvgCpuUsage.HasValue)
                            {
                                double cpuUsage = appHealthInfo.AvgCpuUsage.Value;
                                _appAvgCpuUsageGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(cpuUsage);
                            }

                            if (appHealthInfo.AvgLatencyInMs.HasValue)
                            {
                                double latency = appHealthInfo.AvgLatencyInMs.Value;
                                _appAvgLatencyInMsGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(latency);
                            }

                            if (appHealthInfo.AvgMemoryUsage.HasValue)
                            {
                                double memoryUsage = appHealthInfo.AvgMemoryUsage.Value;
                                _appAvgMemoryUsageGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(memoryUsage);
                            }

                            if (appHealthInfo.Availability.HasValue)
                            {
                                double availability = appHealthInfo.Availability.Value;
                                _appAvailabilityGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(availability);
                            }

                            if (appHealthInfo.Transactions.HasValue)
                            {
                                double transactions = appHealthInfo.Transactions.Value;
                                _appTransactionsGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(transactions);
                            }

                            int health = (int)appHealthInfo.Health;
                            _appHealthGauge.WithLabels(resourceType, resourceId, subscriptionId, location).Set(health);
                        }

                        _logger.LogInternalInformation("Got App group metrics: {ResourceType}, {ResourceId}, {SubscriptionId}, {Location}, {AppHealthInfo}", resourceType, resourceId, subscriptionId, location, appHealthInfoJson);
                    }
                    else
                    {
                        _logger.LogInternalWarning("App group metrics is null");
                    }
                }
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("app_group").Inc();
                _logger.LogInternalWarning(ex, "App group metrics collection failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    private async Task CollectCoreMetrics(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Vertex count
                var vertexCount = await ExecuteCountQuery("g.V().has('isDeleted', false).count()");
                _vertexCountGauge.Set(vertexCount);
                _logger.LogInternalInformation("Updated vertex count: {Count}", vertexCount);

                // Edge count
                var edgeCount = await ExecuteCountQuery("g.E().count()");
                _edgeCountGauge.Set(edgeCount);
                _logger.LogInternalInformation("Updated edge count: {Count}", edgeCount);
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("core").Inc();
                _logger.LogInternalWarning(ex, "Core metrics collection failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        }
    }

    private async Task CollectResourceTypeMetrics(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var resourceTypes = await ExecuteGroupCountQuery("g.V().has('isDeleted', false).not(has('resourceType', 'microsoft.web/sites')).groupCount().by('resourceType')");
                foreach (var type in resourceTypes)
                {
                    _resourceTypeCountGauge.WithLabels(type.Key).Set(type.Value);
                }

                // special handle for webapp / function app
                var webAppCount = await ExecuteGroupCountQuery("g.V().has('resourceType', 'microsoft.web/sites').has('isDeleted', false).groupCount().by('kind')");
                foreach (var type in webAppCount)
                {
                    _resourceTypeCountGauge.WithLabels($"microsoft.web/sites/{type.Key}").Set(type.Value);
                }

                _logger.LogInternalInformation("Updated resource type metrics");
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("resource_type").Inc();
                _logger.LogInternalWarning(ex, "Resource type metrics collection failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    private async Task CollectEdgeTypeMetrics(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var edgeTypes = await ExecuteGroupCountQuery("g.E().groupCount().by(label())");
                foreach (var type in edgeTypes)
                {
                    _edgeTypeCountGauge.WithLabels(type.Key).Set(type.Value);
                }
                _logger.LogInternalInformation("Updated edge type metrics");
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("edge_type").Inc();
                _logger.LogInternalWarning(ex, "Edge type metrics collection failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }

    private async Task CollectPropertyMetrics(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Vertex properties
                var vertexProperties = await ExecuteDeduplicationQuery("g.V().has('isDeleted', false).properties().key().dedup()");
                foreach (var prop in vertexProperties)
                {
                    var count = await ExecuteCountQuery($"g.V().has('isDeleted', false).has('{prop}').count()");
                    _vertexPropertyCountGauge.WithLabels(prop).Set(count);
                }

                // Edge properties
                var edgeProperties = await ExecuteDeduplicationQuery("g.E().properties().key().dedup()");
                foreach (var prop in edgeProperties)
                {
                    var count = await ExecuteCountQuery($"g.E().has('{prop}').count()");
                    _edgePropertyCountGauge.WithLabels(prop).Set(count);
                }

                _logger.LogInternalInformation("Updated property metrics");
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("property").Inc();
                _logger.LogInternalWarning(ex, "Property metrics collection failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(15), cancellationToken);
        }
    }

    private async Task ProcessRegisteredMetrics(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var metrics = _metricsRegistry.GetAllMetrics();

            foreach (var metric in metrics)
            {
                if (metric.Status != "active")
                    continue;

                // If metric task doesn't exist or has completed, start a new one
                if (!_metricTasks.TryGetValue(metric.Name, out var task) || task.IsCompleted)
                {
                    task = RunMetricCollection(metric, cancellationToken);
                    _metricTasks[metric.Name] = task;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }
    }

    private Task RunMetricCollection(MetricDefinition metric, CancellationToken cancellationToken)
    {
        return Task.Run(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ExecuteCustomMetricCollection(metric);

                    // Update last execution time
                    metric.LastUpdated = DateTime.UtcNow;
                    _metricsRegistry.UpdateMetric(metric.Name, metric);
                }
                catch (Exception ex)
                {
                    _errorsCounter.WithLabels("custom").Inc();
                    _logger.LogInternalWarning(ex, "Custom metric collection failed for {MetricName}", metric.Name);
                }

                await Task.Delay(TimeSpan.FromSeconds(metric.ScrapeIntervalSeconds), cancellationToken);
            }
        });
    }

    public async Task<long> ExecuteCountQuery(string query)
    {
        var startTime = DateTime.UtcNow;
        string queryType = "count";

        try
        {
            var resultSet = await _graphDatabaseClient.Query<object>(query);
            var rawResult = resultSet.FirstOrDefault();

            // Convert the result to long
            long result;
            if (rawResult is int intValue)
            {
                result = intValue;
            }
            else if (rawResult is long longValue)
            {
                result = longValue;
            }
            else
            {
                result = Convert.ToInt64(rawResult);
            }

            // Update latency metric
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);

            return result;
        }
        catch (Exception ex)
        {
            // Update latency even for failed queries
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);
            _errorsCounter.WithLabels(queryType).Inc();

            _logger.LogInternalWarning(ex, "Error executing count query: {Query}", query);
            throw new Exception($"Error executing query '{query}'", ex);
        }
    }

    public async Task<Dictionary<string, long>> ExecuteGroupCountQuery(string query)
    {
        var startTime = DateTime.UtcNow;
        string queryType = "group_count";

        try
        {
            var resultSet = await _graphDatabaseClient.Query<Dictionary<string, object>>(query);
            var result = resultSet.FirstOrDefault() ?? new Dictionary<string, object>();

            // Convert to a dictionary of string, long
            var typedResult = result.ToDictionary(
                kvp => kvp.Key,
                kvp => Convert.ToInt64(kvp.Value)
            );

            // Update latency metric
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);

            return typedResult;
        }
        catch (Exception ex)
        {
            // Update latency even for failed queries
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);
            _errorsCounter.WithLabels(queryType).Inc();

            _logger.LogInternalWarning(ex, "Error executing group count query: {Query}", query);
            throw new Exception($"Error executing query '{query}'", ex);
        }
    }

    public async Task<List<string>> ExecuteDeduplicationQuery(string query)
    {
        var startTime = DateTime.UtcNow;
        string queryType = "dedup";

        try
        {
            var resultSet = await _graphDatabaseClient.Query<string>(query);
            var result = resultSet.ToList();

            // Update latency metric
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);

            return result;
        }
        catch (Exception ex)
        {
            // Update latency even for failed queries
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);
            _errorsCounter.WithLabels(queryType).Inc();

            _logger.LogInternalWarning(ex, "Error executing deduplication query: {Query}", query);
            return [];
        }
    }

    public async Task ExecuteCustomMetricCollection(MetricDefinition metric)
    {
        if (string.IsNullOrEmpty(metric.Query))
        {
            _logger.LogInternalWarning("Empty query for metric {MetricName}", metric.Name);
            return;
        }

        var startTime = DateTime.UtcNow;
        string queryType = "custom";

        try
        {
            if (metric.Query.Contains("groupCount()"))
            {
                var result = await ExecuteGroupCountQuery(metric.Query);
                var gauge = Metrics.CreateGauge(metric.Name, metric.Description,
                    new GaugeConfiguration { LabelNames = new[] { "value" } });

                foreach (var item in result)
                {
                    _logger.LogInternalInformation("item.Key: {itemKey}, item.Value: {itemValue}", item.Key, item.Value);
                    gauge.WithLabels(item.Key).Set(item.Value);
                }
            }
            else if (metric.Query.Contains("dedup()"))
            {
                var result = await ExecuteDeduplicationQuery(metric.Query);
                var gauge = Metrics.CreateGauge(metric.Name, metric.Description,
                    new GaugeConfiguration { LabelNames = new[] { "value" } });

                foreach (var item in result)
                {
                    var count = await ExecuteCountQuery($"g.V().has('isDeleted', false).has('{item}').count()");
                    gauge.WithLabels(item).Set(count);
                }
            }
            else
            {
                var result = await ExecuteCountQuery(metric.Query);
                var gauge = Metrics.CreateGauge(metric.Name, metric.Description);
                gauge.Set(result);
            }

            // Update latency metric
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);
        }
        catch (Exception ex)
        {
            // Update latency even for failed queries
            var latency = (DateTime.UtcNow - startTime).TotalSeconds;
            _queryLatencyGauge.WithLabels(queryType).Set(latency);
            _errorsCounter.WithLabels(queryType).Inc();

            _logger.LogInternalWarning(ex, "Error executing custom metric query: {Query} for metric {MetricName}",
                metric.Query, metric.Name);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
