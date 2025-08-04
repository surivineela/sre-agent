# PostgreSQL Performance Investigation

## Description
Comprehensive PostgreSQL performance diagnostic workflow

## Prerequisites
- Access to Azure Monitor metrics
- PostgreSQL server diagnostic settings enabled
- Query Store or pg_stat_statements configured

## Estimated Time
60-90 minutes

## Quick Diagnosis Steps

1. Check CPU and memory metrics in Azure Monitor
2. Identify slow queries via Query Store
3. Examine top waits in Performance Insights
4. Validate index usage and lock statistics

## Steps

### 1. Gather baseline performance metrics
Start by collecting current performance data:

#### CPU and Memory Metrics
- Review CPU utilization over the last 24-48 hours
- Check memory usage patterns
- Identify peak usage periods

#### Connection Metrics
- Monitor active connections vs. max connections
- Check for connection pool exhaustion
- Review connection duration patterns

### 2. Analyze query performance
Identify problematic queries using available tools:

#### Using Query Store (Azure Database for PostgreSQL)
```sql
-- Top queries by execution time
SELECT query_sql_text, calls, total_time, mean_time, max_time,
       start_time, end_time
FROM query_store.qs_view
ORDER BY mean_time DESC
LIMIT 20;

-- Find queries with high I/O waits
SELECT qs.query_sql_text, qs.calls, qs.mean_time,
       qs.shared_blks_read, qs.shared_blks_hit,
       100.0 * qs.shared_blks_hit / NULLIF(qs.shared_blks_hit + qs.shared_blks_read, 0) AS cache_hit_ratio
FROM query_store.qs_view qs
WHERE qs.shared_blks_read + qs.shared_blks_hit > 0
ORDER BY qs.shared_blks_read DESC
LIMIT 10;
```

#### Using pg_stat_statements (if enabled)
```sql
-- Top queries by total time
SELECT query, calls, total_time, mean_time,
       100.0 * total_time / sum(total_time) OVER() AS percentage
FROM pg_stat_statements
ORDER BY total_time DESC
LIMIT 20;
```

### 3. Examine database locks and blocking
Check for locking issues:

```sql
-- Current locks
SELECT blocked_locks.pid AS blocked_pid,
       blocked_activity.usename AS blocked_user,
       blocking_locks.pid AS blocking_pid,
       blocking_activity.usename AS blocking_user,
       blocked_activity.query AS blocked_statement,
       blocking_activity.query AS current_statement_in_blocking_process
FROM pg_catalog.pg_locks blocked_locks
JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid
JOIN pg_catalog.pg_locks blocking_locks ON blocking_locks.locktype = blocked_locks.locktype
JOIN pg_catalog.pg_stat_activity blocking_activity ON blocking_activity.pid = blocking_locks.pid
WHERE NOT blocked_locks.granted;
```

### 4. Review index usage and efficiency
Analyze index performance:

```sql
-- Unused indexes
SELECT schemaname, tablename, indexname, idx_scan
FROM pg_stat_user_indexes
WHERE idx_scan = 0;

-- Index hit ratio
SELECT schemaname, tablename,
       100 * idx_blks_hit / (idx_blks_hit + idx_blks_read) AS index_hit_ratio
FROM pg_statio_user_indexes
WHERE idx_blks_hit + idx_blks_read > 0;
```

### 5. Check table statistics and maintenance
Review table health:

```sql
-- Table sizes and row counts
SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(relid)) AS size,
       n_tup_ins, n_tup_upd, n_tup_del, n_live_tup, n_dead_tup,
       last_vacuum, last_autovacuum, last_analyze, last_autoanalyze
FROM pg_stat_user_tables
ORDER BY pg_total_relation_size(relid) DESC;
```

### 6. Analyze wait events and bottlenecks
Identify system bottlenecks:

#### Using Query Store Wait Statistics (if enabled)
```sql
-- Top wait events by frequency
SELECT event_type, event, calls,
       start_time, end_time
FROM query_store.pgms_wait_sampling_view
ORDER BY calls DESC
LIMIT 20;

-- Wait events for specific queries
SELECT qs.query_sql_text, ws.event_type, ws.event, ws.calls
FROM query_store.qs_view qs
JOIN query_store.pgms_wait_sampling_view ws ON qs.query_id = ws.query_id
WHERE ws.calls > 10
ORDER BY ws.calls DESC;
```

#### Using Azure Monitor
- Review wait statistics in Performance Insights
- Check for I/O bottlenecks
- Monitor lock wait times

#### Database-level analysis
```sql
-- Current activity
SELECT pid, usename, application_name, client_addr, state,
       query_start, state_change, query
FROM pg_stat_activity
WHERE state != 'idle';
```

### 7. Generate performance improvement recommendations
Based on the analysis, provide recommendations:

#### Query Optimization
- Identify queries that need index improvements
- Suggest query rewrites for better performance
- Recommend parameterization for repeated queries

#### Index Recommendations
- Missing indexes for frequent queries
- Redundant indexes that can be removed
- Composite index opportunities

#### Configuration Tuning
- Connection pool sizing
- Memory allocation adjustments
- Maintenance operation scheduling

### 8. Create action plan and monitoring strategy
Develop a follow-up plan:

1. **Immediate Actions**: Critical performance issues
2. **Short-term Improvements**: Quick wins (1-2 weeks)
3. **Long-term Optimizations**: Architectural changes
4. **Monitoring Setup**: Ongoing performance tracking

## Key Performance Indicators to Monitor

### Database Level
- CPU utilization (target: <80%)
- Memory usage (target: <85%)
- Active connections (target: <80% of max)
- Query execution time trends

### Query Level
- Slow query count and duration
- Index hit ratio (target: >95%)
- Lock wait times
- Buffer cache hit ratio

## Summary
This comprehensive workflow helps identify and resolve PostgreSQL performance issues through systematic analysis of metrics, queries, and database health indicators.

## Category
Performance

## Tags
- performance
- cpu
- memory
- queries
