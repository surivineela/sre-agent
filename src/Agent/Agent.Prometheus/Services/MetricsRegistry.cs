using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Agent.Prometheus.Services;

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

    // TODO: move the metrics to a yaml file
    private void RegisterDefaultMetrics()
    {
        // Resource counts by subscription
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_subscription",
            Description = "Count of resources grouped by subscription",
            Query = "g.V().has('subscriptionId').groupCount().by('subscriptionId')",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 300
        });

        // Resource counts by resource group
        RegisterMetric(new MetricDefinition
        {
            Name = "resource_count_by_resource_group",
            Description = "Count of resources grouped by resource group",
            Query = "g.V().has('resourceGroupName').groupCount().by('resourceGroupName')",
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
            Query = "g.V().has('createdTime', gte(datetime() - 30*24*60*60*1000)).count()",
            Type = MetricType.Gauge,
            ScrapeIntervalSeconds = 600
        });

        // Resource tag metrics
        // Currently, there's no tags in the knowledge graph. 
        // RegisterMetric(new MetricDefinition
        // {
        //     Name = "resource_count_by_environment_tag",
        //     Description = "Count of resources grouped by environment tag",
        //     Query = "g.V().has('tags').where(__.values('tags').has('environment')).groupCount().by(__.values('tags').select('environment'))",
        //     Type = MetricType.Gauge,
        //     ScrapeIntervalSeconds = 300
        // });

        // RegisterMetric(new MetricDefinition
        // {
        //     Name = "resources_missing_required_tags",
        //     Description = "Count of resources missing required tags (environment, owner, costCenter)",
        //     Query = "g.V().not(__.has('tags').where(__.values('tags').has('environment').and().has('owner').and().has('costCenter'))).count()",
        //     Type = MetricType.Gauge,
        //     ScrapeIntervalSeconds = 300
        // });

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

        // Currently, there's no tags in the knowledge graph.
        // Resource cost metrics
        // RegisterMetric(new MetricDefinition
        // {
        //     Name = "resources_by_cost_center",
        //     Description = "Count of resources by cost center tag",
        //     Query = "g.V().has('tags').where(__.values('tags').has('costCenter')).groupCount().by(__.values('tags').select('costCenter'))",
        //     Type = MetricType.Gauge,
        //     ScrapeIntervalSeconds = 300
        // });

        // Currently, there's no 'createdTime' in the knowledge graph. Besides, cosmos gremlin doesn't support lambda syntax
        // Resource age distribution
        // RegisterMetric(new MetricDefinition
        // {
        //     Name = "resource_age_distribution",
        //     Description = "Distribution of resources by age in days",
        //     Query = "g.V().has('createdTime').groupCount().by{it.get().value('createdTime') > (new Date().getTime() - 30*24*60*60*1000) ? 'last30days' : it.get().value('createdTime') > (new Date().getTime() - 90*24*60*60*1000) ? 'last90days' : it.get().value('createdTime') > (new Date().getTime() - 180*24*60*60*1000) ? 'last180days' : 'older'}",
        //     Type = MetricType.Gauge,
        //     ScrapeIntervalSeconds = 3600
        // });

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
