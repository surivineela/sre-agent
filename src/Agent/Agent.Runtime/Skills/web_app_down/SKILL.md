# Azure Web App Downtime Diagnosis Skill

Investigate Azure App Service (Web App) availability degradation (downtime, slowness, 5xx spikes, post‑deployment instability) and produce a concise, evidence‑based RCA with one clear mitigation path.

## Scope

Focus ONLY on availability or performance degradation of Azure Web Apps. Exclude: generic resource listing, restart cause analysis (use `web_app_restart`), unrelated configuration/permission issues (inform user if out of scope).

## Workflow Overview

Interpret results in this order. SAFE read operations whose parameters are already known (metrics, deployment activity, exceptions/logs) may be fetched in parallel; analysis must still follow the sequence below.

1. Availability Check (last 30 min)

- Chart availability; compute average availability.
- If >= 99.9% and user does not insist on deeper analysis → report healthy and offer closure.

1. Core Metrics

- CPU, memory, threads (individual line charts). Flag sustained >=80% or spikes overlapping downtime.

1. Correlation

- Determine if CPU/memory spikes align with low availability or errors. Presence of `System.OutOfMemoryException` triggers memory path even without spikes.

1. Deployment / Slot Swap Activity

- List successful swaps (bullets: Timestamp, Operation, Caller) that coincide with downtime (raise WARNING).
- Show unsuccessful swaps/deployments in a table.

1. Exceptions & Logs

- Retrieve three most common exceptions + full stack traces (no truncation) and console logs.
- If access fails, immediately inform user and request required permissions/data.

1. Root Cause Classification

- Choose exactly one primary cause: High CPU, High Memory, Deployment‑induced Application Exceptions, or Memory Exception (`System.OutOfMemoryException`).
- If both deployment swap overlap AND application exceptions present → classify as Deployment‑induced Application Exceptions.

1. Mitigation (Single Path)

- Select ONE mitigation path (see matrix) and execute guidance (may require loading another skill).

1. Final Summary

- Provide: root cause statement, supporting metrics/charts, key deployment events, top exceptions (stack traces), chosen mitigation, before/after metrics, any data gaps.

## Mitigation Decision Matrix

| Condition | Path | Action Highlights |
|-----------|------|-------------------|
| Swap coincides + new exceptions | Deployment‑induced App Exceptions | Open `app_code.md`; rollback/slot swap guidance; then validate recovery |
| High CPU (no swap overlap) | diagnostic_cpu | CPU analysis + consider scale up (SKU) + optimization; show before/after |
| High Memory OR `System.OutOfMemoryException` | diagnostic_memory | Memory analysis (leaks, pressure) + scale guidance; show before/after |
| None of above & availability healthy | None | Report healthy; await user direction |

Open `app_code.md` ONLY when root cause is deployment‑induced app exceptions or user explicitly requests rollback guidance.

## Data Presentation

- Charts: availability, CPU, memory, threads; before/after charts for mitigation.
- Exceptions: full stack traces (3 most common) in bullet list; no truncation.
- Deployment: successful overlapping swaps as bullets; unsuccessful swaps/deployments in a table.

## Missing / Insufficient Data

Never guess. If a required dataset (metrics, logs, deployment activity) is unavailable or access fails:

1. Notify user with concise reason.
2. Request precise missing permission/resource.
3. Continue once data becomes available.

## Top-Level Skills Referenced

- `diagnostic_memory`: memory spikes, sustained high memory, leaks, `System.OutOfMemoryException`, or explicit user memory analysis request.
- `diagnostic_cpu`: CPU spikes/sustained high CPU affecting availability.
- `web_app_restart`: ONLY for restart cause investigations, not general downtime.

## Additional Resource File

- `app_code.md`: Deployment‑induced application exception mitigation & rollback guidance. Do not open unless classification requires it or user explicitly asks.

## Root Cause Statement Format

"Root cause: [PRIMARY_CAUSE]. Evidence: [concise metric/event list]." If deployment‑induced: "Root cause: deployment swap introduced application exceptions." If memory exception: mention presence of `System.OutOfMemoryException`.

## Example (Condensed)

Availability 96.2% (chart). CPU spikes to 95% overlapping error window; memory stable; no swap overlap; exceptions show typical CPU saturation traces; no OOM. Root cause: High CPU. Mitigation: invoke `diagnostic_cpu`, scale from S1→S2, apply CPU optimization. After mitigation: availability 99.95%, CPU avg 55% (charts shown). Summary delivered.

## Quality & Conciseness Principles

Adhere to global system prompt (Safety > Accuracy > Conciseness > Efficiency). Fetch only necessary data. Provide direct RCA first, then supporting evidence. Avoid mentioning tool names to user.

