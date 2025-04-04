// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Prometheus;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using GremlinMetricsExporter;

var builder = WebApplication.CreateBuilder(args);

// Add controller support
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// Prometheus Middleware
builder.Services.AddMetricServer(options =>
{
    options.Port = 9090;
    options.Url = "/metrics";
});

// Add configuration
builder.Services.Configure<GremlinConnectionConfig>(builder.Configuration.GetSection("GremlinConnection"));

// Add services
builder.Services.AddSingleton<IGremlinMetricsService, GremlinMetricsService>();
builder.Services.AddSingleton<IMetricsRegistry, MetricsRegistry>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Start metrics collection
var metricsService = app.Services.GetRequiredService<IGremlinMetricsService>();
app.Lifetime.ApplicationStarted.Register(() => metricsService.StartMetricsCollection());

app.MapGet("/", () => "Resource Graph Metrics API - Documentation at /swagger");

app.MapControllerRoute(
    name: "prometheus",
    pattern: "prometheus/{controller=Home}/{action=Index}/{id?}");

app.Run();

// Configuration
public class GremlinConnectionConfig
{
    public string Hostname { get; set; } = string.Empty;
    public int Port { get; set; } = 443;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// Models
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

public enum MetricType
{
    Counter,
    Gauge,
    Histogram,
    Summary
}

// Interfaces
public interface IGremlinMetricsService
{
    void StartMetricsCollection();
    Task<long> ExecuteCountQuery(string query);
    Task<Dictionary<string, long>> ExecuteGroupCountQuery(string query);
    Task<List<string>> ExecuteDeduplicationQuery(string query);
    Task ExecuteCustomMetricCollection(MetricDefinition metric);
}

public interface IMetricsRegistry
{
    bool RegisterMetric(MetricDefinition metric);
    bool UnregisterMetric(string name);
    List<MetricDefinition> GetAllMetrics();
    MetricDefinition GetMetric(string name);
    void UpdateMetric(string name, MetricDefinition metric);
}

// Implementations
public class GremlinMetricsService : IGremlinMetricsService, IDisposable
{
    private readonly GremlinClient _gremlinClient;
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

    public GremlinMetricsService(
        IOptions<GremlinConnectionConfig> config,
        IMetricsRegistry metricsRegistry,
        ILogger<GremlinMetricsService> logger)
    {
        _metricsRegistry = metricsRegistry;
        _logger = logger;
        _cancellationTokenSource = new CancellationTokenSource();

        // Setup Gremlin client
        var dbName = Environment.GetEnvironmentVariable("COSMOS_DATABASE") ?? "resourcegraph";
        var collectionName = Environment.GetEnvironmentVariable("COSMOS_COLLECTION") ?? "configuration";
        var username = $"/dbs/{dbName}/colls/{collectionName}";

        var gremlinServer = new GremlinServer(
            hostname: $"{Environment.GetEnvironmentVariable("COSMOS_ACCOUNT_NAME")}.gremlin.cosmos.azure.com",
            port: int.Parse(Environment.GetEnvironmentVariable("GREMLIN_PORT") ?? "443"),
            enableSsl: bool.Parse(Environment.GetEnvironmentVariable("GREMLIN_SSL_ENABLED") ?? "true"),
            username: username,
            password: Environment.GetEnvironmentVariable("COSMOS_KEY"));

        _gremlinClient = new GremlinClient(
            gremlinServer,
            new GraphSON2MessageSerializer(new CustomGraphSON2Reader(), new GraphSON2Writer()));

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
    }

    private async Task CollectCoreMetrics(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Vertex count
                var vertexCount = await ExecuteCountQuery("g.V().count()");
                _vertexCountGauge.Set(vertexCount);
                _logger.LogInformation("Updated vertex count: {Count}", vertexCount);

                // Edge count
                var edgeCount = await ExecuteCountQuery("g.E().count()");
                _edgeCountGauge.Set(edgeCount);
                _logger.LogInformation("Updated edge count: {Count}", edgeCount);
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("core").Inc();
                _logger.LogError(ex, "Core metrics collection failed");
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
                var resourceTypes = await ExecuteGroupCountQuery("g.V().groupCount().by('resourceType')");
                foreach (var type in resourceTypes)
                {
                    _resourceTypeCountGauge.WithLabels(type.Key).Set(type.Value);
                }
                _logger.LogInformation("Updated resource type metrics");
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("resource_type").Inc();
                _logger.LogError(ex, "Resource type metrics collection failed");
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
                _logger.LogInformation("Updated edge type metrics");
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("edge_type").Inc();
                _logger.LogError(ex, "Edge type metrics collection failed");
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
                var vertexProperties = await ExecuteDeduplicationQuery("g.V().properties().key().dedup()");
                foreach (var prop in vertexProperties)
                {
                    var count = await ExecuteCountQuery($"g.V().has('{prop}').count()");
                    _vertexPropertyCountGauge.WithLabels(prop).Set(count);
                }

                // Edge properties
                var edgeProperties = await ExecuteDeduplicationQuery("g.E().properties().key().dedup()");
                foreach (var prop in edgeProperties)
                {
                    var count = await ExecuteCountQuery($"g.E().has('{prop}').count()");
                    _edgePropertyCountGauge.WithLabels(prop).Set(count);
                }

                _logger.LogInformation("Updated property metrics");
            }
            catch (Exception ex)
            {
                _errorsCounter.WithLabels("property").Inc();
                _logger.LogError(ex, "Property metrics collection failed");
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
                    _logger.LogError(ex, "Custom metric collection failed for {MetricName}", metric.Name);
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
            var resultSet = await _gremlinClient.SubmitAsync<object>(query);
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

            _logger.LogError(ex, "Error executing count query: {Query}", query);
            throw new Exception($"Error executing query '{query}'", ex);
        }
    }

    public async Task<Dictionary<string, long>> ExecuteGroupCountQuery(string query)
    {
        var startTime = DateTime.UtcNow;
        string queryType = "group_count";

        try
        {
            var resultSet = await _gremlinClient.SubmitAsync<Dictionary<string, object>>(query);
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

            _logger.LogError(ex, "Error executing group count query: {Query}", query);
            throw new Exception($"Error executing query '{query}'", ex);
        }
    }

    public async Task<List<string>> ExecuteDeduplicationQuery(string query)
    {
        var startTime = DateTime.UtcNow;
        string queryType = "dedup";

        try
        {
            var resultSet = await _gremlinClient.SubmitAsync<string>(query);
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

            _logger.LogError(ex, "Error executing deduplication query: {Query}", query);
            throw new Exception($"Error executing query '{query}'", ex);
        }
    }

    public async Task ExecuteCustomMetricCollection(MetricDefinition metric)
    {
        if (string.IsNullOrEmpty(metric.Query))
        {
            _logger.LogWarning("Empty query for metric {MetricName}", metric.Name);
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
                    var count = await ExecuteCountQuery($"g.V().has('{item}').count()");
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

            _logger.LogError(ex, "Error executing custom metric query: {Query} for metric {MetricName}",
                metric.Query, metric.Name);
            throw;
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
        _gremlinClient.Dispose();
    }
}

public class MetricsRegistry : IMetricsRegistry
{
    private readonly ConcurrentDictionary<string, MetricDefinition> _metrics = new();
    private readonly ILogger<MetricsRegistry> _logger;

    public MetricsRegistry(ILogger<MetricsRegistry> logger)
    {
        _logger = logger;

        // Initialize with some useful default metrics
        RegisterDefaultMetrics();
    }

    private void RegisterDefaultMetrics()
    {
        // Resource counts by subscription
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_subscription",
            Description = "Count of resources grouped by subscription",
            Query = "g.V().groupCount().by('subscriptionId')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource counts by resource group
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_resource_group",
            Description = "Count of resources grouped by resource group",
            Query = "g.V().groupCount().by('resourceGroupName')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource counts by location
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_location",
            Description = "Count of resources grouped by location",
            Query = "g.V().has('location').groupCount().by('location')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Connection counts by type
        RegisterMetric(new MetricDefinition
        {
            Name = "connection_count_by_type",
            Description = "Count of connections by edge type",
            Query = "g.E().groupCount().by(label())",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Orphaned resources (no incoming or outgoing edges)
        RegisterMetric(new MetricDefinition
        {
            Name = "orphaned_resources_count",
            Description = "Count of resources with no connections",
            Query = "g.V().not(__.bothE()).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 600
        });

        // Resource status metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_provisioning_state",
            Description = "Count of resources grouped by provisioning state",
            Query = "g.V().has('provisioningState').groupCount().by('provisioningState')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource age metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_created_last_30_days",
            Description = "Count of resources created in the last 30 days",
            Query = "g.V().has('createdTime', gte(new Date().getTime() - 30*24*60*60*1000)).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 600
        });

        // Resource tag metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_environment_tag",
            Description = "Count of resources grouped by environment tag",
            Query = "g.V().has('tags').where(__.values('tags').has('environment')).groupCount().by(__.values('tags').select('environment'))",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "resources_missing_required_tags",
            Description = "Count of resources missing required tags (environment, owner, costCenter)",
            Query = "g.V().not(__.has('tags').where(__.values('tags').has('environment').and().has('owner').and().has('costCenter'))).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource type metrics by subscription
        RegisterMetric(new MetricDefinition
        {
            Name = "vm_count_by_subscription",
            Description = "Count of virtual machines by subscription",
            Query = "g.V().has('resourceType', 'Microsoft.Compute/virtualMachines').groupCount().by('subscriptionId')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "storage_account_count_by_subscription",
            Description = "Count of storage accounts by subscription",
            Query = "g.V().has('resourceType', 'Microsoft.Storage/storageAccounts').groupCount().by('subscriptionId')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Network connectivity metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "vnet_count_by_subscription",
            Description = "Count of virtual networks by subscription",
            Query = "g.V().has('resourceType', 'Microsoft.Network/virtualNetworks').groupCount().by('subscriptionId')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_public_ip",
            Description = "Count of resources with public IP addresses",
            Query = "g.V().where(__.out('has_public_ip')).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource configuration metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "vm_count_by_size",
            Description = "Count of VMs by size/SKU",
            Query = "g.V().has('resourceType', 'Microsoft.Compute/virtualMachines').groupCount().by('vmSize')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "storage_count_by_replication_type",
            Description = "Count of storage accounts by replication type",
            Query = "g.V().has('resourceType', 'Microsoft.Storage/storageAccounts').groupCount().by('replicationType')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Security metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_encryption_enabled",
            Description = "Count of resources with encryption enabled",
            Query = "g.V().has('encryptionEnabled', true).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_encryption_disabled",
            Description = "Count of resources with encryption disabled",
            Query = "g.V().has('encryptionEnabled', false).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "compliant_resources",
            Description = "Count of resources compliant with policy",
            Query = "g.V().has('complianceState', 'Compliant').count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource relationship metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "dependency_count_by_resource",
            Description = "Count of dependencies by resource",
            Query = "g.V().project('id', 'dependencyCount').by('id').by(__.outE('depends_on').count())",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 600
        });

        RegisterMetric(new MetricDefinition
        {
            Name = "resources_by_dependency_count",
            Description = "Resources grouped by number of dependencies",
            Query = "g.V().groupCount().by(__.outE('depends_on').count())",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 600
        });

        // Resource cost metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_by_cost_center",
            Description = "Count of resources by cost center tag",
            Query = "g.V().has('tags').where(__.values('tags').has('costCenter')).groupCount().by(__.values('tags').select('costCenter'))",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource age distribution
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_age_distribution",
            Description = "Distribution of resources by age in days",
            Query = "g.V().has('createdTime').groupCount().by{it.get().value('createdTime') > (new Date().getTime() - 30*24*60*60*1000) ? 'last30days' : it.get().value('createdTime') > (new Date().getTime() - 90*24*60*60*1000) ? 'last90days' : it.get().value('createdTime') > (new Date().getTime() - 180*24*60*60*1000) ? 'last180days' : 'older'}",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 3600
        });

        // Specific resource service metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "database_count_by_engine",
            Description = "Count of database resources by engine type",
            Query = "g.V().or(__.has('resourceType', 'Microsoft.Sql/servers'), __.has('resourceType', 'Microsoft.DBforMySQL/servers'), __.has('resourceType', 'Microsoft.DBforPostgreSQL/servers'), __.has('resourceType', 'Microsoft.DocumentDB/databaseAccounts')).groupCount().by('resourceType')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Connectivity between resources
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_connected_to_key_vault",
            Description = "Count of resources connected to Key Vault",
            Query = "g.V().has('resourceType', 'Microsoft.KeyVault/vaults').in('connected_to').count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Security configuration metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_network_acl",
            Description = "Count of resources with network ACLs configured",
            Query = "g.V().has('networkAclsEnabled', true).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource lock metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_locks",
            Description = "Count of resources with resource locks",
            Query = "g.V().where(__.out('has_lock')).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Metrics for resources in specific regions
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_in_primary_regions",
            Description = "Count of resources in primary regions (East US, West US, West Europe)",
            Query = "g.V().has('location', within('eastus', 'westus', 'westeurope')).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Diagnostic settings
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_diagnostics",
            Description = "Count of resources with diagnostic settings enabled",
            Query = "g.V().has('diagnosticsEnabled', true).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource lock metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_locks",
            Description = "Count of resources with resource locks",
            Query = "g.V().where(__.out('has_lock')).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Subnet metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "subnets_without_nsg",
            Description = "Count of subnets without connected NSG",
            Query = "g.V().has('resourceType', 'microsoft.network/virtualnetworks/subnets').not(__.in().has('resourceType', 'microsoft.network/networksecuritygroups')).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // SQL Connection Strings with plaintext
        RegisterMetric(new MetricDefinition
        {
            Name = "sql_connection_strings_with_plaintext",
            Description = "Count of SQL connection strings with plaintext credentials",
            Query = "g.V().and(has('resourceType', 'microsoft.sql/servers'), has('source', containing('CONNECTION_STRING'))).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Minimum TLS 1.2 metrics
        RegisterMetric(new MetricDefinition
        {
            Name = "resources_with_minimum_tls_1_2",
            Description = "Count of resources with minimum TLS 1.2 settings",
            Query = "g.V().or(has('minTlsVersion', '1.2'), has('tlsVersion', '1.2')).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });
    }

    public bool RegisterMetric(MetricDefinition metric)
    {
        if (string.IsNullOrEmpty(metric.Name))
        {
            _logger.LogWarning("Attempted to register a metric with an empty name");
            return false;
        }

        // Validate metric name for Prometheus compatibility
        if (!IsValidMetricName(metric.Name))
        {
            _logger.LogWarning("Invalid metric name: {MetricName}. Must match [a-zA-Z_:][a-zA-Z0-9_:]*", metric.Name);
            return false;
        }

        metric.LastUpdated = DateTime.UtcNow;
        var result = _metrics.TryAdd(metric.Name, metric);

        if (result)
        {
            _logger.LogInformation("Registered new metric: {MetricName}", metric.Name);
        }
        else
        {
            _logger.LogWarning("Failed to register metric - name already exists: {MetricName}", metric.Name);
        }

        return result;
    }

    public bool UnregisterMetric(string name)
    {
        var result = _metrics.TryRemove(name, out _);

        if (result)
        {
            _logger.LogInformation("Unregistered metric: {MetricName}", name);
        }
        else
        {
            _logger.LogWarning("Failed to unregister metric - not found: {MetricName}", name);
        }

        return result;
    }

    public List<MetricDefinition> GetAllMetrics()
    {
        return _metrics.Values.ToList();
    }

    public MetricDefinition GetMetric(string name)
    {
        if (_metrics.TryGetValue(name, out var metric))
        {
            return metric;
        }

        return new MetricDefinition();
    }

    public void UpdateMetric(string name, MetricDefinition metric)
    {
        if (_metrics.ContainsKey(name))
        {
            _metrics[name] = metric;
            _logger.LogInformation("Updated metric: {MetricName}", name);
        }
        else
        {
            _logger.LogWarning("Cannot update non-existent metric: {MetricName}", name);
        }
    }

    private bool IsValidMetricName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Prometheus metric naming rules
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z_:][a-zA-Z0-9_:]*$");
    }
}

// Controller for API endpoints
[ApiController]
[Route("api/[controller]")]
public class MetricsController : ControllerBase
{
    private readonly IMetricsRegistry _metricsRegistry;
    private readonly IGremlinMetricsService _metricsService;
    private readonly ILogger<MetricsController> _logger;

    public MetricsController(
        IMetricsRegistry metricsRegistry,
        IGremlinMetricsService metricsService,
        ILogger<MetricsController> logger)
    {
        _metricsRegistry = metricsRegistry;
        _metricsService = metricsService;
        _logger = logger;
    }

    [HttpGet]
    public ActionResult<IEnumerable<MetricDefinition>> GetAllMetrics()
    {
        return Ok(_metricsRegistry.GetAllMetrics());
    }

    [HttpGet("{name}")]
    public ActionResult<MetricDefinition> GetMetric(string name)
    {
        var metric = _metricsRegistry.GetMetric(name);

        if (string.IsNullOrEmpty(metric.Name))
        {
            return NotFound();
        }

        return Ok(metric);
    }

    [HttpPost]
    public ActionResult RegisterMetric([FromBody] MetricDefinition metric)
    {
        if (string.IsNullOrEmpty(metric.Name) || string.IsNullOrEmpty(metric.Query))
        {
            return BadRequest("Metric name and query are required");
        }

        var result = _metricsRegistry.RegisterMetric(metric);

        if (result)
        {
            // Immediately try to collect this metric once to validate it works
            try
            {
                _metricsService.ExecuteCustomMetricCollection(metric).Wait();
                return CreatedAtAction(nameof(GetMetric), new { name = metric.Name }, metric);
            }
            catch (Exception ex)
            {
                // If collection fails, unregister the metric
                _metricsRegistry.UnregisterMetric(metric.Name);
                return BadRequest($"Metric registration failed: {ex.Message}");
            }
        }

        return Conflict("A metric with this name already exists");
    }

    [HttpPut("{name}")]
    public ActionResult UpdateMetric(string name, [FromBody] MetricDefinition metric)
    {
        var existingMetric = _metricsRegistry.GetMetric(name);

        if (string.IsNullOrEmpty(existingMetric.Name))
        {
            return NotFound();
        }

        // Preserve the name
        metric.Name = name;

        // Update the metric
        _metricsRegistry.UpdateMetric(name, metric);

        return NoContent();
    }

    [HttpDelete("{name}")]
    public ActionResult UnregisterMetric(string name)
    {
        var result = _metricsRegistry.UnregisterMetric(name);

        if (result)
        {
            return NoContent();
        }

        return NotFound();
    }

    [HttpPost("{name}/test")]
    public async Task<ActionResult> TestMetric(string name)
    {
        var metric = _metricsRegistry.GetMetric(name);

        if (string.IsNullOrEmpty(metric.Name))
        {
            return NotFound();
        }

        try
        {
            await _metricsService.ExecuteCustomMetricCollection(metric);
            return Ok(new { message = "Metric query executed successfully" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{name}/enable")]
    public ActionResult EnableMetric(string name)
    {
        var metric = _metricsRegistry.GetMetric(name);

        if (string.IsNullOrEmpty(metric.Name))
        {
            return NotFound();
        }

        metric.Status = "active";
        _metricsRegistry.UpdateMetric(name, metric);

        return NoContent();
    }

    [HttpPost("{name}/disable")]
    public ActionResult DisableMetric(string name)
    {
        var metric = _metricsRegistry.GetMetric(name);

        if (string.IsNullOrEmpty(metric.Name))
        {
            return NotFound();
        }

        metric.Status = "inactive";
        _metricsRegistry.UpdateMetric(name, metric);

        return NoContent();
    }
}

