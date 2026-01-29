---
name: diagnostic_cpu
description: Diagnose high CPU in Container Apps, Web Apps, and AKS by collecting a sampling trace and ranking methods (user code first). Produces a concise table, highlights the top hotspot, and gives 1-3 targeted remediation suggestions. Retries once on failure; escalates on second.
tools:
  - GetCPUAnalysis
---

# CPU Usage Diagnosis Skill

## Purpose

Provide focused CPU usage profiling and interpretation for Azure compute workloads (Container Apps, Web Apps, AKS) using sampling trace data. Output is concise: highest consumers first (user code prioritized), then a table and 1–3 targeted remediation suggestions.

## When to Load

Load only when the user asks about high CPU, performance degradation, “what is using CPU?”, contention, or code‑level hotspots. Skip if the user only wants a list or static description of a resource.

## Scope & Alignment

Non‑destructive. This skill gathers diagnostic data and suggests optimizations. Any write / restart / scale action must follow main system prompt safety rules. Do not override global conciseness, error handling, or formatting requirements.

## Tool Usage (Internal)

Use GetCPUAnalysis for trace collection. If the resource ID is unknown, first follow the discovery workflow from the system prompt. Do not mention tool names in user‑facing output.

## Workflow

1. Preamble (one sentence): State you are analyzing CPU for the resource (bold the name only if Container App) and that it may take a short time if collection is required.
2. Collection: Invoke GetCPUAnalysis with the resource ID. If samples are very low (<200 inclusive) note limited confidence.
3. Parsing: Rank methods; user code first, then Base Class Library / third‑party. Exclude missing symbols and non‑method frames.
4. Presentation: Show a table (top 5–10 rows) with columns: Rank | Method | CPU% | Inclusive | Exclusive | Assembly/Namespace | Category. Category = User | BCL. Highlight the single highest consumer in a brief sentence.
5. OS / Runtime Notes (only if relevant): Windows (symbols, tight loop/sync); Linux (perf/eBPF, allocation, lock contention); .NET (GC pauses, thread pool starvation, async oversync).
6. Remediation: Provide 1–3 actionable suggestions. Avoid generic advice.
7. Completion: If inconclusive (>30% missing symbols) state limitation and suggest ensuring symbols / re‑running under load.

## Failure Handling

On first failure or timeout: interpret error (permission, invalid ID, timeout, low samples). Retry once. On second failure stop and escalate per global circuit breaker rules with concise message and next step (confirm access / permissions / symbols). No further retries.

## Reporting Rules

- Include the resource name exactly once in the first sentence; bold only if Container App.
- No tool names in user output.
- Keep answer + table + remediation concise; omit unrelated resource data.
- Use a table only if ≥3 method entries; otherwise list inline.
- If one method dominates (≥40%) call it out plainly.

## User / System Namespace Heuristics

Namespaces starting with company / app prefixes (e.g., MyCompany., Contoso., Acme.) are user code. System / framework / third‑party examples: System.*, Microsoft.*, Newtonsoft.*, Npgsql.*, Azure.* → non‑user unless clearly application layer.

## Edge Cases

- Low sample count (<200 inclusive): advise re‑run under representative load.
- High missing symbol rate (>30% rows excluded): recommend building with symbols / enabling symbol server.
- Single method >70% CPU: warn about potential tight loop or blocking call and recommend targeted review.

## Example (Illustrative)

Analyzing CPU usage for **my-container-app** – this may take a moment.

Highest CPU consumer: OrdersController.CalculateTotals (37.4% user code).

| Rank | Method                             | CPU% | Inclusive | Exclusive | Assembly/Namespace     | Category |
|------|------------------------------------|------|-----------|-----------|------------------------|----------|
| 1    | OrdersController.CalculateTotals   | 37.4 | 12456     | 10892     | MyCompany.Api          | User     |
| 2    | PricingEngine.ComputeDiscounts    | 23.1 | 7698      | 6210      | MyCompany.Core         | User     |
| 3    | System.String.Concat              | 8.6  | 3110      | 1245      | System.Private.CoreLib | BCL      |

Focused remediation: streamline discount calculations; avoid repeated string concatenation in hot loop.

## Symbol & Data Quality

Exclude non‑method frames and unresolved symbols. If exclusions materially reduce confidence, state it and focus recommendations on improving trace quality first.

## Remediation Suggestion Style

Each suggestion: hotspot + action + expected benefit (e.g., “Reduce JSON serialization in CalculateTotals using cached contract – lowers CPU by reducing allocations”). Avoid vague language.
