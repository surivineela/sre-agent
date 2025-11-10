# Azure Metrics Analysis & Visualization Skill

## Purpose

Provide focused guidance for analyzing Azure Monitor metrics: discover available metrics (efficiently), retrieve time‑series data, visualize patterns, and produce clear, actionable performance or capacity recommendations.

## Scope

In‑scope: metric discovery (once per resource type), time‑series / dimensional analysis, cross‑resource or period comparisons, trend & anomaly identification, chart generation (time‑series, bar, pie, scatter, heatmap, correlation), optimization and sizing recommendations.

Out of scope: configuration changes, remediation workflows, deep troubleshooting beyond what metric signals alone justify (use other diagnostic skills when needed).

## Core Workflow (internal – do not mention tool names to user)

1. Resolve resource IDs (system prompt discovery flow) and determine the analysis window.
2. If not already done for the resource type, list available metrics once; cache names & dimensions.
3. Select relevant metrics (utilization, saturation, latency, error rates) + appropriate aggregation (Avg, Max, P95, Sum, Count).
4. Query time‑series with necessary dimensional filters / splits (minimal calls, consolidate where possible).
5. Generate the most informative visualization(s) for the question (one primary + optional supporting chart).
6. Interpret patterns: baseline, peaks, trends, anomalies, correlations.
7. Produce concise recommendations (right‑size, scaling policy tuning, caching, index or capacity adjustments) tied directly to observed data.

## Key Principles

• ListAvailableMetrics only once per resource type; reuse results.
• Avoid echoing every intermediate step—follow system prompt conciseness (direct answer first).
• Only ask user for missing essentials (time range, resource scope) after exhausting inference.
• Don’t mention tool names in user‑visible output.

## Metric Selection & Query Guidelines

• Time range: choose smallest window that still captures pattern (e.g., last 24h for recent spikes, 7d for weekly seasonality, 30–90d for capacity planning).
• Aggregation: use P95/P99 for latency sensitivity; Max for saturation; Average or Sum for sustained load; Count for event volume.
• Dimensions: include only those adding discrimination (statusCode, instanceId, operationName). Split by wildcard when comparative series are needed.
• Interval: match metric granularity (do not downsample below native precision unnecessarily).
• Validate response cardinality (too many series → narrow filters; zero series → adjust time range or metric name).

## Visualization Selection

| Goal | Preferred Chart | Notes |
|------|-----------------|-------|
| Trend / seasonality | Time‑series | Add rolling avg for noisy data |
| Distribution share | Pie (sparingly) / Bar | Prefer bar > pie for >5 categories |
| Category comparison | Bar | Sort by value descending |
| Relationship / correlation | Scatter | Include regression line if clear pattern |
| Density / hot spots | Heatmap | Use consistent color scale across comparisons |

## Dimension & Filtering Best Practices

• AND combine filters for precision; use wildcard only when comparative breakout needed.
• Inspect available dimensions before constructing filters; avoid guessing values.
• Explicitly label units (MB, %, ms) in narrative and axes.
• Use heatmap or scatter to validate correlations before recommending action.

## Recommendations Crafting

Tie each recommendation to a concrete observation:
• “Sustained CPU >70% during business hours → consider scale‑out before peak window.”
• “Latency P95 rising week‑over‑week while volume steady → investigate downstream dependency or index fragmentation.”
• “Memory steady with brief spikes only during deployment → no resize needed.”

## Response Pattern

1. Direct answer (1–2 sentences summarizing key finding or delivered visualization).
2. Supporting data (concise metrics table / bullet insights; embed chart descriptions, not raw tool names).
3. Actionable recommendations (only if user’s intent involves optimization / sizing).
4. Next clarification ONLY if essential data missing.

## Validation (internal)

After each metric or chart retrieval: silently check for empty / anomalous results; surface to user only when it changes interpretation (“No data for metric X in last 24h – adjust range?”).

## Examples (internal workflow illustrations)

• VM Fleet Capacity (90d): list metrics once (VM type), pull CPU % + Memory % for all, plot time‑series for representative high / low utilization VMs, recommend rightsizing and scale thresholds.
• Web App Latency Spike (24h): fetch Http5xx + Requests + Latency P95, show time‑series overlay (error vs latency), highlight correlation window, recommend investigation of downstream DB or scaling.
• SQL DTU Pattern (14d): heatmap (hour vs day) to expose peak hours; bar chart comparing average DTU by database; recommend scale adjustment or index tuning for consistently high DTU outliers.

## Output Hygiene

• Avoid long procedural narratives.
• Summarize charts (“CPU utilization shows sustained 65–78% with Monday peak 85%”).
• Limit tables to essential metrics (≤6 columns, ≤10 rows) unless user explicitly requests full detail.

## Edge Cases

• Zero data returned: verify metric name + time range; fallback to broader range.
• Excessive series (>25): narrow dimensions or focus on top N by value.
• Highly volatile metric: apply rolling average description rather than raw jitter.
• Conflicting signals (high CPU, low throughput): note discrepancy and propose follow‑up (e.g., thread starvation, throttling) without speculation.
