# PostgreSQL Command Automation

## Scope

Patterns for safe, efficient PostgreSQL catalog and read‑only query inspection to validate diagnostics (bloat, autovacuum, slow / blocking queries, connections, index usage, schema structure). For progressive deepening only—loaded when tool outputs lack needed granularity.

## Allowed vs Forbidden

Allowed (read‑only): SELECT, SHOW, EXPLAIN, psql meta‑commands (\d, \l, \dt, \di), catalog views (information_schema.*, pg_stat_*, pg_indexes, pg_class, pg_namespace).
Use EXPLAIN ANALYZE only with explicit confirmation (it executes the query). Forbidden: INSERT/UPDATE/DELETE, CREATE/DROP/ALTER, VACUUM, ANALYZE (execution), TRUNCATE, GRANT/REVOKE.

## Query Construction Checklist

1. Objective (one sentence)
2. Target objects verified (database, schema, table, columns)
3. Scoped WHERE filters (time / keys)
4. LIMIT (default ≤50 unless wider scope justified)
5. Minimal column projection
6. (Optional) EXPLAIN for heavy candidate before execution

## Pre‑Execution Validation Snippets

Current DB: SHOW current_database;  Version: SELECT version();
Schemas: \dn  Tables in schema: \dt schema_name.*
Describe table: \d schema.table  Columns: SELECT column_name, data_type FROM information_schema.columns WHERE table_schema='schema' AND table_name='table' ORDER BY ordinal_position LIMIT 50;

## Common Investigations and Query Patterns (Original Examples Preserved)

### A) Table Structure and Existence

Validate table exists:
```
\dt public.*
```
Describe structure:
```
\d public.orders
```
Information schema view:
```sql
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'orders'
ORDER BY ordinal_position;
```

### B) Recent Activity From a Table (Constrained)

Goal: Retrieve recent orders safely.
```sql
SELECT order_id, customer_id, order_date, total_amount
FROM public.orders
WHERE order_date >= CURRENT_DATE - INTERVAL '7 days'
ORDER BY order_date DESC
LIMIT 20;
```

### C) Investigate Slow Queries (Read-only)

Currently running queries:
```sql
SELECT pid, usename, application_name, client_addr,
             state, query_start,
             EXTRACT(EPOCH FROM (now() - query_start)) AS query_duration_seconds,
             LEFT(query, 100) AS query_snippet
FROM pg_stat_activity
WHERE state <> 'idle'
    AND query NOT ILIKE '%pg_stat_activity%'
ORDER BY query_start ASC
LIMIT 20;
```

Long-running queries (>30s):
```sql
SELECT pid, usename,
             EXTRACT(EPOCH FROM (now() - query_start))::INT AS duration_seconds,
             state, query
FROM pg_stat_activity
WHERE state <> 'idle'
    AND query_start < now() - INTERVAL '30 seconds'
    AND query NOT ILIKE '%pg_stat_activity%'
ORDER BY query_start ASC
LIMIT 10;
```

Blocking/blocked chains:
```sql
SELECT
    blocking.pid AS blocking_pid,
    blocking.usename AS blocking_user,
    blocked.pid AS blocked_pid,
    blocked.usename AS blocked_user,
    LEFT(blocked.query, 200) AS blocked_query,
    EXTRACT(EPOCH FROM (now() - blocked.query_start)) AS blocked_duration_seconds
FROM pg_stat_activity AS blocked
JOIN pg_stat_activity AS blocking
    ON blocking.pid = ANY(pg_blocking_pids(blocked.pid))
WHERE blocked.state <> 'idle'
LIMIT 20;
```

Table statistics (dead tuples, autovacuum recency):
```sql
SELECT schemaname, tablename,
             n_live_tup AS live_rows,
             n_dead_tup AS dead_rows,
             ROUND(100.0 * n_dead_tup / NULLIF(n_live_tup + n_dead_tup, 0), 2) AS dead_pct,
             last_vacuum, last_autovacuum, last_analyze, last_autoanalyze
FROM pg_stat_user_tables
WHERE tablename IN ('customers','orders','transactions') OR tablename LIKE '%customer%'
ORDER BY n_dead_tup DESC
LIMIT 15;
```

Index usage:
```sql
SELECT schemaname, tablename, indexname,
             idx_scan AS index_scans, idx_tup_read AS tuples_read, idx_tup_fetch AS tuples_fetched,
             pg_size_pretty(pg_relation_size(indexrelid)) AS index_size
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
    AND tablename IN ('customers','orders','transactions')
ORDER BY idx_scan DESC
LIMIT 20;
```

Explain a candidate query (plan only):
```sql
EXPLAIN
SELECT order_id, customer_id, order_date, total_amount
FROM public.orders
WHERE order_date >= CURRENT_DATE - INTERVAL '7 days'
ORDER BY order_date DESC
LIMIT 20;
```

### D) Connection Pool Exhaustion

Max connections:
```sql
SHOW max_connections;
```

Current connection states:
```sql
SELECT
    COUNT(*) AS total_connections,
    COUNT(*) FILTER (WHERE state = 'active') AS active_connections,
    COUNT(*) FILTER (WHERE state = 'idle') AS idle_connections,
    COUNT(*) FILTER (WHERE state = 'idle in transaction') AS idle_in_transaction,
    COUNT(*) FILTER (WHERE state = 'idle in transaction (aborted)') AS aborted_transactions
FROM pg_stat_activity;
```

Connections by application/user:
```sql
SELECT application_name, usename,
             COUNT(*) AS connection_count,
             COUNT(*) FILTER (WHERE state = 'active') AS active,
             COUNT(*) FILTER (WHERE state = 'idle') AS idle,
             COUNT(*) FILTER (WHERE state = 'idle in transaction') AS idle_in_trans,
             MIN(backend_start) AS oldest_connection,
             MAX(query_start) AS latest_query
FROM pg_stat_activity
WHERE pid <> pg_backend_pid()
GROUP BY application_name, usename
ORDER BY connection_count DESC
LIMIT 20;
```

Long-idle connections:
```sql
SELECT pid, usename, application_name, client_addr, state,
             backend_start, state_change,
             EXTRACT(EPOCH FROM (now() - state_change))::INT AS idle_seconds,
             LEFT(query, 200) AS last_query
FROM pg_stat_activity
WHERE state IN ('idle', 'idle in transaction')
    AND state_change < now() - INTERVAL '10 minutes'
ORDER BY state_change ASC
LIMIT 25;
```

Database-level connection stats:
```sql
SELECT datname, numbackends AS current_connections, stats_reset
FROM pg_stat_database
WHERE datname NOT IN ('template0','template1','postgres')
ORDER BY numbackends DESC
LIMIT 10;
```

### E) Catalog and Index Inspection

List indexes for a table:
```
\di public.orders*
```
Or via catalog:
```sql
SELECT indexname, indexdef
FROM pg_indexes
WHERE schemaname = 'public' AND tablename = 'orders'
ORDER BY indexname;
```

Table size and index sizes:
```sql
SELECT
    relname AS object_name,
    pg_size_pretty(pg_total_relation_size(relid)) AS total_size,
    pg_size_pretty(pg_relation_size(relid)) AS table_size,
    pg_size_pretty(pg_indexes_size(relid)) AS indexes_size
FROM pg_catalog.pg_statio_user_tables
WHERE relname = 'orders'
LIMIT 1;
```

### F) Safe Sampling of Large Tables

Obtain limited sample with explicit ordering:
```sql
SELECT col1, col2
FROM public.large_table
WHERE ts >= CURRENT_DATE - INTERVAL '1 day'
ORDER BY ts DESC
LIMIT 50;
```

Column value distribution (capped):
```sql
SELECT col, COUNT(*) AS cnt
FROM public.large_table
WHERE ts >= CURRENT_DATE - INTERVAL '7 days'
GROUP BY col
ORDER BY cnt DESC
LIMIT 20;
```


## Execution & Presentation

Before running a non‑trivial query: show objective, query text, expected row count (approx), safety notes; ask for confirmation if heavy (large table scan, EXPLAIN ANALYZE). After execution: present concise table (≤50 rows), highlight anomalies (e.g., dead_pct ≥25%). Avoid verbose narration.

## Error Handling

On error: parse message → adjust qualification (schema.table), correct identifiers (consult information_schema), reduce scope (add LIMIT / filters), retry once. If still failing, summarize obstacle and request clarification.

## Safety Recap

Always ensure read‑only, proper LIMIT, validated identifiers, and avoid sensitive columns unless user requested them.
