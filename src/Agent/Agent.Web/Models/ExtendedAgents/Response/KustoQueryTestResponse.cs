// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Web.Models.ExtendedAgents.Response;

/// <summary>
/// Response model for Kusto query test results
/// </summary>
public class KustoQueryTestResponse
{
    /// <summary>
    /// Whether the query executed successfully
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of rows returned (max 50)
    /// </summary>
    public int RowCount { get; set; }

    /// <summary>
    /// Column names in the result set
    /// </summary>
    public List<string> Columns { get; set; } = new();

    /// <summary>
    /// Result rows as dictionaries (column name -> value)
    /// </summary>
    public List<Dictionary<string, object>> Rows { get; set; } = new();

    /// <summary>
    /// Query execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// The actual query executed (with parameter substitution and limit applied)
    /// </summary>
    public string QueryExecuted { get; set; } = string.Empty;

    /// <summary>
    /// Error message if Success is false
    /// </summary>
    public string? ErrorMessage { get; set; }
}
