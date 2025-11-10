# Azure Cache for Redis SRE Skill

## Overview
Efficiently diagnose Azure Cache for Redis performance and reliability issues (latency spikes, timeouts, memory pressure, evictions, low hit ratio, connection churn, suspected network path problems). Provide concise, evidence-based remediation and validate improvements with before/after metrics. Follow the system prompt hierarchy for safety, accuracy, conciseness, efficiency.

## Quick Triage Checklist (run before deep dive)

1. Identify target cache: name, SKU/tier, size, region, clustering/shard count, persistence, replication, TLS.
2. Collect last 30–60 min metrics: Server Load, Connected Clients, Command Latency (P50/P95/P99), Hit/Miss Ratio, Memory Usage, Evictions.
3. Plot time series for each; bar chart for comparative command latency if needed.
4. Correlate anomalies (latency vs load, latency vs connection spikes, evictions vs memory pressure).
5. Classify primary issue domain: Load | Connections | Latency/Network | Memory/Evictions.
6. Formulate immediate mitigations + longer-term optimization; define threshold triggers.
7. Plan validation (which metrics, expected movement, time window).

## Tool Usage Conventions (internal)

- Before a tool call: single-line purpose + key inputs (no tool names in user-facing text).
- After each call: 1–2 line validation (“Data retrieved; latency P95 elevated”) then next action.
- Avoid consecutive tool-only responses—always interpret.
- Missing required parameter → ask targeted question; never guess.

Example purpose line (internal pattern): “Purpose: Retrieve cache metadata and status; Inputs: resourceId, timespan=30m.”

## Analysis Domains & Actions

### 1. Initial Assessment

Use GetRedisCacheInfo to retrieve configuration (SKU, size, shards, persistence, clustering, policies). Note any settings influencing latency or memory (maxmemory-policy, client-output-buffer-limits, latency-monitor). Check for recent failover/maintenance events.

### 2. Server Load

Metric: Server Load (last 30m).
Thresholds: >80% sustained → probable capacity bottleneck; correlates with rising latency/timeouts.
Actions: Reduce expensive commands (large multi-key, blocking Lua); enable batching/pipelining; scale up (larger SKU) or out (add shards). Re-check load + latency post-change.

### 3. Connections

Metrics: Connected Clients count, connection creation rate.
Indicators: Spikes aligned with latency; high churn (frequent create/drop) → overhead; near maxclients.
Actions: Implement/tune pooling; avoid per-request connections; adjust idle/timeouts; verify client library configuration (StackExchange.Redis, Lettuce, Jedis) for retry/backoff.

### 4. Latency & Timeouts

Metrics: Command latency percentiles (P50/P95/P99) + Hit/Miss Ratio.
Patterns: Elevated latency without load → network/DNS path; high miss ratio → backend pressure amplifying latency.
Actions: Region co-location; resolve VNet peering/firewall/DNS issues; optimize command mix (reduce large SCAN/SORT; leverage pipelining); address slow scripts or large key operations; validate percentile improvement.

### 5. Memory & Evictions

Metrics: Memory usage vs maxmemory, fragmentation ratio, eviction rate/policy.
Indicators: Approaching maxmemory; frequent evictions; large or hot keys; fragmentation growth.
Actions: Scale size or add shards; optimize data structures (favor hashes/lists over large blobs); review TTL coverage; ensure eviction policy suits pattern (e.g., allkeys-lfu for skewed access); identify and shard hot keys; re-measure fragmentation and eviction trend.

### 6. Network Path (Latency without Load)

Check regional proximity, VNet routing, firewall, NSGs, DNS resolution, cross-region calls, client-side settings (sync-over-async issues, Nagle). If confirmed network path issue, recommend co-location or path optimization.

### 7. No-Tool Diagnostic Questions (use when data missing or tools unavailable)

- Start time of issue & change history (deployments, config, traffic).
- Error specifics: timeouts vs slowness vs connection drops.
- Workload shape (read-heavy, write-heavy, mixed spikes, batch jobs).
- Frequent commands (to spot costly patterns).
- Connection pooling usage & library versions.

## Problem Categories (Condensed)

| Category | Key Signals | Primary Remedies | Validation Metric |
|----------|-------------|------------------|-------------------|
| Load | >80% sustained; latency rise | Optimize commands; scale up/out | Load <60%; latency P95 down |
| Connections | Client spikes, churn | Implement pooling; tune timeouts | Stable connection count |
| Memory | Near maxmemory; evictions | Scale; optimize structures; TTLs | Lower eviction rate |
| Evictions | Frequent evictions | Policy alignment; shard; TTL coverage | Eviction rate decline |
| Latency (Net) | High latency, normal load | Region alignment; network path fixes | Latency percentiles drop |
| Miss Ratio | High miss ratio | Improve caching strategy; warm critical keys | Increased hit ratio |


## Threshold Reference

- Server Load: >80% sustained → investigate; goal <60% peak after remediation.
- Latency P95: >10 ms for simple GET/SET workloads merits analysis; target <5 ms typical.
- Hit Ratio: <80% for intended caching scenario → optimization opportunity.
- Evictions: Continuous evictions + rising miss ratio → capacity or TTL/policy misalignment.
- Fragmentation ratio (>1.5) sustained → memory efficiency review.

## Data Presentation

- Time series for: Load, Connected Clients, Latency P50/P95/P99, Memory Usage, Evictions.
- Before/after comparison when remediation applied (same window length).
- Bar chart: command latency by command group (optional).
- Always annotate chart intervals where thresholds exceeded.
- Tie each recommendation directly to observed metric + threshold.

## Recommendation Structure

Immediate (mitigation) → Near-term (scaling/tuning) → Validation (specific metric change + expected target).
Example Interpretation: “Server Load 88–92% (10:05–10:27 UTC) with P95 latency jump 3 ms → 40 ms and client spike → capacity saturation.”

## Error Handling (Condensed)

On tool failure: interpret cause (not found, permission, timeout, transient). Provide user-friendly explanation + next step (retry narrowed query, ask for missing parameter, escalate after 2 identical failures). Never expose raw error text. Stop after 2 consecutive failures of same operation and summarize.

## Workflow Recap

Triage → Identify dominant domain → Deep-dive domain metrics → Recommend & execute mitigations (if safe) → Validate with metrics → Conclude or iterate.

## Example Quick Flow

1. GetRedisCacheInfo (config baseline).
2. GetRedisServerLoad + plot; note >80% segments.
3. GetRedisConnectedClients; correlate spikes with latency.
4. GetRedisCommandLatency + Hit/Miss Ratio; identify network vs workload.
5. GetRedisMemoryUsage + GetRedisEvictionMetrics; check headroom & policy impact.
6. Form recommendations (e.g., enable pooling; scale SKU; adjust eviction policy; shard hot keys) + validation plan.

## Example Purpose Line

“Purpose: Analyze Server Load last 30m for bottlenecks; Inputs: resourceId, timespan=30m.”

## Validation Targets (Illustrative)

- Post-scaling: Server Load peak <60% & P95 latency <5 ms over next 24h window.
- After pooling: Connection count stable ±10% across peak hour.
- After eviction/policy tuning: Eviction rate reduced; hit ratio improved >85%.

## Completion Criteria

Issue domain identified, recommendations delivered, and validation plan stated OR validated improvement metrics collected. If metrics contradict expectations, iterate with next most likely domain.

No additional markdown files are referenced by this skill; all guidance is contained here.
