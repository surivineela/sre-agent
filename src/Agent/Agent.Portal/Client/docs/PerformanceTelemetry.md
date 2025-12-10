# Performance Telemetry

Performance telemetry events are logged to track iframe load times and key milestones. These events are only logged when running in iframe mode (not standalone).

## Event Timeline

Events are logged in the following order during a typical page load:

| Order | Event | Source | Description |
|-------|-------|--------|-------------|
| 1 | `agent iframe view loaded` | Agent.Portal | Portal component mounted, about to render iframe |
| 2 | `iframe load` | Agent.Web (iframe) | Iframe JS initialized, `readyForData` message sent to host |
| 3 | `iframe readyfordata received` | Agent.Portal | Portal received `readyForData`, about to send environment info |
| 4 | `iframe threads loaded` | Agent.Web (iframe) | Initial threads (favorite + regular) fetched from API |

## Event Schema

All events use `actionModifier: 'performance'` and include:

```typescript
{
  action: '<event name>',
  actionModifier: 'performance',
  data: {
    immediateTimestamp: number,  // Date.now() at time of logging
    // Only for 'iframe load' event:
    iframePerf?: {
      startTime: number,      // Navigation start time
      loadEventEnd: number,   // When load event completed
      duration: number,       // Total navigation duration
    }
  }
}
```

## Kusto Queries

**View all performance events for a session:**

```kusto
SREAgentTelemetry
| where actionModifier == "performance"
| where timestamp > ago(1d)
| project timestamp, action, data=tostring(customDimensions.data)
| order by timestamp asc
```

**Calculate time between portal mount and threads loaded:**

```kusto
SREAgentTelemetry
| where actionModifier == "performance"
| where timestamp > ago(1d)
| extend immediateTimestamp = tolong(parse_json(tostring(customDimensions.data)).immediateTimestamp)
| summarize
    portalMountTime = minif(immediateTimestamp, action == "agent iframe view loaded"),
    threadsLoadedTime = maxif(immediateTimestamp, action == "iframe threads loaded")
    by bin(timestamp, 1m), sessionId=tostring(customDimensions.sessionId)
| extend totalLoadTimeMs = threadsLoadedTime - portalMountTime
| where isnotnull(totalLoadTimeMs)
| summarize avg(totalLoadTimeMs), percentile(totalLoadTimeMs, 50), percentile(totalLoadTimeMs, 95)
```

**Iframe navigation timing analysis:**

```kusto
SREAgentTelemetry
| where action == "iframe load" and actionModifier == "performance"
| where timestamp > ago(1d)
| extend iframePerf = parse_json(tostring(customDimensions.data)).iframePerf
| extend duration = toreal(iframePerf.duration)
| where isnotnull(duration)
| summarize
    avgDuration = avg(duration),
    p50 = percentile(duration, 50),
    p95 = percentile(duration, 95),
    count = count()
    by bin(timestamp, 1h)
| order by timestamp asc
```
