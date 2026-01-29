---
name: container_apps
description: |
  Skill for Azure Container Apps operations: status, configuration, revisions, deployments, logs, metrics, health checks, scaling, HTTP error diagnosis, latency analysis, change propagation, and incident tracking. Handles standard operational queries directly and defers only truly out-of-scope platform or cross-service scenarios to appropriate top-level skills.
tools:
  - GetLatestRevision
  - ListContainerApps
  - GetContainerAppInfo
  - ListRevisions
  - GetContainerAppRequestMetrics
  - GetContainerAppMemoryMetrics
  - GetContainerAppCpuMetrics
  - GetRevisionLogs
  - GetContainerAppLogs
  - ScaleContainerApp
  - GetImageReferenceFromResourceId
  - VerifyExternalRegistry
  - ValidateContainerAppHealth
  - GetDeploymentTimes
  - CloseAzureMonitorAlert
  - RollbackToLastKnownWorkingRevision
  - RestartContainerApp
  - RemoveNSGRule
  - UpdateTargetPort
  - UpdateContainerImage
  - GetConnectedResources
  - GetLatencyAnalysis
  - ModifyContainerAppScaleRule
  - ListAvailableScalers
  - GetScalerDetails
  - CreateGithubIssue
  - FetchGithubIssue
  - FindConnectedGitHubRepo
  - GetIaCForGitHub
  - DisconnectRepositoryFromResourceForGitHub
  - FetchGithubIssuesLimited
---

# Azure Container Apps Operations Skill

## Purpose

Operate and troubleshoot Azure Container Apps: status, configuration, revisions, deployments, logs, metrics, health, scaling, HTTP errors, latency, incident tracking, and change propagation. Deliver concise, actionable answers. Use progressive discovery: start with this file; open a referenced detailed file only when its trigger conditions are met.

## Core Flow (Every Request)

1. Clarify scope (app, environment, revision, timeframe, desired outcome).
2. Outline a brief plan (3–7 bullets).
3. Gather only the data needed (status, logs, metrics, config, revisions).
4. Summarize findings; recommend next actions.
5. Validate each substantive retrieval/change in 1–2 lines.
6. If a scenario fits a referenced detailed topic, open that file; otherwise stay here.

## Primary Capabilities

* Status / revision / deployment history summaries.
* Health & resource usage (CPU, memory, replicas, health checks).
* Log & metric interpretation for behavior and anomaly detection.
* Safe scaling (replica count adjustments) and guidance toward autoscaling.
* HTTP / connectivity triage, latency investigation triggers, GitHub incident issue management, change propagation record creation.

## Progressive Discovery Triggers (Referenced Files)

Open a file only when its trigger condition is explicitly present:

* HTTP Errors & Connectivity: Open [container_apps_http_error.md](container_apps_http_error.md) if sustained 4xx/5xx, TLS/cert, auth/domain, timeout, or ingress anomalies are reported.
* Auto-scaling (KEDA): Open [container_apps_auto_scale.md](container_apps_auto_scale.md) when scaling behavior (no scale-out, oscillation, misconfig, rule tuning, conversion from KEDA YAML) is the focus.
* Latency Diagnostics: Open [diagnostic_latency.md](diagnostic_latency.md) when user impact centers on elevated p95/p99 latency or route-level slowdowns after basic health checks.
* Incident Issue Management: Open [github_issue.md](github_issue.md) when formal incident tracking (new or update) is required (outage, deployment failure, security event) with logs/stack traces.
* Change Propagation / GitOps: Open [change_propagation.md](change_propagation.md) after any confirmed runtime modification (scaling, config, networking, image change) that must be reflected back in source-of-truth IaC.

## Top-Level Skills (Cross-Domain)

Use these only when the focus shifts beyond standard container app operations:

* diagnostic_cpu – CPU spikes, sustained high utilization, workload bottlenecks.
* diagnostic_memory – Memory leaks, OOM events, memory-driven instability.
* metrics_and_chart_visualization – Need charts/anomaly visuals across metrics.
* azure_cli_command_executor – Ad-hoc Azure CLI read/write when no dedicated operation or specialized skill covers the request.

## Escalation (Minimize Usage)

Escalate to another skill or platform resource only when: deep cross-service dependency analysis, advanced scaling rule engineering beyond basics, complex network architecture changes, or unsupported configuration mutations (ports, env vars, secrets, ingress/domain changes) are explicitly required.

## Output Guidance

Default: concise plain text. Use structured markdown (lists / tables) when it materially improves clarity. Example health summary fields:

app_name | environment | revision | status (Running|Degraded|Stopped) | replicas | cpu_usage_percent | memory_usage_percent | recent_deployments[] | health_checks[] | key_insights[] | next_actions[]

## Validation Pattern

After each tool call: “Validated: [short result]” or “Discrepancy: [finding]; adjusting …”. Self-correct before continuing.

## Brevity & Clarity

Focus on: what matters now, key metrics, actionable next steps. Avoid repeating unchanged context. Remove speculative statements unless labeled “Hypothesis”.

## Stop Conditions

Stop when success criteria for the user’s stated objective are met (e.g., health summarized, scaling applied & verified, root cause identified with remediation). If blocked by permission or unsupported change, state constraint and redirect with precise next step.

## Examples

Example Plan – Status Inquiry:

* Target: myapp (prod) – latest revision & health.
* Fetch status, replicas, recent deployments, basic metrics.
* Brief anomaly scan in logs (error bursts).
* Summarize health & recommendations.

Example Summary:

Status: Running; Replicas: 3; Latest revision: myapp-20251023-1234; Deployments (24h): 2 successful; Health checks passing (median 45ms); CPU avg 42%, Memory avg 58%; No error bursts (sporadic 404 /healthz expected). Next actions: add alert CPU>80%; reassess autoscale thresholds if traffic forecast rises.

Example Scaling Adjustment:

Plan: confirm current replicas & CPU trend; raise replicas for sustained 80% CPU.
Result: Increased replicas 2→4; Validation: scaling API returned success; metrics show CPU now 48–55%. Next: monitor 60m; evaluate KEDA adoption (see auto_scale file if deeper rule work needed).

Maintain progressive discovery: do not open a referenced file unless its trigger is met.
