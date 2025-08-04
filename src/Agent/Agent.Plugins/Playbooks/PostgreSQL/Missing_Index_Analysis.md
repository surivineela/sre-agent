# Missing Index Analysis

## Description
Step-by-step guide to identify and create missing indexes

## Prerequisites
- Query Store enabled or pg_stat_statements configured
- Sufficient disk space for index creation
- Maintenance window identified for index creation

## Estimated Time
30-60 minutes

## Steps

### 1. Identify slow queries via pg_stat_statements or Query Store
Use the following queries to identify slow-running queries that may benefit from indexes:

```sql
-- For pg_stat_statements (if enabled)
SELECT query, calls, total_time, mean_time, rows
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 10;
```

### 2. Analyze execution plans for sequential scans on large tables
For each slow query identified, analyze the execution plan:

```sql
EXPLAIN (ANALYZE, BUFFERS) <your_slow_query>;
```

Look for:
- Sequential scans (Seq Scan) on large tables
- High cost operations
- Tables with many rows being scanned

### 3. Calculate potential performance impact of proposed indexes
Before creating indexes, estimate their impact:
- Consider the selectivity of the columns
- Evaluate the size of the potential index
- Check if existing indexes can be modified instead

### 4. Create indexes using CONCURRENTLY option to avoid locking
When creating indexes on production systems:

```sql
CREATE INDEX CONCURRENTLY idx_table_column ON table_name (column_name);
```

**Important**: Monitor the creation process and ensure it completes successfully.

### 5. Monitor post-implementation performance improvements
After index creation:
- Re-run the slow queries and compare execution times
- Monitor overall database performance
- Check index usage statistics

### 6. Validate that queries are using the new indexes
Verify index usage:

```sql
-- Check index usage
SELECT schemaname, tablename, indexname, idx_scan, idx_tup_read, idx_tup_fetch
FROM pg_stat_user_indexes
WHERE indexname = 'your_index_name';
```

## Summary
This playbook helps identify missing indexes causing performance issues and provides safe index creation procedures. Regular monitoring of query performance and index usage ensures optimal database performance.

## Category
Performance

## Tags
- indexes
- slow-queries
- performance
