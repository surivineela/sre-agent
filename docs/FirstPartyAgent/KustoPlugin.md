# KustoPlugin Documentation

## Overview

The `KustoPlugin` provides a simple interface for executing Kusto queries on a specified cluster and database. It is designed to be used in automated agents and exposes a single kernel function for executing fully qualified Kusto queries, returning results in JSON format.

---

## Kernel Functions

### execute_kusto_query_on_cluster

Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.

**Signature:**
```csharp
[KernelFunction("execute_kusto_query_on_cluster")] public async Task<string> ExecuteClusterKustoQuery( string cluster, string database, string fullQuery, DateTime? NowOverride, Kernel kernel )
```

**Description:**  
Executes the provided Kusto query on the specified cluster and database. Returns the query result as a JSON string. If no rows are returned, the string `"ZERO_ROWS_RETURNED"` is returned. If an error occurs, a failure message is returned.

#### Parameters

- `cluster` (string):  
  The short name of the target Kusto cluster (without URL schema or suffix).  
  *Example: "wawsprod"*

- `database` (string):  
  The name of the target Kusto database.  
  *Example: "wawsprod"*

- `fullQuery` (string):  
  The full Kusto query to execute.  
  *Example: "StormEvents | take 10"*

- `NowOverride` (DateTime?, optional):  
  An optional override for the current time, used for time-based queries.  
  *Example: DateTime.UtcNow*

- `kernel` (Kernel):  
  The Semantic Kernel context (used internally for logging and context; not required from user input).

---