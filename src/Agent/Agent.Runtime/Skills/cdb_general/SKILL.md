# Cosmos DB (CDB) General Diagnostic Skill

## Overview
Provides structured, read‑only diagnostics for Azure Cosmos DB issues (availability, latency, throttling, connectivity) via Azure Support Center product + classification workflow. Goal: rapidly identify the correct support product, run its guided diagnostics, and summarize findings concisely. No remediation or config changes here.

## Core Principles

1. Safety first: all actions are read‑only; never modify Cosmos DB resources.
2. Accuracy: select the most relevant support product and classification; ask for clarification if ambiguity persists after one pass.
3. Conciseness: short status preambles; tables + brief plain‑language interpretations; avoid emojis.
4. Efficiency: do not re‑discover products or re‑list classifications if already cached unless user context changes.
5. Tool names are internal; do not surface them directly to the user.

## Internal Tools (reference only)

| Purpose | Tool |
|---------|------|
| List support products | GetSupportProductsFromArm |
| List problem classifications | GetSupportProblemClassificationsForProduct |
| Run guided diagnostics | GetAzureSupportCenterDiagnosticResultsForQuestion |

## Progressive Discovery & When to Open Other Skills

Open another skill only if a trigger below is met; load one, act, then return.

| Trigger | Action | Rationale |
|---------|--------|-----------|
| Missing/ambiguous Cosmos DB account resource ID, subscription, or region | Use built‑in discovery tools (ListSubscriptions, ListResourceGroups, SearchResource, GetResourceIdForResourceName) | Establish accurate scope before classification |
| Need metric trends (RU consumption, latency, throttling) beyond snapshot results | Open `metrics_and_chart_visualization` | Time‑series correlation for performance or intermittent issues |
| Need CLI commands to fetch additional static properties not in diagnostics | Open `azure_cli_command_executor` | Safe retrieval of supplemental config (consistency level, indexing) |

## Input Clarification (ask only what is missing)

Request concise details if absent: affected account name or resource ID, symptom (e.g., high RU charges vs throttling), onset time window, whether issue is intermittent or persistent. Ask at most once per missing category.

## Diagnostic Workflow

1. Product Discovery
   - Filter support products for Cosmos DB (match relevant product name/id). Cache product GUID.
   - If multiple candidate products remain (rare), present top matches (name + short description) and ask user to choose one.
2. Problem Classification
   - Retrieve classifications; rank by keyword match to user symptom (e.g., “throttle”, “latency”, “timeout”).
   - If confident (single strong match) select automatically and inform user; else present 3–5 options for selection.
3. Run Diagnostics
   - Execute diagnostic for chosen classification.
   - While running (if multi‑step), provide brief status update.
   - Collect raw results; structure into table: Check | Status | Details | Evidence.
4. Interpret
   - For each row: translate to plain meaning (e.g., “High normalized RU consumption indicates partition hot‑spot”).
   - Identify dominant pattern: throttling, latency spike, connectivity failure, index/scaling lag.
5. Summarize Findings
   - Present concise bullet groups: Results, Observations, Likely Causes (ordered by confidence). Avoid speculative causes without evidence.
6. Next Steps (Optional)
   - If classification mismatched (no relevant results), suggest reclassification or more precise symptom/time range.
   - If metrics correlation needed → mention opening metrics skill.

## Results Table Example

| Check | Status | Details | Evidence |
|-------|--------|---------|----------|
| Partition Throttling | Warning | 429 responses elevated | 429 rate: 8% last 15m |
| Replication Latency | Normal | Within expected thresholds | 95p = 60ms |
| Indexing Backlog | Warning | New containers reindexing | Progress: 72% |

Interpretation (example): Elevated 429 rate isolated to a single logical partition; replication healthy; indexing backlog suggests recent schema/index changes increasing RU pressure.

## Constraints

| Rule | Reason |
|------|--------|
| No writes or scaling actions | Skill is diagnosis only |
| Do not guess product/classification | Ensures accuracy; ask if ambiguous |
| Do not repeat clarification prompts | Avoid user fatigue |
| No emojis in user output | Align with system conciseness principle |
| Do not re‑run identical diagnostics unless new evidence/time window | Efficiency |

## Example Flow (Throttling)

1. User reports “frequent 429 errors since upgrade”.
2. Confirm resource ID + time window (past hour). Missing ID → use built‑in discovery tools (ListSubscriptions / ListResourceGroups / SearchResource / GetResourceIdForResourceName) to resolve.
3. Discover and select Cosmos DB support product.
4. Match classification containing “throttle” or “rate exceeded” → run diagnostics.
5. Table shows elevated partition 429s; other checks normal.
6. Summary: “Observed partition‑localized throttling post‑upgrade; replication and indexing normal. Likely hot partition or RU under‑allocation. Consider metrics skill for RU trend verification.”

## Cross References

- `metrics_and_chart_visualization` – correlate RU, latency, and 429 trends over time.
- `azure_cli_command_executor` – fetch static account/container properties if absent from diagnostic results.
- `metrics_and_chart_visualization` – correlate RU, latency, and 429 trends over time.
- `azure_cli_command_executor` – fetch static account/container properties if absent from diagnostic results.

## Completion Format

"[OK] Cosmos DB diagnostic complete – {finding}. Next: {follow_up_or_closure}."
