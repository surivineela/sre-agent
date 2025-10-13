// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using Agent.Cli.Helpers;

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

        DebugLogger.Debug("ToolAvailability", "Scanning local tools directory");

        if (Directory.Exists("tools"))
        {
            DebugLogger.LogFile("SCAN", "tools", "Directory exists, scanning for tools");

            // Check for tools in flat structure: tools/toolname.yaml
            var flatFiles = Directory.GetFiles("tools", "*.yaml", SearchOption.TopDirectoryOnly);
            DebugLogger.Debug("ToolAvailability", $"Found {flatFiles.Length} flat YAML files in tools/");

            foreach (var file in flatFiles)
            {
                var toolName = Path.GetFileNameWithoutExtension(file);
                localTools.Add(toolName);
                DebugLogger.LogFile("FOUND", file, $"Tool: {toolName} (flat structure)");
            }

            // Check for tools in subdirectory structure: tools/toolname/toolname.yaml
            var subDirs = Directory.GetDirectories("tools");
            DebugLogger.Debug("ToolAvailability", $"Found {subDirs.Length} subdirectories in tools/");

            foreach (var subDir in subDirs)
            {
                var toolName = Path.GetFileName(subDir);
                var toolFile = Path.Combine(subDir, $"{toolName}.yaml");
                if (File.Exists(toolFile))
                {
                    localTools.Add(toolName);
                    DebugLogger.LogFile("FOUND", toolFile, $"Tool: {toolName} (subdirectory structure)");
                }
                else
                {
                    DebugLogger.LogFile("MISSING", toolFile, $"Expected tool file not found for {toolName}");
                }
            }

            DebugLogger.Debug("ToolAvailability", $"Total local tools discovered: {localTools.Count} - {string.Join(", ", localTools.Take(10))}{(localTools.Count > 10 ? "..." : "")}");
        }
        else
        {
            DebugLogger.LogFile("MISSING", "tools", "Tools directory does not exist");
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
        DebugLogger.Debug("ToolAvailability", "Fetching regular tools from server API");
        var (toolsSuccess, toolsResponse) = await GetRawToolsJsonAsync();
        DebugLogger.Debug("ToolAvailability", $"Regular tools API - Success: {toolsSuccess}, Response length: {toolsResponse?.Length ?? 0}");

        if (toolsSuccess && !string.IsNullOrEmpty(toolsResponse))
        {
            var toolNames = ToolResponseParser.ParseToolNames(toolsResponse);
            DebugLogger.Debug("ToolAvailability", $"Parsed {toolNames.Count} regular tools: {string.Join(", ", toolNames.Take(10))}{(toolNames.Count > 10 ? "..." : "")}");
            foreach (var toolName in toolNames)
            {
                remoteTools.Add(toolName);
            }
        }
        else if (!string.IsNullOrEmpty(toolsResponse) && !toolsResponse.Contains("Configuration not found"))
        {
            // Only add as error if it's not a configuration issue (server might be unavailable)
            DebugLogger.Debug("ToolAvailability", $"Regular tools API failed: {toolsResponse ?? "null response"}");
            errors.Add($"Failed to retrieve regular tools: {toolsResponse ?? "null response"}");
        }

        // Get tools from extended tools API (get raw JSON for parsing)
        DebugLogger.Debug("ToolAvailability", "Fetching extended tools from server API");
        var (extendedSuccess, extendedResponse) = await GetRawExtendedToolsJsonAsync();
        DebugLogger.Debug("ToolAvailability", $"Extended tools API - Success: {extendedSuccess}, Response length: {extendedResponse?.Length ?? 0}");

        if (extendedSuccess && !string.IsNullOrEmpty(extendedResponse))
        {
            var extendedToolNames = ToolResponseParser.ParseToolNames(extendedResponse);
            DebugLogger.Debug("ToolAvailability", $"Parsed {extendedToolNames.Count} extended tools: {string.Join(", ", extendedToolNames.Take(10))}{(extendedToolNames.Count > 10 ? "..." : "")}");
            foreach (var toolName in extendedToolNames)
            {
                remoteTools.Add(toolName);
            }
        }
        else if (!string.IsNullOrEmpty(extendedResponse) && !extendedResponse.Contains("Configuration not found"))
        {
            // Only add as error if it's not a configuration issue
            DebugLogger.Debug("ToolAvailability", $"Extended tools API failed: {extendedResponse ?? "null response"}");
            errors.Add($"Failed to retrieve extended tools: {extendedResponse ?? "null response"}");
        }

        return (remoteTools, errors);
    }

    /// <summary>
    /// Gets raw JSON response from the regular tools API.
    /// </summary>
    private async Task<(bool Success, string Response)> GetRawToolsJsonAsync()
    {
        var requestId = DebugLogger.LogRequestStart("RegularToolsAPI", "GET /api/v1/incidentplayground/listTools");

        try
        {
            var config = await _apiService.GetConfigurationAsync();
            if (config == null)
            {
                DebugLogger.LogRequestEnd(requestId, "RegularToolsAPI", false, "Configuration not found");
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/listTools?includeAllTools=true";
            DebugLogger.LogConfig("RegularToolsURL", requestUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Add auth header if not localhost
            var isLocalhost = CliConfigurationService.IsLocalhost(config.ResourceUrl);
            DebugLogger.LogAuth($"Request target: {(isLocalhost ? "localhost (no auth required)" : "remote (auth required)")}");

            if (!isLocalhost)
            {
                var token = await _apiService.GetAccessTokenForInternalUseAsync();
                if (string.IsNullOrEmpty(token))
                {
                    DebugLogger.LogAuth("Failed to obtain access token");
                    DebugLogger.LogRequestEnd(requestId, "RegularToolsAPI", false, "Failed to get access token");
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                DebugLogger.LogAuth("Access token added to request");
            }

            DebugLogger.LogHttpRequest("GET", requestUrl);

            var httpClient = _apiService.GetHttpClient();
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "Unknown", content);

            if (response.IsSuccessStatusCode)
            {
                DebugLogger.LogRequestEnd(requestId, "RegularToolsAPI", true, $"Retrieved {content?.Length ?? 0} characters");
                return (true, content ?? "");
            }
            else
            {
                DebugLogger.LogRequestEnd(requestId, "RegularToolsAPI", false, $"HTTP {response.StatusCode}");
                return (false, $"❌ Failed to retrieve tools. Status: {response.StatusCode}, Content: {content}");
            }
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.Message.Contains("SSL connection could not be established"))
                {
                    message = "The SSL connection could not be established, see inner exception.";
                    DebugLogger.LogNetwork("SSL connection failed - possible certificate issues");
                }
                else if (httpEx.Message.Contains("actively refused"))
                {
                    message = "The request was actively refused by the target machine.";
                    DebugLogger.LogNetwork("Connection refused - server may not be running");
                }
            }
            else if (ex is TaskCanceledException && ex.Message.Contains("timeout"))
            {
                message = "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.";
                DebugLogger.LogNetwork("Request timeout - server may be overloaded");
            }

            DebugLogger.LogRequestEnd(requestId, "RegularToolsAPI", false, $"Exception: {ex.GetType().Name}");
            return (false, $"❌ Failed to get raw tools: {message}");
        }
    }

    /// <summary>
    /// Gets raw JSON response from the extended tools API.
    /// </summary>
    private async Task<(bool Success, string Response)> GetRawExtendedToolsJsonAsync()
    {
        var requestId = DebugLogger.LogRequestStart("ExtendedToolsAPI", "GET /api/v1/extendedAgent/tools");

        try
        {
            var config = await _apiService.GetConfigurationAsync();
            if (config == null)
            {
                DebugLogger.LogRequestEnd(requestId, "ExtendedToolsAPI", false, "Configuration not found");
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/tools";
            DebugLogger.LogConfig("ExtendedToolsURL", requestUrl);

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            // Add auth header if not localhost
            var isLocalhost = CliConfigurationService.IsLocalhost(config.ResourceUrl);
            DebugLogger.LogAuth($"Request target: {(isLocalhost ? "localhost (no auth required)" : "remote (auth required)")}");

            if (!isLocalhost)
            {
                var token = await _apiService.GetAccessTokenForInternalUseAsync();
                if (string.IsNullOrEmpty(token))
                {
                    DebugLogger.LogAuth("Failed to obtain access token");
                    DebugLogger.LogRequestEnd(requestId, "ExtendedToolsAPI", false, "Failed to get access token");
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                DebugLogger.LogAuth("Access token added to request");
            }

            DebugLogger.LogHttpRequest("GET", requestUrl);

            var httpClient = _apiService.GetHttpClient();
            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "Unknown", content);

            if (response.IsSuccessStatusCode)
            {
                DebugLogger.LogRequestEnd(requestId, "ExtendedToolsAPI", true, $"Retrieved {content?.Length ?? 0} characters");
                return (true, content ?? "");
            }
            else
            {
                DebugLogger.LogRequestEnd(requestId, "ExtendedToolsAPI", false, $"HTTP {response.StatusCode}");
                return (false, $"❌ Failed to retrieve extended tools. Status: {response.StatusCode}, Content: {content}");
            }
        }
        catch (Exception ex)
        {
            var message = ex.Message;
            if (ex is HttpRequestException httpEx)
            {
                if (httpEx.Message.Contains("SSL connection could not be established"))
                {
                    message = "The SSL connection could not be established, see inner exception.";
                    DebugLogger.LogNetwork("SSL connection failed - possible certificate issues");
                }
                else if (httpEx.Message.Contains("actively refused"))
                {
                    message = "The request was actively refused by the target machine.";
                    DebugLogger.LogNetwork("Connection refused - server may not be running");
                }
            }
            else if (ex is TaskCanceledException && ex.Message.Contains("timeout"))
            {
                message = "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.";
                DebugLogger.LogNetwork("Request timeout - server may be overloaded");
            }

            DebugLogger.LogRequestEnd(requestId, "ExtendedToolsAPI", false, $"Exception: {ex.GetType().Name}");
            return (false, $"❌ Failed to get raw extended tools: {message}");
        }
    }
}
