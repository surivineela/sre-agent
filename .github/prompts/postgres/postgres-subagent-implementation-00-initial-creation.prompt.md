---
mode: agent
---

Below is an implementation guide for a Postgres subagent. Implement this subagent in the codebase. Be sure to check the implementation against
this document to ensure it is complete, and do not stop until all success criteria is met to the best of your ability.

# PostgreSQL Intelligence Agent v0.1 - Implementation Design
## Agent SRE V2 Framework Implementation

---

## Executive Summary

This document defines the implementation approach for a PostgreSQL Intelligence Agent within the Agent SRE V2 framework. Following the established patterns in the codebase, this agent will be implemented as a V2 agent using the plugin architecture and YAML-based configuration.

### Key Design Goals
- **Framework Compliance**: Follow existing V2 agent patterns (FunctionAppAgent, ContainerAppAgent, etc.)
- **Plugin Architecture**: Use Interface → Implementation → Definition pattern
- **Tool-First Approach**: All diagnostics via structured tool calls
- **YAML Configuration**: Agent behavior defined in YAML specification
- **Automatic Discovery**: Tools discovered via `[AgentToolPlugin]` attribute

---

## Architecture Overview

```
Meta Agent
    ↓ Handoff
PostgreSQL Diagnostic Agent (V2 Agent)
    ↓ Tool Calls
PostgreSQL Plugins
    ↓ Data Access
PostgreSQL Target System + Azure Monitor
```

### Component Relationships
- **PostgreSQL Agent**: YAML-defined V2 agent in `AgentsV2/PostgreSQLAgent.yaml`
- **PostgreSQL Plugins**: Tool providers following the Interface/Implementation/Definition pattern
- **Tool Discovery**: Automatic via `[AgentToolPlugin]` attribute and DI registration

---

## Core Components

### 1. PostgreSQL Agent (V2 Agent)

**Implementation**: YAML file following existing patterns like `FunctionAppAgent.yaml`

#### Key Characteristics
- **YAML Configuration**: Agent behavior, tools, and handoffs defined in YAML
- **Framework Integration**: Uses existing V2 agent runner and tool discovery
- **Standard Tools**: Access to `NotifyUser`, `HandoffBack`, `WaitInMilliSeconds`, etc.
- **Tool Orchestrated**: All diagnostics via plugin tool calls

#### Agent YAML Structure
```yaml
name: postgresql_diagnostic_agent

system_prompt: |
  You are an **Azure PostgreSQL SRE Agent**. Your specialty is diagnosing 
  PostgreSQL performance issues, connectivity problems, and configuration errors.
  
handoff_description: "Handoff to this agent for PostgreSQL diagnostic issues"

handoffs:
  - meta_agent
  - diagnostic_cpu_agent
  - diagnostic_memory_agent

tools:
  - GetPostgreSQLMetrics
  - CheckPostgreSQLConnectivity
  - AnalyzeSlowQueries
  - GetResourceHealth
  - NotifyUser
  - HandoffBack
```

### 2. PostgreSQL Plugins (Tool Providers)

**Implementation**: Following the Interface → Implementation → Definition pattern used throughout the codebase.

#### Plugin Structure

**Interface** (`IPostgreSQLPlugin.cs`):
```csharp
public interface IPostgreSQLPlugin
{
    Guid? ThreadId { get; set; }  // Required by Agent Handbook for context injection
    Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window);
    Task<ConnectionTestResult> CheckPostgreSQLConnectivityAsync(string resourceId);
    Task<SlowQueryAnalysis> AnalyzeSlowQueriesAsync(string resourceId, TimeSpan window);
    Task<ResourceHealthStatus> GetResourceHealthAsync(string resourceId);
    Task<List<PlaybookInfo>> ListAvailablePlaybooksAsync();
    Task<PlaybookContent> GetPlaybookAsync(string playbookName);
}
```

**Implementation** (`PostgreSQLPlugin.cs`):
```csharp
public class PostgreSQLPlugin : IPostgreSQLPlugin
{
    private readonly IGraphDatabaseClient _databaseClient;
    private readonly IAzureMonitorClient _azureMonitorClient;
    private readonly ILogger<PostgreSQLPlugin> _logger;

    // Implementation methods...
}
```

**Definition** (`PostgreSQLPluginDefinition.cs`):
```csharp
[AgentToolPlugin]
public class PostgreSQLPluginDefinition
{
    private readonly IPostgreSQLPlugin _postgreSQLPlugin;

    public PostgreSQLPluginDefinition(IPostgreSQLPlugin postgreSQLPlugin)
    {
        _postgreSQLPlugin = postgreSQLPlugin;
    }

    [Description("Gets PostgreSQL performance metrics including connections, query performance, and resource utilization")]
    public async Task<PostgreSQLMetrics> GetPostgreSQLMetrics(
        [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId,
        [Description("Time window for metrics (in minutes)")] int windowMinutes = 30)
    {
        return await _postgreSQLPlugin.GetPostgreSQLMetricsAsync(resourceId, TimeSpan.FromMinutes(windowMinutes));
    }

    [Description("Tests connectivity to PostgreSQL server and analyzes connection issues")]
    public async Task<ConnectionTestResult> CheckPostgreSQLConnectivity(
        [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
    {
        return await _postgreSQLPlugin.CheckPostgreSQLConnectivityAsync(resourceId);
    }
}
```

---

## Implementation Following Agent Handbook Patterns

Based on the Agent Handbook, the implementation follows these steps:

### Step 1: Create Plugin Structure

Following the **Interface → Implementation → Definition** pattern:

#### 1.1 Interface (`src/Agent/Agent.Plugins/Interface/IPostgreSQLPlugin.cs`)
```csharp
public interface IPostgreSQLPlugin
{
    Guid? ThreadId { get; set; }  // Required by Agent Handbook for context injection
    Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window);
    Task<ConnectionTestResult> CheckPostgreSQLConnectivityAsync(string resourceId);
    Task<SlowQueryAnalysis> AnalyzeSlowQueriesAsync(string resourceId, TimeSpan window);
    Task<ResourceHealthStatus> GetResourceHealthAsync(string resourceId);
    Task<List<PlaybookInfo>> ListAvailablePlaybooksAsync();
    Task<PlaybookContent> GetPlaybookAsync(string playbookName);
}
```

#### 1.2 Implementation (`src/Agent/Agent.Plugins/Implementation/PostgreSQLPlugin.cs`)
```csharp
public class PostgreSQLPlugin : IPostgreSQLPlugin
{
    private readonly IGraphDatabaseClient _databaseClient;
    private readonly IAzureMonitorClient _azureMonitorClient;
    private readonly IPostgreSQLQueryExecutor _queryExecutor;
    private readonly ILogger<PostgreSQLPlugin> _logger;
    
    public Guid? ThreadId { get; set; }  // Framework-injected context

    public PostgreSQLPlugin(
        IGraphDatabaseClient databaseClient,
        IAzureMonitorClient azureMonitorClient,
        IPostgreSQLQueryExecutor queryExecutor,
        ILogger<PostgreSQLPlugin> logger)
    {
        _databaseClient = databaseClient;
        _azureMonitorClient = azureMonitorClient;
        _queryExecutor = queryExecutor;
        _logger = logger;
    }

    public async Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window)
    {
        _logger.LogInternalInformation($"[get_postgresql_metrics] Retrieving metrics for {resourceId}");
        
        // Implementation: Query Azure Monitor and PostgreSQL system tables
        var metrics = await _azureMonitorClient.GetMetricsAsync(resourceId, window);
        var dbMetrics = await _queryExecutor.ExecuteSystemQuery(resourceId, "SELECT * FROM pg_stat_database");
        
        return new PostgreSQLMetrics
        {
            ResourceId = resourceId,
            CpuPercent = metrics.CpuPercent,
            MemoryPercent = metrics.MemoryPercent,
            ConnectionCount = dbMetrics.ConnectionCount,
            ActiveQueries = dbMetrics.ActiveQueries
        };
    }

    // Additional implementation methods...
}
```

#### 1.3 Definition (`src/Agent/Agent.Plugins/Definitions/PostgreSQLPluginDefinition.cs`)
```csharp
[AgentToolPlugin]
public class PostgreSQLPluginDefinition : ContextToolTarget<AgentContext>
{
    private readonly IPostgreSQLPlugin _postgreSQLPlugin;

    public PostgreSQLPluginDefinition(IPostgreSQLPlugin postgreSQLPlugin)
    {
        _postgreSQLPlugin = postgreSQLPlugin;
    }

    [Description("Gets PostgreSQL metrics: CPU, memory, connections, query stats")]
    public async Task<PostgreSQLMetrics> GetPostgreSQLMetrics(
        [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId,
        [Description("Time window for metrics in minutes (default: 30)")] int windowMinutes = 30)
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        return await _postgreSQLPlugin.GetPostgreSQLMetricsAsync(resourceId, TimeSpan.FromMinutes(windowMinutes));
    }

    [Description("Tests connectivity to PostgreSQL server and identifies connection issues")]
    public async Task<ConnectionTestResult> CheckPostgreSQLConnectivity(
        [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        return await _postgreSQLPlugin.CheckPostgreSQLConnectivityAsync(resourceId);
    }

    [Description("Analyzes slow-running queries and identifies performance bottlenecks")]
    public async Task<SlowQueryAnalysis> AnalyzeSlowQueries(
        [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId,
        [Description("Time window for analysis in minutes (default: 60)")] int windowMinutes = 60)
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        return await _postgreSQLPlugin.AnalyzeSlowQueriesAsync(resourceId, TimeSpan.FromMinutes(windowMinutes));
    }

    [Description("Gets Azure resource health status and recent health events")]
    public async Task<ResourceHealthStatus> GetResourceHealth(
        [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        return await _postgreSQLPlugin.GetResourceHealthAsync(resourceId);
    }

    [Description("Lists available PostgreSQL troubleshooting playbooks")]
    public async Task<List<PlaybookInfo>> ListAvailablePlaybooks()
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        return await _postgreSQLPlugin.ListAvailablePlaybooksAsync();
    }

    [Description("Retrieves a PostgreSQL troubleshooting playbook with detailed steps")]
    public async Task<PlaybookContent> GetPlaybook(
        [Description("The name of the playbook to retrieve")] string playbookName)
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        return await _postgreSQLPlugin.GetPlaybookAsync(playbookName);
    }
}
```

### Step 2: Create Agent YAML

Following the pattern from `FunctionAppAgent.yaml`:

#### File: `src/Agent/Agent.Runtime/AgentsV2/PostgreSQLAgent.yaml`
```yaml
name: postgresql_diagnostic_agent

system_prompt: |
    You are **Azure PostgreSQL SRE Agent**. Your specialty is diagnosing and resolving Azure PostgreSQL 
    performance issues, connectivity problems, and configuration errors.
    
    You systematically identify root causes by analyzing metrics, query performance, connection patterns, 
    and configuration settings. You generate actionable reports with specific remediation steps.

    <CORE_RESPONSIBILITY_SCOPE>
    - Diagnose Azure PostgreSQL performance issues including slow queries, high CPU/memory usage, and connection problems
    - Analyze PostgreSQL metrics, logs, and system statistics to identify bottlenecks and optimization opportunities  
    - Provide specific remediation recommendations including index suggestions, query optimization, and configuration tuning
    - Guide users through PostgreSQL troubleshooting workflows using established playbooks
    </CORE_RESPONSIBILITY_SCOPE>

    ** Diagnosis Steps **
    1. Get PostgreSQL metrics to understand current performance baseline
    2. Check connectivity and identify any connection-related issues
    3. Analyze slow queries to identify performance bottlenecks
    4. Review resource health for any Azure-level issues
    5. Use relevant playbooks for systematic troubleshooting
    6. Provide specific recommendations with actionable steps

    ** Guidelines **
    - Always gather metrics first to establish a performance baseline
    - Use playbooks for systematic approach to common issues
    - Provide specific, actionable recommendations with exact commands when possible
    - Consider both database-level and Azure platform-level causes

handoff_description: "Handoff to this agent for Azure PostgreSQL diagnostic and performance issues"

handoffs:
  - meta_agent
  - diagnostic_cpu_agent
  - diagnostic_memory_agent

tools:
  - GetPostgreSQLMetrics
  - CheckPostgreSQLConnectivity
  - AnalyzeSlowQueries
  - GetResourceHealth
  - ListAvailablePlaybooks
  - GetPlaybook
  - NotifyUser
  - HandoffBack
  - WaitInMilliSeconds

common_prompts:
  - notify_user
  - ground_rail

max_reflection_count: 2

custom_reflection_note: |
  1. Have I gathered sufficient metrics to understand the performance baseline?
  2. Have I considered both database-level and Azure platform-level causes?
  3. Are my recommendations specific and actionable?
```

### Step 3: Register in Dependency Injection

Following the pattern in `Program.cs`:

```csharp
// In Program.cs or ServiceCollectionExtensions
builder.Services
    .AddTransient<IPostgreSQLPlugin, PostgreSQLPlugin>()
    .AddTransient<PostgreSQLPluginDefinition>();
```

### Step 4: Tool Discovery

Tools are automatically discovered via the `[AgentToolPlugin]` attribute. The framework will:

1. Scan assemblies for classes with `[AgentToolPlugin]`
2. Register tool methods with the `ToolFactory`
3. Make tools available to agents that list them in their YAML `tools:` section

---

## Data Models

Following the pattern from existing plugins:

```csharp
public record PostgreSQLMetrics(
    string ResourceId,
    double CpuPercent,
    double MemoryPercent,
    int ConnectionCount,
    int ActiveQueries,
    double QueryDurationMs,
    double CacheHitRatio,
    List<string> TopQueries);

public record ConnectionTestResult(
    bool IsSuccessful,
    string ErrorMessage,
    TimeSpan ConnectionTime,
    List<string> Issues);

public record SlowQueryAnalysis(
    List<SlowQuery> SlowQueries,
    List<string> IndexRecommendations,
    List<string> OptimizationSuggestions);

public record ResourceHealthStatus(
    string HealthState,
    List<string> RecentEvents,
    DateTime LastUpdated);

public record PlaybookInfo(
    string Name,
    string Description,
    string Category);

public record PlaybookContent(
    string Name,
    List<PlaybookStep> Steps,
    string Summary);
```

## Diagnostic Settings Integration

### Challenge: Finding the Correct Log Analytics Workspace

A critical implementation detail is ensuring that metrics queries target the correct Log Analytics workspace where the PostgreSQL Flexible Server's diagnostic settings send logs and metrics. 
The Azure Monitor client may be configured with a default workspace that differs from where the actual database metrics are stored.

### Solution: Query Diagnostic Settings

**Method**: `GetDiagnosticWorkspaceForResource`
```csharp
public async Task<string?> GetDiagnosticWorkspaceForResource(string resourceId)
{
    var armClient = await _armClientFactory.GetArmOperationClient();
    var resourceIdentifier = new ResourceIdentifier(resourceId);
    
    try
    {
        // Get diagnostic settings for the resource
        var resource = armClient.GetGenericResource(resourceIdentifier);
        var diagnosticSettings = resource.GetDiagnosticSettings();
        
        await foreach (var diagnosticSetting in diagnosticSettings)
        {
            var data = diagnosticSetting.Data;
            
            // Check if this diagnostic setting sends to Log Analytics
            if (!string.IsNullOrEmpty(data.WorkspaceId))
            {
                _logger.LogInternalInformation($"Found Log Analytics workspace for {resourceId}: {data.WorkspaceId}");
                return data.WorkspaceId;
            }
        }
        
        _logger.LogInternalWarning($"No Log Analytics workspace found in diagnostic settings for {resourceId}");
        return null;
    }
    catch (Exception ex)
    {
        _logger.LogInternalError(ex, $"Error getting diagnostic settings for {resourceId}");
        return null;
    }
}
```

### Integration with PostgreSQL Plugin

```csharp
public async Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window)
{
    // Step 1: Get the specific workspace for this PostgreSQL resource
    var workspaceId = await GetDiagnosticWorkspaceForResource(resourceId);
    
    if (string.IsNullOrEmpty(workspaceId))
    {
        _logger.LogInternalWarning($"No diagnostic workspace found for {resourceId}, using default Azure Monitor client");
        // Fallback to default Azure Monitor metrics
        return await GetMetricsFromAzureMonitor(resourceId, window);
    }
    
    // Step 2: Query the specific workspace for PostgreSQL metrics
    var metrics = await _logAnalyticsService.QueryWorkspaceAsync(workspaceId, BuildPostgreSQLMetricsQuery(), window);
    
    return ProcessMetricsResponse(metrics);
}
```

### PostgreSQL-Specific Metrics Queries

Once you have the correct workspace, you can query for PostgreSQL-specific metrics:

```csharp
private string BuildPostgreSQLMetricsQuery()
{
    return @"
        AzureMetrics
        | where ResourceProvider == 'MICROSOFT.DBFORPOSTGRESQL'
        | where ResourceId contains '/flexibleServers/'
        | where MetricName in (
            'cpu_percent',
            'memory_percent', 
            'active_connections',
            'connections_failed',
            'storage_percent',
            'storage_used',
            'backup_storage_used',
            'network_bytes_egress',
            'network_bytes_ingress'
        )
        | extend MetricValue = case(
            MetricName == 'cpu_percent', Average,
            MetricName == 'memory_percent', Average,
            MetricName == 'active_connections', Average,
            MetricName == 'connections_failed', Total,
            MetricName == 'storage_percent', Average,
            MetricName == 'storage_used', Average,
            MetricName == 'backup_storage_used', Average,
            MetricName == 'network_bytes_egress', Total,
            MetricName == 'network_bytes_ingress', Total,
            Average
        )
        | summarize 
            CpuPercent = max(case(MetricName == 'cpu_percent', MetricValue, 0.0)),
            MemoryPercent = max(case(MetricName == 'memory_percent', MetricValue, 0.0)),
            ActiveConnections = max(case(MetricName == 'active_connections', MetricValue, 0.0)),
            FailedConnections = max(case(MetricName == 'connections_failed', MetricValue, 0.0)),
            StoragePercent = max(case(MetricName == 'storage_percent', MetricValue, 0.0))
        by ResourceId
        | project 
            ResourceId,
            CpuPercent,
            MemoryPercent, 
            ActiveConnections,
            FailedConnections,
            StoragePercent,
            Timestamp = now()";
}
```

### Integration Points

- **Tool**: `GetPostgreSQLMetrics` - Uses diagnostic workspace for accurate metrics
- **Tool**: `CheckPostgreSQLConnectivity` - Can verify workspace connectivity as part of diagnostics
- **Tool**: `AnalyzeSlowQueries` - Ensures slow query logs come from the correct workspace

This approach resolves the uncertainty about which metrics workspace contains the actual PostgreSQL diagnostic data.

---


## Implementation Phases

### Phase 1: Core Plugin Framework
- [ ] Create `IPostgreSQLPlugin` interface with essential methods
- [ ] Implement `PostgreSQLPlugin` with basic Azure Monitor integration
- [ ] Create `PostgreSQLPluginDefinition` with `[AgentToolPlugin]` attribute
- [ ] Set up dependency injection registration
- [ ] Add basic logging and error handling

### Phase 2: Core Diagnostic Tools
- [ ] Implement `GetPostgreSQLMetrics` with Azure Monitor API
- [ ] Implement `CheckPostgreSQLConnectivity` with connection testing
- [ ] Implement `AnalyzeSlowQueries` with query performance analysis
- [ ] Implement `GetResourceHealth` with Azure Resource Health API
- [ ] Add comprehensive data models and return types

### Phase 3: Agent Configuration
- [ ] Create `PostgreSQLAgent.yaml` following established patterns
- [ ] Configure system prompts with PostgreSQL-specific guidance
- [ ] Set up tool mappings and handoff configurations
- [ ] Add reflection count and custom reflection notes
- [ ] Test agent discovery and tool registration

### Phase 4: Playbook Integration
- [ ] Implement `ListAvailablePlaybooks` and `GetPlaybook` methods
- [ ] Create embedded PostgreSQL troubleshooting playbooks
- [ ] Add playbook execution guidance to system prompts
- [ ] Test playbook-driven diagnostic workflows

### Phase 5: Testing and Integration
- [ ] End-to-end testing with Agent SRE framework
- [ ] Performance testing and optimization
- [ ] Error scenario testing and resilience validation
- [ ] Documentation and runbook creation
- [ ] Production readiness review

---

## Configuration and Deployment

### Configuration Structure

Following existing patterns in `appsettings.json`:

```json
{
  "PostgreSQLPlugin": {
    "ConnectionTimeout": "00:00:30",
    "QueryTimeout": "00:01:00",
    "MetricsWindow": "00:30:00",
    "MaxRetryAttempts": 3
  },
  "AzureMonitor": {
    "ApiTimeout": "00:00:10",
    "RetryCount": 3
  },
  "Playbooks": {
    "EmbeddedPath": "/playbooks/postgresql",
    "CacheDuration": "01:00:00"
  }
}
```

## Configuration Validation and User Guidance Pattern

### Challenge: Missing Diagnostic Setup

PostgreSQL diagnostic effectiveness heavily depends on proper configuration of several Azure and database-level features:

1. **Azure Diagnostic Settings** - Routes logs/metrics to Log Analytics
2. **Query Store Feature** - Enables query performance tracking 
3. **Log Analytics Workspace** - Stores diagnostic data
4. **Performance Insights** - Provides query-level metrics
5. **Connection Logging** - Tracks connection patterns

### Solution: Configuration Validation Pattern

Following the established codebase pattern from `FunctionAppConfigurationChecksPlugin` and agent communication guidelines:

#### 1. Configuration Detection Tools

```csharp
[Description("Validates PostgreSQL diagnostic configuration and identifies missing setup steps")]
public async Task<PostgreSQLConfigurationStatus> ValidatePostgreSQLConfiguration(
    [Description("The full Azure resource ID of the PostgreSQL server")] string resourceId)
{
    var configStatus = new PostgreSQLConfigurationStatus
    {
        ResourceId = resourceId,
        Issues = new List<ConfigurationIssue>(),
        Recommendations = new List<ConfigurationRecommendation>()
    };

    // Check diagnostic settings
    var diagnosticWorkspace = await GetDiagnosticWorkspaceForResource(resourceId);
    if (string.IsNullOrEmpty(diagnosticWorkspace))
    {
        configStatus.Issues.Add(new ConfigurationIssue
        {
            Severity = "High",
            Issue = "Diagnostic Settings Not Configured",
            Impact = "Limited visibility into database performance, errors, and connection issues",
            Category = "Monitoring"
        });
        
        configStatus.Recommendations.Add(new ConfigurationRecommendation
        {
            Title = "Configure Diagnostic Settings",
            Steps = GetDiagnosticSettingsInstructions(resourceId),
            Priority = "High",
            EstimatedTime = "5-10 minutes"
        });
    }

    // Check Query Store
    var queryStoreEnabled = await CheckQueryStoreEnabled(resourceId);
    if (!queryStoreEnabled)
    {
        configStatus.Issues.Add(new ConfigurationIssue
        {
            Severity = "Medium", 
            Issue = "Query Store Not Enabled",
            Impact = "Cannot analyze slow queries, missing query performance insights",
            Category = "Performance"
        });
        
        configStatus.Recommendations.Add(new ConfigurationRecommendation
        {
            Title = "Enable Query Store",
            Steps = GetQueryStoreInstructions(),
            Priority = "Medium",
            EstimatedTime = "2-3 minutes"
        });
    }

    return configStatus;
}
```

#### 2. User Communication Pattern

Following the `NotifyUser` pattern established throughout the codebase:

```csharp
public async Task<PostgreSQLMetrics> GetPostgreSQLMetricsAsync(string resourceId, TimeSpan window)
{
    // Step 1: Always validate configuration first
    var configStatus = await ValidatePostgreSQLConfiguration(resourceId);
    
    // Step 2: Notify user about configuration issues using established pattern
    if (configStatus.Issues.Any())
    {
        await NotifyUserAboutConfigurationIssues(configStatus);
    }
    
    // Step 3: Proceed with limited diagnostics, clearly documenting limitations
    var metrics = await GetAvailableMetrics(resourceId, window, configStatus);
    
    // Step 4: Include configuration guidance in results
    if (configStatus.HasCriticalIssues)
    {
        await NotifyUserAboutLimitedDiagnostics(configStatus);
    }
    
    return metrics;
}

private async Task NotifyUserAboutConfigurationIssues(PostgreSQLConfigurationStatus configStatus)
{
    var message = BuildConfigurationWarningMessage(configStatus);
    await _notifyUserTool.NotifyUser(message);
}
```

#### 3. Structured Messaging Pattern

Following the established formatting patterns from agents like `ContainerAppQuotaAgent`:

```csharp
private string BuildConfigurationWarningMessage(PostgreSQLConfigurationStatus configStatus)
{
    var sb = new StringBuilder();
    
    sb.AppendLine("⚠️ **PostgreSQL Configuration Issues Detected**");
    sb.AppendLine();
    sb.AppendLine("I've detected some configuration issues that limit my diagnostic capabilities:");
    sb.AppendLine();
    
    foreach (var issue in configStatus.Issues)
    {
        sb.AppendLine($"## 🔧 {issue.Issue}");
        sb.AppendLine($"**Impact**: {issue.Impact}");
        sb.AppendLine($"**Severity**: {issue.Severity}");
        sb.AppendLine();
    }
    
    sb.AppendLine("## 📋 Recommended Setup Steps");
    sb.AppendLine();
    
    foreach (var rec in configStatus.Recommendations)
    {
        sb.AppendLine($"### {rec.Title} ({rec.Priority} Priority)");
        sb.AppendLine($"**Estimated Time**: {rec.EstimatedTime}");
        sb.AppendLine();
        sb.AppendLine("**Steps**:");
        foreach (var step in rec.Steps)
        {
            sb.AppendLine($"1. {step}");
        }
        sb.AppendLine();
    }
    
    sb.AppendLine("💡 **I can proceed with limited diagnostics now, but enabling these features will significantly improve my analysis capabilities.**");
    
    return sb.ToString();
}
```

#### 4. Specific Setup Instructions

**Diagnostic Settings Setup**:
```csharp
private List<string> GetDiagnosticSettingsInstructions(string resourceId)
{
    var resourceParts = ParseResourceId(resourceId);
    
    return new List<string>
    {
        "Navigate to Azure Portal → Your PostgreSQL server",
        "Go to 'Monitoring' → 'Diagnostic settings'", 
        "Click 'Add diagnostic setting'",
        "Name: 'PostgreSQLDiagnostics'",
        "Select logs: PostgreSQLLogs, QueryStoreRuntimeStatistics, QueryStoreWaitStatistics",
        "Select metrics: AllMetrics",
        "Destination: Send to Log Analytics workspace",
        $"Workspace: Select or create workspace in {resourceParts.Region}",
        "Click 'Save'"
    };
}
```

**Query Store Setup**:
```csharp
private List<string> GetQueryStoreInstructions()
{
    return new List<string>
    {
        "Connect to your PostgreSQL server using psql or Azure Data Studio",
        "Run: `SELECT name, setting FROM pg_settings WHERE name LIKE 'pg_qs%';`",
        "If Query Store is disabled, enable it:",
        "Execute: `ALTER SYSTEM SET shared_preload_libraries = 'pg_stat_statements,pg_qs';`",
        "Execute: `ALTER SYSTEM SET pg_qs.enable = 1;`",
        "Execute: `ALTER SYSTEM SET pg_qs.track_utility = 'on';`",
        "Restart PostgreSQL server (via Azure Portal)",
        "Verify: `SELECT * FROM query_store.runtime_stats LIMIT 5;`"
    };
}
```

#### 5. Playbook Integration

Following the playbook pattern, create specific guidance for configuration scenarios:

```csharp
[Description("Retrieves setup playbook for PostgreSQL diagnostic configuration")]
public async Task<PlaybookContent> GetSetupPlaybook(
    [Description("The configuration area needing setup: 'diagnostics', 'querystore', 'monitoring'")] string setupArea)
{
    var playbooks = new Dictionary<string, PlaybookContent>
    {
        ["diagnostics"] = new PlaybookContent
        {
            Name = "PostgreSQL Diagnostic Settings Setup",
            Steps = GetDiagnosticSettingsPlaybook(),
            Summary = "Complete guide to enabling Azure diagnostic settings for PostgreSQL monitoring"
        },
        ["querystore"] = new PlaybookContent  
        {
            Name = "PostgreSQL Query Store Setup",
            Steps = GetQueryStorePlaybook(),
            Summary = "Step-by-step guide to enable and configure Query Store for performance monitoring"
        }
    };
    
    return playbooks.GetValueOrDefault(setupArea.ToLower()) ?? 
           throw new ArgumentException($"Unknown setup area: {setupArea}");
}
```

#### 6. Agent YAML Integration

Update the PostgreSQL Agent system prompt to include configuration guidance:

```yaml
system_prompt: |
    ** Configuration Validation **
    - Always validate diagnostic configuration before proceeding with analysis
    - Use NotifyUser to clearly communicate any configuration limitations
    - Provide specific, actionable setup instructions with exact steps
    - Continue with available diagnostics while noting limitations
    - Offer setup playbooks for missing configurations
    
    ** Communication Guidelines **
    - Use professional indicators: ⚠️ for issues, 🔧 for fixes, 💡 for tips
    - Format messages in clear, scannable markdown
    - Provide estimated time for setup steps  
    - Be transparent about diagnostic limitations
```

### Integration Points

- **Tool**: `ValidatePostgreSQLConfiguration` - Comprehensive config validation
- **Tool**: `GetSetupPlaybook` - Detailed setup instructions  
- **Pattern**: `NotifyUser` for all configuration communications
- **Pattern**: Professional formatting with indicators and markdown
- **Pattern**: Specific, time-estimated action items

This approach ensures users are guided toward optimal PostgreSQL diagnostic setup while maintaining full transparency about current limitations.

### Observability

Following existing logging patterns with `LogInternalInformation`:

```csharp
// Metrics to track
_logger.LogInternalInformation($"[postgresql_metrics] Retrieved {metrics.Count} metrics for {resourceId}");
_logger.LogInternalWarning($"[postgresql_connectivity] Connection test failed for {resourceId}: {error}");
_logger.LogInternalError(ex, $"[postgresql_query_analysis] Error analyzing queries for {resourceId}");
```

Standard metrics following existing patterns:
- `postgresql_plugin_tool_calls_total`
- `postgresql_plugin_tool_duration_seconds`
- `postgresql_plugin_errors_total`
- `postgresql_agent_investigations_total`

---

## Testing Strategy

### Unit Tests
Following existing test patterns:
- Plugin method testing with mocked dependencies
- Tool discovery and registration testing
- Data model serialization testing
- Error handling scenario testing

### Integration Tests
- End-to-end agent workflow testing
- Azure Monitor API integration testing
- PostgreSQL connectivity testing (with test instances)
- YAML configuration validation

#### YAML Validation
Validate agent configuration syntax and tool references:
```bash
dotnet test Agent.Runtime.Tests --filter YAMLValidation
```

### Test Structure
```
src/Agent/Agent.Tests.Unit/Plugins/PostgreSQLPluginTests.cs
src/Agent/Agent.Tests.Integration/PostgreSQLAgentIntegrationTests.cs
src/Agent/Agent.Tests.End2End/PostgreSQLAgentE2ETests.cs
```

---

## Success Criteria

### Functional Requirements
- [ ] Agent discoverable through V2 framework
- [ ] All tools properly registered and callable
- [ ] Comprehensive PostgreSQL metrics collection
- [ ] Actionable diagnostic recommendations
- [ ] Playbook-driven troubleshooting workflows

### Quality Requirements
- [ ] Consistent with existing codebase patterns
- [ ] Proper error handling and logging
- [ ] Performance within acceptable bounds
- [ ] Clear and actionable tool descriptions
- [ ] Comprehensive test coverage

### Integration Requirements
- [ ] Seamless handoff to/from meta agent
- [ ] Compatible with existing diagnostic agents (CPU, Memory)
- [ ] Follows established V2 agent conventions
- [ ] Proper dependency injection registration

---

## Forward Compatibility

The V0.1 implementation is designed for easy extension:

### Additional Tools
- New tools can be added to the plugin interface and definition
- YAML configuration updated to include new tools
- No framework changes required

### Enhanced Diagnostics
- Query plan analysis tools
- Index recommendation engines
- Configuration optimization suggestions
- Historical trend analysis

### Multi-Agent Evolution
If needed, the single agent can be split into specialized agents:
- PostgreSQL Query Performance Agent
- PostgreSQL Connectivity Agent  
- PostgreSQL Configuration Agent

The plugin architecture supports this evolution without breaking changes.

---

## Risk Mitigation

### Technical Risks

| Risk | Impact | Mitigation |
|------|---------|------------|
| Azure Monitor API limits | Medium | Rate limiting and caching |
| PostgreSQL connectivity issues | High | Connection pooling and retry logic |
| Tool discovery failures | High | Comprehensive unit tests |
| Performance bottlenecks | Medium | Async/await and timeout controls |

### Operational Risks

| Risk | Impact | Mitigation |
|------|---------|------------|
| False diagnostic results | High | Conservative recommendations and validation |
| Complex tool descriptions | Medium | Clear, concise descriptions following patterns |
| Framework compatibility | Medium | Follow established V2 agent patterns exactly |

---

## Conclusion

The PostgreSQL Intelligence Agent v0.1 provides a solid foundation for PostgreSQL diagnostics within the Agent SRE framework. 
By following established V2 agent patterns and the plugin architecture, this implementation integrates seamlessly with existing infrastructure while providing powerful PostgreSQL diagnostic capabilities.

Key implementation priorities:
1. **Follow Established Patterns**: Use the same Interface/Implementation/Definition structure as existing plugins
2. **V2 Agent Compliance**: Implement as a YAML-configured V2 agent like FunctionAppAgent
3. **Tool Discovery**: Leverage `[AgentToolPlugin]` for automatic tool registration
4. **Framework Integration**: Use existing common tools and handoff mechanisms

This approach ensures consistency with the codebase while delivering immediate value for PostgreSQL diagnostics.

# Agent SRE PostgreSQL Agent v0.1 - Reasoning Design
## V2 Agent Implementation Pattern

This section describes the reasoning approach for the PostgreSQL Diagnostic Agent, implemented as a V2 agent following the established patterns in the Agent SRE codebase.

---

## Architecture Context

Following the V2 agent pattern used by agents like `FunctionAppAgent`:
- **Meta Agent** initiates handoffs based on problem classification
- **PostgreSQL Diagnostic Agent** handles PostgreSQL-specific investigations
- **PostgreSQL Plugin Tools** provide diagnostic capabilities
- **Agent SRE Framework** manages conversation flow and tool execution

---

## Tool Categories

Following the plugin pattern established in the codebase:

### PostgreSQL Metrics Tools
- `GetPostgreSQLMetrics` - Performance metrics from Azure Monitor
- `GetResourceHealth` - Azure resource health status

### Query Analysis Tools
- `AnalyzeSlowQueries` - Query performance analysis
- `CheckPostgreSQLConnectivity` - Connection testing

### Troubleshooting Tools
- `ListAvailablePlaybooks` - Available diagnostic workflows
- `GetPlaybook` - Specific troubleshooting steps

### Framework Tools
- `NotifyUser` - User communication
- `HandoffBack` - Return to meta agent
- `WaitInMilliSeconds` - Timing control

---

## Agent Implementation

### System Prompt Strategy

Following the pattern from `FunctionAppAgent.yaml`:

```yaml
system_prompt: |
    You are **Azure PostgreSQL SRE Agent**. Your specialty is diagnosing and resolving Azure PostgreSQL 
    performance issues, connectivity problems, and configuration errors.
    
    You systematically identify root causes by analyzing metrics, query performance, connection patterns, 
    and configuration settings. You generate actionable reports with specific remediation steps.

    <CORE_RESPONSIBILITY_SCOPE>
    - Diagnose Azure PostgreSQL performance issues including slow queries, high CPU/memory usage, and connection problems
    - Analyze PostgreSQL metrics, logs, and system statistics to identify bottlenecks and optimization opportunities  
    - Provide specific remediation recommendations including index suggestions, query optimization, and configuration tuning
    - Guide users through PostgreSQL troubleshooting workflows using established playbooks
    </CORE_RESPONSIBILITY_SCOPE>

    ** Diagnosis Steps **
    1. Get PostgreSQL metrics to understand current performance baseline
    2. Check connectivity and identify any connection-related issues
    3. Analyze slow queries to identify performance bottlenecks
    4. Review resource health for any Azure-level issues
    5. Use relevant playbooks for systematic troubleshooting
    6. Provide specific recommendations with actionable steps

    ** Guidelines **
    - Always gather metrics first to establish a performance baseline
    - Use playbooks for systematic approach to common issues
    - Provide specific, actionable recommendations with exact commands when possible
    - Consider both database-level and Azure platform-level causes
```

### Example Investigation Flow

#### Investigation Scenario
```
User Report: "PostgreSQL database performance has degraded significantly over the past hour. 
Query response times have increased from ~50ms to 2-3 seconds."
```

#### Agent Reasoning Process

**Step 1: Establish Baseline**
```
THOUGHT: Need to understand current performance state and compare to baseline.

ACTION: GetPostgreSQLMetrics(resourceId: "/subscriptions/.../servers/pg-prod-01", windowMinutes: 60)

OBSERVATION: 
- CPU: 45% (normal range)
- Memory: 78% (elevated)
- Active Connections: 95/100 (near limit)
- Cache Hit Ratio: 62% (below optimal 95%+)
- Average Query Duration: 2.3s (baseline: 50ms)
```

**Step 2: Investigate Connectivity**
```
THOUGHT: High connection count and poor cache hit ratio suggest connection or query issues.

ACTION: CheckPostgreSQLConnectivity(resourceId: "/subscriptions/.../servers/pg-prod-01")

OBSERVATION:
- Connection Test: Successful
- Connection Pool: 95/100 connections active
- Connection Duration: Average 45 minutes (unusually long)
- Issues: Long-running connections holding resources
```

**Step 3: Analyze Query Performance**
```
THOUGHT: Need to identify specific queries causing performance degradation.

ACTION: AnalyzeSlowQueries(resourceId: "/subscriptions/.../servers/pg-prod-01", windowMinutes: 60)

OBSERVATION:
- Top Slow Query: "SELECT * FROM user_activity WHERE event_date >= '2025-06-01'"
- Execution Count: 1,247 times in last hour
- Average Duration: 2.1 seconds
- Execution Plan: Sequential Scan on user_activity (cost=0.00..1,250,000.00)
- Missing Index: event_date column not indexed
```

**Step 4: Get Relevant Playbook**
```
THOUGHT: This appears to be a classic missing index scenario. Let me get the playbook for this.

ACTION: ListAvailablePlaybooks()

OBSERVATION: Available playbooks include "PostgreSQL_Performance_Investigation", "Missing_Index_Analysis", "Connection_Pool_Optimization"

ACTION: GetPlaybook(playbookName: "Missing_Index_Analysis")

OBSERVATION:
Steps:
1. Identify slow queries via pg_stat_statements
2. Analyze execution plans for sequential scans
3. Calculate index impact estimation
4. Recommend concurrent index creation
5. Monitor post-implementation performance
```

**Step 5: Framework Reflection**
```
REFLECTION: Based on the evidence:
- Performance degradation correlates with increased query frequency on unindexed column
- Connection pool saturation due to long-running queries
- Cache hit ratio poor due to excessive I/O from sequential scans
- Clear remediation path available via index creation

CONFIDENCE: High - evidence clearly points to missing index as root cause
```

**Final Response**
```
## Root Cause Analysis

**Primary Issue**: Missing index on `user_activity.event_date` column causing performance degradation

**Evidence Summary**:
- Query frequency increased to 1,247 executions/hour
- Sequential scan execution plan with cost of 1,250,000
- Cache hit ratio dropped to 62% (optimal: 95%+)
- Connection pool near saturation due to slow queries

**Immediate Remediation**:
```sql
CREATE INDEX CONCURRENTLY idx_user_activity_event_date 
ON user_activity(event_date);
```

**Expected Impact**: 
- Query duration: 2.3s → ~50ms (95%+ reduction)
- Cache hit ratio improvement to 95%+
- Connection pool utilization reduction

**Additional Recommendations**:
1. Monitor query performance after index creation
2. Consider connection pooling optimization
3. Review application query patterns for similar issues
```

---

## Framework Integration

### Agent Configuration

**File**: `src/Agent/Agent.Runtime/AgentsV2/PostgreSQLAgent.yaml`

```yaml
name: postgresql_diagnostic_agent

handoff_description: "Handoff to this agent for Azure PostgreSQL diagnostic and performance issues"

handoffs:
  - meta_agent
  - diagnostic_cpu_agent  
  - diagnostic_memory_agent

tools:
  - GetPostgreSQLMetrics
  - CheckPostgreSQLConnectivity
  - AnalyzeSlowQueries
  - GetResourceHealth
  - ListAvailablePlaybooks
  - GetPlaybook
  - NotifyUser
  - HandoffBack
  - WaitInMilliSeconds

max_reflection_count: 2

custom_reflection_note: |
  1. Have I gathered sufficient metrics to understand the performance baseline?
  2. Have I considered both database-level and Azure platform-level causes?
  3. Are my recommendations specific and actionable?
```

### Tool Registration

Tools are automatically registered via the `[AgentToolPlugin]` attribute:

```csharp
[AgentToolPlugin]
public class PostgreSQLPluginDefinition : ContextToolTarget<AgentContext>
{
    [Description("Gets PostgreSQL metrics: CPU, memory, connections")]
    public async Task<PostgreSQLMetrics> GetPostgreSQLMetrics(...)
    {
        var threadId = Context?.ThreadId ?? throw new Exception("Context not set");
        // Implementation uses threadId for tracking and logging
        return await _postgreSQLPlugin.GetPostgreSQLMetricsAsync(...);
    }
    
    [Description("Tests connectivity and identifies connection issues")]
    public async Task<ConnectionTestResult> CheckPostgreSQLConnectivity(...)
    
    // Additional tools...
}
```

### Dependency Injection

Following established patterns:

```csharp
// In Program.cs
builder.Services
    .AddTransient<IPostgreSQLPlugin, PostgreSQLPlugin>()
    .AddTransient<PostgreSQLPluginDefinition>();
```

---

## Framework Benefits

### Automatic Capabilities

The V2 framework provides these capabilities automatically:

1. **Conversation Management**: Context preservation across tool calls
2. **Error Handling**: Standardized error responses and recovery
3. **Tool Discovery**: Automatic registration of available tools
4. **Handoff Management**: Seamless transitions between agents
5. **Reflection Logic**: Built-in reasoning validation via reflection count
6. **Output Formatting**: Consistent response formatting

### Comparison to Custom Implementation

| Aspect | V2 Framework | Custom Implementation |
|--------|-------------|----------------------|
| **Development Time** | Minimal (YAML + plugins) | Extensive (custom reasoning) |
| **Maintenance** | Framework managed | Custom code maintenance |
| **Consistency** | Follows established patterns | Requires custom patterns |
| **Integration** | Seamless | Complex integration work |
| **Tool Discovery** | Automatic | Manual registration |
| **Error Handling** | Standardized | Custom implementation |

---

## Quality Assurance

### Framework Validation

The V2 framework provides built-in quality mechanisms:

1. **Tool Validation**: Automatic parameter validation
2. **Conversation Flow**: Structured conversation management
3. **Error Recovery**: Standard error handling patterns
4. **Performance Monitoring**: Built-in metrics and logging
5. **Reflection Logic**: Configurable reasoning validation

### Testing Strategy

Following existing test patterns:

```csharp
// Unit Tests
[Test]
public async Task PostgreSQLPlugin_GetMetrics_ReturnsValidData()
{
    // Test plugin methods in isolation
}

// Integration Tests  
[Test]
public async Task PostgreSQLAgent_EndToEndWorkflow_SuccessfulDiagnosis()
{
    // Test full agent workflow
}
```

---

## Success Metrics

### Framework Integration
- [ ] Agent automatically discovered by V2 framework
- [ ] All tools properly registered and callable
- [ ] Handoffs work seamlessly with meta agent
- [ ] Standard conversation patterns followed

### Diagnostic Quality
- [ ] Accurate problem identification in test scenarios
- [ ] Actionable recommendations provided
- [ ] Performance metrics properly interpreted
- [ ] Playbook-driven workflows effective

### Code Quality
- [ ] Follows established plugin patterns
- [ ] Proper error handling and logging
- [ ] Comprehensive test coverage
- [ ] Clear and descriptive tool documentation

---

## Conclusion

The PostgreSQL Agent v0.1 leverages the V2 agent framework to provide powerful PostgreSQL diagnostics with minimal custom implementation.
By following established patterns and using the framework's built-in capabilities, this approach delivers:

**Key Advantages**:
1. **Rapid Development**: YAML configuration + plugin pattern
2. **Framework Integration**: Seamless operation with existing infrastructure  
3. **Proven Patterns**: Following successful FunctionApp and ContainerApp agent implementations
4. **Maintainability**: Framework-managed conversation flow and tool discovery
5. **Extensibility**: Easy addition of new tools and capabilities

This design provides a solid foundation for PostgreSQL diagnostics while maintaining consistency with the established Agent SRE architecture.
