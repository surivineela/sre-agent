using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;

namespace Agent.Plugins.Definitions
{
    /// <summary>
    /// Definition for the PostgreSQL Plugin
    /// </summary>
    [AgentToolPlugin]
    public class PostgreSQLPluginDefinition
    {
        private readonly IPostgreSQLPlugin _postgreSQLPlugin;

        /// <summary>
        /// Constructor for PostgreSQLPluginDefinition
        /// </summary>
        /// <param name="postgreSQLPlugin">The PostgreSQL Plugin implementation</param>
        public PostgreSQLPluginDefinition(IPostgreSQLPlugin postgreSQLPlugin)
        {
            _postgreSQLPlugin = postgreSQLPlugin;
        }

        /// <summary>
        /// Gets PostgreSQL performance metrics using Core metrics only for fastest response
        /// </summary>
        [Description("Gets PostgreSQL Core performance metrics (CPU, memory, connections, storage) for fastest response (~5 seconds). " +
                    "PERFORMANCE: Optimized for speed vs comprehensive analysis. " +
                    "COVERAGE: CPU/Memory/Storage percentage, Active/Max connections, basic performance indicators. " +
                    "USE WHEN: Quick health checks, initial assessment, baseline establishment, when enhanced metrics unavailable. " +
                    "ALTERNATIVE: Use GetPostgreSQLMetricsWithGroups for enhanced analysis with session data, wait events, per-database metrics. " +
                    "EFFICIENCY: ~5 seconds vs 49+ seconds with legacy approach.")]
        public async Task<PostgreSQLMetrics> GetPostgreSQLMetrics(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId,
            [Description("Time window for metrics collection in minutes (default: 30)")] int windowMinutes = 30)
        {
            var window = TimeSpan.FromMinutes(windowMinutes);
            return await _postgreSQLPlugin.GetPostgreSQLMetricsAsync(resourceId, window);
        }

        /// <summary>
        /// Tests connectivity to PostgreSQL server and analyzes connection issues
        /// </summary>
        [Description("Tests connectivity to PostgreSQL server and analyzes connection pool status, connection duration patterns, and identifies connectivity issues. " +
                    "Provides detailed analysis of connection health and potential bottlenecks.")]
        public async Task<ConnectionTestResult> CheckPostgreSQLConnectivity(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.CheckPostgreSQLConnectivityAsync(resourceId);
        }

        /// <summary>
        /// Analyzes slow-running queries and identifies performance bottlenecks
        /// </summary>
        [Description("Analyzes slow-running queries using Query Store data to identify performance bottlenecks, missing indexes, and inefficient query patterns. " +
                    "Provides specific recommendations for query optimization and index creation.")]
        public async Task<SlowQueryAnalysis> AnalyzeSlowQueries(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId,
            [Description("Time window for analysis in minutes (default: 60)")] int windowMinutes = 60)
        {
            var window = TimeSpan.FromMinutes(windowMinutes);
            return await _postgreSQLPlugin.AnalyzeSlowQueriesAsync(resourceId, window);
        }

        /// <summary>
        /// Gets Azure resource health status and recent health events
        /// </summary>
        [Description("Gets Azure resource health status for the PostgreSQL server including recent health events, availability status, and platform-level issues. " +
                    "Provides insights into Azure platform health affecting the database server.")]
        public async Task<ResourceHealthStatus> GetResourceHealth(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.GetResourceHealthAsync(resourceId);
        }

        /// <summary>
        /// Lists available diagnostic playbooks for PostgreSQL troubleshooting
        /// </summary>
        [Description("Lists all available diagnostic playbooks for PostgreSQL troubleshooting including performance investigation, missing index analysis, " +
                    "connection optimization, and configuration setup guides.")]
        public async Task<List<PlaybookInfo>> ListAvailablePlaybooks()
        {
            return await _postgreSQLPlugin.ListAvailablePlaybooksAsync();
        }

        /// <summary>
        /// Retrieves specific troubleshooting playbook content
        /// </summary>
        [Description("Retrieves detailed content for a specific PostgreSQL troubleshooting playbook including step-by-step instructions, prerequisites, " +
                    "estimated time, and implementation guidance. Ensure ListAvailablePlaybooks is called first to see available options.")]
        public async Task<PlaybookContent> GetPlaybook(
            [Description("The name of the playbook to retrieve (use ListAvailablePlaybooks to see available options)")] string playbookName)
        {
            return await _postgreSQLPlugin.GetPlaybookAsync(playbookName);
        }

        /// <summary>
        /// Validates PostgreSQL diagnostic configuration and identifies missing setup steps
        /// </summary>
        [Description("Validates PostgreSQL diagnostic configuration including diagnostic settings, Query Store, Performance Insights, and connection logging. " +
                    "Identifies missing configurations and provides setup instructions for optimal monitoring capabilities.")]
        public async Task<PostgreSQLConfigurationStatus> ValidatePostgreSQLConfiguration(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.ValidatePostgreSQLConfigurationAsync(resourceId);
        }

        /// <summary>
        /// Gets the Log Analytics workspace where PostgreSQL diagnostic settings send logs and metrics
        /// </summary>
        [Description("Identifies the Log Analytics workspace configured for PostgreSQL diagnostic settings to ensure metrics queries target the correct data source. " +
                    "Essential for accurate performance monitoring and troubleshooting.")]
        public async Task<string?> GetDiagnosticWorkspaceForResource(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.GetDiagnosticWorkspaceForResourceAsync(resourceId);
        }

        /// <summary>
        /// Analyzes PostgreSQL table bloat by comparing actual vs estimated table sizes
        /// </summary>
        [Description("Analyzes PostgreSQL table bloat by comparing actual table sizes to estimated sizes based on live tuples. " +
                    "Identifies tables with excessive bloat that may need vacuum attention. Critical for maintaining PostgreSQL performance.")]
        public async Task<TableBloatAnalysis> AnalyzeTableBloat(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.AnalyzeTableBloat(resourceId);
        }

        /// <summary>
        /// Checks autovacuum configuration and identifies disabled autovacuum tables
        /// </summary>
        [Description("Checks autovacuum configuration for all tables, identifying tables with disabled autovacuum and showing current settings. " +
                    "Critical for maintaining PostgreSQL performance as disabled autovacuum leads to table bloat.")]
        public async Task<AutovacuumConfigurationAnalysis> AnalyzeAutovacuumConfiguration(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.AnalyzeAutovacuumConfiguration(resourceId);
        }

        /// <summary>
        /// Shows table activity statistics including insert/update/delete rates and vacuum history
        /// </summary>
        [Description("Shows detailed table activity statistics including insert/update/delete rates, vacuum history, and dead tuple ratios. " +
                    "Helps identify high-activity tables needing attention and correlates activity with bloat issues.")]
        public async Task<TableActivityAnalysis> AnalyzeTableActivity(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.AnalyzeTableActivity(resourceId);
        }

        /// <summary>
        /// Gets comprehensive PostgreSQL database overview including size, settings, and health
        /// </summary>
        [Description("Gets comprehensive PostgreSQL database overview including database size, table counts, tuple statistics, and global autovacuum settings. " +
                    "Provides baseline information for PostgreSQL health assessment.")]
        public async Task<DatabaseOverviewAnalysis> GetDatabaseOverview(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.GetDatabaseOverview(resourceId);
        }

        /// <summary>
        /// Performs comprehensive PostgreSQL health analysis combining multiple diagnostic areas
        /// </summary>
        [Description("Performs comprehensive PostgreSQL health analysis combining bloat, autovacuum, activity, and overview data. " +
                    "Use this for complete diagnostic assessment and overall health status. Provides prioritized issues and recommendations.")]
        public async Task<PostgreSQLHealthAnalysis> AnalyzePostgreSQLHealth(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.AnalyzePostgreSQLHealth(resourceId);
        }

        /// <summary>
        /// Gets PostgreSQL performance metrics with specific metric groups for optimized collection
        /// </summary>
        [Description("Gets PostgreSQL performance metrics with targeted metric groups for optimal performance. " +
                    "PERFORMANCE: Core(~5s), Enhanced(~8s), Database(~10s), Comprehensive(~12s) vs Legacy(49s+). " +
                    "GROUPS: Core=CPU/Memory/Connections(always), Enhanced=Sessions/WaitEvents(requires config), " +
                    "Database=Per-DB stats(requires config), Saturation=Disk/IO(always), Activity=Queries/Txns(requires config). " +
                    "USE WHEN: Need specific metrics faster than full collection. " +
                    "CONFIGURATION: Enhanced/Database/Activity groups require 'metrics.collector_database_activity=ON'. " +
                    "EXAMPLES: Core=['Core'] for quick check, Enhanced=['Core','Enhanced'] for session analysis, " +
                    "Comprehensive=['Core','Enhanced','Database','Saturation','Activity'] for full analysis.")]
        public async Task<PostgreSQLMetricsWithGroups> GetPostgreSQLMetricsWithGroups(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId,
            [Description("Time window for metrics collection (e.g., '00:15:00' for 15 minutes)")] TimeSpan window,
            [Description("Metric groups to collect: Core (CPU/Memory/Connections), Enhanced (Sessions/WaitEvents), Database (Per-DB stats), Saturation (Disk/IO), Activity (Queries/Txns). Null defaults to Core only for fastest response.")] PostgreSQLMetricGroup[]? metricGroups = null)
        {
            return await _postgreSQLPlugin.GetPostgreSQLMetricsWithGroupsAsync(resourceId, window, metricGroups);
        }

        /// <summary>
        /// Validates enhanced metrics configuration and returns available metric groups
        /// </summary>
        [Description("Validates PostgreSQL enhanced metrics configuration and identifies which metric groups are available. " +
                    "CHECKS: Server parameters like 'metrics.collector_database_activity', 'metrics.autovacuum_diagnostics'. " +
                    "RETURNS: Available metric groups, missing configuration, and setup instructions. " +
                    "USE WHEN: Planning metrics collection strategy, troubleshooting missing enhanced metrics, " +
                    "or guiding users through PostgreSQL metrics setup. " +
                    "PERFORMANCE: ~3 seconds vs 22+ seconds with legacy configuration checks.")]
        public async Task<PostgreSQLEnhancedMetricsStatus> ValidateEnhancedMetricsConfiguration(
            [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
        {
            return await _postgreSQLPlugin.CheckEnhancedMetricsConfigurationAsync(resourceId);
        }
    }
}
