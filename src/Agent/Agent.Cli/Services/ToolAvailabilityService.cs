using System.Text.Json;
using System.Net.Http.Headers;
using Agent.Cli.Services;
using Agent.Cli.Models;

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

        // Get tools from regular tools API (get raw JSON, not formatted output)
        var (toolsSuccess, toolsResponse) = await GetRawToolsJsonAsync();
        Console.WriteLine($"🔍 DEBUG ToolAvailabilityService: Regular tools API - Success: {toolsSuccess}, Response length: {toolsResponse?.Length ?? 0}");
        if (toolsSuccess && !string.IsNullOrEmpty(toolsResponse))
        {
            var toolNames = ToolResponseParser.ParseToolNames(toolsResponse);
            Console.WriteLine($"🔍 DEBUG ToolAvailabilityService: Parsed {toolNames.Count} regular tools");
            foreach (var toolName in toolNames)
            {
                remoteTools.Add(toolName);
            }
        }
        else if (!string.IsNullOrEmpty(toolsResponse) && !toolsResponse.Contains("Configuration not found"))
        {
            // Only add as error if it's not a configuration issue (server might be unavailable)
            errors.Add($"Failed to retrieve regular tools: {toolsResponse ?? "null response"}");
        }

        // Get tools from extended tools API (get raw JSON for parsing)
        var (extendedSuccess, extendedResponse) = await GetRawExtendedToolsJsonAsync();
        Console.WriteLine($"🔍 DEBUG ToolAvailabilityService: Extended tools API - Success: {extendedSuccess}, Response length: {extendedResponse?.Length ?? 0}");
        if (extendedSuccess && !string.IsNullOrEmpty(extendedResponse))
        {
            var extendedToolNames = ToolResponseParser.ParseToolNames(extendedResponse);
            Console.WriteLine($"🔍 DEBUG ToolAvailabilityService: Parsed {extendedToolNames.Count} extended tools");
            foreach (var toolName in extendedToolNames)
            {
                remoteTools.Add(toolName);
            }
        }
        else if (!string.IsNullOrEmpty(extendedResponse) && !extendedResponse.Contains("Configuration not found"))
        {
            // Only add as error if it's not a configuration issue
            errors.Add($"Failed to retrieve extended tools: {extendedResponse ?? "null response"}");
        }

        return (remoteTools, errors);
    }

    /// <summary>
    /// Gets raw JSON response from the regular tools API.
    /// </summary>
    private async Task<(bool Success, string Response)> GetRawToolsJsonAsync()
    {
        try
        {
            var config = await _apiService.GetConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/listTools";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await _apiService.GetAccessTokenForInternalUseAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var httpClient = _apiService.GetHttpClient();
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, content);
            }
            else
            {
                return (false, $"❌ Failed to retrieve tools. Status: {response.StatusCode}, Content: {content}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to get raw tools: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets raw JSON response from the extended tools API.
    /// </summary>
    private async Task<(bool Success, string Response)> GetRawExtendedToolsJsonAsync()
    {
        try
        {
            var config = await _apiService.GetConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/tools";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await _apiService.GetAccessTokenForInternalUseAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var httpClient = _apiService.GetHttpClient();
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return (true, content);
            }
            else
            {
                return (false, $"❌ Failed to retrieve extended tools. Status: {response.StatusCode}, Content: {content}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to get raw extended tools: {ex.Message}");
        }
    }
}
