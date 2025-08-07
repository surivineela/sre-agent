using System.Text.Json;
using Agent.Cli.Services;

namespace Agent.Cli.Services;

/// <summary>
/// Service for checking tool availability from local and remote sources.
/// </summary>
public class ToolAvailabilityService
{
    private readonly ApiService _apiService;

    public ToolAvailabilityService(ApiService apiService)
    {
        _apiService = apiService;
    }

    /// <summary>
    /// Gets all available tools from local directory and remote server.
    /// </summary>
    /// <returns>A tuple containing local tools, remote tools, and any error messages.</returns>
    public async Task<(HashSet<string> LocalTools, HashSet<string> RemoteTools, List<string> Errors)> GetAvailableToolsAsync()
    {
        var localTools = GetLocalTools();
        var (remoteTools, remoteErrors) = await GetRemoteToolsAsync();
        
        return (localTools, remoteTools, remoteErrors);
    }

    /// <summary>
    /// Gets tools available in the local tools directory.
    /// </summary>
    /// <returns>Set of local tool names.</returns>
    public HashSet<string> GetLocalTools()
    {
        var localTools = new HashSet<string>();
        
        if (Directory.Exists("tools"))
        {
            // Check for tools in flat structure: tools/toolname.yaml
            var flatFiles = Directory.GetFiles("tools", "*.yaml", SearchOption.TopDirectoryOnly);
            foreach (var file in flatFiles)
            {
                var toolName = Path.GetFileNameWithoutExtension(file);
                localTools.Add(toolName);
            }

            // Check for tools in subdirectory structure: tools/toolname/toolname.yaml
            var subDirs = Directory.GetDirectories("tools");
            foreach (var subDir in subDirs)
            {
                var toolName = Path.GetFileName(subDir);
                var toolFile = Path.Combine(subDir, $"{toolName}.yaml");
                if (File.Exists(toolFile))
                {
                    localTools.Add(toolName);
                }
            }
        }

        return localTools;
    }

    /// <summary>
    /// Gets tools available from the remote server.
    /// </summary>
    /// <returns>A tuple containing remote tools and any error messages.</returns>
    public async Task<(HashSet<string> RemoteTools, List<string> Errors)> GetRemoteToolsAsync()
    {
        var remoteTools = new HashSet<string>();
        var errors = new List<string>();

        try
        {
            // Get tools from regular tools API
            var (toolsSuccess, toolsResponse) = await _apiService.ListToolsAsync();
            if (toolsSuccess)
            {
                var toolNames = ParseToolNamesFromResponse(toolsResponse);
                foreach (var toolName in toolNames)
                {
                    remoteTools.Add(toolName);
                }
            }
            else if (!toolsResponse.Contains("Configuration not found"))
            {
                // Only add as error if it's not a configuration issue (server might be unavailable)
                errors.Add($"Failed to retrieve regular tools: {toolsResponse}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error retrieving regular tools: {ex.Message}");
        }

        try
        {
            // Get tools from extended tools API
            var (extendedSuccess, extendedResponse) = await _apiService.ListExtendedToolsAsync();
            if (extendedSuccess)
            {
                var extendedToolNames = ParseExtendedToolNamesFromResponse(extendedResponse);
                foreach (var toolName in extendedToolNames)
                {
                    remoteTools.Add(toolName);
                }
            }
            else if (!extendedResponse.Contains("Configuration not found"))
            {
                // Only add as error if it's not a configuration issue
                errors.Add($"Failed to retrieve extended tools: {extendedResponse}");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error retrieving extended tools: {ex.Message}");
        }

        return (remoteTools, errors);
    }

    /// <summary>
    /// Parses tool names from the regular tools API response.
    /// </summary>
    private HashSet<string> ParseToolNamesFromResponse(string response)
    {
        var toolNames = new HashSet<string>();
        
        try
        {
            var jsonDoc = JsonDocument.Parse(response);
            
            JsonElement[] tools;
            
            // Check if response has the new nested structure
            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement) && 
                dataElement.ValueKind == JsonValueKind.Object &&
                dataElement.TryGetProperty("tools", out var toolsElement) &&
                toolsElement.ValueKind == JsonValueKind.Array)
            {
                // New nested structure: { "data": { "tools": [...], "pagination": {...} } }
                tools = toolsElement.EnumerateArray().ToArray();
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Legacy array structure: [...]
                tools = jsonDoc.RootElement.EnumerateArray().ToArray();
            }
            else
            {
                return toolNames;
            }

            foreach (var tool in tools)
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
        }
        catch (JsonException)
        {
            // Failed to parse as JSON, might be plain text response
        }

        return toolNames;
    }

    /// <summary>
    /// Parses tool names from the extended tools API response.
    /// </summary>
    private HashSet<string> ParseExtendedToolNamesFromResponse(string response)
    {
        var toolNames = new HashSet<string>();
        
        try
        {
            var jsonDoc = JsonDocument.Parse(response);
            
            JsonElement tools = default;
            bool foundTools = false;
            
            // Try different response structure patterns
            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
            {
                // Pattern 1: { "data": { "tools": [...] } }
                if (dataElement.ValueKind == JsonValueKind.Object && 
                    dataElement.TryGetProperty("tools", out tools) && tools.ValueKind == JsonValueKind.Array)
                {
                    foundTools = true;
                }
                // Pattern 2: { "data": [...] }
                else if (dataElement.ValueKind == JsonValueKind.Array)
                {
                    tools = dataElement;
                    foundTools = true;
                }
            }
            // Pattern 3: { "tools": [...] }
            else if (jsonDoc.RootElement.TryGetProperty("tools", out tools) && tools.ValueKind == JsonValueKind.Array)
            {
                foundTools = true;
            }
            // Pattern 4: Direct array
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                tools = jsonDoc.RootElement;
                foundTools = true;
            }

            if (foundTools)
            {
                foreach (var tool in tools.EnumerateArray())
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
            }
        }
        catch (JsonException)
        {
            // Failed to parse as JSON, might be plain text response
        }

        return toolNames;
    }
}
