# Analysis

This folder contains scripts for analyzing SRE Agent telemetry and generating insights. Any queries used for key metrics should be checked in here and changes should be peer-reviewed.

Also include prompts for GitHub Copilot to interpret query results.

## Tips for working with these scripts in VS Code

[Kusto Notebooks](https://microsoftit.visualstudio.com/OneITVSO/_wiki/wikis/OneITVSO.wiki/72891/Using-Kusto-Notebooks-in-VSCode) allow you to execute KQL scripts interactively, render charts, and reference table structures.

Note: executing Kusto queries from requires a VPN connection.

If Azure authentication isn't working for you, in Settings, set `microsoft-authentication.implementation` to `msal-no-broker`.

## Scripts

### churn.kql

Identifies agents and customers that have "churned" by analyzing agent deletion patterns.

### IntentMet.kql

Calculates the average intent met score based on thread resolution scores, split by first party and third party customers.

### KV-Cache-Rate.kql

Calculates the proportion of LLM token requests served from the key-value (KV) cache rather than recomputed from scratch, indicating how effectively prior attention states are being reused.

### Task-Success-Rate.kql

Calculates the task success rate for SRE Agent thread evaluations, split by first party and third party customers.

### Total-Sync-Async-Threads.kql

Calculates total number of threads and the percentage of human-initiated (synchronous) vs agent-initiated (asynchronous) threads for both first party and third party SRE Agents.

### TotalTokens.kql

Calculates the total tokens (in billions) consumed by first party and third party SRE Agents.

### TTFT-Latency.kql

Calculates the average Time to First Response (TTFT) for SRE Agent threads, with percentile breakdowns for first party and third party customers.

### UniqueAgents-FirstParty.kql

Reports the number of unique agents, subscriptions, and service groups belonging to first party customers over time.

### UniqueAgents-ThirdParty.kql

Reports the number of unique agents, subscriptions, and customers belonging to third party customers over time.

## Dashboards

[Incident Metrics - SRE Agent Dashboard - Power BI](https://msit.powerbi.com/groups/4b1d49cf-e1b8-44d4-a9d0-c4ff48dab1b0/reports/e7ff65c1-472b-4fcc-afdb-085ce43b4b6e/d577d1c0c00e218b0705?experience=power-bi)
