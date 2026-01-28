# SRE Agent Stored Functions

Global reusable Kusto stored functions for SRE Agent analytics.

## Cluster
```
https://sreagent-sec.swedencentral.kusto.windows.net/sreagent
```

## Deployment
Run `deploy-functions.kql` in the Kusto database to create/update these functions.

## ⚠️ IMPORTANT: Baked-in Business Rules

These functions have the following rules baked in. **Do not modify or override these.**

### Excluded Agents (1P)
The following agents are excluded from ALL 1P metrics:
- `saziz-115--59688f2c` (SRE Agent team testing)

Applied in: `AgentMetadata()`, `UniqueFirstPartyAgents()`

### Thread Source Filtering
Only these 3 sources are included in thread source metrics:
- `Incident`
- `Conversation`
- `ScheduledTask`

Ignored: DailyReport, BestPractices, WelcomeMessage, Unknown

Applied in: `ThreadSourcePercentiles()`, `ThreadSourceUsageTrend()`

### Thread Source Join Strategy
Uses **INNER join** (not leftouter) to exclude threads without a known source.

## Functions

### AgentMetadata()
Base agent metadata from `ExtendedAgentDocumentDBState()`. Returns:
- `AgentName`, `CustomerCategory`, `OfferType`, `ServiceGroupName`, `CustomerName`, `SubscriptionId`

**Excludes:** `saziz-115--59688f2c` from 1P

Use this to join with event tables for enrichment.

### UniqueFirstPartyAgents(StartDate, EndDate)
Weekly unique 1P agent counts. Returns:
- `CustomWeek`, `UniqueAgents`, `UniqueSubscriptions`, `UniqueServiceGroups`

Based on: `analysis/UniqueAgents-FirstParty.kql`

### UniqueThirdPartyAgents(StartDate, EndDate)
Weekly unique 3P agent counts. Returns:
- `CustomWeek`, `UniqueAgents`, `UniqueSubscriptions`, `UniqueCustomers`

Based on: `analysis/UniqueAgents-ThirdParty.kql`

### TotalTokensByWeek(StartDate, EndDate)
Weekly token consumption with cost. Returns:
- `CustomWeek`, `TotalCombinedTokensBillions`, `FirstPartyTokensBillions`, `ThirdPartyTokensBillions`
- `CacheHitRate`, `TotalCostPerToken`, `RequestCount`

Based on: `analysis/TotalTokens.kql`

## Usage Example
```kql
// Get unique 1P agents for last 90 days
UniqueFirstPartyAgents(ago(90d), now())

// Get token consumption for Q4
TotalTokensByWeek(datetime(2025-10-01), datetime(2026-01-01))

// Get agent metadata for joins
AgentMetadata()
| where CustomerCategory == "FirstParty"
```

## Relationship to Report Functions
```
Level 0: All(), ExtendedAgentDocumentDBState()  <- Existing in database
           │
Level 1: SREAgentStoredFunctions/               <- THIS FOLDER (global)
           │  ├── AgentMetadata()
           │  ├── UniqueFirstPartyAgents()
           │  ├── UniqueThirdPartyAgents()
           │  └── TotalTokensByWeek()
           │
Level 2: usage-analysis/functions/              <- Report-specific
              ├── ModelResponseEvents()
              ├── ThreadEvents()
              └── TokenMetrics()
```
