// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using Agent.Framework;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Plugin implementation for dynamically invoking tools from the aggregate tool registry.
    /// Each instance manages tools for a specific aggregate type.
    /// Automatically builds aggregated description when tools are registered.
    /// </summary>
    public class AggregateToolCallPlugin : IAggregateToolCallPlugin
    {
        private readonly string _aggregateType;
        private readonly ConcurrentDictionary<string, object> _tools = new();
        private readonly Dictionary<string, string> _toolDescriptions = new(); // Maps tool name to its description

        public AggregateToolCallPlugin(string aggregateType)
        {
            if (string.IsNullOrWhiteSpace(aggregateType))
            {
                throw new ArgumentException("Aggregate type cannot be null or whitespace.", nameof(aggregateType));
            }
            _aggregateType = aggregateType;
        }

        /// <summary>
        /// Gets the aggregated description of all registered tools.
        /// </summary>
        public string GetAggregatedDescription()
        {
            if (_toolDescriptions.Count == 0)
            {
                return string.Empty;
            }

            var description = new System.Text.StringBuilder();
            description.Append($"=== AVAILABLE {_aggregateType.ToUpperInvariant()} === ");

            int toolNumber = 1;
            foreach (var toolDesc in _toolDescriptions.Values)
            {
                // Add tool number prefix and separator
                if (toolNumber > 1)
                {
                    description.Append(" | ");
                }
                description.Append($"{toolNumber}. {toolDesc}");
                toolNumber++;
            }

            return description.ToString();
        }

        /// <summary>
        /// Updates the stored description for a tool.
        /// </summary>
        private void UpdateToolDescription<TContext>(string toolName, IDeferredToolFunction<TContext> toolFunction) where TContext : class
        {
            try
            {
                var aiFunction = toolFunction.GetToolFunction();

                // Build and store the tool's description
                var toolDesc = BuildSingleToolDescription(aiFunction, toolName);
                _toolDescriptions[toolName] = toolDesc;
            }
            catch (Exception)
            {
                // If we fail to get the tool description, don't update
            }
        }

        /// <summary>
        /// Builds the description for a single tool.
        /// </summary>
        private string BuildSingleToolDescription(AIFunction aiFunction, string toolName)
        {
            var parts = new List<string>();

            parts.Add($"{aiFunction.Name}");

            if (!string.IsNullOrWhiteSpace(aiFunction.Description))
            {
                parts.Add($"Description: {aiFunction.Description}");
            }

            // Get parameters from the JSON schema
            try
            {
                var jsonSchema = aiFunction.JsonSchema;
                if (jsonSchema.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (jsonSchema.TryGetProperty("properties", out var properties) &&
                        properties.ValueKind == System.Text.Json.JsonValueKind.Object)
                    {
                        var requiredParams = new HashSet<string>();
                        if (jsonSchema.TryGetProperty("required", out var required) &&
                            required.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (var req in required.EnumerateArray())
                            {
                                if (req.ValueKind == System.Text.Json.JsonValueKind.String)
                                {
                                    requiredParams.Add(req.GetString() ?? string.Empty);
                                }
                            }
                        }

                        var paramList = new List<string>();
                        foreach (var prop in properties.EnumerateObject())
                        {
                            var paramName = prop.Name;
                            string? paramDescription = null;

                            if (prop.Value.TryGetProperty("description", out var descElement) &&
                                descElement.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                paramDescription = descElement.GetString();
                            }

                            var isRequired = requiredParams.Contains(paramName);
                            var optional = isRequired ? "(required)" : "(optional)";

                            if (!string.IsNullOrWhiteSpace(paramDescription))
                            {
                                paramList.Add($"{paramName}{optional}: {paramDescription}");
                            }
                            else
                            {
                                paramList.Add($"{paramName}{optional}");
                            }
                        }

                        if (paramList.Count > 0)
                        {
                            parts.Add($"Input Parameters: {string.Join(" - ", paramList)}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If we fail to parse JSON schema, skip parameters section
            }

            // Join all parts with " - " separator, no newlines
            return string.Join(" ", parts);
        }

        /// <summary>
        /// Registers a tool with this aggregate type and updates the aggregated description.
        /// </summary>
        public void RegisterTool<TContext>(string aggregateType, string toolName, IDeferredToolFunction<TContext> toolFunction) where TContext : class
        {
            // Validate that the aggregate type matches this plugin's type
            if (!string.Equals(aggregateType, _aggregateType, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"This plugin is for aggregate type '{_aggregateType}', but attempted to register tool for '{aggregateType}'.", nameof(aggregateType));
            }

            if (string.IsNullOrWhiteSpace(toolName))
            {
                throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(toolName));
            }

            if (toolFunction == null)
            {
                throw new ArgumentNullException(nameof(toolFunction));
            }

            _tools[toolName] = toolFunction;

            // Store the tool's description
            UpdateToolDescription(toolName, toolFunction);
        }

        /// <summary>
        /// Invokes a tool/method by name with the provided arguments.
        /// </summary>
        /// <param name="methodName">The name of the tool/method to invoke.</param>
        /// <param name="arguments">JSON string containing the arguments as key-value pairs.</param>
        /// <returns>A JSON string containing the result or error information.</returns>
        public async Task<string> CallToolFunctionAsync(string methodName, string arguments)
        {
            try
            {
                // Find the tool in this aggregate type's registry
                if (!_tools.TryGetValue(methodName, out var toolFunctionObj))
                {
                    return JsonSerializer.Serialize(new
                    {
                        Success = false,
                        Error = $"Tool '{methodName}' not found in aggregate type '{_aggregateType}'."
                    });
                }

                // Parse the arguments JSON
                Dictionary<string, object?>? argsDictionary;
                try
                {
                    argsDictionary = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments);
                    if (argsDictionary == null)
                    {
                        return JsonSerializer.Serialize(new
                        {
                            Success = false,
                            Error = "Failed to parse arguments JSON. Arguments cannot be null."
                        });
                    }
                }
                catch (JsonException ex)
                {
                    return JsonSerializer.Serialize(new
                    {
                        Success = false,
                        Error = $"Invalid JSON format for arguments: {ex.Message}"
                    });
                }

                // Get the AIFunction from the deferred tool function using reflection
                // Since we don't know the exact TContext at runtime, we use reflection to call GetToolFunction
                var toolFunctionType = toolFunctionObj.GetType();
                var getToolFunctionMethod = toolFunctionType.GetMethod("GetToolFunction", new[] { typeof(Guid?), typeof(Agent.Framework.Agent<>).MakeGenericType(toolFunctionType.GetGenericArguments()[0]) });

                if (getToolFunctionMethod == null)
                {
                    // Try the simpler overload
                    getToolFunctionMethod = toolFunctionType.GetMethod("GetToolFunction", Type.EmptyTypes);
                }

                AIFunction? aiFunction = null;
                if (getToolFunctionMethod != null)
                {
                    aiFunction = getToolFunctionMethod.Invoke(toolFunctionObj, new object?[] { null, null }) as AIFunction;
                }

                if (aiFunction == null)
                {
                    return JsonSerializer.Serialize(new
                    {
                        Success = false,
                        Error = $"Failed to get AIFunction for tool '{methodName}'."
                    });
                }

                // Create AIFunctionArguments from the dictionary
                var aiFunctionArgs = new AIFunctionArguments(argsDictionary);

                // Invoke the function
                var result = await aiFunction.InvokeAsync(aiFunctionArgs);

                // Return the result as JSON
                return JsonSerializer.Serialize(new
                {
                    Success = true,
                    MethodName = methodName,
                    Result = result
                });
            }
            catch (KeyNotFoundException ex)
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = $"Tool '{methodName}' not found: {ex.Message}"
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    Success = false,
                    Error = $"Error invoking tool '{methodName}': {ex.Message}",
                    ExceptionType = ex.GetType().Name
                });
            }
        }
    }
}
