// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents an MCP (Model Context Protocol) tool execution with its parameters and status.
/// </summary>
public class McpToolExecution
{
    /// <summary>
    /// Unique identifier for this execution.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The full tool name (e.g., "kusto-mcp_kusto_query").
    /// </summary>
    public string FullToolName { get; set; } = string.Empty;

    /// <summary>
    /// The MCP server name (e.g., "kusto-mcp").
    /// </summary>
    public string McpServerName { get; set; } = string.Empty;

    /// <summary>
    /// The tool name without the MCP prefix (e.g., "kusto_query").
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name for the tool (e.g., "Kusto Query").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Current execution status.
    /// </summary>
    public McpToolExecutionStatus Status { get; set; } = McpToolExecutionStatus.Running;

    /// <summary>
    /// When the execution started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the execution completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Structured parameters for the tool execution.
    /// </summary>
    public McpToolParameters? Parameters { get; set; }

    /// <summary>
    /// The result of the execution (if completed).
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Status of an MCP tool execution.
/// </summary>
public enum McpToolExecutionStatus
{
    /// <summary>
    /// Tool is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Tool completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Tool execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Tool execution was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Parameters for an MCP tool execution, structured by tool type.
/// </summary>
public class McpToolParameters
{
    // Common parameters
    /// <summary>
    /// Raw parameters dictionary for any tool.
    /// </summary>
    public Dictionary<string, object?>? Raw { get; set; }

    // Kusto-specific parameters
    /// <summary>
    /// Kusto cluster URL (for Kusto tools).
    /// </summary>
    public string? ClusterUrl { get; set; }

    /// <summary>
    /// Kusto database name (for Kusto tools).
    /// </summary>
    public string? Database { get; set; }

    /// <summary>
    /// KQL query (for kusto_query tool).
    /// </summary>
    public string? Query { get; set; }

    /// <summary>
    /// Table name (for kusto_sample, kusto_table_schema tools).
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Sample size (for kusto_sample tool).
    /// </summary>
    public int? SampleSize { get; set; }

    // ADO-specific parameters
    /// <summary>
    /// Repository name (for ADO tools).
    /// </summary>
    public string? Repository { get; set; }

    /// <summary>
    /// Source branch (for PR creation).
    /// </summary>
    public string? SourceBranch { get; set; }

    /// <summary>
    /// Target branch (for PR creation).
    /// </summary>
    public string? TargetBranch { get; set; }

    /// <summary>
    /// Pull request title.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Pull request description.
    /// </summary>
    public string? Description { get; set; }
}
