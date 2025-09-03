using System.Text.Json;

namespace Agent.Cli.Services;

/// <summary>
/// Shared utility for parsing tool responses from various API endpoints.
/// Handles different response structures in a consistent way.
/// </summary>
public static class ToolResponseParser
{
    /// <summary>
    /// Parses tool elements from a JSON response, handling various response structures.
    /// </summary>
    /// <param name="jsonResponse">The raw JSON response string.</param>
    /// <returns>Array of tool JsonElements, or empty array if parsing fails.</returns>
    public static JsonElement[] ParseToolElements(string jsonResponse)
    {
        if (string.IsNullOrEmpty(jsonResponse))
        {
            return Array.Empty<JsonElement>();
        }

        try
        {
            var jsonDoc = JsonDocument.Parse(jsonResponse);
            
            // Check the type of the root element first
            if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Direct array structure: [...]
                return jsonDoc.RootElement.EnumerateArray().ToArray();
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Try different nested structure patterns
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    // Pattern 1: { "data": { "tools": [...] } } - Full nested structure
                    if (dataElement.ValueKind == JsonValueKind.Object && 
                        dataElement.TryGetProperty("tools", out var toolsElement) &&
                        toolsElement.ValueKind == JsonValueKind.Array)
                    {
                        return toolsElement.EnumerateArray().ToArray();
                    }
                    // Pattern 2: { "data": [...] } - Data is direct array
                    else if (dataElement.ValueKind == JsonValueKind.Array)
                    {
                        return dataElement.EnumerateArray().ToArray();
                    }
                }
                // Pattern 3: { "tools": [...] } - Direct tools property
                else if (jsonDoc.RootElement.TryGetProperty("tools", out var directToolsElement) && 
                         directToolsElement.ValueKind == JsonValueKind.Array)
                {
                    return directToolsElement.EnumerateArray().ToArray();
                }
            }
        }
        catch (JsonException)
        {
            // Failed to parse as JSON, return empty array
        }

        return Array.Empty<JsonElement>();
    }

    /// <summary>
    /// Extracts tool names from an array of tool JsonElements.
    /// </summary>
    /// <param name="toolElements">Array of tool JsonElements.</param>
    /// <returns>HashSet of tool names.</returns>
    public static HashSet<string> ExtractToolNames(JsonElement[] toolElements)
    {
        var toolNames = new HashSet<string>();
        
        foreach (var tool in toolElements)
        {
            if (tool.TryGetProperty("name", out var nameElement))
            {
                var name = nameElement.GetString();
                if (!string.IsNullOrEmpty(name))
                {
                    toolNames.Add(name);
                }
            }
        }

        return toolNames;
    }

    /// <summary>
    /// Extracts tool names from a JSON response.
    /// Convenience method that combines ParseToolElements and ExtractToolNames.
    /// </summary>
    /// <param name="jsonResponse">The raw JSON response string.</param>
    /// <returns>HashSet of tool names.</returns>
    public static HashSet<string> ParseToolNames(string jsonResponse)
    {
        var toolElements = ParseToolElements(jsonResponse);
        return ExtractToolNames(toolElements);
    }

    /// <summary>
    /// Extracts detailed tool information for display purposes using ConsoleUI formatting.
    /// </summary>
    /// <param name="toolElements">Array of tool JsonElements.</param>
    /// <returns>List of formatted tool information strings.</returns>
    public static List<string> ExtractToolDisplayInfo(JsonElement[] toolElements)
    {
        var toolInfo = new List<string>();
        
        foreach (var tool in toolElements)
        {
            var name = tool.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Unknown" : "Unknown";
            var category = tool.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? "" : "";
            var description = tool.TryGetProperty("description", out var descElement) ? descElement.GetString() ?? "" : "";
            var pluginName = tool.TryGetProperty("pluginName", out var pluginElement) ? pluginElement.GetString() ?? "" : "";
            
            var output = Helpers.ConsoleUI.CaptureOutput(() =>
            {
                Console.WriteLine();
                Helpers.ConsoleUI.WriteBullet(name, ConsoleColor.White, 0);
                
                if (!string.IsNullOrEmpty(category))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Category", category, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                if (!string.IsNullOrEmpty(description))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Description", description, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                if (!string.IsNullOrEmpty(pluginName))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Plugin", pluginName, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                
                // Get parameters
                if (tool.TryGetProperty("parameters", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Array)
                {
                    var parameters = paramsElement.EnumerateArray().Select(p => p.GetString()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                    if (parameters.Any())
                    {
                        Helpers.ConsoleUI.WriteKeyValue("  Parameters", string.Join(", ", parameters), 13, ConsoleColor.Gray, ConsoleColor.White);
                    }
                    else
                    {
                        Helpers.ConsoleUI.WriteKeyValue("  Parameters", "None", 13, ConsoleColor.Gray, ConsoleColor.White);
                    }
                }
            });
            toolInfo.Add(output);
        }

        return toolInfo;
    }

    /// <summary>
    /// Extracts detailed extended tool information for display purposes using ConsoleUI formatting.
    /// Extended tools have different properties than regular tools.
    /// </summary>
    /// <param name="toolElements">Array of extended tool JsonElements.</param>
    /// <returns>List of formatted extended tool information strings.</returns>
    public static List<string> ExtractExtendedToolDisplayInfo(JsonElement[] toolElements)
    {
        var toolInfo = new List<string>();
        
        foreach (var tool in toolElements)
        {
            var name = tool.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Unknown" : "Unknown";
            var description = tool.TryGetProperty("description", out var descElement) ? descElement.GetString() ?? "" : "";
            var type = tool.TryGetProperty("type", out var typeElement) ? typeElement.GetString() ?? "" : "";
            var createdAt = tool.TryGetProperty("created_at", out var createdElement) ? createdElement.GetString() ?? "" : "";
            var updatedAt = tool.TryGetProperty("updated_at", out var updatedElement) ? updatedElement.GetString() ?? "" : "";
            
            var output = Helpers.ConsoleUI.CaptureOutput(() =>
            {
                Console.WriteLine();
                Helpers.ConsoleUI.WriteBullet(name, ConsoleColor.White, 0);
                
                if (!string.IsNullOrEmpty(description))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Description", description, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                if (!string.IsNullOrEmpty(type))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Type", type, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                if (!string.IsNullOrEmpty(createdAt))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Created", createdAt, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                if (!string.IsNullOrEmpty(updatedAt))
                {
                    Helpers.ConsoleUI.WriteKeyValue("  Updated", updatedAt, 13, ConsoleColor.Gray, ConsoleColor.White);
                }
                
                // Get parameters if available
                if (tool.TryGetProperty("parameters", out var paramsElement))
                {
                    if (paramsElement.ValueKind == JsonValueKind.Array)
                    {
                        var parameters = new List<string>();
                        foreach (var param in paramsElement.EnumerateArray())
                        {
                            if (param.ValueKind == JsonValueKind.String)
                            {
                                var paramStr = param.GetString();
                                if (!string.IsNullOrEmpty(paramStr))
                                {
                                    parameters.Add(paramStr);
                                }
                            }
                            else if (param.ValueKind == JsonValueKind.Object)
                            {
                                // Handle parameter object structure - extract the name
                                var paramName = param.TryGetProperty("name", out var paramNameElement) ? paramNameElement.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(paramName))
                                {
                                    parameters.Add(paramName);
                                }
                            }
                        }
                        if (parameters.Any())
                        {
                            Helpers.ConsoleUI.WriteKeyValue("  Parameters", string.Join(", ", parameters), 13, ConsoleColor.Gray, ConsoleColor.White);
                        }
                    }
                    else if (paramsElement.ValueKind == JsonValueKind.Object)
                    {
                        // Handle parameter object structure
                        var paramNames = new List<string>();
                        foreach (var param in paramsElement.EnumerateObject())
                        {
                            paramNames.Add(param.Name);
                        }
                        if (paramNames.Any())
                        {
                            Helpers.ConsoleUI.WriteKeyValue("  Parameters", string.Join(", ", paramNames), 13, ConsoleColor.Gray, ConsoleColor.White);
                        }
                    }
                }
                
                // Get connector info if available
                if (tool.TryGetProperty("connector", out var connectorElement))
                {
                    if (connectorElement.ValueKind == JsonValueKind.String)
                    {
                        var connectorType = connectorElement.GetString() ?? "";
                        if (!string.IsNullOrEmpty(connectorType))
                        {
                            Helpers.ConsoleUI.WriteKeyValue("  Connector", connectorType, 13, ConsoleColor.Gray, ConsoleColor.White);
                        }
                    }
                    else if (connectorElement.ValueKind == JsonValueKind.Object)
                    {
                        // Handle connector object structure
                        var connectorType = connectorElement.TryGetProperty("type", out var connectorTypeElement) ? connectorTypeElement.GetString() ?? "" : "";
                        if (!string.IsNullOrEmpty(connectorType))
                        {
                            Helpers.ConsoleUI.WriteKeyValue("  Connector", connectorType, 13, ConsoleColor.Gray, ConsoleColor.White);
                        }
                    }
                }
            });
            toolInfo.Add(output);
        }

        return toolInfo;
    }
}
