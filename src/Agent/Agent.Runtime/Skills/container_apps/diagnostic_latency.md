# Latency Diagnostics

## Overview
Diagnose and analyze application latency across Azure compute platforms, including Azure Container Apps, Azure Web Apps, and Azure Kubernetes Service (AKS). Produce comprehensive latency reports that highlight where time is spent, quantify impact, and recommend next steps. Use structured, repeatable workflows, present clear status updates, and set expectations for runtime when collecting and analyzing data.

## Capabilities
- End-to-end latency analysis for Container Apps, Web Apps, and AKS workloads.
- Aggregated and per-endpoint/method latency profiling (p50/p90/p95/p99).
- Error correlation (HTTP status, exception bursts) with latency degradation.
- Volume-aware interpretation (RPS, sample size, time window) to avoid noise.
- Identification of hotspots: network, DNS, TLS, app code, dependencies, cold starts, scaling, and readiness/liveness impacts.
- Actionable recommendations, validation steps, and follow-up monitoring guidance.

## Workflow
1. Clarify Scope
   - Confirm resource type (Container App, Web App, AKS), environment/namespace, and specific service or deployment.
   - Collect resource identifiers (resource ID for Azure resources; namespace/deployment for AKS).
   - Establish time window (e.g., last 15m, 1h, 24h) and peak vs off-peak context.

2. Set Expectations
   - Inform the user that collection and analysis may take a few minutes.
   - Provide a brief plan with intended data sources and outputs.

3. Collect and Analyze
   - Invoke GetLatencyAnalysis with the resource ID and specified time window.
   - Retrieve aggregated latency (p50/p90/p95/p99), error rates, request rates, and endpoint/method breakdowns.
   - Correlate latency with changes in replicas, deployments/revisions, autoscaling events, and dependency health.

4. Summarize Progress
   - Provide concise updates that include target resource names throughout the workflow.
   - For Container Apps, always render the app name in bold in user-facing summaries.

5. Report and Recommend
   - Present findings with a methods/endpoints table and an executive summary.
   - Call out most-impacted methods, time windows, and likely root causes.
   - Recommend concrete next actions (e.g., profiling, caching, connection pooling, autoscaling threshold tuning, dependency retries, circuit breakers).

6. Validate and Follow Up
   - Describe quick validation checks (e.g., recheck p95 after change, confirm reduced error bursts).
   - Propose monitoring/alerts for regression detection.

## Inputs
- resource_id: Azure resource ID (Container App, Web App) or a resolvable identifier for AKS services.
- time_window: ISO8601 duration or explicit start/end (default: last 1 hour).
- scope filters (optional): specific revision/deployment, namespace, or service name.

## Output Structure
- Executive Summary: One paragraph with key findings and user impact.
- Hotspots: Bullet list of top latency contributors and suspected causes.
- Methods Table: Endpoint/method breakdown with latency/error metrics.
- Correlations: Notable relationships (deployments, scaling, dependency health).
- Recommendations: Ordered actions with expected impact and effort.
- Validation Plan: How to confirm improvements post-change.

### Recommended Methods Table Columns
- method_or_route: string (HTTP method + route/path or RPC method)
- p50_ms: number
- p90_ms: number
- p95_ms: number
- p99_ms: number
- error_rate_percent: number
- rps: number
- sample_size: number
- timeframe: string (e.g., last 60m)
- suspected_cause: string
- notes: string

## Best Practices
- Maintain resource context in every update; include exact resource name(s). For Container Apps, display the app name in bold in summaries.
- Interpret metrics in context of traffic volume; low sample sizes can mislead p95/p99.
- Check both server-side and dependency-side latency (databases, caches, downstream services).
- Correlate with recent changes (code, config, scaling, network policies, certificates).
- Distinguish cold starts, image pull delays, and probe-related restarts from steady-state latency.
- Investigate ingress/egress and DNS/TLS handshake times when tail latencies spike without code changes.
- Validate improvements by comparing before/after metrics over the same traffic patterns.
- Favor small, reversible changes; roll out gradually and monitor.

## Common Diagnostic Angles
- Compute/Scaling: Insufficient replicas, throttling, or sudden scale-in causing queue buildup.
- Network: High egress latency, SNAT exhaustion, DNS resolution delays, TLS renegotiation.
- Application: Synchronous I/O, blocking calls, GC pauses, lock contention, N+1 queries.
- Dependencies: Database slow queries, saturated caches, third-party API slowness, connection pool exhaustion.
- Platform Events: New deployment/revision, configuration changes, autoscaler thresholds, readiness/liveness failures.

## Example: Container App Latency Report
Plan:
- Confirm target resource and time window.
- Run GetLatencyAnalysis for the resource ID (last 60m).
- Summarize p95/p99 latency and top impacted routes.
- Correlate with revisions and replica counts.
- Recommend actions and validation steps.

Executive Summary:
- Target: Bold app name as required. Example: Experience elevated tail latency for **aca-orders-api** over the last 60m, with p95 at 820 ms (+65% vs baseline) during traffic spikes between 10:05–10:20 UTC.

Methods (top 5 by p95):
| method_or_route | p50_ms | p90_ms | p95_ms | p99_ms | error_rate_percent | rps | sample_size | timeframe | suspected_cause | notes |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- | --- |
| GET /orders/{id} | 120 | 540 | 780 | 1600 | 1.2 | 22 | 79200 | last 60m | DB query latency | Spikes align with DB CPU >80% |
| POST /orders | 180 | 620 | 900 | 1800 | 2.8 | 9 | 32400 | last 60m | Write contention | Elevated lock waits |
| GET /healthz | 15 | 20 | 25 | 40 | 0.0 | 1 | 3600 | last 60m | None | Baseline |
| GET /inventory/{sku} | 95 | 420 | 610 | 1300 | 0.9 | 14 | 50400 | last 60m | Downstream API | 429/5xx bursts at 10:12 |
| GET /search | 150 | 520 | 800 | 1700 | 1.6 | 7 | 25200 | last 60m | GC pauses | GC pause p95 180 ms |

Correlations:
- Latency spikes coincide with scale-in from 6→3 replicas at 10:02 UTC and DB CPU >80%.
- Downstream inventory API returned intermittent 5xx with elevated response times.

Recommendations:
- Increase min replicas from 3→5 during peak hours; adjust scale-in cooldown to 20m.
- Add index for orders lookup; review query plan and connection pool max size.
- Implement client-side retry with backoff for inventory API; add circuit breaker at 5xx >2%.
- Tune GC (server GC, heap limits) and remove synchronous blocking in search path.

Validation Plan:
- Reassess p95/p99 and error rates 30–60m post changes under comparable load.
- Add alert: p95 > 700 ms for 10m and error rate > 2% for any critical route.

## Example: Web App Latency Snapshot
- Scope: /checkout and /payment routes, last 30m.
- Findings: p95 rose to 650 ms; 80% of additional latency from downstream payment gateway; TLS handshake time increased.
- Actions: Enable connection reuse, reduce DNS TTL to mitigate failover delays, pre-warm instances during marketing campaigns.

## Example: AKS Service Latency
- Scope: Namespace payments, Deployment payments-api, last 2h.
- Findings: p99 spikes linked to HPA oscillation and node-level CPU throttling.
- Actions: Raise HPA min replicas, add PodDisruptionBudget, provision nodes with higher CPU, add request/limit headroom to reduce throttling.

## Validation Checks
- After each data retrieval, add a short confirmation:
  - “Validated: GetLatencyAnalysis returned 5 routes, sample_size=78k over last 60m; metrics within expected ranges.”
  - If missing or inconsistent, request a longer window or verify resource ID.

## Notes on Presentation
- Keep summaries concise and include the target resource name. For Container Apps, always show the app name in bold.
- Use tables for method-level detail and add concise notes per row.
- When structured output improves clarity, include an explicit “Output Format” block and adhere to it.
