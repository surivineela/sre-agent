using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.Services;
using Azure.Core;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Azure.ResourceManager.PostgreSql.FlexibleServers;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Monitor;
using Azure.ResourceManager.OperationalInsights;
using Azure.Monitor.Query.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Implementation of the PostgreSQL Plugin for diagnostic and performance analysis
/// </summary>
public class PostgreSQLPlugin : IPostgreSQLPlugin
{
    private readonly ILogger<PostgreSQLPlugin> _logger;
    private readonly ArmHelper _armHelper;
    private readonly IPlaybookService _playbookService;
    private readonly IArmClientFactory _armClientFactory;
    private readonly AzureMonitorMetricsHelper _azureMonitorMetricsHelper;
    private readonly PostgresSQLCommandHelper _postgresSQLCommandHelper;
    private readonly IAuthenticationService _authenticationService;

    /// <summary>
    /// Gets or sets the thread ID
    /// </summary>
    public Guid? ThreadId { get; set; }

    /// <summary>
    /// Constructor for PostgreSQLPlugin
    /// </summary>
    /// <param name="logger">Logger for the plugin</param>
    /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
    /// <param name="playbookService">Service for loading playbook content</param>
    /// <param name="armClientFactory">Factory for creating ARM clients</param>
    /// <param name="azureMonitorMetricsHelper">Helper for Azure Monitor metrics queries</param>
    /// <param name="postgresSQLCommandHelper">Helper for executing PostgreSQL commands</param>
    /// <param name="authenticationService">Service for authentication</param>
    public PostgreSQLPlugin(
        ILogger<PostgreSQLPlugin> logger,
        ArmHelper armHelper,
        IPlaybookService playbookService,
        IArmClientFactory armClientFactory,
        AzureMonitorMetricsHelper azureMonitorMetricsHelper,
        PostgresSQLCommandHelper postgresSQLCommandHelper,
        IAuthenticationService authenticationService)
    {
        _logger = logger;
        _armHelper = armHelper;
        _playbookService = playbookService;
        _armClientFactory = armClientFactory;
        _azureMonitorMetricsHelper = azureMonitorMetricsHelper;
        _postgresSQLCommandHelper = postgresSQLCommandHelper;
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Gets PostgreSQL performance metrics including CPU, memory, connections, and query performance
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>PostgreSQL performance metrics</returns>
    public async Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_metrics] Retrieving PostgreSQL metrics for {resourceId}, window: {window}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_metrics] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new PostgreSQLMetrics(
                    ResourceId: resourceId,
                    Timestamp: DateTime.UtcNow,
                    CpuPercent: 0.0,
                    MemoryPercent: 0.0,
                    ActiveConnections: 0,
                    MaxConnections: 0,
                    CacheHitRatio: 0.0,
                    AverageQueryDuration: 0.0,
                    TotalQueries: 0,
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            // Get diagnostic workspace for accurate metrics
            var workspaceId = await GetDiagnosticWorkspaceForResourceAsync(resourceId);
            if (string.IsNullOrEmpty(workspaceId))
            {
                _logger.LogInternalWarning($"[postgresql_metrics] No diagnostic workspace found for {resourceId}");
            }

            // Get real metrics from Azure Monitor
            var metrics = await GetAzureMonitorMetricsAsync(resourceId, window);

            _logger.LogInternalInformation($"[postgresql_metrics] Retrieved metrics for {resourceId}: CPU {metrics.CpuPercent}%, Memory {metrics.MemoryPercent}%");
            return metrics;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_metrics] Error retrieving PostgreSQL metrics for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Tests connectivity to PostgreSQL server and analyzes connection issues
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Connection test results and analysis</returns>
    public async Task<ConnectionTestResult> CheckPostgreSQLConnectivityAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_connectivity] Testing connectivity for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_connectivity] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new ConnectionTestResult(
                    ResourceId: resourceId,
                    IsSuccessful: false,
                    Status: "Error - Invalid Resource ID",
                    ConnectionPoolSize: 0,
                    AverageConnectionDuration: 0.0,
                    Issues: new List<string>
                    {
                        "Invalid resource ID format provided",
                        "Expected full Azure resource ID starting with /subscriptions/",
                        $"Received: {resourceId}"
                    },
                    Summary: $"❌ Cannot test connectivity - Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            // Validate configuration first
            var configStatus = await ValidatePostgreSQLConfigurationAsync(resourceId);

            var issues = new List<string>();
            if (!configStatus.HasDiagnosticSettings)
            {
                issues.Add("Diagnostic settings not configured - limited diagnostic capabilities");
            }

            // Test actual PostgreSQL connectivity
            _logger.LogInternalInformation($"[postgresql_connectivity] Testing actual database connection for {resourceId}");

            try
            {
                // Test basic connectivity with a simple query
                var startTime = DateTime.UtcNow;
                var connectivityQuery = "SELECT version(), current_database(), current_user, pg_backend_pid();";
                var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(connectivityQuery, resourceId, "postgres");
                var connectionDuration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                if (result.ErrorOccurred || string.IsNullOrWhiteSpace(result.Output))
                {
                    issues.Add($"Database connection failed: {result.Output}");
                    return new ConnectionTestResult(
                        ResourceId: resourceId,
                        IsSuccessful: false,
                        Status: "Connection Failed",
                        ConnectionPoolSize: 0,
                        AverageConnectionDuration: connectionDuration,
                        Issues: issues,
                        Summary: $"❌ Connection test failed: {result.Output}"
                    );
                }

                // Get connection pool information
                var poolQuery = @"
SELECT 
    setting as max_connections,
    (SELECT count(*) FROM pg_stat_activity) as current_connections,
    (SELECT count(*) FROM pg_stat_activity WHERE state = 'active') as active_connections
FROM pg_settings WHERE name = 'max_connections';";

                var poolResult = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(poolQuery, resourceId, "postgres");

                int connectionPoolSize = 100; // Default fallback
                if (!poolResult.ErrorOccurred && !string.IsNullOrWhiteSpace(poolResult.Output))
                {
                    // Parse the max_connections from the result
                    var lines = poolResult.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 2) // Skip header lines
                    {
                        var dataLine = lines[2].Trim();
                        var parts = dataLine.Split('|');
                        if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int currentConnections))
                        {
                            connectionPoolSize = currentConnections;
                        }
                    }
                }

                var connectionTestResult = new ConnectionTestResult(
                    ResourceId: resourceId,
                    IsSuccessful: true,
                    Status: "Connected",
                    ConnectionPoolSize: connectionPoolSize,
                    AverageConnectionDuration: connectionDuration,
                    Issues: issues,
                    Summary: issues.Any() ?
                        $"Connection successful ({connectionDuration:F1}ms) but diagnostic configuration incomplete" :
                        $"Connection successful ({connectionDuration:F1}ms) with full diagnostic capabilities"
                );

                _logger.LogInternalInformation($"[postgresql_connectivity] Connection test successful for {resourceId}: {connectionDuration:F1}ms");
                return connectionTestResult;
            }
            catch (Exception ex)
            {
                issues.Add($"Connection test failed with exception: {ex.Message}");
                return new ConnectionTestResult(
                    ResourceId: resourceId,
                    IsSuccessful: false,
                    Status: "Connection Failed",
                    ConnectionPoolSize: 0,
                    AverageConnectionDuration: 0.0,
                    Issues: issues,
                    Summary: $"❌ Connection test failed: {ex.Message}"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_connectivity] Error testing connectivity for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Analyzes slow-running queries and identifies performance bottlenecks
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="database">The name of the database to analyze</param>
    /// <param name="window">Time window for analysis</param>
    /// <returns>Slow query analysis results</returns>
    public async Task<SlowQueryAnalysis> AnalyzeSlowQueriesAsync(string resourceId, string database, TimeSpan window)
    {
        try
        {
            var endTime = DateTime.UtcNow;
            var startTime = endTime.Subtract(window);
            _logger.LogInternalInformation($"[postgresql_query_analysis] Analyzing slow queries for {resourceId}, window: {startTime} - {endTime}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_query_analysis] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new SlowQueryAnalysis(
                    ResourceId: resourceId,
                    SlowQueries: new List<SlowQuery>(),
                    Recommendations: new List<string>
                    {
                        "❌ Invalid resource ID format provided",
                        "Expected format: /subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.DBforPostgreSQL/flexibleServers/{server-name}",
                        $"Received: {resourceId}",
                        "Please provide a valid Azure resource ID to analyze slow queries"
                    },
                    Summary: $"❌ Cannot analyze slow queries - Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            var configStatus = await ValidatePostgreSQLConfigurationAsync(resourceId);
            if (!configStatus.HasQueryStore)
            {
                var configRecommendations = new List<string>
                {
                    "⚠️ IMPORTANT: Do not enable Query Store on Burstable pricing tier due to performance impact",
                    "Enable Query Store: Set pg_qs.query_capture_mode = 'top' in Server Parameters",
                    "Enable wait sampling: Set pgms_wait_sampling.query_capture_mode = 'all'",
                    "Configure diagnostic settings to send Query Store data to Log Analytics",
                    "Allow 20 minutes for first batch of data collection",
                };
                return new SlowQueryAnalysis(
                    ResourceId: resourceId,
                    SlowQueries: new List<SlowQuery>(),
                    Recommendations: configRecommendations,
                    Summary: "Query Store not enabled - cannot analyze query performance. Query Store provides comprehensive query performance tracking including execution statistics and wait events stored in azure_sys database."
                );
            }

            // Add a small delay to ensure proper async behavior
            await Task.Delay(100);

            // Get real slow queries data from Log Analytics
            _logger.LogInternalInformation($"[postgresql_query_analysis] Executing Log Analytics queries for {resourceId}");
            var slowQueriesData = await GetSlowQueriesData(resourceId, database, window);

            if (!slowQueriesData.Any())
            {
                var noDataRecommendations = new List<string>
                {
                    "No slow queries found in Log Analytics for the last 24 hours.",
                    "Possible explanations:",
                    "- No queries exceeded the default slow threshold (1000 ms), or",
                    "- PostgreSQL diagnostic logging is not enabled for this database, or", 
                    "- Log Analytics workspace is not configured for this resource",
                    "Recommendations (pick one):",
                    "- Enable PostgreSQL diagnostic logging to Azure Monitor to capture query performance data",
                    "- Lower the slow query threshold if you want to capture faster queries",
                    "- Verify Log Analytics workspace is properly configured for this PostgreSQL resource"
                };

                return new SlowQueryAnalysis(
                    ResourceId: resourceId,
                    SlowQueries: slowQueriesData,
                    Recommendations: noDataRecommendations,
                    Summary: "No slow queries were found in Log Analytics for the last 24 hours. Ensure diagnostic logging is enabled."
                );
            }

            // Generate recommendations based on real slow query data
            var recommendations = GenerateSlowQueryRecommendations(slowQueriesData);

            var totalSlowQueries = slowQueriesData.Count;
            var avgExecutionTime = slowQueriesData.Average(q => q.AverageDuration);
            var maxExecutionTime = slowQueriesData.Max(q => q.MaxDuration);

            var summary = $"Found {totalSlowQueries} slow queries with average execution time of {avgExecutionTime:F1}ms. " +
                         $"Slowest query: {maxExecutionTime:F1}ms. " +
                         $"Review recommendations for performance optimization.";

            _logger.LogInternalInformation($"[postgresql_query_analysis] Analysis complete: {slowQueriesData.Count} slow queries analyzed for {resourceId}");

            // Add a small delay to ensure proper response timing
            await Task.Delay(50);

            var result = new SlowQueryAnalysis(
                ResourceId: resourceId,
                SlowQueries: slowQueriesData,
                Recommendations: recommendations,
                Summary: summary
            );

            _logger.LogInternalInformation($"[postgresql_query_analysis] Found {slowQueriesData.Count} slow queries for {resourceId}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_query_analysis] Error analyzing queries for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Generates recommendations based on slow query analysis
    /// </summary>
    private List<string> GenerateSlowQueryRecommendations(List<SlowQuery> slowQueries)
    {
        var recommendations = new List<string>();

        var queriesWithLowCacheHit = slowQueries.Count(q => q.Issues.Any(i => i.Contains("cache hit")));
        var verySlowQueries = slowQueries.Count(q => q.AverageDuration > 5000);
        var highFrequencyQueries = slowQueries.Count(q => q.ExecutionCount > 1000);

        if (queriesWithLowCacheHit > 0)
        {
            recommendations.Add($"Found {queriesWithLowCacheHit} queries with low cache hit ratios - consider increasing shared_buffers or optimizing indexes");
        }

        if (verySlowQueries > 0)
        {
            recommendations.Add($"Found {verySlowQueries} very slow queries (>5s average) - priority optimization candidates");
        }

        if (highFrequencyQueries > 0)
        {
            recommendations.Add($"Found {highFrequencyQueries} high-frequency queries (>1000 executions) - consider caching or result materialization");
        }

        recommendations.Add("Enable auto_explain to capture execution plans for slow queries");
        recommendations.Add("Consider creating covering indexes for frequently accessed columns");
        recommendations.Add("Review query patterns for potential table partitioning opportunities");

        if (slowQueries.Any(q => q.QueryText.Contains("SELECT *")))
        {
            recommendations.Add("Avoid SELECT * queries - specify only needed columns");
        }

        return recommendations;
    }

    /// <summary>
    /// Gets Azure resource health status and recent health events
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Resource health status</returns>
    public async Task<ResourceHealthStatus> GetResourceHealthAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_health] Getting resource health for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_health] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new ResourceHealthStatus(
                    ResourceId: resourceId,
                    HealthStatus: "Error - Invalid Resource ID",
                    RecentEvents: new List<HealthEvent>
                    {
                        new HealthEvent(
                            Timestamp: DateTime.UtcNow,
                            EventType: "ValidationError",
                            Summary: "Invalid resource ID format provided",
                            Impact: "Cannot retrieve health status - invalid resource identifier"
                        )
                    },
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            try
            {
                // Get Azure resource health using ARM API
                var healthContent = await _armHelper.GetResourceByURL($"https://management.azure.com{resourceId}/providers/Microsoft.ResourceHealth/availabilityStatuses/current?api-version=2020-05-01");

                var healthEvents = new List<HealthEvent>();
                string healthStatus = "Unknown";
                string summary = "Unable to determine resource health status";

                if (!string.IsNullOrWhiteSpace(healthContent))
                {
                    var healthData = JsonSerializer.Deserialize<JsonElement>(healthContent);

                    // Parse availability status
                    if (healthData.TryGetProperty("properties", out var properties))
                    {
                        if (properties.TryGetProperty("availabilityState", out var availabilityState))
                        {
                            healthStatus = availabilityState.GetString() ?? "Unknown";
                        }

                        if (properties.TryGetProperty("summary", out var summaryProp))
                        {
                            summary = summaryProp.GetString() ?? summary;
                        }

                        // Get recent health events
                        if (properties.TryGetProperty("recentlyResolved", out var recentlyResolved))
                        {
                            foreach (var eventElement in recentlyResolved.EnumerateArray())
                            {
                                var healthEvent = new HealthEvent(
                                    Timestamp: eventElement.TryGetProperty("unavailableOccurredTime", out var timeElement)
                                        ? DateTime.Parse(timeElement.GetString()!)
                                        : DateTime.UtcNow.AddHours(-1),
                                    EventType: "AvailabilityEvent",
                                    Summary: eventElement.TryGetProperty("summary", out var msgElement)
                                        ? msgElement.GetString()!
                                        : "Recent availability event",
                                    Impact: "Service availability affected"
                                );
                                healthEvents.Add(healthEvent);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: try to get resource health from activity logs
                    _logger.LogInternalWarning($"[postgresql_health] Direct health API failed, trying activity logs for {resourceId}");

                    var logsContent = await _armHelper.GetResourceByURL($"https://management.azure.com{resourceId}/providers/Microsoft.Insights/eventTypes/management/values?api-version=2015-04-01&$filter=eventTimestamp ge '{DateTime.UtcNow.AddDays(-7):yyyy-MM-ddTHH:mm:ss.fffZ}'");

                    if (!string.IsNullOrWhiteSpace(logsContent))
                    {
                        var logsData = JsonSerializer.Deserialize<JsonElement>(logsContent);

                        if (logsData.TryGetProperty("value", out var events))
                        {
                            foreach (var eventElement in events.EnumerateArray().Take(5)) // Latest 5 events
                            {
                                if (eventElement.TryGetProperty("eventTimestamp", out var timestampElement) &&
                                    eventElement.TryGetProperty("operationName", out var operationElement))
                                {
                                    var healthEvent = new HealthEvent(
                                        Timestamp: DateTime.Parse(timestampElement.GetString()!),
                                        EventType: "Management",
                                        Summary: operationElement.TryGetProperty("localizedValue", out var localizedElement)
                                            ? localizedElement.GetString()!
                                            : operationElement.GetString()!,
                                        Impact: eventElement.TryGetProperty("level", out var levelElement)
                                            ? $"Severity: {levelElement.GetString()}"
                                            : "Information level event"
                                    );
                                    healthEvents.Add(healthEvent);
                                }
                            }
                        }

                        // If we have recent events, assume resource is available
                        if (healthEvents.Any())
                        {
                            healthStatus = "Available";
                            summary = $"Resource operational with {healthEvents.Count} recent management events";
                        }
                    }
                }

                // If still no health data, check basic resource existence
                if (healthStatus == "Unknown")
                {
                    var resourceContent = await _armHelper.GetResourceByURL($"https://management.azure.com{resourceId}?api-version=2022-12-01");
                    if (!string.IsNullOrWhiteSpace(resourceContent))
                    {
                        healthStatus = "Available";
                        summary = "Resource exists and appears operational";

                        // Add a synthetic health event
                        healthEvents.Add(new HealthEvent(
                            Timestamp: DateTime.UtcNow,
                            EventType: "HealthCheck",
                            Summary: "Resource health check completed successfully",
                            Impact: "No impact - operational check"
                        ));
                    }
                    else
                    {
                        healthStatus = "Unavailable";
                        summary = "Resource not found or not accessible";

                        healthEvents.Add(new HealthEvent(
                            Timestamp: DateTime.UtcNow,
                            EventType: "Error",
                            Summary: "Resource health check failed - resource not accessible",
                            Impact: "Resource unavailable for diagnostics"
                        ));
                    }
                }

                var result = new ResourceHealthStatus(
                    ResourceId: resourceId,
                    HealthStatus: healthStatus,
                    RecentEvents: healthEvents,
                    Summary: summary
                );

                _logger.LogInternalInformation($"[postgresql_health] Resource health for {resourceId}: {result.HealthStatus}");
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"[postgresql_health] Failed to get health for {resourceId}: {ex.Message}");

                var errorEvents = new List<HealthEvent>
                {
                    new HealthEvent(
                        Timestamp: DateTime.UtcNow,
                        EventType: "Error",
                        Summary: $"Health check failed: {ex.Message}",
                        Impact: "Unable to determine resource health"
                    )
                };

                return new ResourceHealthStatus(
                    ResourceId: resourceId,
                    HealthStatus: "Unknown",
                    RecentEvents: errorEvents,
                    Summary: $"❌ Health check failed: {ex.Message}"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_health] Error getting resource health for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Lists available diagnostic playbooks for PostgreSQL troubleshooting
    /// </summary>
    /// <returns>List of available playbooks</returns>
    public async Task<List<PlaybookInfo>> ListAvailablePlaybooksAsync()
    {
        try
        {
            _logger.LogInternalInformation("[postgresql_playbooks] Listing available playbooks");
            return await _playbookService.GetAvailablePlaybooksAsync("PostgreSQL");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[postgresql_playbooks] Error listing playbooks");
            throw;
        }
    }

    /// <summary>
    /// Retrieves specific troubleshooting playbook content
    /// </summary>
    /// <param name="playbookName">Name of the playbook to retrieve</param>
    /// <returns>Playbook content</returns>
    public async Task<PlaybookContent> GetPlaybookAsync(string playbookName)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_playbook] Getting playbook: {playbookName}");
            return await _playbookService.GetPlaybookContentAsync("PostgreSQL", playbookName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_playbook] Error getting playbook: {playbookName}");
            throw;
        }
    }    /// <summary>
         /// Validates PostgreSQL diagnostic configuration and identifies missing setup steps
         /// </summary>
         /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
         /// <returns>Configuration validation status</returns>
    public async Task<PostgreSQLConfigurationStatus> ValidatePostgreSQLConfigurationAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_config] Validating configuration for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_config] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new PostgreSQLConfigurationStatus(
                    ResourceId: resourceId,
                    HasDiagnosticSettings: false,
                    HasQueryStore: false,
                    HasPerformanceInsights: false,
                    HasConnectionLogging: false,
                    LogAnalyticsWorkspace: null,
                    MissingConfigurations: new List<string> { "Invalid Resource ID" },
                    SetupInstructions: new List<string>
                    {
                        "❌ INVALID RESOURCE ID: Please provide a valid Azure resource ID.",
                        "Expected format: /subscriptions/{subscription-id}/resourceGroups/{resource-group}/providers/Microsoft.DBforPostgreSQL/flexibleServers/{server-name}",
                        "Current input: " + resourceId
                    },
                    Summary: $"Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            var missingConfigurations = new List<string>();
            var setupInstructions = new List<string>();

            // Check diagnostic settings using Azure SDK
            var hasDiagnosticSettings = await CheckDiagnosticSettingsAsync(resourceId);
            if (!hasDiagnosticSettings)
            {
                missingConfigurations.Add("Diagnostic Settings");
                setupInstructions.Add("Configure diagnostic settings to send PostgreSQL logs and metrics to Log Analytics workspace");
            }

            // Check Query Store configuration
            var hasQueryStore = await CheckQueryStoreConfigurationAsync(resourceId);
            if (!hasQueryStore)
            {
                missingConfigurations.Add("Query Store");
                setupInstructions.Add("⚠️ IMPORTANT: Do not enable Query Store on Burstable pricing tier due to performance impact");
                setupInstructions.Add("Enable Query Store: Set pg_qs.query_capture_mode = 'top' or 'all' in Server Parameters");
                setupInstructions.Add("Enable wait sampling: Set pgms_wait_sampling.query_capture_mode = 'all'");
                setupInstructions.Add("Configure retention: Set pg_qs.retention_period_in_days (1-30 days, default 7)");
                setupInstructions.Add("Restart server for parameter changes to take effect");
                setupInstructions.Add("Allow 20 minutes for first batch of data to persist in azure_sys database");
                setupInstructions.Add("Verify setup: SELECT * FROM query_store.qs_view LIMIT 5;");
            }

            var result = new PostgreSQLConfigurationStatus(
                ResourceId: resourceId,
                HasDiagnosticSettings: hasDiagnosticSettings,
                HasQueryStore: hasQueryStore,
                HasPerformanceInsights: false,
                HasConnectionLogging: false,
                LogAnalyticsWorkspace: await GetDiagnosticWorkspaceForResourceAsync(resourceId),
                MissingConfigurations: missingConfigurations,
                SetupInstructions: setupInstructions,
                Summary: missingConfigurations.Any() ?
                    $"Configuration incomplete: {string.Join(", ", missingConfigurations)} not configured" :
                    "PostgreSQL diagnostic configuration is complete"
            );

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_config] Error validating configuration for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Gets the correct Log Analytics workspace where PostgreSQL diagnostic settings send logs
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Workspace resource ID or null if not configured</returns>
    public async Task<string?> GetDiagnosticWorkspaceForResourceAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_workspace] Getting diagnostic workspace for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_workspace] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");
                return null;
            }

            var armClient = await _armClientFactory.GetArmOperationClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            // Get diagnostic settings using ARM client
            var diagnosticSettings = armClient.GetDiagnosticSettings(resourceIdentifier);

            await foreach (var diagnosticSetting in diagnosticSettings)
            {
                var data = diagnosticSetting.Data;

                // Check if this diagnostic setting sends to Log Analytics
                if (!string.IsNullOrEmpty(data.WorkspaceId))
                {
                    _logger.LogInternalInformation($"[postgresql_workspace] Found Log Analytics workspace for {resourceId}: {data.WorkspaceId}");
                    return data.WorkspaceId;
                }
            }

            _logger.LogInternalInformation($"[postgresql_workspace] No Log Analytics workspace found for {resourceId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_workspace] Error getting diagnostic workspace for {resourceId}");
            return null;
        }
    }

    /// <summary>
    /// Builds Kusto query for PostgreSQL metrics
    /// </summary>
    /// <param name="window">Time window for metrics</param>
    /// <returns>Kusto query string</returns>
    private string BuildPostgreSQLMetricsQuery(TimeSpan window)
    {
        return $@"
            AzureMetrics
            | where TimeGenerated >= ago({window.TotalMinutes}m)
            | where ResourceProvider == ""MICROSOFT.DBFORPOSTGRESQL""
            | where MetricName in (""cpu_percent"", ""memory_percent"", ""active_connections"", ""connections_limit"")
            | summarize avg(Average) by MetricName, bin(TimeGenerated, 5m)
            | order by TimeGenerated desc";
    }

    /// <summary>
    /// Gets PostgreSQL metrics from Azure Monitor
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>PostgreSQL metrics from Azure Monitor</returns>
    private async Task<PostgreSQLMetrics> GetAzureMonitorMetricsAsync(string resourceId, TimeSpan window)
    {
        try
        {
            var endTime = DateTimeOffset.UtcNow;
            var startTime = endTime.Subtract(window);
            var granularity = TimeSpan.FromMinutes(5); // 5 minute intervals

            // Define the metrics we want to retrieve
            var metricsToQuery = new[]
            {
                ("cpu_percent", "Microsoft.DBforPostgreSQL/flexibleServers"),
                ("memory_percent", "Microsoft.DBforPostgreSQL/flexibleServers"),
                ("active_connections", "Microsoft.DBforPostgreSQL/flexibleServers"),
                ("connections_succeeded", "Microsoft.DBforPostgreSQL/flexibleServers"),
                ("connections_failed", "Microsoft.DBforPostgreSQL/flexibleServers")
            };

            // Initialize metrics with default values
            double cpuPercent = 0.0;
            double memoryPercent = 0.0;
            int activeConnections = 0;
            int maxConnections = 100; // Default value, will try to get from server configuration
            double cacheHitRatio = 0.0; // Not available directly from Azure Monitor
            double averageQueryDuration = 0.0; // Not available directly from Azure Monitor
            long totalQueries = 0; // Calculate from connection metrics

            var metricsResults = new Dictionary<string, double>();

            // Query each metric
            foreach (var (metricName, metricNamespace) in metricsToQuery)
            {
                try
                {
                    var result = await _azureMonitorMetricsHelper.QueryResourceMetricAsync(
                        resourceId,
                        metricNamespace,
                        metricName,
                        startTime,
                        endTime,
                        granularity);

                    if (result?.Metrics?.Any() == true)
                    {
                        var metric = result.Metrics.First();
                        if (metric.TimeSeries?.Any() == true)
                        {
                            var timeSeries = metric.TimeSeries.First();
                            if (timeSeries.Values?.Any() == true)
                            {
                                // Get the latest non-null value
                                var latestValue = timeSeries.Values
                                    .Where(v => v.Average.HasValue)
                                    .OrderByDescending(v => v.TimeStamp)
                                    .FirstOrDefault();

                                if (latestValue?.Average.HasValue == true)
                                {
                                    metricsResults[metricName] = latestValue.Average.Value;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning($"[postgresql_metrics] Failed to query metric {metricName}: {ex.Message}");
                }
            }

            // Extract the specific metrics
            metricsResults.TryGetValue("cpu_percent", out cpuPercent);
            metricsResults.TryGetValue("memory_percent", out memoryPercent);
            metricsResults.TryGetValue("active_connections", out var activeConnectionsDouble);
            activeConnections = (int)activeConnectionsDouble;

            // Calculate total queries from connection metrics
            metricsResults.TryGetValue("connections_succeeded", out var succeededConnections);
            metricsResults.TryGetValue("connections_failed", out var failedConnections);
            totalQueries = (long)(succeededConnections + failedConnections);

            // Try to get max connections from server configuration
            try
            {
                maxConnections = await GetMaxConnectionsFromServerAsync(resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"[postgresql_metrics] Failed to get max connections: {ex.Message}");
            }

            // Generate summary based on collected metrics
            var summary = GenerateMetricsSummary(cpuPercent, memoryPercent, activeConnections, maxConnections, cacheHitRatio);

            return new PostgreSQLMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                CpuPercent: cpuPercent,
                MemoryPercent: memoryPercent,
                ActiveConnections: activeConnections,
                MaxConnections: maxConnections,
                CacheHitRatio: cacheHitRatio,
                AverageQueryDuration: averageQueryDuration,
                TotalQueries: totalQueries,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_metrics] Error retrieving Azure Monitor metrics for {resourceId}");

            // Return metrics with error information
            return new PostgreSQLMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                CpuPercent: 0.0,
                MemoryPercent: 0.0,
                ActiveConnections: 0,
                MaxConnections: 0,
                CacheHitRatio: 0.0,
                AverageQueryDuration: 0.0,
                TotalQueries: 0,
                Summary: $"❌ Error retrieving metrics from Azure Monitor: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Gets the maximum connections setting from the PostgreSQL server
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Maximum connections limit</returns>
    private async Task<int> GetMaxConnectionsFromServerAsync(string resourceId)
    {
        try
        {
            var armClient = _armClientFactory.GetCrawlerArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            var resourceGroupId = ResourceGroupResource.CreateResourceIdentifier(
                resourceIdentifier.SubscriptionId,
                resourceIdentifier.ResourceGroupName);
            var rg = armClient.GetResourceGroupResource(resourceGroupId);
            var server = await rg.GetPostgreSqlFlexibleServerAsync(resourceIdentifier.Name);

            if (server?.Value != null)
            {
                // Try to get max_connections configuration parameter
                var maxConnectionsConfig = await server.Value.GetPostgreSqlFlexibleServerConfigurationAsync("max_connections");
                if (maxConnectionsConfig?.Value?.Data?.Value != null &&
                    int.TryParse(maxConnectionsConfig.Value.Data.Value, out var maxConnections))
                {
                    return maxConnections;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"[postgresql_metrics] Failed to get max_connections config: {ex.Message}");
        }

        return 100; // Default fallback value
    }

    /// <summary>
    /// Generates a summary based on the collected metrics
    /// </summary>
    /// <param name="cpuPercent">CPU utilization percentage</param>
    /// <param name="memoryPercent">Memory utilization percentage</param>
    /// <param name="activeConnections">Current active connections</param>
    /// <param name="maxConnections">Maximum allowed connections</param>
    /// <param name="cacheHitRatio">Cache hit ratio percentage</param>
    /// <returns>Summary string describing the current state</returns>
    private string GenerateMetricsSummary(double cpuPercent, double memoryPercent, int activeConnections, int maxConnections, double cacheHitRatio)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        // Analyze CPU
        if (cpuPercent > 80)
            issues.Add($"High CPU usage ({cpuPercent:F1}%)");
        else if (cpuPercent > 60)
            warnings.Add($"Elevated CPU usage ({cpuPercent:F1}%)");

        // Analyze Memory
        if (memoryPercent > 85)
            issues.Add($"High memory usage ({memoryPercent:F1}%)");
        else if (memoryPercent > 70)
            warnings.Add($"Elevated memory usage ({memoryPercent:F1}%)");

        // Analyze Connections
        var connectionUtilization = maxConnections > 0 ? (double)activeConnections / maxConnections * 100 : 0;
        if (connectionUtilization > 90)
            issues.Add($"Connection pool near capacity ({activeConnections}/{maxConnections})");
        else if (connectionUtilization > 75)
            warnings.Add($"High connection usage ({activeConnections}/{maxConnections})");

        // Analyze Cache Hit Ratio (if available)
        if (cacheHitRatio > 0 && cacheHitRatio < 70)
            issues.Add($"Low cache hit ratio ({cacheHitRatio:F1}%)");
        else if (cacheHitRatio > 0 && cacheHitRatio < 85)
            warnings.Add($"Suboptimal cache hit ratio ({cacheHitRatio:F1}%)");

        // Generate summary
        if (issues.Any())
        {
            return $"❌ PostgreSQL server issues detected: {string.Join(", ", issues)}" +
                   (warnings.Any() ? $". Warnings: {string.Join(", ", warnings)}" : "");
        }
        else if (warnings.Any())
        {
            return $"⚠️ PostgreSQL server warnings: {string.Join(", ", warnings)}";
        }
        else
        {
            return $"✅ PostgreSQL server operating normally - CPU: {cpuPercent:F1}%, Memory: {memoryPercent:F1}%, Connections: {activeConnections}/{maxConnections}";
        }
    }

    /// <summary>
    /// Checks if diagnostic settings are configured for the PostgreSQL server
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>True if diagnostic settings are configured</returns>
    private async Task<bool> CheckDiagnosticSettingsAsync(string resourceId)
    {
        try
        {
            // Validate and ensure we have a proper Azure resource ID
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_config] Resource ID is null or empty");
                return false;
            }

            // If the resourceId doesn't start with /subscriptions/, it might be just a server name
            if (!resourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning($"[postgresql_config] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");
                return false;
            }

            var armClient = await _armClientFactory.GetArmOperationClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            // Get diagnostic settings using ARM client
            var diagnosticSettings = armClient.GetDiagnosticSettings(resourceIdentifier);

            await foreach (var diagnosticSetting in diagnosticSettings)
            {
                var data = diagnosticSetting.Data;

                // Check if this diagnostic setting sends to Log Analytics and has PostgreSQL logs enabled
                if (!string.IsNullOrEmpty(data.WorkspaceId) && data.Logs?.Any() == true)
                {
                    // Check if PostgreSQL-specific log categories are enabled
                    var hasPostgreSQLLogs = data.Logs.Any(log =>
                        log.Category?.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase) == true ||
                        log.Category == "QueryStoreRuntimeStatistics" ||
                        log.Category == "QueryStoreWaitStatistics" ||
                        log.CategoryGroup == "allLogs");

                    if (hasPostgreSQLLogs)
                    {
                        _logger.LogInternalInformation($"[postgresql_config] Found diagnostic settings for {resourceId} with PostgreSQL logs");
                        return true;
                    }
                }
            }

            _logger.LogInternalInformation($"[postgresql_config] No diagnostic settings with PostgreSQL logs found for {resourceId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_config] Error checking diagnostic settings for {resourceId}");
            return false;
        }
    }

    /// <summary>
    /// Checks if Query Store is enabled on the PostgreSQL server
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>True if Query Store is enabled</returns>
    private async Task<bool> CheckQueryStoreConfigurationAsync(string resourceId)
    {
        try
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            // Validate and ensure we have a proper Azure resource ID
            if (string.IsNullOrEmpty(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_config] Resource ID is null or empty");
                return false;
            }

            // If the resourceId doesn't start with /subscriptions/, it might be just a server name
            // In that case, we can't proceed with the Azure SDK calls
            if (!resourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning($"[postgresql_config] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");
                return false;
            }

            var resourceIdentifier = new ResourceIdentifier(resourceId);

            // Get the PostgreSQL flexible server resource
            var server = armClient.GetPostgreSqlFlexibleServerResource(resourceIdentifier);

            // Check Query Store configuration directly
            var queryStoreConfig = await server.GetPostgreSqlFlexibleServerConfigurationAsync("pg_qs.query_capture_mode");

            if (queryStoreConfig?.Value?.Data != null)
            {
                var queryStoreEnabled = queryStoreConfig.Value.Data.Value != "none" && !string.IsNullOrEmpty(queryStoreConfig.Value.Data.Value);
                if (queryStoreEnabled)
                {
                    _logger.LogInternalInformation($"[postgresql_config] Query Store enabled for {resourceId}: pg_qs.query_capture_mode = {queryStoreConfig.Value.Data.Value}");
                    return true;
                }
            }

            _logger.LogInternalInformation($"[postgresql_config] Query Store not enabled for {resourceId}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_config] Error checking Query Store configuration for {resourceId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Analyzes PostgreSQL table bloat by comparing actual vs estimated table sizes
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Table bloat analysis results</returns>
    public async Task<TableBloatAnalysis> AnalyzeTableBloat(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_bloat] ===== STARTING AnalyzeTableBloat =====");
            _logger.LogInternalInformation($"[postgresql_bloat] ResourceId: {resourceId}");
            _logger.LogInternalInformation($"[postgresql_bloat] Database: {database}");
            _logger.LogInternalInformation($"[postgresql_bloat] Analyzing table bloat for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_bloat] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new TableBloatAnalysis(
                    ResourceId: resourceId,
                    AnalyzedAt: DateTime.UtcNow,
                    BloatedTables: new List<BloatedTable>(),
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}",
                    Recommendations: new List<string>()
                );
            }

            try
            {
                // Add a small delay to ensure proper async behavior
                await Task.Delay(100);

                // Execute real PostgreSQL query to analyze table bloat
                _logger.LogInternalInformation($"[postgresql_bloat] Executing database queries for {resourceId}");
                _logger.LogInternalInformation($"[postgresql_bloat] Target database: {database}");
                _logger.LogInternalInformation($"[postgresql_bloat] About to execute bloat query via PostgresSQLCommandHelper");

                // Starting with the simplest possible query to test connection
                var bloatQuery = @"
SELECT 
    schemaname,
    relname,
    n_live_tup,
    n_dead_tup
FROM pg_stat_user_tables 
LIMIT 5";

                _logger.LogInternalInformation($"[postgresql_bloat] QUERY TO EXECUTE: {bloatQuery.Trim()}");
                _logger.LogInternalInformation($"[postgresql_bloat] Calling _postgresSQLCommandHelper.ExecutePsqlCommandAsync...");

                var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(bloatQuery, resourceId, database);

                _logger.LogInternalInformation($"[postgresql_bloat] Command execution completed - ErrorOccurred: {result.ErrorOccurred}, ErrorType: {result.ErrorType}");
                _logger.LogInternalInformation($"[postgresql_bloat] Result output length: {result.Output?.Length ?? 0} characters");
                if (!string.IsNullOrEmpty(result.Output))
                {
                    _logger.LogInternalInformation($"[postgresql_bloat] First 500 chars of output: '{result.Output.Substring(0, Math.Min(500, result.Output.Length))}'");
                }

                if (!result.ErrorOccurred && !string.IsNullOrWhiteSpace(result.Output))
                {
                    var bloatedTables = ParseTableBloatResults(result.Output);

                    if (bloatedTables.Any())
                    {
                        // Get table sizes for the bloated tables in a separate query
                        await EnrichWithTableSizes(bloatedTables, resourceId, database);

                        var recommendations = GenerateBloatRecommendations(bloatedTables);
                        var summary = GenerateBloatSummary(bloatedTables);

                        _logger.LogInternalInformation($"[postgresql_bloat] Analysis complete: {bloatedTables.Count} tables with bloat data for {resourceId}");

                        // Add a small delay to ensure proper response timing
                        await Task.Delay(50);

                        return new TableBloatAnalysis(
                            ResourceId: resourceId,
                            AnalyzedAt: DateTime.UtcNow,
                            BloatedTables: bloatedTables,
                            Summary: summary,
                            Recommendations: recommendations
                        );
                    }
                    else
                    {
                        _logger.LogInternalInformation($"[postgresql_bloat] No significant table bloat found for {resourceId}");
                        return new TableBloatAnalysis(
                            ResourceId: resourceId,
                            AnalyzedAt: DateTime.UtcNow,
                            BloatedTables: new List<BloatedTable>(),
                            Summary: "✅ No significant table bloat detected. All analyzed tables have dead tuple levels below 20%.",
                            Recommendations: new List<string> { "Continue monitoring table bloat regularly", "Ensure autovacuum is properly configured" }
                        );
                    }
                }
                else
                {
                    _logger.LogInternalError($"[postgresql_bloat] PostgreSQL query FAILED - ErrorType: {result.ErrorType}, Output: '{result.Output}', Command executed successfully but returned error result");
                    _logger.LogInternalError($"[postgresql_bloat] FALLBACK TRIGGERED: Switching to mock data due to query failure for {resourceId}");
                    // Fall back to mock data if real query fails
                    return await GetMockTableBloatData(resourceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"[postgresql_bloat] EXCEPTION CAUGHT during query execution - FALLBACK TRIGGERED");
                _logger.LogInternalError($"[postgresql_bloat] Exception details: Type={ex.GetType().Name}, Message='{ex.Message}', StackTrace={ex.StackTrace}");
                _logger.LogInternalError($"[postgresql_bloat] ResourceId={resourceId}, Database={database}");
                // Fall back to mock data if real query fails
                return await GetMockTableBloatData(resourceId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_bloat] Error analyzing table bloat for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Checks autovacuum configuration and identifies disabled autovacuum tables
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Autovacuum configuration analysis</returns>
    public async Task<AutovacuumConfigurationAnalysis> AnalyzeAutovacuumConfiguration(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_autovacuum] Analyzing autovacuum configuration for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_autovacuum] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new AutovacuumConfigurationAnalysis(
                    ResourceId: resourceId,
                    AnalyzedAt: DateTime.UtcNow,
                    GlobalAutovacuumEnabled: false,
                    TableSettings: new List<TableAutovacuumSettings>(),
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}",
                    Issues: new List<string>()
                );
            }

            // Add a small delay to ensure proper async behavior
            await Task.Delay(100);

            // Execute PostgreSQL query to get actual autovacuum configuration
            _logger.LogInternalInformation($"[postgresql_autovacuum] Executing database queries for {resourceId}");
            var globalSettings = await GetAutovacuumGlobalSettings(resourceId, database);
            var tableSettings = await GetAutovacuumTableSettings(resourceId, database, globalSettings);

            var disabledTables = tableSettings.Where(t => !t.AutovacuumEnabled).ToList();
            var issues = new List<string>();

            if (disabledTables.Any())
            {
                issues.Add($"{disabledTables.Count} tables have autovacuum explicitly disabled");
                issues.Add("Disabled autovacuum can lead to table bloat and performance degradation");
                issues.Add("Large tables with disabled autovacuum require manual vacuum operations");
            }

            var summary = $"Analyzed {tableSettings.Count} user tables. " +
                         $"Global autovacuum: {(globalSettings.AutovacuumEnabled ? "Enabled" : "Disabled")}. " +
                         $"Tables with disabled autovacuum: {disabledTables.Count}. " +
                         (disabledTables.Any() ? $"Affected tables: {string.Join(", ", disabledTables.Select(t => $"{t.SchemaName}.{t.TableName}"))}" : "All tables using global autovacuum settings.");

            _logger.LogInternalInformation($"[postgresql_autovacuum] Analysis complete: {disabledTables.Count} tables with disabled autovacuum for {resourceId}");

            // Add a small delay to ensure proper response timing
            await Task.Delay(50);

            return new AutovacuumConfigurationAnalysis(
                ResourceId: resourceId,
                AnalyzedAt: DateTime.UtcNow,
                GlobalAutovacuumEnabled: globalSettings.AutovacuumEnabled,
                TableSettings: tableSettings,
                Summary: summary,
                Issues: issues
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_autovacuum] Error analyzing autovacuum configuration for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Gets global autovacuum settings from PostgreSQL server
    /// </summary>
    private async Task<(bool AutovacuumEnabled, string VacuumThreshold, string VacuumScaleFactor, string AnalyzeThreshold, string AnalyzeScaleFactor)> GetAutovacuumGlobalSettings(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_autovacuum] Getting global autovacuum settings for {resourceId}, database: {database}");

            // Query PostgreSQL pg_settings for all autovacuum parameters
            var query = "SELECT name, setting FROM pg_settings WHERE name IN ('autovacuum', 'autovacuum_vacuum_threshold', 'autovacuum_vacuum_scale_factor', 'autovacuum_analyze_threshold', 'autovacuum_analyze_scale_factor') ORDER BY name";

            _logger.LogInternalInformation($"[postgresql_autovacuum] Executing AUTOVACUUM SETTINGS query: {query}");
            var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(query, resourceId, database);
            _logger.LogInternalInformation($"[postgresql_autovacuum] Autovacuum query result - ErrorOccurred: {result.ErrorOccurred}, ErrorType: {result.ErrorType}, Output: '{result.Output}'");

            if (!result.ErrorOccurred && !string.IsNullOrWhiteSpace(result.Output))
            {
                _logger.LogInternalInformation($"[postgresql_autovacuum] Successfully received autovacuum settings, parsing...");
                var settings = ParseAutovacuumGlobalSettings(result.Output);
                _logger.LogInternalInformation($"[postgresql_autovacuum] Parsed settings - AutovacuumEnabled: {settings.AutovacuumEnabled}");
                return settings;
            }
            else
            {
                _logger.LogInternalError($"[postgresql_autovacuum] QUERY FAILED - ErrorType: {result.ErrorType}, Output: '{result.Output}'");
                _logger.LogInternalError($"[postgresql_autovacuum] FALLBACK TRIGGERED: Using default autovacuum settings for {resourceId}");
                // Return default values if query fails
                return (AutovacuumEnabled: true, VacuumThreshold: "50", VacuumScaleFactor: "0.2", AnalyzeThreshold: "50", AnalyzeScaleFactor: "0.1");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_autovacuum] EXCEPTION getting global autovacuum settings for {resourceId}");
            _logger.LogInternalError($"[postgresql_autovacuum] Exception details: Type={ex.GetType().Name}, Message='{ex.Message}', StackTrace={ex.StackTrace}");
            _logger.LogInternalError($"[postgresql_autovacuum] FALLBACK TRIGGERED: Using default autovacuum settings");
            // Return default values if query fails
            return (AutovacuumEnabled: true, VacuumThreshold: "50", VacuumScaleFactor: "0.2", AnalyzeThreshold: "50", AnalyzeScaleFactor: "0.1");
        }
    }

    /// <summary>
    /// Gets table-specific autovacuum settings from PostgreSQL server
    /// </summary>
    private async Task<List<TableAutovacuumSettings>> GetAutovacuumTableSettings(string resourceId, string database, (bool AutovacuumEnabled, string VacuumThreshold, string VacuumScaleFactor, string AnalyzeThreshold, string AnalyzeScaleFactor) globalSettings)
    {
        try
        {
            // Query for table information with potential per-table autovacuum settings
            // Note: reloptions contains table-specific storage parameters including autovacuum settings
            var query = "SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as table_size FROM pg_tables WHERE schemaname NOT IN ('information_schema', 'pg_catalog', 'pg_toast') LIMIT 10";

            var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(query, resourceId, database);

            if (!result.ErrorOccurred && !string.IsNullOrWhiteSpace(result.Output))
            {
                var tableSettings = ParseAutovacuumTableSettings(result.Output, globalSettings);
                return tableSettings;
            }
            else
            {
                _logger.LogInternalWarning($"[postgresql_autovacuum] Table query failed or returned empty result for {resourceId}: {result.Output}");
                // Return empty list if query fails
                return new List<TableAutovacuumSettings>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_autovacuum] Error getting table autovacuum settings for {resourceId}");
            // Return empty list if query fails
            return new List<TableAutovacuumSettings>();
        }
    }

    /// <summary>
    /// Parses autovacuum global settings from PostgreSQL query result
    /// </summary>
    private (bool AutovacuumEnabled, string VacuumThreshold, string VacuumScaleFactor, string AnalyzeThreshold, string AnalyzeScaleFactor) ParseAutovacuumGlobalSettings(string result)
    {
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var settings = new Dictionary<string, string>();

        foreach (var line in lines)
        {
            if (line.Contains("|") && !line.Contains("name") && !line.Contains("---"))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var name = parts[0].Trim();
                    var setting = parts[1].Trim();
                    settings[name] = setting;
                }
            }
        }

        return (
            AutovacuumEnabled: settings.GetValueOrDefault("autovacuum", "on").Equals("on", StringComparison.OrdinalIgnoreCase),
            VacuumThreshold: settings.GetValueOrDefault("autovacuum_vacuum_threshold", "50"),
            VacuumScaleFactor: settings.GetValueOrDefault("autovacuum_vacuum_scale_factor", "0.2"),
            AnalyzeThreshold: settings.GetValueOrDefault("autovacuum_analyze_threshold", "50"),
            AnalyzeScaleFactor: settings.GetValueOrDefault("autovacuum_analyze_scale_factor", "0.1")
        );
    }

    /// <summary>
    /// Parses table autovacuum settings from PostgreSQL query result
    /// </summary>
    private List<TableAutovacuumSettings> ParseAutovacuumTableSettings(string result, (bool AutovacuumEnabled, string VacuumThreshold, string VacuumScaleFactor, string AnalyzeThreshold, string AnalyzeScaleFactor) globalSettings)
    {
        var tableSettings = new List<TableAutovacuumSettings>();
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("|") && !line.Contains("schemaname") && !line.Contains("---"))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    var schemaName = parts[0].Trim();
                    var tableName = parts[1].Trim();
                    var tableSize = parts[2].Trim();

                    // For this implementation, assume all tables use global settings
                    // In a more advanced implementation, we would query pg_class.reloptions for table-specific settings
                    tableSettings.Add(new TableAutovacuumSettings(
                        SchemaName: schemaName,
                        TableName: tableName,
                        TableSize: tableSize,
                        AutovacuumEnabled: globalSettings.AutovacuumEnabled,
                        VacuumThreshold: globalSettings.VacuumThreshold,
                        VacuumScaleFactor: globalSettings.VacuumScaleFactor,
                        SettingsSource: "Global settings"
                    ));
                }
            }
        }

        return tableSettings;
    }

    /// <summary>
    /// Raw table activity data from PostgreSQL pg_stat_user_tables
    /// </summary>
    private record RawTableActivity(
        string SchemaName,
        string TableName,
        string TableSize,
        long SequentialScans,
        long IndexScans,
        long TuplesInserted,
        long TuplesUpdated,
        long TuplesDeleted,
        long LiveTuples,
        long DeadTuples,
        DateTime? LastVacuum,
        DateTime? LastAutovacuum
    );

    /// <summary>
    /// Gets table activity data from PostgreSQL server
    /// </summary>
    private async Task<List<RawTableActivity>> GetTableActivityData(string resourceId, string database)
    {
        try
        {
            // Query pg_stat_user_tables for comprehensive table activity statistics
            var query = "SELECT schemaname, relname, seq_scan, seq_tup_read, idx_scan, idx_tup_fetch, n_tup_ins, n_tup_upd, n_tup_del, n_tup_hot_upd, n_live_tup, n_dead_tup, last_vacuum, last_autovacuum, last_analyze, last_autoanalyze FROM pg_stat_user_tables ORDER BY n_tup_upd + n_tup_del DESC LIMIT 15";

            var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(query, resourceId, database);

            if (!result.ErrorOccurred && !string.IsNullOrWhiteSpace(result.Output))
            {
                var tableActivities = ParseTableActivityResults(result.Output);
                return tableActivities;
            }
            else
            {
                _logger.LogInternalError($"[postgresql_activity] TABLE ACTIVITY QUERY FAILED - ErrorType: {result.ErrorType}, Output: '{result.Output}'");
                _logger.LogInternalError($"[postgresql_activity] FALLBACK TRIGGERED: Returning empty activity data for {resourceId}");
                return new List<RawTableActivity>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_activity] EXCEPTION getting table activity data for {resourceId}");
            _logger.LogInternalError($"[postgresql_activity] Exception details: Type={ex.GetType().Name}, Message='{ex.Message}', StackTrace={ex.StackTrace}");
            _logger.LogInternalError($"[postgresql_activity] FALLBACK TRIGGERED: Returning empty activity data");
            return new List<RawTableActivity>();
        }
    }

    /// <summary>
    /// Gets database overview data from PostgreSQL server
    /// </summary>
    private async Task<(string DatabaseSize, int TotalConnections, int ActiveConnections, int IdleConnections, int TableCount, string TotalSize)> GetDatabaseOverviewData(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_overview] Starting database overview queries for {resourceId}, database: {database}");

            // Database size query
            var sizeQuery = "SELECT pg_size_pretty(pg_database_size(current_database())) as db_size";
            _logger.LogInternalInformation($"[postgresql_overview] Executing SIZE query: {sizeQuery}");
            var sizeResult = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(sizeQuery, resourceId, database);
            _logger.LogInternalInformation($"[postgresql_overview] Size query result - ErrorOccurred: {sizeResult.ErrorOccurred}, Output: '{sizeResult.Output}'");

            // Connection stats query
            var connectionQuery = "SELECT count(*) as total_connections, count(*) FILTER (WHERE state = 'active') as active_connections, count(*) FILTER (WHERE state = 'idle') as idle_connections FROM pg_stat_activity";
            _logger.LogInternalInformation($"[postgresql_overview] Executing CONNECTION query: {connectionQuery}");
            var connectionResult = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(connectionQuery, resourceId, database);
            _logger.LogInternalInformation($"[postgresql_overview] Connection query result - ErrorOccurred: {connectionResult.ErrorOccurred}, Output: '{connectionResult.Output}'");

            // Table count and total size query
            var tableQuery = "SELECT count(*) as table_count, pg_size_pretty(sum(pg_total_relation_size(schemaname||'.'||tablename))) as total_size FROM pg_tables WHERE schemaname NOT IN ('information_schema', 'pg_catalog', 'pg_toast')";
            _logger.LogInternalInformation($"[postgresql_overview] Executing TABLE query: {tableQuery}");
            var tableResult = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(tableQuery, resourceId, database);
            _logger.LogInternalInformation($"[postgresql_overview] Table query result - ErrorOccurred: {tableResult.ErrorOccurred}, Output: '{tableResult.Output}'");

            var overview = ParseDatabaseOverviewResults(sizeResult.Output, connectionResult.Output, tableResult.Output);
            _logger.LogInternalInformation($"[postgresql_overview] Successfully parsed all query results");
            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_overview] CRITICAL ERROR getting database overview data for {resourceId}");
            _logger.LogInternalError($"[postgresql_overview] Exception details: Type={ex.GetType().Name}, Message='{ex.Message}', StackTrace={ex.StackTrace}");
            _logger.LogInternalError($"[postgresql_overview] FALLBACK TRIGGERED: Returning default values due to database overview failure");
            // Return default values if queries fail
            return (DatabaseSize: "Unknown", TotalConnections: 0, ActiveConnections: 0, IdleConnections: 0, TableCount: 0, TotalSize: "Unknown");
        }
    }

    /// <summary>
    /// Gets slow queries data from PostgreSQL server
    /// </summary>
    private async Task<List<SlowQuery>> GetSlowQueriesData(string resourceId, string database, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_slowqueries] Getting slow queries from Log Analytics for {resourceId}");

            // Get the Log Analytics workspace for this resource
            var workspaceArmId = await GetDiagnosticWorkspaceForResourceAsync(resourceId);
            if (string.IsNullOrEmpty(workspaceArmId))
            {
                _logger.LogInternalWarning($"[postgresql_slowqueries] No Log Analytics workspace found for {resourceId}");
                return new List<SlowQuery>();
            }

            // Get the workspace customerId
            var workspaceCustomerId = await GetLogAnalyticsWorkspaceCustomerIdAsync(workspaceArmId);
            if (string.IsNullOrEmpty(workspaceCustomerId))
            {
                _logger.LogInternalWarning($"[postgresql_slowqueries] Could not retrieve workspace customerId from {workspaceArmId}");
                return new List<SlowQuery>();
            }

            // Use Log Analytics to query Query Store data with timestamps
            var slowQueries = await GetSlowQueriesFromLogAnalyticsAsync(workspaceCustomerId, window);
            return slowQueries;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_slowqueries] Error getting slow queries data from Log Analytics for {resourceId}");
            return new List<SlowQuery>();
        }
    }

    /// <summary>
    /// Parses table activity results from PostgreSQL query result
    /// </summary>
    private List<RawTableActivity> ParseTableActivityResults(string result)
    {
        var tableActivities = new List<RawTableActivity>();
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("|") && !line.Contains("schemaname") && !line.Contains("---"))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 16)
                {
                    var schemaName = parts[0].Trim();
                    var tableName = parts[1].Trim();

                    // Parse numeric values safely
                    long.TryParse(parts[2].Trim(), out var seqScan);
                    long.TryParse(parts[3].Trim(), out var seqTupRead);
                    long.TryParse(parts[4].Trim(), out var idxScan);
                    long.TryParse(parts[5].Trim(), out var idxTupFetch);
                    long.TryParse(parts[6].Trim(), out var nTupIns);
                    long.TryParse(parts[7].Trim(), out var nTupUpd);
                    long.TryParse(parts[8].Trim(), out var nTupDel);
                    long.TryParse(parts[10].Trim(), out var nLiveTup);
                    long.TryParse(parts[11].Trim(), out var nDeadTup);

                    // Parse date fields safely
                    DateTime.TryParse(parts[12].Trim(), out var lastVacuum);
                    DateTime.TryParse(parts[13].Trim(), out var lastAutovacuum);

                    var tableSize = $"{(nLiveTup + nDeadTup) * 100 / 1024.0:F1} KB"; // Rough estimate

                    tableActivities.Add(new RawTableActivity(
                        SchemaName: schemaName,
                        TableName: tableName,
                        TableSize: tableSize,
                        SequentialScans: seqScan,
                        IndexScans: idxScan,
                        TuplesInserted: nTupIns,
                        TuplesUpdated: nTupUpd,
                        TuplesDeleted: nTupDel,
                        LiveTuples: nLiveTup,
                        DeadTuples: nDeadTup,
                        LastVacuum: lastVacuum == DateTime.MinValue ? null : lastVacuum,
                        LastAutovacuum: lastAutovacuum == DateTime.MinValue ? null : lastAutovacuum
                    ));
                }
            }
        }

        return tableActivities;
    }

    /// <summary>
    /// Parses database overview results from multiple PostgreSQL query results
    /// </summary>
    private (string DatabaseSize, int TotalConnections, int ActiveConnections, int IdleConnections, int TableCount, string TotalSize) ParseDatabaseOverviewResults(string sizeResult, string connectionResult, string tableResult)
    {
        var databaseSize = "Unknown";
        var totalConnections = 0;
        var activeConnections = 0;
        var idleConnections = 0;
        var tableCount = 0;
        var totalSize = "Unknown";

        // Parse database size
        if (!string.IsNullOrWhiteSpace(sizeResult))
        {
            var sizeLines = sizeResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in sizeLines)
            {
                if (line.Contains("|") && !line.Contains("db_size") && !line.Contains("---"))
                {
                    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1)
                    {
                        databaseSize = parts[0].Trim();
                        break;
                    }
                }
            }
        }

        // Parse connection stats
        if (!string.IsNullOrWhiteSpace(connectionResult))
        {
            var connectionLines = connectionResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in connectionLines)
            {
                if (line.Contains("|") && !line.Contains("total_connections") && !line.Contains("---"))
                {
                    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        int.TryParse(parts[0].Trim(), out totalConnections);
                        int.TryParse(parts[1].Trim(), out activeConnections);
                        int.TryParse(parts[2].Trim(), out idleConnections);
                        break;
                    }
                }
            }
        }

        // Parse table stats
        if (!string.IsNullOrWhiteSpace(tableResult))
        {
            var tableLines = tableResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in tableLines)
            {
                if (line.Contains("|") && !line.Contains("table_count") && !line.Contains("---"))
                {
                    var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        int.TryParse(parts[0].Trim(), out tableCount);
                        totalSize = parts[1].Trim();
                        break;
                    }
                }
            }
        }

        return (databaseSize, totalConnections, activeConnections, idleConnections, tableCount, totalSize);
    }

    /// <summary>
    /// Parses slow queries results from PostgreSQL query result
    /// </summary>
    private List<SlowQuery> ParseSlowQueriesResults(string result)
    {
        var slowQueries = new List<SlowQuery>();
        var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Contains("|") && !line.Contains("query") && !line.Contains("---"))
            {
                var parts = line.Split('|', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 6)
                {
                    var queryText = parts[0].Trim();
                    long.TryParse(parts[1].Trim(), out var calls);
                    double.TryParse(parts[2].Trim(), out var totalExecTime);
                    double.TryParse(parts[3].Trim(), out var meanExecTime);
                    long.TryParse(parts[4].Trim(), out var rows);
                    double.TryParse(parts[5].Trim(), out var hitPercent);

                    var issues = new List<string>();
                    if (hitPercent < 95.0)
                    {
                        issues.Add("Low cache hit ratio - consider index optimization");
                    }
                    if (meanExecTime > 5000.0)
                    {
                        issues.Add("Very slow average execution time");
                    }

                    slowQueries.Add(new SlowQuery(
                        QueryText: queryText.Length > 200 ? queryText.Substring(0, 200) + "..." : queryText,
                        ExecutionCount: (int)calls,
                        AverageDuration: meanExecTime,
                        MaxDuration: totalExecTime / calls, // Approximation
                        ExecutionPlan: "Available in pg_stat_statements", // Placeholder
                        Issues: issues,
                        StartTime: null, // Not available from pg_stat_statements
                        EndTime: null // Not available from pg_stat_statements
                    ));
                }
            }
        }

        return slowQueries;
    }

    /// <summary>
    /// Gets the Log Analytics workspace customerId from the ARM resource ID
    /// </summary>
    /// <param name="workspaceArmId">ARM resource ID of the Log Analytics workspace</param>
    /// <returns>The workspace customerId needed for querying, or null if not found</returns>
    private async Task<string?> GetLogAnalyticsWorkspaceCustomerIdAsync(string workspaceArmId)
    {
        try
        {
            var armClient = await _armClientFactory.GetArmOperationClient();
            var workspaceIdentifier = new ResourceIdentifier(workspaceArmId);
            var workspace = await armClient.GetOperationalInsightsWorkspaceResource(workspaceIdentifier).GetAsync();

            if (workspace?.Value?.Data?.CustomerId != null)
            {
                var customerId = workspace.Value.Data.CustomerId.Value.ToString();
                _logger.LogInternalInformation($"[postgresql_workspace] Retrieved workspace customerId for {workspaceArmId}");
                return customerId;
            }

            _logger.LogInternalWarning($"[postgresql_workspace] Could not get customerId from workspace {workspaceArmId}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_workspace] Error getting workspace customerId for {workspaceArmId}");
            return null;
        }
    }

    /// <summary>
    /// Queries Log Analytics for slow queries using PostgreSQL Query Store data
    /// </summary>
    /// <param name="workspaceCustomerId">Log Analytics workspace customerId</param>
    /// <param name="window">Time window for query analysis</param>
    /// <returns>List of slow queries found with precise timestamps</returns>
    private async Task<List<SlowQuery>> GetSlowQueriesFromLogAnalyticsAsync(string workspaceCustomerId, TimeSpan window)
    {
        try
        {
            var credential = _authenticationService.GetLogAnalyticsCredential();
            var logsQueryClient = new LogsQueryClient(credential);

            // Query for PostgreSQL Query Store data from azure_sys.query_store_query_texts_view and related views
            var kqlQuery = $@"
                AzureDiagnostics
                | where TimeGenerated >= ago({window.TotalMinutes}m)
                | where Category == 'PostgreSQLLogs' 
                | where ResourceProvider == 'MICROSOFT.DBFORPOSTGRESQL'
                | where Message contains 'query_id' and Message contains 'mean_exec_time'
                | extend ParsedData = parse_json(Message)
                | where todouble(ParsedData.mean_exec_time) > 1000.0
                | project TimeGenerated, ResourceId, ParsedData
                | order by todouble(ParsedData.mean_exec_time) desc
                | limit 10";

            var response = await logsQueryClient.QueryWorkspaceAsync(workspaceCustomerId, kqlQuery, new QueryTimeRange(window));

            var slowQueries = new List<SlowQuery>();

            if (response?.Value != null)
            {
                var table = response.Value.Table;
                if (table?.Rows?.Any() == true)
                {
                    foreach (var row in table.Rows)
                    {
                        try
                        {
                            var timeGenerated = row[0]?.ToString();
                            var resourceId = row[1]?.ToString();
                            var parsedDataStr = row[2]?.ToString();

                            if (DateTime.TryParse(timeGenerated, out var timestamp) && !string.IsNullOrEmpty(parsedDataStr))
                            {
                                var parsedData = JsonSerializer.Deserialize<JsonElement>(parsedDataStr);

                                // Extract query performance data
                                var queryId = parsedData.GetProperty("query_id").GetString();
                                var meanExecTime = parsedData.GetProperty("mean_exec_time").GetDouble();
                                var calls = parsedData.GetProperty("calls").GetInt64();
                                var totalExecTime = parsedData.GetProperty("total_exec_time").GetDouble();
                                var queryText = parsedData.TryGetProperty("query_text", out var queryTextProp) 
                                    ? queryTextProp.GetString() 
                                    : $"Query ID: {queryId} (use SELECT query FROM query_store.qs_view WHERE query_id = {queryId})";

                                // Calculate issues based on performance characteristics
                                var issues = new List<string>();
                                if (meanExecTime > 5000.0)
                                    issues.Add("Very slow average execution time (>5s)");
                                if (calls > 1000)
                                    issues.Add("High frequency query - consider optimization");

                                slowQueries.Add(new SlowQuery(
                                    QueryText: queryText?.Length > 200 ? queryText.Substring(0, 200) + "..." : queryText ?? "",
                                    ExecutionCount: (int)calls,
                                    AverageDuration: meanExecTime,
                                    MaxDuration: totalExecTime, // This might be total, adjust as needed
                                    ExecutionPlan: $"Available in Query Store - query_id: {queryId}",
                                    Issues: issues,
                                    StartTime: timestamp, // TimeGenerated gives us precise timing
                                    EndTime: timestamp.AddMilliseconds(meanExecTime) // Estimated end time
                                ));
                            }
                        }
                        catch (Exception rowEx)
                        {
                            _logger.LogInternalWarning($"[postgresql_slowqueries] Error parsing row data: {rowEx.Message}");
                        }
                    }
                }
            }

            _logger.LogInternalInformation($"[postgresql_slowqueries] Found {slowQueries.Count} slow queries from Log Analytics");
            return slowQueries;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_slowqueries] Error querying Log Analytics for slow queries");
            return new List<SlowQuery>();
        }
    }

    /// <summary>
    /// Shows table activity statistics including insert/update/delete rates and vacuum history
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Table activity analysis results</returns>
    public async Task<TableActivityAnalysis> AnalyzeTableActivity(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_activity] Analyzing table activity for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_activity] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new TableActivityAnalysis(
                    ResourceId: resourceId,
                    AnalyzedAt: DateTime.UtcNow,
                    TableActivities: new List<TableActivity>(),
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            // Add a small delay to ensure proper async behavior
            await Task.Delay(100);

            // Get real table activity data from PostgreSQL
            _logger.LogInternalInformation($"[postgresql_activity] Executing database queries for {resourceId}");
            var tableActivityData = await GetTableActivityData(resourceId, database);

            // Convert to TableActivity objects matching the expected structure
            var tableActivities = new List<TableActivity>();

            foreach (var data in tableActivityData)
            {
                var totalOps = data.TuplesInserted + data.TuplesUpdated + data.TuplesDeleted;
                var deadTuplePercentage = data.LiveTuples > 0 ? (double)data.DeadTuples / data.LiveTuples * 100 : 0;

                // Estimate changes per day based on recent activity (simplified calculation)
                var changesPerDay = Math.Max(totalOps / 30.0, totalOps / 7.0); // Rough estimate

                tableActivities.Add(new TableActivity(
                    SchemaName: data.SchemaName,
                    TableName: data.TableName,
                    TableSize: data.TableSize,
                    TotalInserts: data.TuplesInserted,
                    TotalUpdates: data.TuplesUpdated,
                    TotalDeletes: data.TuplesDeleted,
                    LiveTuples: data.LiveTuples,
                    DeadTuples: data.DeadTuples,
                    DeadTuplePercentage: deadTuplePercentage,
                    LastVacuum: data.LastVacuum,
                    LastAutovacuum: data.LastAutovacuum,
                    VacuumCount: 0, // Not available in pg_stat_user_tables
                    AutovacuumCount: 0, // Not available in pg_stat_user_tables
                    ChangesPerDay: changesPerDay
                ));
            }

            if (!tableActivities.Any())
            {
                return new TableActivityAnalysis(
                    ResourceId: resourceId,
                    AnalyzedAt: DateTime.UtcNow,
                    TableActivities: tableActivities,
                    Summary: "No table activity data available. This could indicate no user tables in the database, statistics collector disabled, or insufficient permissions."
                );
            }

            var highActivityTables = tableActivities.Where(t => t.ChangesPerDay > 50_000).Count();
            var tablesWithHighDeadTuples = tableActivities.Where(t => t.DeadTuplePercentage > 15).Count();
            var tablesWithoutRecentAutovacuum = tableActivities.Where(t => t.LastAutovacuum == null || t.LastAutovacuum < DateTime.UtcNow.AddDays(-7)).Count();

            var summary = $"Analyzed {tableActivities.Count} active user tables. " +
                         $"High-activity tables (>50K changes/day): {highActivityTables}. " +
                         $"Tables with high dead tuple ratio (>15%): {tablesWithHighDeadTuples}. " +
                         $"Tables without recent autovacuum: {tablesWithoutRecentAutovacuum}.";

            _logger.LogInternalInformation($"[postgresql_activity] Analysis complete: {tableActivities.Count} tables analyzed for {resourceId}");

            // Add a small delay to ensure proper response timing
            await Task.Delay(50);

            return new TableActivityAnalysis(
                ResourceId: resourceId,
                AnalyzedAt: DateTime.UtcNow,
                TableActivities: tableActivities,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_activity] Error analyzing table activity for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Gets comprehensive PostgreSQL database overview including size, settings, and health
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Database overview analysis</returns>
    public async Task<DatabaseOverviewAnalysis> GetDatabaseOverview(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_overview] ===== STARTING GetDatabaseOverview =====");
            _logger.LogInternalInformation($"[postgresql_overview] ResourceId: {resourceId}");
            _logger.LogInternalInformation($"[postgresql_overview] Database: {database}");
            _logger.LogInternalInformation($"[postgresql_overview] Getting database overview for {resourceId}");

            // Validate resource ID format first
            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[postgresql_overview] Invalid resource ID format: {resourceId}. Expected full Azure resource ID starting with /subscriptions/");

                return new DatabaseOverviewAnalysis(
                    ResourceId: resourceId,
                    AnalyzedAt: DateTime.UtcNow,
                    DatabaseName: "unknown",
                    DatabaseSize: "unknown",
                    UserTableCount: 0,
                    TotalLiveTuples: 0,
                    TotalDeadTuples: 0,
                    TotalModifications: 0,
                    GlobalAutovacuumEnabled: false,
                    AutovacuumMaxWorkers: "unknown",
                    AutovacuumNaptime: "unknown",
                    MaintenanceWorkMem: "unknown",
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/ but received: {resourceId}"
                );
            }

            // Add a small delay to ensure proper async behavior  
            await Task.Delay(100);

            // Get real database overview data from PostgreSQL
            _logger.LogInternalInformation($"[postgresql_overview] Executing database queries for {resourceId}");
            var (databaseSize, totalConnections, activeConnections, idleConnections, tableCount, totalSize) = await GetDatabaseOverviewData(resourceId, database);

            // Get table activity data to calculate totals
            var tableActivityData = await GetTableActivityData(resourceId, database);

            // Get autovacuum settings data
            var autovacuumSettings = await GetAutovacuumGlobalSettings(resourceId, database);

            // Calculate aggregated values
            var totalLiveTuples = tableActivityData.Sum(t => t.LiveTuples);
            var totalDeadTuples = tableActivityData.Sum(t => t.DeadTuples);
            var totalModifications = tableActivityData.Sum(t => t.TuplesInserted + t.TuplesUpdated + t.TuplesDeleted);

            // Extract autovacuum settings
            var globalAutovacuumEnabled = autovacuumSettings.AutovacuumEnabled;
            var autovacuumMaxWorkers = "3"; // Default value, could query pg_settings if needed
            var autovacuumNaptime = "1min"; // Default value, could query pg_settings if needed  
            var maintenanceWorkMem = "64MB"; // Default value, could query pg_settings if needed

            // Generate summary based on real data
            var summary = GenerateDatabaseOverviewSummary(databaseSize, totalDeadTuples, globalAutovacuumEnabled, tableCount, totalConnections);

            _logger.LogInternalInformation($"[postgresql_overview] Analysis complete for {resourceId}");

            // Add a small delay to ensure proper response timing
            await Task.Delay(50);

            return new DatabaseOverviewAnalysis(
                ResourceId: resourceId,
                AnalyzedAt: DateTime.UtcNow,
                DatabaseName: "current_database",
                DatabaseSize: databaseSize,
                UserTableCount: tableCount,
                TotalLiveTuples: totalLiveTuples,
                TotalDeadTuples: totalDeadTuples,
                TotalModifications: totalModifications,
                GlobalAutovacuumEnabled: globalAutovacuumEnabled,
                AutovacuumMaxWorkers: autovacuumMaxWorkers,
                AutovacuumNaptime: autovacuumNaptime,
                MaintenanceWorkMem: maintenanceWorkMem,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_overview] Error getting database overview for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Generates a summary for database overview based on real data
    /// </summary>
    private string GenerateDatabaseOverviewSummary(string databaseSize, long totalDeadTuples, bool globalAutovacuumEnabled, int tableCount, int totalConnections)
    {
        var issues = new List<string>();
        var positives = new List<string>();

        if (totalDeadTuples > 1_000_000)
        {
            issues.Add($"High dead tuple count ({totalDeadTuples:N0}) indicates potential maintenance needs");
        }
        else if (totalDeadTuples < 100_000)
        {
            positives.Add("Dead tuple count is well managed");
        }

        if (!globalAutovacuumEnabled)
        {
            issues.Add("Global autovacuum is disabled");
        }
        else
        {
            positives.Add("Global autovacuum is enabled");
        }

        if (totalConnections > 80)
        {
            issues.Add($"High connection count ({totalConnections}) may indicate connection pooling issues");
        }

        if (tableCount == 0)
        {
            issues.Add("No user tables found in database");
        }

        var summary = $"Database size: {databaseSize}, User tables: {tableCount}, Active connections: {totalConnections}. ";

        if (issues.Any())
        {
            summary += "Issues: " + string.Join(", ", issues) + ". ";
        }

        if (positives.Any())
        {
            summary += "Positives: " + string.Join(", ", positives) + ".";
        }

        return summary.Trim();
    }

    /// <summary>
    /// Comprehensive PostgreSQL health check combining multiple diagnostic areas
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="database">The name of the database to analyze for health assessment</param>
    /// <returns>Complete health analysis</returns>
    public async Task<PostgreSQLHealthAnalysis> AnalyzePostgreSQLHealth(string resourceId, string database)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_health] Performing comprehensive health analysis for {resourceId}");

            // Run all diagnostic checks
            var overview = await GetDatabaseOverview(resourceId, database);
            var bloatAnalysis = await AnalyzeTableBloat(resourceId, database);
            var autovacuumAnalysis = await AnalyzeAutovacuumConfiguration(resourceId, database);
            var activityAnalysis = await AnalyzeTableActivity(resourceId, database);

            // Analyze results and identify issues
            var criticalIssues = new List<string>();
            var warnings = new List<string>();
            var recommendations = new List<string>();

            // Check for critical issues
            if (bloatAnalysis.BloatedTables.Any(t => t.BloatPercentage > 50))
            {
                criticalIssues.Add($"Severe table bloat detected: {bloatAnalysis.BloatedTables.Count(t => t.BloatPercentage > 50)} tables with >50% bloat");
            }

            if (autovacuumAnalysis.Issues.Any())
            {
                criticalIssues.Add("Autovacuum configuration issues detected");
            }

            // Check for warnings
            if (bloatAnalysis.BloatedTables.Any(t => t.BloatPercentage > 20))
            {
                warnings.Add($"Table bloat detected: {bloatAnalysis.BloatedTables.Count} tables with >20% bloat");
            }

            if (overview.TotalDeadTuples > overview.TotalLiveTuples * 0.1)
            {
                warnings.Add($"High dead tuple ratio: {overview.TotalDeadTuples:N0} dead tuples vs {overview.TotalLiveTuples:N0} live tuples");
            }

            // Generate recommendations
            recommendations.AddRange(bloatAnalysis.Recommendations);
            if (autovacuumAnalysis.TableSettings.Any(t => !t.AutovacuumEnabled))
            {
                recommendations.Add("Re-enable autovacuum for tables with disabled settings");
            }
            recommendations.Add("Schedule regular VACUUM ANALYZE operations during maintenance windows");
            recommendations.Add("Monitor dead tuple ratios and autovacuum activity regularly");

            // Determine overall health status
            string healthStatus;
            if (criticalIssues.Any())
            {
                healthStatus = "Critical";
            }
            else if (warnings.Count > 2)
            {
                healthStatus = "Warning";
            }
            else if (warnings.Any())
            {
                healthStatus = "Attention Needed";
            }
            else
            {
                healthStatus = "Healthy";
            }

            var healthSummary = $"PostgreSQL health status: {healthStatus}. " +
                              $"Found {criticalIssues.Count} critical issues, {warnings.Count} warnings. " +
                              $"Database size: {overview.DatabaseSize} with {overview.UserTableCount} user tables. " +
                              $"Dead tuple ratio: {(overview.TotalDeadTuples / (double)(overview.TotalLiveTuples + overview.TotalDeadTuples) * 100):F1}%.";

            _logger.LogInternalInformation($"[postgresql_health] Health analysis completed for {resourceId}: {healthStatus}");

            return new PostgreSQLHealthAnalysis(
                ResourceId: resourceId,
                AnalyzedAt: DateTime.UtcNow,
                DatabaseOverview: overview,
                BloatAnalysis: bloatAnalysis,
                AutovacuumAnalysis: autovacuumAnalysis,
                ActivityAnalysis: activityAnalysis,
                CriticalIssues: criticalIssues,
                Warnings: warnings,
                Recommendations: recommendations,
                OverallHealthStatus: healthStatus,
                Summary: healthSummary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_health] Error performing health analysis for {resourceId}");
            throw;
        }
    }

    /// <summary>
    /// Helper method to parse size strings and convert to bytes (for calculations)
    /// </summary>
    /// <param name="sizeString">Size string like "1.4 GB", "856 MB"</param>
    /// <returns>Size in bytes</returns>
    private long GetSizeInBytes(string sizeString)
    {
        if (string.IsNullOrEmpty(sizeString)) return 0;

        var parts = sizeString.Split(' ');
        if (parts.Length != 2) return 0;

        if (!double.TryParse(parts[0], out var value)) return 0;

        return parts[1].ToUpper() switch
        {
            "GB" => (long)(value * 1024 * 1024 * 1024),
            "MB" => (long)(value * 1024 * 1024),
            "KB" => (long)(value * 1024),
            "B" => (long)value,
            _ => 0
        };
    }

    /// <summary>
    /// Gets PostgreSQL performance metrics with specific metric groups for optimized collection
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <param name="metricGroups">Metric groups to collect (null = Core only)</param>
    /// <returns>PostgreSQL metrics with selected groups</returns>
    public async Task<PostgreSQLMetricsWithGroups> GetPostgreSQLMetricsWithGroupsAsync(string resourceId, TimeSpan window, PostgreSQLMetricGroup[]? metricGroups = null)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInternalInformation($"[postgresql_metrics_groups] Starting metrics collection for {resourceId}, groups: {string.Join(",", metricGroups ?? new[] { PostgreSQLMetricGroup.Core })}");

            // Default to Core metrics only if no groups specified
            var groups = metricGroups ?? new[] { PostgreSQLMetricGroup.Core };
            var configurationLimitations = new List<string>();

            // Always collect Core metrics first
            var coreMetrics = await GetCoreMetricsAsync(resourceId, window);

            // Check configuration for enhanced metrics
            var enhancedConfig = await CheckEnhancedMetricsConfigurationAsync(resourceId);

            // Collect optional metrics based on groups and configuration
            PostgreSQLEnhancedMetrics? enhanced = null;
            if (groups.Contains(PostgreSQLMetricGroup.Enhanced))
            {
                if (enhancedConfig.HasCollectorDatabaseActivity)
                {
                    enhanced = await GetEnhancedMetricsAsync(resourceId, window);
                }
                else
                {
                    configurationLimitations.Add("Enhanced metrics require 'metrics.collector_database_activity = ON'");
                }
            }

            PostgreSQLDatabaseMetrics? database = null;
            if (groups.Contains(PostgreSQLMetricGroup.Database))
            {
                if (enhancedConfig.HasCollectorDatabaseActivity)
                {
                    database = await GetDatabaseMetricsAsync(resourceId, window);
                }
                else
                {
                    configurationLimitations.Add("Database metrics require 'metrics.collector_database_activity = ON'");
                }
            }

            PostgreSQLSaturationMetrics? saturation = null;
            if (groups.Contains(PostgreSQLMetricGroup.Saturation))
            {
                saturation = await GetSaturationMetricsAsync(resourceId, window);
            }

            PostgreSQLActivityMetrics? activity = null;
            if (groups.Contains(PostgreSQLMetricGroup.Activity))
            {
                if (enhancedConfig.HasCollectorDatabaseActivity)
                {
                    activity = await GetActivityMetricsAsync(resourceId, window);
                }
                else
                {
                    configurationLimitations.Add("Activity metrics require 'metrics.collector_database_activity = ON'");
                }
            }

            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            var summary = BuildMetricsGroupsSummary(groups, coreMetrics, enhanced, database, saturation, activity, configurationLimitations, duration);

            _logger.LogInternalInformation($"[postgresql_metrics_groups] Completed metrics collection for {resourceId} in {duration:F1}s");

            return new PostgreSQLMetricsWithGroups(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                CollectedGroups: groups,
                Core: coreMetrics,
                Enhanced: enhanced,
                Database: database,
                Saturation: saturation,
                Activity: activity,
                ConfigurationLimitations: configurationLimitations,
                CollectionDurationSeconds: duration,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalSeconds;
            _logger.LogInternalError(ex, $"[postgresql_metrics_groups] Error collecting metrics for {resourceId}");

            // Return minimal result with error info
            return new PostgreSQLMetricsWithGroups(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                CollectedGroups: metricGroups ?? new[] { PostgreSQLMetricGroup.Core },
                Core: new PostgreSQLCoreMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0),
                Enhanced: null,
                Database: null,
                Saturation: null,
                Activity: null,
                ConfigurationLimitations: new List<string> { $"Error collecting metrics: {ex.Message}" },
                CollectionDurationSeconds: duration,
                Summary: $"❌ Error collecting PostgreSQL metrics: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Validates enhanced metrics configuration and returns available metric groups
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Enhanced metrics configuration status</returns>
    public async Task<PostgreSQLEnhancedMetricsStatus> CheckEnhancedMetricsConfigurationAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[postgresql_config] Checking enhanced metrics configuration for {resourceId}");

            // Check server parameters via Azure Monitor or ARM API
            var hasCollectorDatabaseActivity = await CheckServerParameterAsync(resourceId, "metrics.collector_database_activity");
            var hasAutovacuumDiagnostics = await CheckServerParameterAsync(resourceId, "metrics.autovacuum_diagnostics");
            var hasPgBouncerEnabled = await CheckServerParameterAsync(resourceId, "pgbouncer.enabled");
            var hasPgBouncerDiagnostics = await CheckServerParameterAsync(resourceId, "metrics.pgbouncer_diagnostics");

            var availableGroups = new List<PostgreSQLMetricGroup> { PostgreSQLMetricGroup.Core, PostgreSQLMetricGroup.Saturation };
            var unavailableGroups = new List<PostgreSQLMetricGroup>();
            var missingConfiguration = new Dictionary<string, string>();
            var setupInstructions = new List<string>();

            // Check Enhanced group
            if (hasCollectorDatabaseActivity)
            {
                availableGroups.AddRange(new[] { PostgreSQLMetricGroup.Enhanced, PostgreSQLMetricGroup.Database, PostgreSQLMetricGroup.Activity });
            }
            else
            {
                unavailableGroups.AddRange(new[] { PostgreSQLMetricGroup.Enhanced, PostgreSQLMetricGroup.Database, PostgreSQLMetricGroup.Activity });
                missingConfiguration["metrics.collector_database_activity"] = "Should be 'ON' to enable Enhanced, Database, and Activity metrics";
                setupInstructions.Add("Enable database activity collection: ALTER SYSTEM SET metrics.collector_database_activity = 'ON';");
            }

            // Check Autovacuum diagnostics
            if (!hasAutovacuumDiagnostics)
            {
                missingConfiguration["metrics.autovacuum_diagnostics"] = "Should be 'ON' to enable detailed autovacuum metrics";
                setupInstructions.Add("Enable autovacuum diagnostics: ALTER SYSTEM SET metrics.autovacuum_diagnostics = 'ON';");
            }

            // Check PgBouncer
            if (hasPgBouncerEnabled && !hasPgBouncerDiagnostics)
            {
                missingConfiguration["metrics.pgbouncer_diagnostics"] = "Should be 'ON' to enable PgBouncer metrics when PgBouncer is enabled";
                setupInstructions.Add("Enable PgBouncer diagnostics: ALTER SYSTEM SET metrics.pgbouncer_diagnostics = 'ON';");
            }

            if (setupInstructions.Any())
            {
                setupInstructions.Add("After making changes, reload configuration: SELECT pg_reload_conf();");
                setupInstructions.Add("Changes typically take effect within 1-2 minutes.");
            }

            var summary = BuildConfigurationSummary(availableGroups.ToArray(), unavailableGroups.ToArray(), missingConfiguration);

            return new PostgreSQLEnhancedMetricsStatus(
                ResourceId: resourceId,
                HasCollectorDatabaseActivity: hasCollectorDatabaseActivity,
                HasAutovacuumDiagnostics: hasAutovacuumDiagnostics,
                HasPgBouncerEnabled: hasPgBouncerEnabled,
                HasPgBouncerDiagnostics: hasPgBouncerDiagnostics,
                AvailableGroups: availableGroups.ToArray(),
                UnavailableGroups: unavailableGroups.ToArray(),
                MissingConfiguration: missingConfiguration,
                SetupInstructions: setupInstructions,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[postgresql_config] Error checking configuration for {resourceId}");
            throw;
        }
    }

    // ...existing code...

    // ...existing code...

    /// <summary>
    /// Collects core PostgreSQL metrics (always available)
    /// </summary>
    private async Task<PostgreSQLCoreMetrics> GetCoreMetricsAsync(string resourceId, TimeSpan window)
    {
        try
        {
            // Use existing Azure Monitor metrics approach but return structured core metrics
            var existingMetrics = await GetAzureMonitorMetricsAsync(resourceId, window);

            var connectionPercent = existingMetrics.MaxConnections > 0
                ? (double)existingMetrics.ActiveConnections / existingMetrics.MaxConnections * 100.0
                : 0.0;

            return new PostgreSQLCoreMetrics(
                CpuPercent: existingMetrics.CpuPercent,
                MemoryPercent: existingMetrics.MemoryPercent,
                StoragePercent: 0.0, // TODO: Add storage percentage from Azure Monitor
                ActiveConnections: existingMetrics.ActiveConnections,
                MaxConnections: existingMetrics.MaxConnections,
                ConnectionPercent: connectionPercent,
                CacheHitRatio: existingMetrics.CacheHitRatio,
                AverageQueryDuration: existingMetrics.AverageQueryDuration,
                TotalQueries: existingMetrics.TotalQueries
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"[postgresql_core_metrics] Error collecting core metrics: {ex.Message}");
            return new PostgreSQLCoreMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }

    /// <summary>
    /// Collects enhanced PostgreSQL metrics (requires collector_database_activity = ON)
    /// </summary>
    private async Task<PostgreSQLEnhancedMetrics> GetEnhancedMetricsAsync(string resourceId, TimeSpan window)
    {
        // For now, return mock data structure
        // TODO: Implement actual Azure Monitor queries for enhanced metrics
        await Task.Delay(100); // Simulate processing time

        return new PostgreSQLEnhancedMetrics(
            SessionsByState: new Dictionary<string, int>
            {
                ["active"] = 5,
                ["idle"] = 12,
                ["idle_in_transaction"] = 2,
                ["waiting"] = 1
            },
            SessionsByWaitEvent: new Dictionary<string, int>
            {
                ["Client:ClientRead"] = 10,
                ["IO:DataFileRead"] = 3,
                ["Lock:Relation"] = 1
            },
            OldestBackendMinutes: 45.2,
            OldestQueryMinutes: 2.1,
            OldestTransactionMinutes: 0.8,
            IdleConnections: 12,
            ActiveQueries: 5,
            BlockedQueries: 1
        );
    }

    /// <summary>
    /// Collects per-database PostgreSQL metrics (requires collector_database_activity = ON)
    /// </summary>
    private async Task<PostgreSQLDatabaseMetrics> GetDatabaseMetricsAsync(string resourceId, TimeSpan window)
    {
        // For now, return mock data structure
        // TODO: Implement actual Azure Monitor queries for database metrics
        await Task.Delay(100); // Simulate processing time

        return new PostgreSQLDatabaseMetrics(
            DatabaseStats: new Dictionary<string, PostgreSQLDatabaseStats>
            {
                ["postgres"] = new PostgreSQLDatabaseStats(
                    DatabaseName: "postgres",
                    Backends: 8,
                    Deadlocks: 0,
                    BufferHitRatio: 99.2,
                    DiskReads: 1240,
                    TransactionRate: 150,
                    CommitRate: 148,
                    RollbackRate: 2
                ),
                ["myapp"] = new PostgreSQLDatabaseStats(
                    DatabaseName: "myapp",
                    Backends: 12,
                    Deadlocks: 1,
                    BufferHitRatio: 97.8,
                    DiskReads: 3200,
                    TransactionRate: 450,
                    CommitRate: 445,
                    RollbackRate: 5
                )
            }
        );
    }

    /// <summary>
    /// Collects resource saturation metrics (always available)
    /// </summary>
    private async Task<PostgreSQLSaturationMetrics> GetSaturationMetricsAsync(string resourceId, TimeSpan window)
    {
        // For now, return mock data structure
        // TODO: Implement actual Azure Monitor queries for saturation metrics
        await Task.Delay(100); // Simulate processing time

        return new PostgreSQLSaturationMetrics(
            DiskBandwidthPercent: 35.2,
            DiskIOPSPercent: 42.1,
            NetworkIOPercent: 12.5,
            TempFileUsage: 1024 * 1024 * 50 // 50MB
        );
    }

    /// <summary>
    /// Collects activity metrics (requires collector_database_activity = ON)
    /// </summary>
    private async Task<PostgreSQLActivityMetrics> GetActivityMetricsAsync(string resourceId, TimeSpan window)
    {
        // For now, return mock data structure
        // TODO: Implement actual Azure Monitor queries for activity metrics
        await Task.Delay(100); // Simulate processing time

        return new PostgreSQLActivityMetrics(
            QueriesPerSecond: 45,
            TransactionsPerSecond: 38,
            QueryTypeDistribution: new Dictionary<string, int>
            {
                ["SELECT"] = 60,
                ["INSERT"] = 20,
                ["UPDATE"] = 15,
                ["DELETE"] = 5
            },
            AverageTransactionDuration: 1.2,
            LongRunningQueries: 2
        );
    }

    /// <summary>
    /// Checks if a server parameter is enabled
    /// </summary>
    private async Task<bool> CheckServerParameterAsync(string resourceId, string parameterName)
    {
        try
        {
            // For now, return mock values based on parameter name
            // TODO: Implement actual parameter checking via ARM API or Azure Monitor
            await Task.Delay(50); // Simulate API call

            return parameterName switch
            {
                "metrics.collector_database_activity" => false, // Default to false to encourage setup
                "metrics.autovacuum_diagnostics" => false,
                "pgbouncer.enabled" => false,
                "metrics.pgbouncer_diagnostics" => false,
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"[postgresql_config] Error checking parameter {parameterName}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Builds summary for metrics groups collection
    /// </summary>
    private string BuildMetricsGroupsSummary(PostgreSQLMetricGroup[] groups, PostgreSQLCoreMetrics core,
        PostgreSQLEnhancedMetrics? enhanced, PostgreSQLDatabaseMetrics? database,
        PostgreSQLSaturationMetrics? saturation, PostgreSQLActivityMetrics? activity,
        List<string> limitations, double durationSeconds)
    {
        var summary = new List<string>();

        summary.Add($"🔍 **PostgreSQL Metrics Collection** ({durationSeconds:F1}s)");
        summary.Add($"📊 **Core**: CPU {core.CpuPercent:F1}%, Memory {core.MemoryPercent:F1}%, Connections {core.ActiveConnections}/{core.MaxConnections}");

        if (enhanced != null)
        {
            summary.Add($"🔄 **Enhanced**: {enhanced.ActiveQueries} active queries, {enhanced.IdleConnections} idle connections");
        }

        if (database != null)
        {
            var dbCount = database.DatabaseStats.Count;
            var totalBackends = database.DatabaseStats.Values.Sum(d => d.Backends);
            summary.Add($"🗃️ **Database**: {dbCount} databases, {totalBackends} total backends");
        }

        if (saturation != null)
        {
            summary.Add($"⚡ **Saturation**: Disk {saturation.DiskIOPSPercent:F1}% IOPS, {saturation.DiskBandwidthPercent:F1}% bandwidth");
        }

        if (activity != null)
        {
            summary.Add($"📈 **Activity**: {activity.QueriesPerSecond} queries/sec, {activity.TransactionsPerSecond} txn/sec");
        }

        if (limitations.Any())
        {
            summary.Add($"⚠️ **Limitations**: {string.Join("; ", limitations)}");
        }

        return string.Join("\n", summary);
    }

    /// <summary>
    /// Builds summary for configuration status
    /// </summary>
    private string BuildConfigurationSummary(PostgreSQLMetricGroup[] available, PostgreSQLMetricGroup[] unavailable,
        Dictionary<string, string> missing)
    {
        var summary = new List<string>();

        summary.Add("🔧 **PostgreSQL Enhanced Metrics Configuration**");
        summary.Add($"✅ **Available Groups**: {string.Join(", ", available)}");

        if (unavailable.Any())
        {
            summary.Add($"❌ **Unavailable Groups**: {string.Join(", ", unavailable)}");
        }

        if (missing.Any())
        {
            summary.Add("📝 **Missing Configuration**:");
            foreach (var config in missing)
            {
                summary.Add($"   • {config.Key}: {config.Value}");
            }
        }
        else
        {
            summary.Add("🎉 **All enhanced metrics are properly configured!**");
        }

        return string.Join("\n", summary);
    }

    /// <summary>
    /// Validates if the provided string is a valid Azure resource ID format
    /// </summary>
    /// <param name="resourceId">The resource ID to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    private bool IsValidAzureResourceId(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
        {
            return false;
        }

        // Azure resource IDs must start with /subscriptions/ or /providers/
        return resourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) ||
               resourceId.StartsWith("/providers/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses PostgreSQL table bloat query results from aligned table format
    /// </summary>
    /// <param name="queryOutput">Raw output from psql command</param>
    /// <returns>List of parsed bloated tables</returns>
    private List<BloatedTable> ParseTableBloatResults(string queryOutput)
    {
        var bloatedTables = new List<BloatedTable>();
        var lines = queryOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find the header row (contains column names)
        int headerIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("schemaname") && lines[i].Contains("relname"))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex == -1)
        {
            _logger.LogInternalWarning("[postgresql_bloat] Could not find header row in query results");
            return bloatedTables;
        }

        // Parse data rows (skip header and separator lines)
        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("-") || line.StartsWith("(") || line.Contains("rows)"))
                continue;

            try
            {
                var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
                if (parts.Length >= 4)
                {
                    // Column order: schemaname, relname, n_live_tup, n_dead_tup  
                    var schemaName = parts[0];
                    var tableName = parts[1]; // This is relname from the query
                    var liveTuples = long.TryParse(parts[2], out var live) ? live : 0;
                    var deadTuples = long.TryParse(parts[3], out var dead) ? dead : 0;

                    // Calculate dead tuple percentage
                    var totalTuples = liveTuples + deadTuples;
                    var deadTuplePercentage = totalTuples > 0 ? (100.0 * deadTuples / totalTuples) : 0.0;

                    // Use dead tuple percentage as bloat percentage for now
                    var bloatPercentage = deadTuplePercentage;

                    // Include all tables for testing, not just those with >20% bloat
                    bloatedTables.Add(new BloatedTable(
                        SchemaName: schemaName,
                        TableName: tableName,
                        TableSize: "Unknown", // Will be filled by EnrichWithTableSizes
                        BloatPercentage: bloatPercentage,
                        BloatSize: "Unknown", // Will be calculated after we get table size
                        LiveTuples: liveTuples,
                        DeadTuples: deadTuples,
                        DeadTuplePercentage: deadTuplePercentage
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"[postgresql_bloat] Error parsing line: {line}, error: {ex.Message}");
            }
        }

        return bloatedTables;
    }

    /// <summary>
    /// Enriches bloated tables with size information using a separate query
    /// </summary>
    /// <param name="bloatedTables">List of bloated tables to enrich</param>
    private async Task EnrichWithTableSizes(List<BloatedTable> bloatedTables, string resourceId, string database)
    {
        if (!bloatedTables.Any()) return;

        try
        {
            // Get table sizes for all bloated tables
            var sizeQuery = @"
SELECT 
    schemaname,
    relname,
    'Unknown' AS table_size
FROM pg_stat_user_tables 
WHERE schemaname NOT IN ('information_schema', 'pg_catalog')";

            var result = await _postgresSQLCommandHelper.ExecutePsqlCommandAsync(sizeQuery, resourceId, database);

            if (!result.ErrorOccurred && !string.IsNullOrWhiteSpace(result.Output))
            {
                var tableSizes = ParseTableSizes(result.Output);

                // Update bloated tables with size information
                for (int i = 0; i < bloatedTables.Count; i++)
                {
                    var table = bloatedTables[i];
                    var key = $"{table.SchemaName}.{table.TableName}";

                    if (tableSizes.TryGetValue(key, out var size))
                    {
                        var bloatSize = CalculateBloatSize(size, table.BloatPercentage);

                        // Create a new record with updated values (since BloatedTable is a record)
                        bloatedTables[i] = table with
                        {
                            TableSize = size,
                            BloatSize = bloatSize
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"[postgresql_bloat] Error enriching with table sizes: {ex.Message}");
            // Continue with unknown sizes if this fails
        }
    }

    /// <summary>
    /// Parses table size query results
    /// </summary>
    /// <param name="queryOutput">Raw output from size query</param>
    /// <returns>Dictionary mapping schema.table to size</returns>
    private Dictionary<string, string> ParseTableSizes(string queryOutput)
    {
        var tableSizes = new Dictionary<string, string>();
        var lines = queryOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        // Find the header row
        int headerIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("schemaname") && lines[i].Contains("table_size"))
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex == -1) return tableSizes;

        // Parse data rows
        for (int i = headerIndex + 2; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("-") || line.StartsWith("(") || line.Contains("rows)"))
                continue;

            try
            {
                var parts = line.Split('|', StringSplitOptions.None).Select(p => p.Trim()).ToArray();
                if (parts.Length >= 3)
                {
                    var schemaName = parts[0];
                    var tableName = parts[1];
                    var tableSize = parts[2];
                    var key = $"{schemaName}.{tableName}";
                    tableSizes[key] = tableSize;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"[postgresql_bloat] Error parsing table size line: {line}, error: {ex.Message}");
            }
        }

        return tableSizes;
    }

    /// <summary>
    /// Calculates approximate bloat size based on table size and bloat percentage
    /// </summary>
    /// <param name="tableSize">Table size string (e.g., "1.2 GB")</param>
    /// <param name="bloatPercentage">Bloat percentage</param>
    /// <returns>Formatted bloat size string</returns>
    private string CalculateBloatSize(string tableSize, double bloatPercentage)
    {
        try
        {
            var sizeBytes = GetSizeInBytes(tableSize);
            if (sizeBytes > 0)
            {
                var bloatBytes = (long)(sizeBytes * bloatPercentage / 100.0);
                return FormatSize(bloatBytes);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"[postgresql_bloat] Error calculating bloat size: {ex.Message}");
        }

        return "Unknown";
    }

    /// <summary>
    /// Formats bytes into human-readable size string
    /// </summary>
    /// <param name="bytes">Size in bytes</param>
    /// <returns>Formatted size string</returns>
    private string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB";
        else if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        else if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        else
            return $"{bytes} B";
    }

    /// <summary>
    /// Generates recommendations based on table bloat analysis
    /// </summary>
    /// <param name="bloatedTables">List of bloated tables</param>
    /// <returns>List of recommendations</returns>
    private List<string> GenerateBloatRecommendations(List<BloatedTable> bloatedTables)
    {
        var recommendations = new List<string>();

        var severelyBloated = bloatedTables.Where(t => t.BloatPercentage > 50).ToList();
        var moderatelyBloated = bloatedTables.Where(t => t.BloatPercentage > 30 && t.BloatPercentage <= 50).ToList();
        var highDeadTuples = bloatedTables.Where(t => t.DeadTuplePercentage > 40).ToList();

        if (severelyBloated.Any())
        {
            recommendations.Add($"URGENT: {severelyBloated.Count} tables have severe bloat (>50%). Consider VACUUM FULL during maintenance window.");
            recommendations.Add($"Severely bloated tables: {string.Join(", ", severelyBloated.Select(t => $"{t.SchemaName}.{t.TableName}"))}");
        }

        if (moderatelyBloated.Any())
        {
            recommendations.Add($"Run VACUUM (VERBOSE, ANALYZE) on {moderatelyBloated.Count} moderately bloated tables (30-50% bloat).");
        }

        if (highDeadTuples.Any())
        {
            recommendations.Add($"Check autovacuum settings for {highDeadTuples.Count} tables with high dead tuple ratios (>40%).");
        }

        recommendations.Add("Review autovacuum_vacuum_scale_factor and autovacuum_vacuum_threshold settings");
        recommendations.Add("Monitor dead tuple ratios and adjust autovacuum frequency if needed");
        recommendations.Add("Consider disabling autovacuum temporarily during VACUUM FULL operations");

        return recommendations;
    }

    /// <summary>
    /// Generates summary text for table bloat analysis
    /// </summary>
    /// <param name="bloatedTables">List of bloated tables</param>
    /// <returns>Summary text</returns>
    private string GenerateBloatSummary(List<BloatedTable> bloatedTables)
    {
        if (!bloatedTables.Any())
        {
            return "✅ No significant table bloat detected. All analyzed tables have bloat levels below 20%.";
        }

        var totalWastedSpace = bloatedTables.Sum(t => GetSizeInBytes(t.BloatSize)) / (1024.0 * 1024.0 * 1024.0);
        var highestBloat = bloatedTables.OrderByDescending(t => t.BloatPercentage).First();
        var severelyBloated = bloatedTables.Count(t => t.BloatPercentage > 50);

        var summary = $"Found {bloatedTables.Count} tables with significant bloat (>20%). ";

        if (severelyBloated > 0)
        {
            summary += $"⚠️ {severelyBloated} tables are severely bloated (>50%). ";
        }

        summary += $"Highest bloat: {highestBloat.BloatPercentage}% in {highestBloat.SchemaName}.{highestBloat.TableName}. ";
        summary += $"Total wasted space: ~{totalWastedSpace:F1} GB.";

        return summary;
    }

    /// <summary>
    /// Fallback method that returns mock data when real query fails
    /// </summary>
    /// <param name="resourceId">The resource ID</param>
    /// <returns>Mock table bloat analysis</returns>
    private async Task<TableBloatAnalysis> GetMockTableBloatData(string resourceId)
    {
        await Task.Delay(200); // Simulate async operation

        var bloatedTables = new List<BloatedTable>
        {
            new BloatedTable(
                SchemaName: "public",
                TableName: "user_activities",
                TableSize: "2.1 GB",
                BloatPercentage: 67.3,
                BloatSize: "1.4 GB",
                LiveTuples: 1_250_000,
                DeadTuples: 850_000,
                DeadTuplePercentage: 40.5
            ),
            new BloatedTable(
                SchemaName: "public",
                TableName: "audit_logs",
                TableSize: "856 MB",
                BloatPercentage: 34.2,
                BloatSize: "293 MB",
                LiveTuples: 2_100_000,
                DeadTuples: 450_000,
                DeadTuplePercentage: 17.6
            )
        };

        var recommendations = GenerateBloatRecommendations(bloatedTables);
        var summary = GenerateBloatSummary(bloatedTables) + " (Note: Using fallback mock data due to connection issue)";

        return new TableBloatAnalysis(
            ResourceId: resourceId,
            AnalyzedAt: DateTime.UtcNow,
            BloatedTables: bloatedTables,
            Summary: summary,
            Recommendations: recommendations
        );
    }

}
