// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Tools;

/// <summary>
/// Non-generic interface for YAML tool executors.
/// Allows factories to return tools without generic type constraints.
/// </summary>
public interface IYamlToolExecutor
{
    Task<string> ExecuteAsync(string threadId, AIFunctionArguments arguments);
    JsonElement CreateSchema();
}

/// <summary>
/// Base class for all YAML tool executors.
/// Provides common functionality for schema generation and parameter handling.
/// </summary>
public abstract class YamlToolExecutor<TDefinition> : IYamlToolExecutor where TDefinition : YamlToolDefinitionBase
{
    protected readonly TDefinition ToolDefinition;

    protected YamlToolExecutor(TDefinition definition)
    {
        ToolDefinition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    /// <summary>
    /// Executes the tool with the provided arguments.
    /// All V2 tools must implement this method.
    /// </summary>
    /// <param name="threadId">The thread/conversation ID for context</param>
    /// <param name="arguments">Arguments from the LLM including tool parameters and metadata</param>
    /// <returns>Tool execution result as a string</returns>
    public abstract Task<string> ExecuteAsync(string threadId, AIFunctionArguments arguments);

    /// <summary>
    /// Creates a JSON schema for the tool based on YAML parameter definitions.
    /// Override this method if custom schema logic is needed.
    /// </summary>
    public virtual JsonElement CreateSchema()
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in ToolDefinition.Parameters)
        {
            // Map YAML types to JSON schema types
            var paramType = param.Type switch
            {
                "int" => "integer",
                "bool" => "boolean",
                "double" => "number",
                "array" => "array",
                _ => "string"
            };

            var propertySchema = new Dictionary<string, object>
            {
                ["type"] = paramType,
                ["description"] = param.Description ?? $"Parameter {param.Name}"
            };

            // Add additional schema information if needed
            if (paramType == "array")
            {
                propertySchema["items"] = new { type = "string" }; // Default array items to string
            }

            properties[param.Name] = propertySchema;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        var schema = new
        {
            type = "object",
            properties = properties,
            required = required.ToArray()
        };

        var json = JsonSerializer.Serialize(schema);
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Converts AIFunctionArguments to Dictionary&lt;string, string&gt; for internal processing.
    /// </summary>
    protected static Dictionary<string, string> ConvertToStringDictionary(AIFunctionArguments arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in arguments)
        {
            result[kvp.Key] = kvp.Value?.ToString() ?? string.Empty;
        }

        return result;
    }
}
