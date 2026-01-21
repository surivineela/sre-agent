---
name: aks_general
description: The AKS General Skill provides expertise in managing Azure Kubernetes Service (AKS) resources, enabling tasks such as monitoring, troubleshooting, creating, and updating Kubernetes resources, except deletions. It supports effective operations and diagnosis of workloads while facilitating configuration management and resource optimization.
tools:
  - DiscoverPrometheusMetrics
  - GetMetricsLabels
  - QueryPrometheusMetrics
  - PlotTimeSeriesData
  - PlotPieChart
  - PlotBarChart
  - PlotScatter
  - VisualizeAKSMicroserviceTopology
  - SearchRunbooks
  - RunKubectlReadCommand
  - RunKubectlWriteCommand
  - RunKubectlCommandHelp
  - SearchResourceByName
  - ListResourcesByType
  - GetKubeResourceMetricsRange
  - RunAzCliReadCommands
  - RemoveNSGRule
  - CreateGithubIssue
  - FetchGithubIssue
  - FindConnectedGitHubRepo
  - GetIaCForGitHub
  - DisconnectRepositoryFromResourceForGitHub
  - FetchGithubIssuesLimited
---

# Azure Kubernetes Service (AKS) – Core Operations Skill

All markdown files in this folder collectively form a single AKS operations skill. This core file is the starting point; the sibling files are supplementary references you open progressively (network, remediation, deep workload diagnosis, command execution) only when their triggers appear.

## When to Use

Use this skill first for any AKS / Kubernetes operations question before deciding whether to load a more specialized file.

## Progressive Supplementary Discovery Map

Open a supplementary file only when a clear trigger is present; avoid speculative loading.

| Situation / Need | Supplementary File | Rationale |
|------------------|--------------------|-----------|
| Workload unhealthy (restarts, probe failures, rollout issues) | aks_remediation.md | Safe corrective action patterns |
| Conflicting signals / no clear cause | aks_workload_diagnose.md | Structured hypothesis loop |
| Connectivity, DNS, ingress/egress, NetworkPolicy issues | aks_network_remediation.md | Flow classification & network playbooks |
| Need specific kubectl read/write syntax & safety | kubectl_command_executor.md | Command execution safeguards |
| Need to record infrastructure changes | change_propagation.md | Change/audit record patterns |
| Need to create or augment tracking issue | github_issue.md | Incident / issue lifecycle |

## Core Principles (Aligned with System Prompt)

1. Safety: Never destructive beyond permitted scope (no deletions here). Validate context & required identifiers first.
2. Accuracy > speed: Collect minimal sufficient evidence; avoid guessing.
3. Conciseness: First sentence answers; only supporting data the user needs.
4. No raw tool error dumps: Summarize errors (identity, missing permission, required role, suggested next step). Offer retry after user fixes.
5. Don’t mention tool names to the user; describe actions ("I’ll check the deployment status").
6. Parallel actions only when independent and read‑only.

## Standard Operational Pattern

Plan → Collect → Analyze → Act (optional) → Verify → Report.

| Phase | Purpose | Minimal Output |
|-------|---------|----------------|
| Plan | Clarify goal & success criteria (1–3 bullets) | Goal + key resources |
| Collect | Gather status/events/logs/metrics | Structured snippets / tables |
| Analyze | Correlate signals; form concise finding(s) | Cause hypotheses or confirmed cause |
| Act | Safe, reversible change (if needed) | Action summary + expectation |
| Verify | Show post‑state vs expected | Delta table / short lines |
| Report | Final answer (1–2 sentences) + evidence | Summary + next step (if any) |

## Evidence Categories (Reference Not Repetition)

- Workload: `kubectl get/describe` (conditions, replicas, events)
- Pods: state, restarts, container reasons, prior logs if crash
- Logs: current + previous container logs for error patterns
- Metrics (if available): CPU, memory, restarts, latency (summarize; avoid raw noise)
- Network (only if signaling): service/endpoints presence, basic connectivity
- Rollout history: ReplicaSets / revisions

## Permission / Access Errors

When an AKS API (or related operation) fails with Forbidden/403 or similar:

- Extract and present: failing identity, required permission/role, high‑level action needed.
- Do NOT paste raw multi‑line or JSON error blocks verbatim.
- Ask user to confirm once access is granted; then retry.
- Never attempt to self‑elevate or fabricate role commands.

## Safety & Write Boundaries

- No deletions. If deletion requested → state it is out of scope and suggest approved external process.
- For any write (scaling, patch, label, rollout undo):
  1. Confirm target (namespace, kind, name)
  2. Present minimal change + impact + rollback
  3. Get explicit confirmation (unless user already approved in the same turn)
  4. Execute one change → verify → proceed / stop

## Validation Template

For every executed change or key read producing a decision:

```
Observed: <concise fact>
Expected: <target or invariant>
Result: <match | mismatch + next step>
```

Only include mismatches if they influence next action.

## When to Open Supplementary Files

| Trigger Signal | Open File | Example |
|----------------|-----------|---------|
| Probe failing + needs rollback or config correction | aks_remediation.md | Restart loops after new image |
| Conflicting logs/events; unclear root cause | aks_workload_diagnose.md | Restarts + no clear error |
| DNS failures, EXTERNAL-IP pending, NetworkPolicy blocks | aks_network_remediation.md | Pending ingress IP |
| Need create/apply/patch/scale syntax & safety | kubectl_command_executor.md | Label add + scale |

### Detailed Triggers (Reference)

| File | Trigger Details |
|------|-----------------|
| aks_remediation.md | `CrashLoopBackOff` / repeated restarts; replica mismatch (desired≠ready) unresolved; probe failures with clear config error or recent rollout; resource pressure (`OOMKilled`, sustained CPU throttling) needing limit/request adjustment |
| aks_workload_diagnose.md | Symptoms persist after rollback or probe/resource tweak; conflicting indicators (restarts + no clear log error + partial readiness); need to correlate change timing vs symptom onset |
| aks_network_remediation.md | Network-centric signals: DNS resolution failures; Service with no endpoints; ingress EXTERNAL-IP pending; egress timeouts/TLS failures; NetworkPolicy denials; IP exhaustion; asymmetric routing; mesh policy blocks |
| kubectl_command_executor.md | Need exact safe syntax for planned write (scale, patch, label, set image, probe tweak) or dry-run preview; complex patch or rollback sequencing |

## Metrics & Tabular Data

- ≤5 related items → inline list/table.
- >5 items → summary + top N (≤5) + counts.
- Highlight anomalies (e.g., replicas mismatch, high restarts) with **bold**.

## Communication Pattern

First sentence = direct answer. Then supporting evidence (tables or bullets). Omit internal reasoning. Avoid enumerating every command executed.

## Quick Examples

CrashLoop triage (high level):

1. Collect pod list (restarts) + describe failing pod + previous logs
2. Identify repeated init crash due to config key missing
3. Propose adding missing env + restart (or rollback) with rollback plan
4. Verify restarts stabilize → report

Replica mismatch:

1. Observe desired=5 ready=3
2. Check events for pull errors; logs show image 404
3. Recommend rollback to last known good image; verify ready=5/5

Resource pressure summary example output:

| Node | CPU% | Mem% | Notes |
|------|------|------|-------|
| n1 | 82 | 70 | Near CPU saturation |
| n2 | 45 | 68 | Normal |

Answer sentence: “Two nodes healthy; one node near sustained CPU saturation—consider scaling or redistributing workload.”

## Cross References

Use supplementary files only when triggered; they inherit these core principles:

- `aks_remediation.md`
- `aks_workload_diagnose.md`
- `aks_network_remediation.md`
- `kubectl_command_executor.md`
- `change_propagation.md`
- `github_issue.md`

## Out of Scope

- Cluster provisioning or node pool scaling (Azure ARM level)
- Destructive operations (deletes) or permission escalation
- Raw unreadable error dumps

## Completion Checklist (Internal)

Ensure before final answer:

- Direct answer present first.
- Evidence supports conclusion.
- No raw errors or tool names.
- Only necessary tables/data included.

Then respond.
