---
name: scheduled_task
description: |
  Load this skill when the user asks to create, list, inspect, pause, resume, cancel, or view history for a recurring monitoring / maintenance task, OR requests help shaping an autonomous scheduled agent workflow (e.g. "set up a task to watch latency every 15 minutes" / "pause my daily cleanup job").
  Do NOT load for one-off ad-hoc queries or simple immediate checks; answer those directly. Load only once per thread unless tasks context is explicitly cleared. Align responses with the main system prompt: start with the direct answer (1-2 sentences), then supporting task details—do NOT emit a preliminary checklist.
tools:
  - CreateScheduledMonitoringTask
  - ListScheduledTasks
  - PauseScheduledTask
  - ResumeScheduledTask
  - CancelScheduledTask
  - GetTaskExecutionHistory
---

# Scheduled Task Skill

## Purpose

Provide disciplined creation and management of recurring Azure monitoring / maintenance tasks. Focus on: correct schedule, safe scope, duplicate avoidance, and clear autonomous execution instructions. Follow system prompt communication rules (direct answer first, no mandatory upfront checklist).

## When This Guidance Applies

User intent involves: creating a scheduled monitoring/maintenance workflow, listing existing tasks, inspecting configuration, pausing, resuming, canceling, or viewing execution history. For ad‑hoc one‑time checks, respond directly without invoking scheduled task patterns.

## Core Parameters

| Field | Guidance |
|-------|----------|
| name | ≤60 chars, clear purpose, avoid noisy punctuation. |
| description | Single line (≤140 chars) high‑level intent only. More detail belongs in agentPrompt. |
| agentPrompt | Multi‑line operational instructions: goal, scoped resources (subscription / resource IDs), time window, metrics/data sources, constraints (cost & safety), idempotence, output format, escalation conditions. Must state autonomous scheduled run. |
| cronExpression | Standard 5-part: minute hour day month day-of-week. Derive from natural language if provided. |
| durationHours | Null/0 = indefinite; otherwise integer auto-stop window. Suggest adding if high frequency. |
| maxExecutions | Null = unlimited; else positive cap. Use when short exploratory monitoring. |
| useCurrentThread | true = run in this thread; false = isolated thread. Clarify trade-offs (shared context vs isolation). |

## agentPrompt Construction Checklist (internal)

1. Autonomous run statement
2. Precise scope (subscription, resource groups, resource IDs)
3. Narrow time window (e.g. last 15–30 min) unless trend analysis needed
4. Data sources & metrics (Azure Monitor metrics, logs, KQL queries)
5. Constraints (API call cap, non‑destructive actions only unless user explicitly permitted)
6. Idempotence rule (e.g. “If unchanged state, output 'No material changes'”)
7. Output format (summary + key metrics + 1–3 recommendations)
8. Escalation trigger (e.g. anomaly persists N consecutive runs)

Avoid secrets, redundant prose, or repeating description.

### Example Template (Azure Adapted)

```text
Autonomous Scheduled Run
Scope: Subscription <SUB-ID>, Resource Group rg-app-prod, Web App prod-api
Time Window: Analyze ONLY last 15 minutes
Goal: Detect sustained HTTP 5xx rate >2% and response time p95 degradation >20% vs previous hour
Data: Azure Monitor metrics - Requests, Http5xx, ResponseTime; KQL (AppRequests table filtered to prod-api)
Constraints: Max 300 metric queries; no write operations
Idempotence: If metrics within normal thresholds & no config changes since last run -> output "No material changes"
Output: Summary + current 5xx %, p95 latency, top 2 suspected causes
Escalation: If thresholds breached 3 consecutive runs -> include "Escalation suggested"
```

## Scheduling & Frequency

Common patterns: `*/5 * * * *` (5 min), `*/15 * * * *` (15 min), `0 * * * *` (hourly), `0 0 * * *` (daily).
Challenge overly aggressive schedules (e.g. every 1 min) for high‑cost analyses; propose safer alternative (every 5–15 min) with rationale.
Natural language → derive cron + durationHours ("every 15 minutes for 2 hours" → cron `*/15 * * * *`, durationHours 2).
If ambiguous, present 1–2 options with concise trade‑offs (freshness vs cost).

## Duplicate Detection (Before Creation)

1. List existing tasks (filter by name substring / resource IDs).
2. Compare purpose, metrics, cron overlap.
3. If similar: present candidate(s) with key deltas (name, cron, scope). Ask whether to reuse, adjust, or proceed with a distinct task.
4. If user wants unsupported "modify" and no tool exists: advise recreate via CreateScheduledMonitoringTask (show changed fields).

## Management Operations Mapped to Tools

| Intent | Tool | Output Focus |
|--------|------|--------------|
| create | CreateScheduledMonitoringTask | Echo id + schedule + next run + limits |
| list | ListScheduledTasks | Compact table: Id, Name, Status, Cron, LastExec, ExecCount |
| inspect (if supported by list detail) | ListScheduledTasks (filter / single) | Full config fields |
| pause | PauseScheduledTask | State changed + confirmation no further runs |
| resume | ResumeScheduledTask | Active + next run time |
| cancel | CancelScheduledTask | Permanent stop + irreversible note |
| history | GetTaskExecutionHistory | Recent N (10–20) runs: timestamp, success, brief error summary |

## Creation Response Content

Direct answer (success/failure) → supporting details:

- taskId
- name
- cronExpression + derived frequency (human phrase)
- durationHours or "indefinite"
- maxExecutions (if set)
- useCurrentThread (Yes/No)
- next scheduled run (UTC)

Flag: duplicates resolved? aggressive frequency challenged? missing escalation logic?

## Validation & Safety

On create, verify: cron sane (no sub‑minute), agentPrompt has scope + time window, idempotence & escalation defined for anomaly detection tasks, durationHours present for high‑frequency exploratory tasks. If missing critical parameters (cron/schedule, agentPrompt, description) ask only for those.
Never expose secrets or internal implementation details. Reject schedules that would exceed reasonable cost without justification.

## History Review Guidance

Return last 10–20 executions. For repeated failures: summarize dominant error pattern and suggest agentPrompt refinement (narrow metrics, adjust thresholds, extend durationHours). For consistent success + no changes: suggest lowering frequency if high.

## Examples

1. Natural Language Creation: "Monitor API latency every 15 minutes for 2 hours" → cron `*/15 * * * *`, durationHours 2, agentPrompt includes 15‑min window + latency metrics.
2. Aggressive Schedule Challenge: Request for 1‑minute CPU heavy analysis → propose `*/5 * * * *` citing cost + minimal benefit.
3. Duplicate: Requested "Disk Space Check" every 15 min; existing "Disk Free Monitor S-42" same scope & cron → present existing task, ask whether reuse or differentiate (e.g. add alert threshold change).
4. Pause/Resume: After pause, confirm status=Paused; after resume, show next run UTC.
5. History: Show table of recent runs; highlight 3 consecutive anomalies → advise escalation per agentPrompt rule.

## Edge Cases

- Missing cron but natural language schedule: derive then confirm.
- User asks to "modify" without modify tool: explain limitation; offer recreate with changes.
- Extremely long durationHours + high frequency: warn about cost, suggest cap or reduced frequency.
- Duplicate names differing only by case/punctuation: treat as potential duplicate.
- Indefinite high‑frequency task without escalation/idempotence: require adding those before creation.

## Output Style Alignment

Follow system prompt: Answer first (1–2 sentences) → concise structured details. No prefacing checklist unless complexity (≥5 coordinated changes) justifies ToDo usage per system rules.

## Reference

Cron fields: minute hour day month day-of-week. Supports ranges (e.g. 0-6), lists (1,3,5), steps (*/15). Ensure derived cron matches stated intent exactly.

## Summary

This guidance ensures scheduled tasks are deliberate, non‑duplicative, cost‑aware, and operationally useful with clear autonomous execution semantics.
