---
name: postgresql
description: Load this skill when the user asks about Azure Database for PostgreSQL performance (CPU, memory, connections, storage), autovacuum status, table bloat or dead tuples, configuration validation, connectivity failures, slow / blocking queries, index usage, or how to remediate issues.
tools:
  - GetResourceIdForResourceName
  - GetDatabaseOverview
  - AnalyzePostgreSQLHealth
  - AnalyzeTableBloat
  - AnalyzeAutovacuumConfiguration
  - AnalyzeTableActivity
  - GetPostgreSQLMetrics
  - GetPostgreSQLMetricsWithGroups
  - ValidateEnhancedMetricsConfiguration
  - CheckPostgreSQLConnectivity
  - AnalyzeSlowQueries
  - GetResourceHealth
  - ValidatePostgreSQLConfiguration
  - GetDiagnosticWorkspaceForResource
  - ListAvailablePlaybooks
  - GetPlaybook
  - PlotTimeSeriesData
  - PlotBarChart
  - PlotScatter
  - WaitInMilliSeconds
  - RunPsqlReadCommand
  - ValidatePsqlCommand
---

# Azure PostgreSQL SRE Skill

## Purpose

Provide focused diagnostics and safe remediation guidance for Azure Database for PostgreSQL: performance, storage, autovacuum, table bloat, dead tuples, connectivity, slow / blocking queries, and configuration health. Follows progressive discovery: start with high‑value overview data; only load deeper analysis or catalog inspection (see `psql_command_automation.md`) if signals justify it. Never mention tool names to the user. Align with the global system prompt: concise direct answer first, then supporting tables.

## Core Diagnostic Flow (Progressive)

Run steps only if their trigger condition is met; skip unneeded steps to preserve efficiency.

1. Overview (mandatory) — Get structural + high-level tuple and autovacuum signals.
2. Core Metrics (mandatory) — 60‑minute CPU, memory, connections, storage, cache hit ratio.
3. Table Bloat — Trigger: overview or metrics show high dead tuples OR user asks about bloat.
4. Autovacuum Configuration — Trigger: dead tuples high, autovacuum disabled suspicion, or bloat ≥20%.
5. Table Activity — Trigger: high modifications, stale autovacuum, performance complaints.
6. Connectivity — Trigger: user mentions connection / timeout / saturation OR metrics show connection pressure.
7. Resource Health — Always include if user mentions incidents or instability; otherwise include after core performance diagnostics.
8. Comprehensive Health — Trigger: any CRITICAL finding (e.g., cache hit ratio <90%, dead tuple ratio extreme, severe bloat, autovacuum off on large tables).
9. Slow Query Analysis — Trigger: user mentions slow queries / latency OR cache hit ratio low OR blocking suspected.
10. Remediation SQL — Always generate tailored, read‑only suggested commands; execution requires user confirmation for any maintenance action.

## Output Pattern

For each executed step:

- Present a compact table (or two if logically distinct) with required fields.
- Prefix summary line using status syntax: `[OK] Step 3 Bloat: 3 tables >20% (largest 47%)` or `[WARN] Step 4 Autovacuum: disabled on 2 large tables`.
- Do not restate how data was gathered; omit tool names.
- Reserve narrative for interpretation only when it helps next decision.

## Data Fields (Minimum Required)

Overview: Name, Size, Table Count, Dead Tuple Count (global), Modifications summary, Autovacuum status/params (workers, naptime, work mem).
Core Metrics: CPU %, Memory %, Storage %, Active / Total Connections, Cache Hit Ratio %.
Table Bloat: Table, Bloat %, Estimated Wasted Size (GB), Dead Tuples (count / %).
Autovacuum Config: Global status, Disabled table count, Each disabled table: size, dead tuples %, last vacuum/autovacuum.
Table Activity: Table, Ops/day (approx), Dead Tuples %, Last Vacuum, Flags (e.g., HIGH_DEAD, STALE_VACUUM).
Connectivity: Status, Duration, Pool saturation %, Failure / timeout indicators.
Resource Health: Health status, Recent events (timestamp, summary, impact).
Comprehensive Health: Critical issues list, Warnings list, Aggregates (dead tuple ratio overall, severe bloat tables, disabled autovacuum count).
Slow Queries: Query store status, Time window, Top slow queries (text snippet, count, avg ms, max ms), blocking chains if available, key recommendations.
Remediation SQL: Commands grouped by category (VACUUM, ENABLE AUTOVACUUM, TUNE, INDEX). Provide placeholders schema.table, index names; flag commands that require confirmation.

## Severity Heuristics

Define status tags to keep output concise:

- CRITICAL: Cache hit <90%, any table bloat ≥30%, autovacuum disabled on table >5GB, dead tuple ratio ≥25% on large table, sustained connection saturation ≥85%.
- WARN: Bloat 20–29%, cache hit 90–94%, dead tuple ratio 15–24%, connection saturation 70–84%.

Map to status prefix: `[OK]`, `[WARN]`, `[CRITICAL]`.

## Wait / Timing Guidance

Use `WaitInMilliSeconds(2000)` only if a preceding tool requires short stabilization (e.g., metrics aggregation) or returns incomplete data on first attempt. Do NOT add artificial waits after purely static data tools. If a tool explicitly documents additional latency need, extend to 5000 ms. Skip waits when chaining purely read operations whose results are immediately available.

## Progressive Deepening Rules

After Steps 1–2 evaluate whether deeper catalog inspection is required:

- If high dead tuples or bloat suspicion AND table granularity missing → load `psql_command_automation.md` and run safe read queries for validation.
- If slow queries suspected but top query details absent → load `psql_command_automation.md` for pg_stat_activity / index usage patterns.

Do not load additional files more than once unless cleared.

## Remediation Guidance

Generate commands; do not execute without explicit confirmation:

```sql
VACUUM (VERBOSE, ANALYZE) schema.table;
ALTER TABLE schema.table SET (autovacuum_enabled = true);
ALTER TABLE schema.table SET (autovacuum_vacuum_scale_factor = 0.1);
CREATE INDEX CONCURRENTLY idx_name ON schema.table(column);
```

Tailor recommendations: only include commands relevant to identified issues. For multiple similar tables, show one template then list affected tables.

## Safety & Alignment

- Never mention raw tool names or internal mechanics to user.
- Validate `resourceId` scope before first call (subscription/context handled by main prompt).
- Ask for missing critical parameters only when not inferable.
- Flag potential disruptive actions and seek confirmation.
- Provide direct answer first when user asks a pointed question (e.g., "Is autovacuum working?").

## Reference: Catalog / Query Inspection File

When deeper validation needed, load `psql_command_automation.md` (see triggers above). That file supplies safe query construction patterns; do not duplicate them here.

## Final Summary (Optional)

After last executed step (or early if user only requested a single check) optionally synthesize: one sentence answer + key metrics table + next recommended action. Omit if user already satisfied.
