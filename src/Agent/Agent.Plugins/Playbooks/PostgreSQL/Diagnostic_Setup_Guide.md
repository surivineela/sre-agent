# Diagnostic Setup Guide

## Description
Complete setup guide for PostgreSQL diagnostics

## Prerequisites
- Log Analytics workspace available
- Appropriate permissions to modify server parameters
- Understanding of performance impact of logging

## Estimated Time
45-90 minutes

## Steps

### 1. Enable diagnostic settings to send logs to Log Analytics workspace
Configure Azure PostgreSQL to send diagnostic data:

1. Navigate to your PostgreSQL server in the Azure portal
2. Go to **Monitoring** > **Diagnostic settings**
3. Click **Add diagnostic setting**
4. Configure the following logs:
   - PostgreSQL Server Logs
   - Query Store Runtime Statistics
   - Query Store Wait Statistics

### 2. Configure Query Store for query performance tracking

> **⚠️ Important**: Do not enable Query Store on Burstable pricing tier as it would cause performance impact.

Query Store is an opt-in feature that tracks query performance over time. Configure it in the Azure portal:

**Step 2.1: Enable Query Store**
1. Navigate to your PostgreSQL server in the Azure portal
2. Go to **Settings** > **Server parameters**
3. Search for `pg_qs.query_capture_mode`
4. Set the value to:
   - `top` - to track top-level queries only
   - `all` - to track all queries including nested ones (execute inside functions/procedures)
5. Click **Save**

**Step 2.2: Configure Query Store Parameters**
Set additional Query Store parameters as needed:

```sql
-- Core Query Store settings
ALTER SYSTEM SET pg_qs.query_capture_mode = 'top';  -- or 'all'
ALTER SYSTEM SET pg_qs.max_query_text_length = 6000;
ALTER SYSTEM SET pg_qs.retention_period_in_days = 7;
ALTER SYSTEM SET pg_qs.interval_length_minutes = 15;  -- Data aggregation window

-- Optional: Enable query plan storage
ALTER SYSTEM SET pg_qs.store_query_plans = 'on';

-- Query parameters capture
ALTER SYSTEM SET pg_qs.parameters_capture_mode = 'capture_parameterless_only';
```

**Step 2.3: Enable Wait Sampling**
For wait statistics, configure wait sampling:

1. Search for `pgms_wait_sampling.query_capture_mode` parameter
2. Set the value to `all`
3. Optionally adjust `pgms_wait_sampling.history_period` (default: 100ms)

```sql
-- Enable wait event sampling
ALTER SYSTEM SET pgms_wait_sampling.query_capture_mode = 'all';
ALTER SYSTEM SET pgms_wait_sampling.history_period = 100;  -- milliseconds
```

**Step 2.4: Apply Changes**
Restart the PostgreSQL server for parameter changes to take effect.

> **Note**: Allow up to 20 minutes for the first batch of data to persist in the `azure_sys` database.

### 3. Enable Performance Insights for query-level metrics
In the Azure portal:
1. Go to **Monitoring** > **Performance Insights**
2. Enable Performance Insights
3. Configure retention period (1-7 days)

### 4. Set up connection logging parameters
Configure connection and authentication logging:

```sql
-- Enable connection logging
ALTER SYSTEM SET log_connections = 'on';
ALTER SYSTEM SET log_disconnections = 'on';
ALTER SYSTEM SET log_hostname = 'on';

-- Configure authentication logging
ALTER SYSTEM SET log_statement = 'ddl';  -- or 'all' for comprehensive logging
```

### 5. Configure log_statement and log_duration parameters
Set up query logging based on your monitoring needs:

```sql
-- Log slow queries (adjust threshold as needed)
ALTER SYSTEM SET log_min_duration_statement = 1000;  -- 1 second

-- Log all statements (use carefully in production)
-- ALTER SYSTEM SET log_statement = 'all';

-- Enable query duration logging
ALTER SYSTEM SET log_duration = 'on';
```

### 6. Validate diagnostic data collection
After configuration, verify data collection:

1. **Check Query Store data**:
```sql
-- Query runtime statistics
SELECT * FROM query_store.qs_view LIMIT 5;

-- Wait statistics (if wait sampling enabled)
SELECT * FROM query_store.pgms_wait_sampling_view LIMIT 5;

-- Query plans (if query plan storage enabled)
SELECT * FROM query_store.query_plans_view LIMIT 5;
```

2. **Verify diagnostic data**:
```sql
-- Check if Query Store is collecting data
SELECT
    query_sql_text,
    calls,
    total_time,
    mean_time,
    start_time,
    end_time
FROM query_store.qs_view
WHERE calls > 0
ORDER BY total_time DESC
LIMIT 10;
```

3. **Verify Log Analytics data**:
   - Navigate to your Log Analytics workspace
   - Query AzureDiagnostics table for PostgreSQL data

4. **Test Performance Insights**:
   - Execute some queries
   - Check Performance Insights dashboard for data

### 7. Query Store Management Functions (Optional)
Query Store provides administrative functions for data management:

```sql
-- Reset all Query Store data (use with caution)
-- Only members of azure_pg_admin role can execute this
SELECT query_store.qs_reset();

-- Reset only in-memory staging data
SELECT query_store.staging_data_reset();
```

> **Note**: These functions should only be used when you need to clear Query Store data completely.

## Important Considerations

### Query Store Specific Notes
- **Do NOT enable Query Store on Burstable pricing tier** due to performance impact
- Query Store data is stored in the `azure_sys` database
- Up to 500 distinct queries per 15-minute window are stored
- Queries are normalized - similar queries with different literals share the same `query_id`
- Query Store operates in read-only mode when server storage is full
- On read replicas, Query Store doesn't record queries due to read-only mode

### Parameter Guidelines
- `pg_qs.interval_length_minutes`: Aggregation window (1-30 minutes, default 15)
- `pg_qs.retention_period_in_days`: Data retention (1-30 days, default 7)
- `pg_qs.max_query_text_length`: Maximum query text length (100-10000 chars, default 6000)
- Static parameters like `pg_qs.interval_length_minutes` require server restart

### Performance Impact
- Extensive logging can impact performance
- Start with minimal logging and gradually increase
- Monitor server performance after enabling diagnostics
- Query Store adds minimal overhead when properly configured

### Storage Costs
- Diagnostic logs consume storage in Log Analytics
- Configure appropriate retention periods
- Monitor costs and adjust as needed

### Security
- Log files may contain sensitive data
- Ensure proper access controls
- Consider data privacy requirements

## Summary
Comprehensive guide to set up full diagnostic capabilities for PostgreSQL monitoring. Proper diagnostic setup enables proactive monitoring and faster troubleshooting of database issues.

## Category
Configuration

## Tags
- setup
- monitoring
- diagnostics
