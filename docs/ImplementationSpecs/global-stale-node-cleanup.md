# Global Stale Node Cleanup

## Overview

Add a periodic global cleanup pass that soft-deletes stale nodes across the entire graph, not just within currently-crawled scopes. This fixes orphaned nodes that remain when resources are removed from crawl roots.

**Status**: Implemented

---

## Problem Statement

The current stale node cleanup only runs within scopes that are actively being crawled:

```csharp
// SubscriptionCrawler - only runs if subscription IS in crawl roots
await CrawlerExtensions.SoftDeleteStaleNodesWithFilter(_graphDbClient,
    new Dictionary<string, string> { { "subscriptionId", subNode.SubscriptionId } },
    deleteBefore);
```

**When a subscription/resource group is removed from crawl roots:**
1. Crawler no longer visits that scope
2. `SoftDeleteStaleNodesWithFilter` is never called for that scope
3. Nodes remain in the graph forever with `isDeleted = false`

---

## Solution Design

### Generalize SoftDeleteStaleNodesWithFilter

Modify the existing `SoftDeleteStaleNodesWithFilter` to handle empty filter dictionaries. When no filters are provided, the query runs against all nodes in the graph (omitting the `.and()` clause).

**Query behavior:**
- With filters: `g.V().and(has('subscriptionId','X')).has('isDeleted', false).not(__.has('nonCrawled', true)).has('updateTs', P.lt(...))...`
- Without filters (empty dict): `g.V().has('isDeleted', false).not(__.has('nonCrawled', true)).has('updateTs', P.lt(...))...`

**Note:** Adding `.has('isDeleted', false)` avoids re-soft-deleting already-deleted nodes, which would wastefully update their `updateTs`.

### Periodic Global Cleanup

Run a global stale node cleanup every N crawler iterations (e.g., every 10 runs = ~5 hours at 30min intervals).

### Key Differences from Per-Scope Cleanup

| Aspect | Per-Scope Cleanup | Global Cleanup |
|--------|-------------------|----------------|
| When | After crawling each scope | Every N crawler iterations |
| Scope | Single subscription/RG/namespace | Entire graph |
| Filter | `has('subscriptionId', 'X')` | None (empty dict) |
| Threshold | 35 minutes | 6 hours (to avoid false positives) |

### Threshold Considerations

The global cleanup needs a longer threshold because:
- Crawl roots may temporarily fail to crawl (API errors, rate limits)
- Full crawl cycle may take longer than expected
- We want to be conservative to avoid accidental data loss

**Threshold:** `6 hours` (12 crawl cycles at 30min interval)

---

## Implementation Steps

### Step 1: Add Global Stale Threshold Constant

**File**: [`src/Agent/Agent.Graph/Crawler/CrawlerExtensions.cs`](src/Agent/Agent.Graph/Crawler/CrawlerExtensions.cs)

```csharp
public static readonly TimeSpan GlobalStaleThreshold = TimeSpan.FromHours(6);
```

### Step 2: Generalize SoftDeleteStaleNodesWithFilter

**File**: [`src/Agent/Agent.Graph/Crawler/CrawlerExtensions.cs`](src/Agent/Agent.Graph/Crawler/CrawlerExtensions.cs)

Modify to handle empty props using StringBuilder to construct the query incrementally:

```csharp
public static Task SoftDeleteStaleNodesWithFilter(IGraphDatabaseClient client, IDictionary<string, string> props, DateTimeOffset updateTimestamp)
{
    var updateTs = updateTimestamp.Ticks;
    var deleteBeforeWithOffset = updateTimestamp.AddMinutes(-35).Ticks;

    var queryBuilder = new StringBuilder("g.V()");

    // Add scope filter if properties provided
    if (props.Count > 0)
    {
        var filter = string.Join(",", props.Select(kvp => $"has('{kvp.Key}','{kvp.Value}')"));
        queryBuilder.Append($".and({filter})");
    }

    // Common query parts
    queryBuilder.Append($".has('isDeleted', false)");
    queryBuilder.Append($".not(__.has('nonCrawled', true))");
    queryBuilder.Append($".has('updateTs', P.lt({deleteBeforeWithOffset}))");
    queryBuilder.Append($".property('isDeleted', true)");
    queryBuilder.Append($".property('updateTs', {updateTs})");

    return client.Query(queryBuilder.ToString());
}
```

**Note:** The `.has('isDeleted', false)` is added to avoid re-soft-deleting already-deleted nodes.

### Step 3: Add Crawler Iteration Counter and Global Cleanup Call

**File**: [`src/Agent/Agent.Web/Services/TimerService.cs`](src/Agent/Agent.Web/Services/TimerService.cs)

Add iteration counter field:
```csharp
private int _crawlerIterationCount = 0;
private const int GlobalCleanupInterval = 10; // Run every 10 iterations
```

Modify the crawler timer callback to add global cleanup before `DeleteStaleSoftDeletedNodes`:
```csharp
_crawlerIterationCount++;

// Run the full crawl
await _crawlerService.CrawlAsync(roots, cancellationToken: cancellationToken);

// Every N iterations: run global stale node cleanup (soft-delete orphaned nodes)
if (_crawlerIterationCount % GlobalCleanupInterval == 0)
{
    await _crawlerService.GlobalCleanupStaleNodes(cancellationToken);
}

// Always run: cleanup old soft-deleted nodes (hard delete after 3 days)
await _crawlerService.DeleteStaleSoftDeletedNodes(cancellationToken);
```

### Step 4: Add GlobalCleanupStaleNodes Method

**File**: [`src/Agent/Agent.Graph/Services/ResourceGraphCrawlerService.cs`](src/Agent/Agent.Graph/Services/ResourceGraphCrawlerService.cs)

```csharp
public async Task GlobalCleanupStaleNodes(CancellationToken cancellationToken)
{
    try
    {
        _logger.LogInternalInformation("Running global stale node cleanup");
        var deleteBefore = DateTimeOffset.UtcNow - CrawlerExtensions.GlobalStaleThreshold;

        // Use empty props to run cleanup across entire graph
        await CrawlerExtensions.SoftDeleteStaleNodesWithFilter(_graphDbClient, new Dictionary<string, string>(), deleteBefore);

        _logger.LogInternalInformation($"Global cleanup completed - soft-deleted nodes older than {CrawlerExtensions.GlobalStaleThreshold}");
    }
    catch (Exception ex)
    {
        _logger.LogInternalError(ex, "Error during global stale node cleanup");
    }
}
```

### Step 5: Add Interface Method

**File**: [`src/Agent/Agent.Graph/Interfaces/ICrawlerService.cs`](src/Agent/Agent.Graph/Interfaces/ICrawlerService.cs)

```csharp
Task GlobalCleanupStaleNodes(CancellationToken cancellationToken = default);
```

### Step 6: Fix Timer Handling in TimerService

**File**: [`src/Agent/Agent.Web/Services/TimerService.cs`](src/Agent/Agent.Web/Services/TimerService.cs)

Add missing timers to both `StopAsync()` and `Dispose()`. Currently missing:
- `_tlsTimer`
- `_sourceCodeCrawlerTimer`
- `_cveCrawlerTimer`
- `_dailyReportTimer`
- `_scoreCardTimer`
- `_appServiceCrawlerTimer`
- `_pagerDutyWelcomeTimer`
- `_feedbackRCATimer`
- `_trajectoryEvaluatorTimer` (already in StopAsync, missing from Dispose)
- `_githubAccessTokenTimer`
- `_logFlushTimer`
- `_linuxAppServiceConfigScannerTimer` (missing from StopAsync)

**StopAsync:**
```csharp
public Task StopAsync(CancellationToken cancellationToken)
{
    _logger.LogInternalInformation("Stopping background services...");

    _crawlerTimer?.Change(Timeout.Infinite, 0);
    _tlsTimer?.Change(Timeout.Infinite, 0);
    _sourceCodeCrawlerTimer?.Change(Timeout.Infinite, 0);
    _cveCrawlerTimer?.Change(Timeout.Infinite, 0);
    _dailyReportTimer?.Change(Timeout.Infinite, 0);
    _scoreCardTimer?.Change(Timeout.Infinite, 0);
    _appServiceCrawlerTimer?.Change(Timeout.Infinite, 0);
    _pagerDutyWelcomeTimer?.Change(Timeout.Infinite, 0);
    _feedbackRCATimer?.Change(Timeout.Infinite, 0);
    _threadEvaluatorTimer?.Change(Timeout.Infinite, 0);
    _trajectoryEvaluatorTimer?.Change(Timeout.Infinite, 0);
    _githubAccessTokenTimer?.Change(Timeout.Infinite, 0);
    _logFlushTimer?.Change(Timeout.Infinite, 0);
    _localAuthScannerTimer?.Change(Timeout.Infinite, 0);
    _linuxAppServiceConfigScannerTimer?.Change(Timeout.Infinite, 0);
    _scheduledTaskTimer?.Change(Timeout.Infinite, 0);
    _heartbeatTimer?.Change(Timeout.Infinite, 0);

    // Stop all generic timers
    foreach (var scanner in GenericSubAgentScannerTimers)
    {
        scanner.Timer?.Change(Timeout.Infinite, 0);
    }

    return Task.CompletedTask;
}
```

**Dispose:**
```csharp
public void Dispose()
{
    _logger.LogInternalInformation("Disposing Azure Resource Crawler Worker");

    _crawlerTimer?.Dispose();
    _tlsTimer?.Dispose();
    _sourceCodeCrawlerTimer?.Dispose();
    _cveCrawlerTimer?.Dispose();
    _dailyReportTimer?.Dispose();
    _scoreCardTimer?.Dispose();
    _appServiceCrawlerTimer?.Dispose();
    _pagerDutyWelcomeTimer?.Dispose();
    _feedbackRCATimer?.Dispose();
    _threadEvaluatorTimer?.Dispose();
    _trajectoryEvaluatorTimer?.Dispose();
    _githubAccessTokenTimer?.Dispose();
    _logFlushTimer?.Dispose();
    _localAuthScannerTimer?.Dispose();
    _linuxAppServiceConfigScannerTimer?.Dispose();
    _scheduledTaskTimer?.Dispose();
    _heartbeatTimer?.Dispose();

    // Dispose generic timers
    foreach (var scanner in GenericSubAgentScannerTimers)
    {
        scanner.Timer?.Dispose();
    }
}
```

---

## Cleanup Flow Summary

| Step | Frequency | What It Does |
|------|-----------|--------------|
| Per-scope cleanup | Every crawl, per scope | Soft-deletes stale nodes within crawled subscriptions/RGs/namespaces |
| Global cleanup | Every 10 crawls (~5 hours) | Soft-deletes stale nodes across entire graph (catches orphans) |
| Hard delete | Every crawl | Removes nodes with `isDeleted = true` older than 3 days |

---

## Configuration

The `GlobalCleanupInterval` (10 iterations) can be made configurable if needed:

```csharp
public class CrawlerCleanupSettings
{
    public int GlobalCleanupIntervalIterations { get; set; } = 10;
}
```
