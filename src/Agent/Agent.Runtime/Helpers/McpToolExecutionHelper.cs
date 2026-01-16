// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Helpers;

/// <summary>
/// Helper class for creating and parsing MCP tool execution objects.
/// </summary>
public static class McpToolExecutionHelper
{
    /// <summary>
    /// Creates an McpToolExecution object from a function call.
    /// </summary>
    public static McpToolExecution CreateFromFunctionCall(FunctionCallContent functionCall)
    {
        var (mcpServerName, toolName, displayName) = ParseMcpToolName(functionCall.Name);

        return new McpToolExecution
        {
            Id = Guid.NewGuid(),
            FullToolName = functionCall.Name,
            McpServerName = mcpServerName,
            ToolName = toolName,
            DisplayName = displayName,
            Status = McpToolExecutionStatus.Running,
            StartedAt = DateTime.UtcNow,
            Parameters = ExtractParameters(toolName, functionCall.Arguments)
        };
    }

    /// <summary>
    /// Parses an MCP tool name into its components.
    /// Format: "{mcp-server-name}_{tool_name}" (e.g., "kusto-mcp_kusto_query")
    /// </summary>
    public static (string McpServerName, string ToolName, string DisplayName) ParseMcpToolName(string fullToolName)
    {
        var firstUnderscoreIndex = fullToolName.IndexOf('_');

        if (firstUnderscoreIndex == -1)
        {
            return (fullToolName, fullToolName, FormatDisplayName(fullToolName));
        }

        var mcpServerName = fullToolName[..firstUnderscoreIndex];
        var toolName = fullToolName[(firstUnderscoreIndex + 1)..];

        return (mcpServerName, toolName, FormatDisplayName(toolName));
    }

    /// <summary>
    /// Checks if a tool name matches the MCP naming pattern.
    /// </summary>
    public static bool IsMcpTool(string toolName)
    {
        // MCP tools have format: {server-name}_{tool_name}
        // Common MCP server prefixes
        var mcpPrefixes = new[]
        {
            "kusto-mcp_",
            "ado-mcp_",
        };

        return mcpPrefixes.Any(prefix => toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Formats a tool name into a user-friendly display name.
    /// e.g., "kusto_query" -> "Kusto Query"
    /// </summary>
    private static string FormatDisplayName(string toolName)
    {
        return string.Join(' ', toolName
            .Replace('_', ' ')
            .Replace('-', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));
    }

    /// <summary>
    /// Extracts structured parameters from a function call's arguments.
    /// </summary>
    private static McpToolParameters ExtractParameters(string toolName, IDictionary<string, object?>? arguments)
    {
        if (arguments == null)
        {
            return new McpToolParameters();
        }

        var parameters = new McpToolParameters
        {
            Raw = arguments.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        };

        // Extract Kusto-specific parameters
        if (toolName.Contains("kusto", StringComparison.OrdinalIgnoreCase))
        {
            parameters.ClusterUrl = GetStringValue(arguments, "cluster_url", "clusterUrl", "cluster");
            parameters.Database = GetStringValue(arguments, "database", "db", "database_name");
            parameters.Query = GetStringValue(arguments, "query", "kql", "kusto_query");
            parameters.TableName = GetStringValue(arguments, "table", "table_name", "tableName");
            parameters.SampleSize = GetIntValue(arguments, "sample_size", "sampleSize", "count");
        }

        // Extract ADO-specific parameters
        if (toolName.Contains("ado", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("azure-devops", StringComparison.OrdinalIgnoreCase) ||
            toolName.Contains("pull_request", StringComparison.OrdinalIgnoreCase))
        {
            parameters.Repository = GetStringValue(arguments, "repository", "repo", "repository_name");
            parameters.SourceBranch = GetStringValue(arguments, "source_branch", "sourceBranch", "from_branch");
            parameters.TargetBranch = GetStringValue(arguments, "target_branch", "targetBranch", "to_branch");
            parameters.Title = GetStringValue(arguments, "title", "pr_title");
            parameters.Description = GetStringValue(arguments, "description", "pr_description", "body");
        }

        return parameters;
    }

    /// <summary>
    /// Gets a string value from arguments, trying multiple possible key names.
    /// </summary>
    private static string? GetStringValue(IDictionary<string, object?> arguments, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (arguments.TryGetValue(key, out var value) && value != null)
            {
                return value switch
                {
                    string s => s,
                    JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString(),
                    _ => value.ToString()
                };
            }
        }
        return null;
    }

    /// <summary>
    /// Gets an int value from arguments, trying multiple possible key names.
    /// </summary>
    private static int? GetIntValue(IDictionary<string, object?> arguments, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (arguments.TryGetValue(key, out var value) && value != null)
            {
                return value switch
                {
                    int i => i,
                    long l => (int)l,
                    JsonElement je when je.ValueKind == JsonValueKind.Number => je.GetInt32(),
                    string s when int.TryParse(s, out var parsed) => parsed,
                    _ => null
                };
            }
        }
        return null;
    }
}
