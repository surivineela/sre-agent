using Agent.Plugins.Models;

namespace Agent.Plugins.Interface;

/// <summary>
/// Plugin for diagnosing and analyzing PostgreSQL performance and connectivity issues
/// </summary>
public interface IPostgreSQLPlugin
{
    /// <summary>
    /// Gets the thread ID for the plugin
    /// </summary>
    Guid? ThreadId { get; set; }

    /// <summary>
    /// Gets PostgreSQL performance metrics including CPU, memory, connections, and query performance
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>PostgreSQL performance metrics</returns>
    Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window);

    /// <summary>
    /// Tests connectivity to PostgreSQL server and analyzes connection issues
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Connection test results and analysis</returns>
    Task<ConnectionTestResult> CheckPostgreSQLConnectivityAsync(string resourceId);

    /// <summary>
    /// Analyzes slow-running queries and identifies performance bottlenecks
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="startTime">The start time for the metric query range (Absolute in UTC or relative). Examples: '2024-03-05 10:50:00', '20 hours ago', '3 days ago'. Prefer relative format for recent values (e.g: '24 hours ago', '2 days ago'). Validation start date should be within last 90 days</param>
    /// <param name="endTime">The end time for the metric query range (Absolute in UTC or relative). Examples: '2024-03-05 10:50:00', 'now', 'an hour ago'. Prefer relative format for recent value (e.g: 'now', '1 hour ago'). Validation limit end date from last 90 days</param>
    /// <returns>Slow query analysis results</returns>
    Task<SlowQueryAnalysis> AnalyzeSlowQueriesAsync(string resourceId, DateTimeOffset startTime, DateTimeOffset endTime);

    /// <summary>
    /// Gets Azure resource health status and recent health events
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Resource health status</returns>
    Task<ResourceHealthStatus> GetResourceHealthAsync(string resourceId);

    /// <summary>
    /// Lists available diagnostic playbooks for PostgreSQL troubleshooting
    /// </summary>
    /// <returns>List of available playbooks</returns>
    Task<List<PlaybookInfo>> ListAvailablePlaybooksAsync();

    /// <summary>
    /// Retrieves specific troubleshooting playbook content
    /// </summary>
    /// <param name="playbookName">Name of the playbook to retrieve</param>
    /// <returns>Playbook content</returns>
    Task<PlaybookContent> GetPlaybookAsync(string playbookName);

    /// <summary>
    /// Validates PostgreSQL diagnostic configuration and identifies missing setup steps
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Configuration validation status</returns>
    Task<PostgreSQLConfigurationStatus> ValidatePostgreSQLConfigurationAsync(string resourceId);

    /// <summary>
    /// Gets the correct Log Analytics workspace where PostgreSQL diagnostic settings send logs
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Workspace resource ID or null if not configured</returns>
    Task<string?> GetDiagnosticWorkspaceForResourceAsync(string resourceId);

    /// <summary>
    /// Analyzes PostgreSQL table bloat by comparing actual vs estimated table sizes
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Table bloat analysis results</returns>
    Task<TableBloatAnalysis> AnalyzeTableBloat(string resourceId);

    /// <summary>
    /// Checks autovacuum configuration and identifies disabled autovacuum tables
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Autovacuum configuration analysis</returns>
    Task<AutovacuumConfigurationAnalysis> AnalyzeAutovacuumConfiguration(string resourceId);

    /// <summary>
    /// Shows table activity statistics including insert/update/delete rates and vacuum history
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Table activity analysis results</returns>
    Task<TableActivityAnalysis> AnalyzeTableActivity(string resourceId);

    /// <summary>
    /// Gets comprehensive PostgreSQL database overview including size, settings, and health
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Database overview analysis</returns>
    Task<DatabaseOverviewAnalysis> GetDatabaseOverview(string resourceId);

    /// <summary>
    /// Comprehensive PostgreSQL health check combining multiple diagnostic areas
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Complete health analysis</returns>
    Task<PostgreSQLHealthAnalysis> AnalyzePostgreSQLHealth(string resourceId);

    /// <summary>
    /// Gets PostgreSQL performance metrics with specific metric groups for optimized collection
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <param name="metricGroups">Metric groups to collect (null = Core only)</param>
    /// <returns>PostgreSQL metrics with selected groups</returns>
    Task<PostgreSQLMetricsWithGroups> GetPostgreSQLMetricsWithGroupsAsync(string resourceId, TimeSpan window, PostgreSQLMetricGroup[]? metricGroups = null);

    /// <summary>
    /// Validates enhanced metrics configuration and returns available metric groups
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the PostgreSQL server</param>
    /// <returns>Enhanced metrics configuration status</returns>
    Task<PostgreSQLEnhancedMetricsStatus> CheckEnhancedMetricsConfigurationAsync(string resourceId);
}

/// <summary>
/// PostgreSQL performance metrics data
/// </summary>
public record PostgreSQLMetrics(
    string ResourceId,
    DateTime Timestamp,
    double CpuPercent,
    double MemoryPercent,
    int ActiveConnections,
    int MaxConnections,
    double CacheHitRatio,
    double AverageQueryDuration,
    long TotalQueries,
    string Summary);

/// <summary>
/// Connection test result data
/// </summary>
public record ConnectionTestResult(
    string ResourceId,
    bool IsSuccessful,
    string Status,
    int ConnectionPoolSize,
    double AverageConnectionDuration,
    List<string> Issues,
    string Summary);

/// <summary>
/// Slow query analysis data
/// </summary>
public record SlowQueryAnalysis(
    string ResourceId,
    List<SlowQuery> SlowQueries,
    List<string> Recommendations,
    string Summary);

/// <summary>
/// Individual slow query information
/// </summary>
public record SlowQuery(
    string QueryText,
    int ExecutionCount,
    double AverageDuration,
    double MaxDuration,
    string ExecutionPlan,
    List<string> Issues);

/// <summary>
/// Resource health status data
/// </summary>
public record ResourceHealthStatus(
    string ResourceId,
    string HealthStatus,
    List<HealthEvent> RecentEvents,
    string Summary);

/// <summary>
/// Health event information
/// </summary>
public record HealthEvent(
    DateTime Timestamp,
    string EventType,
    string Summary,
    string Impact);

/// <summary>
/// PostgreSQL configuration validation status
/// </summary>
public record PostgreSQLConfigurationStatus(
    string ResourceId,
    bool HasDiagnosticSettings,
    bool HasQueryStore,
    bool HasPerformanceInsights,
    bool HasConnectionLogging,
    string? LogAnalyticsWorkspace,
    List<string> MissingConfigurations,
    List<string> SetupInstructions,
    string Summary);

/// <summary>
/// Table bloat analysis results
/// </summary>
public record TableBloatAnalysis(
    string ResourceId,
    DateTime AnalyzedAt,
    List<BloatedTable> BloatedTables,
    string Summary,
    List<string> Recommendations
);

/// <summary>
/// Individual bloated table information
/// </summary>
public record BloatedTable(
    string SchemaName,
    string TableName,
    string TableSize,
    double BloatPercentage,
    string BloatSize,
    long LiveTuples,
    long DeadTuples,
    double DeadTuplePercentage
);

/// <summary>
/// Autovacuum configuration analysis results
/// </summary>
public record AutovacuumConfigurationAnalysis(
    string ResourceId,
    DateTime AnalyzedAt,
    bool GlobalAutovacuumEnabled,
    List<TableAutovacuumSettings> TableSettings,
    string Summary,
    List<string> Issues
);

/// <summary>
/// Table autovacuum settings information
/// </summary>
public record TableAutovacuumSettings(
    string SchemaName,
    string TableName,
    string TableSize,
    bool AutovacuumEnabled,
    string VacuumThreshold,
    string VacuumScaleFactor,
    string SettingsSource // "Global" or "Table-specific"
);

/// <summary>
/// Table activity analysis results
/// </summary>
public record TableActivityAnalysis(
    string ResourceId,
    DateTime AnalyzedAt,
    List<TableActivity> TableActivities,
    string Summary
);

/// <summary>
/// Individual table activity information
/// </summary>
public record TableActivity(
    string SchemaName,
    string TableName,
    string TableSize,
    long TotalInserts,
    long TotalUpdates,
    long TotalDeletes,
    long LiveTuples,
    long DeadTuples,
    double DeadTuplePercentage,
    DateTime? LastVacuum,
    DateTime? LastAutovacuum,
    int VacuumCount,
    int AutovacuumCount,
    double ChangesPerDay
);

/// <summary>
/// Database overview analysis results
/// </summary>
public record DatabaseOverviewAnalysis(
    string ResourceId,
    DateTime AnalyzedAt,
    string DatabaseName,
    string DatabaseSize,
    int UserTableCount,
    long TotalLiveTuples,
    long TotalDeadTuples,
    long TotalModifications,
    bool GlobalAutovacuumEnabled,
    string AutovacuumMaxWorkers,
    string AutovacuumNaptime,
    string MaintenanceWorkMem,
    string Summary
);

/// <summary>
/// Comprehensive PostgreSQL health analysis
/// </summary>
public record PostgreSQLHealthAnalysis(
    string ResourceId,
    DateTime AnalyzedAt,
    DatabaseOverviewAnalysis DatabaseOverview,
    TableBloatAnalysis BloatAnalysis,
    AutovacuumConfigurationAnalysis AutovacuumAnalysis,
    TableActivityAnalysis ActivityAnalysis,
    List<string> CriticalIssues,
    List<string> Warnings,
    List<string> Recommendations,
    string OverallHealthStatus,
    string Summary
);

/// <summary>
/// PostgreSQL metric groups for targeted collection
/// </summary>
public enum PostgreSQLMetricGroup
{
    /// <summary>
    /// Core metrics: CPU, Memory, Storage, Active/Max connections
    /// Always available, fastest collection (~5 seconds)
    /// </summary>
    Core,

    /// <summary>
    /// Enhanced metrics: Sessions by state, wait events, oldest backend/query/transaction
    /// Requires: metrics.collector_database_activity = ON
    /// Performance: +2-3 seconds
    /// </summary>
    Enhanced,

    /// <summary>
    /// Database-specific metrics: Per-database backends, deadlocks, buffer hits, transaction rates
    /// Requires: metrics.collector_database_activity = ON
    /// Performance: +2-3 seconds
    /// </summary>
    Database,

    /// <summary>
    /// Resource saturation metrics: Disk bandwidth/IOPS consumption percentages
    /// Always available, Performance: +1-2 seconds
    /// </summary>
    Saturation,

    /// <summary>
    /// Query and transaction activity metrics
    /// Requires: metrics.collector_database_activity = ON
    /// Performance: +1-2 seconds
    /// </summary>
    Activity
}

/// <summary>
/// Enhanced PostgreSQL metrics with group-based collection results
/// </summary>
public record PostgreSQLMetricsWithGroups(
    string ResourceId,
    DateTime Timestamp,
    PostgreSQLMetricGroup[] CollectedGroups,
    PostgreSQLCoreMetrics Core,
    PostgreSQLEnhancedMetrics? Enhanced,
    PostgreSQLDatabaseMetrics? Database,
    PostgreSQLSaturationMetrics? Saturation,
    PostgreSQLActivityMetrics? Activity,
    List<string> ConfigurationLimitations,
    double CollectionDurationSeconds,
    string Summary);

/// <summary>
/// Core PostgreSQL metrics (always available)
/// </summary>
public record PostgreSQLCoreMetrics(
    double CpuPercent,
    double MemoryPercent,
    double StoragePercent,
    int ActiveConnections,
    int MaxConnections,
    double ConnectionPercent,
    double CacheHitRatio,
    double AverageQueryDuration,
    long TotalQueries);

/// <summary>
/// Enhanced PostgreSQL metrics (requires collector_database_activity = ON)
/// </summary>
public record PostgreSQLEnhancedMetrics(
    Dictionary<string, int> SessionsByState,
    Dictionary<string, int> SessionsByWaitEvent,
    double OldestBackendMinutes,
    double OldestQueryMinutes,
    double OldestTransactionMinutes,
    int IdleConnections,
    int ActiveQueries,
    int BlockedQueries);

/// <summary>
/// Per-database PostgreSQL metrics (requires collector_database_activity = ON)
/// </summary>
public record PostgreSQLDatabaseMetrics(
    Dictionary<string, PostgreSQLDatabaseStats> DatabaseStats);

/// <summary>
/// Individual database statistics
/// </summary>
public record PostgreSQLDatabaseStats(
    string DatabaseName,
    int Backends,
    int Deadlocks,
    double BufferHitRatio,
    long DiskReads,
    long TransactionRate,
    long CommitRate,
    long RollbackRate);

/// <summary>
/// Resource saturation metrics (always available)
/// </summary>
public record PostgreSQLSaturationMetrics(
    double DiskBandwidthPercent,
    double DiskIOPSPercent,
    double NetworkIOPercent,
    double TempFileUsage);

/// <summary>
/// Query and transaction activity metrics (requires collector_database_activity = ON)
/// </summary>
public record PostgreSQLActivityMetrics(
    long QueriesPerSecond,
    long TransactionsPerSecond,
    Dictionary<string, int> QueryTypeDistribution,
    double AverageTransactionDuration,
    int LongRunningQueries);

/// <summary>
/// Enhanced metrics configuration validation status
/// </summary>
public record PostgreSQLEnhancedMetricsStatus(
    string ResourceId,
    bool HasCollectorDatabaseActivity,
    bool HasAutovacuumDiagnostics,
    bool HasPgBouncerEnabled,
    bool HasPgBouncerDiagnostics,
    PostgreSQLMetricGroup[] AvailableGroups,
    PostgreSQLMetricGroup[] UnavailableGroups,
    Dictionary<string, string> MissingConfiguration,
    List<string> SetupInstructions,
    string Summary);
