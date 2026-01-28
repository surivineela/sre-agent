# Usage Analysis KQL Queries

This folder contains KQL queries for analyzing SRE Agent usage metrics, focusing on token usage duration and consumption patterns.

## Prerequisites: Deploy Functions First

Before running queries, deploy the stored functions:

```bash
# 1. Deploy global functions (SREAgentStoredFunctions)
#    - UniqueFirstPartyAgents(), UniqueThirdPartyAgents(), TotalTokensByWeek(), AgentMetadata()

# 2. Deploy usage-analysis functions (functions/deploy-functions.kql)
#    - ModelResponseEvents(), ThreadSourcePercentiles(), TokenMetrics()
```

## Function Architecture

```
Level 0: All(), ExtendedAgentDocumentDBState()     <- Existing in database
           │
Level 1: SREAgentStoredFunctions/                   <- Global (peer folder)
           │  ├── AgentMetadata()
           │  ├── ModelResponseEvents()
           │  ├── ThreadSourcePercentiles()        <- Joins CreateThread for ThreadSource
           │  ├── ThreadSourceUsageTrend()         <- Joins CreateThread for ThreadSource
           │  ├── TokenMetrics()
           │  ├── TokenTrend()
           │  ├── PercentileSummary()
           │  ├── UniqueFirstPartyAgents()
           │  ├── UniqueThirdPartyAgents()
           │  └── TotalTokensByWeek()
```

**IMPORTANT:** Functions are NOT YET deployed to Kusto. Query files inline the
function logic until deployment. See `SREAgentStoredFunctions/deploy-functions.kql`
for the canonical function definitions.

### ModelResponseEvents(StartDate, EndDate)
Enriched `GenerateModelResponse` events with agent metadata.
**NOTE:** ThreadSource is NOT on this table - must join with CreateThread events.

**Returns:** PreciseTimeStamp, AgentName, ThreadId, Duration, CustomerCategory, OfferType, ServiceGroupName, CustomerName, InputToken, OutputToken, CachedToken

### ThreadSourcePercentiles(StartDate, EndDate, Category)
P50/P90 duration percentiles by ThreadSource per day. 
**Handles ThreadSource join internally** - joins CreateThread events to get ThreadSource.

**Returns:** Day, ThreadSource, P50, P90, ThreadCount

### ThreadSourceUsageTrend(StartDate, EndDate, Category)
Total minutes by ThreadSource per day.
**Handles ThreadSource join internally** - joins CreateThread events to get ThreadSource.

**Returns:** Day, ThreadSource, TotalMinutes, ThreadCount, AgentCount

### TokenMetrics(StartDate, EndDate, Category, ByDay)
Token consumption aggregated by customer category.
- `Category`: "FirstParty" or "ThirdParty"
- `ByDay`: true for daily breakdown, false for period totals

**Returns:** Day, TotalInputTokens, TotalOutputTokens, TotalCachedTokens, TotalDurationMinutes, CallCount, AgentCount, TotalTokens, CacheRate, AvgTokensPerCall

## Query Files

| File | Description | Dashboard Use |
|------|-------------|---------------|
| `00-base-usage-metrics.kql` | Base query for total duration per agent per day with percentile analysis | Fleet-wide metrics |
| `01-usage-by-thread-source.kql` | Usage breakdown by thread source (Conversation, Incident, ScheduledTask, etc.) | Legacy (use 09 instead) |
| `02-usage-by-customer-type.kql` | Usage analysis segmented by Internal vs External customers | Chart 1 (both dashboards) |
| `03-top-customers-by-usage.kql` | Top N customers by total token usage duration | Chart 4 (3P dashboard) |
| `04-usage-trends-by-thread-source.kql` | Weekly usage trends with week-over-week changes | Optional analysis |
| `05-token-usage-metrics.kql` | Detailed token consumption (Input, Output, Cached, Reasoning) | Legacy (use 11 instead) |
| `06-1p-3p-percentiles.kql` | 1P vs 3P agent P50/P90 percentiles by day | Summary cards, Chart 5 |
| `07-top-agents-by-usage.kql` | Top N agents with IsInternal flag | Chart 2 (filter by type) |
| `08-customer-type-percentiles.kql` | Internal/External P50/P90 by day | Legacy (use 06 instead) |
| `09-thread-source-by-customer-type.kql` | Thread source breakdown split by 1P/3P | Chart 3 (both dashboards) |
| `10-top-1p-service-groups.kql` | Top 1P service groups (by resourceGroup) | Chart 4 (1P dashboard) |
| `11-token-metrics-by-customer-type.kql` | Token consumption split by 1P/3P | Summary cards (both dashboards) |

## Thread Source Values

The `ThreadSource` field indicates the origin of agent interactions:

| ThreadSource | Description |
|--------------|-------------|
| `Conversation` | Regular chat conversations (default) |
| `Incident` | ICM, PagerDuty, AzMonitor, ServiceNow incidents |
| `ScheduledTask` | Scheduled task executions |
| `Agent` | Proactive agent-created threads (e.g., daily reports) |
| `Alert` | Alert/webhook triggered threads |
| `Teams` | Teams channel/chat interactions |
| `DailyReport` | Daily report scans |
| `Portal` | Legacy portal conversations |
| `Playground` | Configuration playground threads |
| `WelcomeMessage` | Welcome message threads |
| `BestPractices` | Best practices/security recommendations |

## Customer Type Classification

Customer type is determined via Product360CustomerSubscriptions:
- **Internal**: `OfferType` contains "Internal"
- **External**: All other subscription types
- **Unknown**: Subscriptions not found in Product360

## Data Sources

- **Primary**: `AgentActionEvents` - Contains duration, token counts, and thread metadata
- **Customer Mapping**: `AgentDocumentDBState` - Links agent names to subscriptions
- **Customer Info**: `Product360CustomerSubscriptions` (customerdomrptwus3prod cluster) - CloudCustomerName, TPID, OfferType

## Usage Notes

1. All queries use parameterized dates via `declare query_parameters`
2. All queries use `All('TableName')` syntax for cross-cluster access
3. Commented sections provide alternative views (uncomment as needed)
4. Duration is in milliseconds; queries convert to minutes for readability
5. Token fields: `InputToken`, `OutputToken`, `CachedToken`, `ReasoningToken`

## Query Parameters

All queries support the following parameters with defaults:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `StartDate` | datetime | `ago(30d)` | Start of analysis period |
| `EndDate` | datetime | `now()` | End of analysis period |
| `TopN` | int | `25` | Number of top customers (03-top-customers-by-usage.kql only) |

### How to Pass Parameters

**Kusto Explorer / Azure Data Explorer:**
```kql
// Example: Last 7 days
.execute script <|
declare query_parameters(StartDate:datetime = ago(7d), EndDate:datetime = now());
// ... rest of query
```

**REST API / SDK:**
```json
{
  "parameters": {
    "StartDate": "2026-01-01T00:00:00Z",
    "EndDate": "2026-01-27T00:00:00Z"
  }
}
```

**Command-line (Kusto.Cli):**
```bash
kusto query -db sreagent -cluster "https://sreagent-sec.swedencentral.kusto.windows.net" \
  -script "00-base-usage-metrics.kql" \
  -parameter "StartDate=datetime(2026-01-01)" \
  -parameter "EndDate=datetime(2026-01-27)"
```

## Usage Metrics Explained

- **Duration**: Time spent on `GenerateModelResponse` action (LLM inference time)
- **TotalDurationMinutes**: Sum of all inference durations in minutes
- **Token counts**: Actual token consumption metrics
- **CacheHitRate**: Percentage of input tokens served from cache
