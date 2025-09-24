using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Validations;
using Agent.Framework;
using Azure.Core;
using Azure.Identity;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Services;

public class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CliConfigurationService _configService;
    private readonly TokenService _tokenService;
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public ApiService()
    {
        _configService = new CliConfigurationService();
        _tokenService = new TokenService();

        var handler = new AuthenticationHandler(_tokenService)
        {
            InnerHandler = new HttpClientHandler()
        };

        _httpClient = new HttpClient(handler);
    }

    /// <summary>
    /// Makes an HTTP request with comprehensive debug logging
    /// </summary>
    private async Task<(HttpResponseMessage Response, string Content, long ResponseTimeMs)> MakeHttpRequestAsync(HttpRequestMessage request)
    {
        var stopwatch = Stopwatch.StartNew();

        // Log request details (including headers)
        var requestContent = request.Content != null ? await request.Content.ReadAsStringAsync() : null;
        var requestHeaders = request.Headers.ToList();
        if (request.Content != null)
        {
            foreach (var h in request.Content.Headers)
            {
                requestHeaders.Add(new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value));
            }
        }
        DebugLogger.LogHttpRequest(
            request.Method.ToString(),
            request.RequestUri?.ToString() ?? "unknown",
            requestHeaders,
            request.Content?.Headers?.ContentType?.ToString(),
            requestContent
        );

        // Log authentication
        if (request.Headers.Authorization != null)
        {
            DebugLogger.LogAuth($"Using {request.Headers.Authorization.Scheme} authentication");
        }
        else
        {
            DebugLogger.LogAuth("No authentication header");
        }

        try
        {
            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            stopwatch.Stop();

            // Log response details (including headers)
            var responseHeaders = response.Headers.ToList();
            foreach (var h in response.Content.Headers)
            {
                responseHeaders.Add(new KeyValuePair<string, IEnumerable<string>>(h.Key, h.Value));
            }
            DebugLogger.LogHttpResponse(
                (int)response.StatusCode,
                response.StatusCode.ToString(),
                responseHeaders,
                content,
                stopwatch.ElapsedMilliseconds
            );

            return (response, content, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DebugLogger.LogNetwork($"HTTP request failed after {stopwatch.ElapsedMilliseconds}ms: {ex.Message}");
            throw;
        }
    }

    public async Task<(bool Success, string Response)> TestConnectionAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            return await TestConnectionAsync(config.ResourceUrl);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"Unexpected error: {ex.Message}");
            return (false, $"✗ Connection failed: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> TestConnectionAsync(string resourceUrl)
    {
        try
        {
            DebugLogger.LogConfig("ResourceUrl", resourceUrl);
            DebugLogger.LogConfig("IsLocalhost", CliConfigurationService.IsLocalhost(resourceUrl).ToString());

            var request = new HttpRequestMessage(HttpMethod.Get, $"{resourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    DebugLogger.Debug("Parsing", "Parsing connection test response as JSON");

                    // Parse the response to get agent names using robust parsing
                    var jsonDoc = JsonDocument.Parse(content);
                    var agentCount = jsonDoc.RootElement.ValueKind == JsonValueKind.Array
                        ? jsonDoc.RootElement.GetArrayLength()
                        : 0;

                    DebugLogger.Debug("Response", $"Successfully parsed JSON with {agentCount} agents");

                    return (true, $"✅ Connection successful! Found {agentCount} agents.");
                }
                catch (JsonException ex)
                {
                    DebugLogger.Debug("Parsing", $"JSON exception: {ex.Message}");
                    return (false, $"[ERROR] Invalid JSON response from server: {ex.Message}\n   The server may have returned an error page instead of expected data.\n   This often indicates authentication or permission issues.");
                }
            }
            else
            {
                return (false, FormatConnectionError(response, content, resourceUrl));
            }
        }
        catch (HttpRequestException ex)
        {
            DebugLogger.LogNetwork($"Connection failed: {ex.Message}");
            return (false, $"[ERROR] Network connection failed: {ex.Message}\n   Check if the URL is correct and accessible: {resourceUrl}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            DebugLogger.LogNetwork($"Connection timed out: {resourceUrl}");
            return (false, $"[ERROR] Connection timed out: {resourceUrl}\n   The server may be unreachable or overloaded.");
        }
        catch (JsonException ex)
        {
            DebugLogger.Debug("Parsing", $"JSON exception: {ex.Message}");
            return (false, $"[ERROR] Invalid JSON response from server: {ex.Message}\n   The server may have returned an error page instead of expected data.\n   This often indicates authentication or permission issues.");
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"Unexpected error: {ex.Message}");
            return (false, $"❌ Connection failed: {ex.Message}");
        }
    }

    private string FormatConnectionError(HttpResponseMessage response, string content, string resourceUrl)
    {
        var statusCode = (int)response.StatusCode;
        var statusName = response.StatusCode.ToString();

        // Check if response is HTML (common for error pages)
        var isHtmlResponse = content.TrimStart().StartsWith("<", StringComparison.OrdinalIgnoreCase);

        var errorMessage = $"❌ Connection failed: HTTP {statusCode} ({statusName})";

        // Provide specific guidance based on status code
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized: // 401
                errorMessage += "\n   Authentication failed. This usually means:";
                errorMessage += "\n   • You need to run 'az login' first";
                errorMessage += "\n   • Your Azure CLI session has expired";
                errorMessage += "\n   • The token audience/scope is incorrect";
                break;

            case HttpStatusCode.Forbidden: // 403
                errorMessage += "\n   Access denied. This usually means:";
                errorMessage += "\n   • You don't have permission to access this SRE Agent resource";
                errorMessage += "\n   • Cross-tenant access needs to be configured";
                errorMessage += "\n   • Your user account needs to be added as an admin to the resource";
                errorMessage += "\n   \n   To fix cross-tenant access:";
                errorMessage += "\n   1. Get your Object ID from: https://ms.portal.azure.com/#view/Microsoft_AAD_IAM/ActiveDirectoryMenuBlade/~/Overview";
                errorMessage += "\n   2. Find the ARM resource ID for this SRE Agent";
                errorMessage += "\n   3. Run: az resource patch --ids <ARM_RESOURCE_ID> -p '{\"adminUsers\":[{\"objectId\":\"<YOUR_OBJECT_ID>\",\"tenantId\":\"72f988bf-86f1-41af-91ab-2d7cd011db47\"}]}'";
                break;

            case HttpStatusCode.NotFound: // 404
                errorMessage += $"\n   The endpoint was not found. Check if the URL is correct: {resourceUrl}";
                break;

            case HttpStatusCode.InternalServerError: // 500
                errorMessage += "\n   Server error. The SRE Agent service may be experiencing issues.";
                break;

            case HttpStatusCode.BadGateway: // 502
            case HttpStatusCode.ServiceUnavailable: // 503
            case HttpStatusCode.GatewayTimeout: // 504
                errorMessage += "\n   The service is temporarily unavailable. Please try again later.";
                break;
        }

        // If we got an HTML response, it's likely an error page
        if (isHtmlResponse)
        {
            errorMessage += "\n   \n   ⚠️  Server returned an HTML error page instead of expected JSON data.";
            errorMessage += "\n   This typically indicates a misconfiguration/invalid url.";

            // Try to extract title from HTML for more context
            var titleMatch = System.Text.RegularExpressions.Regex.Match(content, @"<title[^>]*>([^<]+)</title>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (titleMatch.Success && !string.IsNullOrWhiteSpace(titleMatch.Groups[1].Value))
            {
                errorMessage += $"\n   Error page title: {titleMatch.Groups[1].Value.Trim()}";
            }
        }
        else
        {
            // Try to parse JSON error response
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                {
                    var error = errorElement.GetString();
                    errorMessage += $"\n   Server error: {error}";

                    if (jsonDoc.RootElement.TryGetProperty("error_description", out var descElement))
                    {
                        var description = descElement.GetString();
                        errorMessage += $"\n   Details: {description}";

                        // Specific handling for audience validation errors
                        if (description?.Contains("Audience validation failed") == true)
                        {
                            errorMessage += "\n   \n   This is a token audience validation error.";
                            errorMessage += "\n   The server expects a different token audience than what was provided.";
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // If it's not JSON and not HTML, show a truncated version of the response
                var truncatedContent = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
                errorMessage += $"\n   Server response: {truncatedContent}";
            }
        }

        return errorMessage;
    }

    public async Task<(bool Success, string Response)> ApplyAgentAsync(string agentName)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            DebugLogger.Debug("ApplyAgent", $"Starting agent apply for '{agentName}'");

            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            DebugLogger.LogConfig("ResourceUrl", config.ResourceUrl);

            // Check if agent YAML file exists
            var agentFilePath = Path.Combine("agents", $"{agentName}.yaml");
            if (!File.Exists(agentFilePath))
            {
                // Try the subdirectory structure
                var agentFilePathSubdir = Path.Combine("agents", agentName, $"{agentName}.yaml");
                if (!File.Exists(agentFilePathSubdir))
                {
                    DebugLogger.LogFile("SEARCH", agentFilePath, "File not found");
                    DebugLogger.LogFile("SEARCH", agentFilePathSubdir, "File not found");
                    return (false, $"Agent file not found: {agentFilePath} or {agentFilePathSubdir}");
                }
                agentFilePath = agentFilePathSubdir;
            }

            DebugLogger.LogFile("READ", agentFilePath);

            // Read the agent YAML file
            var agentYamlContent = await File.ReadAllTextAsync(agentFilePath);
            DebugLogger.LogFile("READ", agentFilePath, $"Content size: {agentYamlContent.Length} characters");

            // Parse the agent YAML to extract the tools list
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            DebugLogger.Debug("YAML", "Deserializing agent YAML content");

            // Parse agent data and extract tools using shared helper
            var agentData = deserializer.Deserialize<object>(agentYamlContent);
            var toolNames = Agent.Core.Helpers.ExtendedAgents.AgentYamlParser.ExtractToolNames(agentYamlContent);

            DebugLogger.Debug("Tools", $"Agent references {toolNames.Count} tools: {string.Join(", ", toolNames)}");

            // Validate agent descriptor structure before proceeding
            try
            {
                // Parse the YAML to validate the agent descriptor structure
                var yamlDeserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                var agentDocument = yamlDeserializer.Deserialize<Dictionary<string, object>>(agentYamlContent);

                // Extract spec section which contains the actual agent descriptor
                if (!agentDocument.TryGetValue("spec", out var specObj) || specObj is not Dictionary<string, object> spec)
                {
                    return (false, "❌ Agent validation failed:\n  - Invalid YAML structure: 'spec' section is required");
                }

                // Validate YAML structure - check for common indentation mistakes
                var structureValidationErrors = ValidateYamlStructure(agentDocument, spec);
                if (structureValidationErrors.Count > 0)
                {
                    var errorMessage = string.Join("\n", structureValidationErrors.Select(e => $"  - {e}"));
                    DebugLogger.Debug("Validation", $"YAML structure validation failed: {errorMessage}");
                    return (false, $"❌ YAML structure validation failed:\n{errorMessage}");
                }

                // Deserialize the spec section as YamlAgentDescriptor for validation
                var specYaml = new SerializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build()
                    .Serialize(spec);

                var agentDescriptor = yamlDeserializer.Deserialize<YamlAgentDescriptor>(specYaml);

                var validationErrors = new List<string>();
                AgentDescriptorValidation.ValidateAgentDescriptor(agentDescriptor, out validationErrors);

                if (validationErrors.Count > 0)
                {
                    var errorMessage = string.Join("\n", validationErrors.Select(e => $"  - {e}"));
                    DebugLogger.Debug("Validation", $"Agent validation failed: {errorMessage}");
                    return (false, $"❌ Agent validation failed:\n{errorMessage}");
                }

                DebugLogger.Debug("Validation", "Agent descriptor validation passed");
            }
            catch (Exception ex)
            {
                DebugLogger.Debug("Validation", $"Agent descriptor parsing failed: {ex.Message}");
                return (false, $"❌ Failed to parse agent descriptor: {ex.Message}");
            }

            // Get available tools from local and remote sources
            var toolAvailabilityService = new ToolAvailabilityService(this);
            var (localTools, remoteTools, errors) = await toolAvailabilityService.GetAvailableToolsAsync();

            // Log the results using debug logger
            DebugLogger.Debug("Tools", $"Found {localTools.Count} local tools: {string.Join(", ", localTools.Take(5))}{(localTools.Count > 5 ? "..." : "")}");
            DebugLogger.Debug("Tools", $"Found {remoteTools.Count} remote tools: {string.Join(", ", remoteTools.Take(5))}{(remoteTools.Count > 5 ? "..." : "")}");
            if (errors.Any())
            {
                DebugLogger.Debug("Tools", $"Errors: {string.Join("; ", errors)}");
            }

            // Load available tool YAML files locally and track missing tools
            var toolsData = new List<object>();
            var missingLocallyButRemote = new List<string>();
            var completelyMissingTools = new List<string>();

            foreach (var toolName in toolNames)
            {
                // Check if tool exists locally first
                if (localTools.Contains(toolName))
                {
                    var toolFilePath = Path.Combine("tools", $"{toolName}.yaml");
                    if (!File.Exists(toolFilePath))
                    {
                        // Try the subdirectory structure
                        toolFilePath = Path.Combine("tools", toolName, $"{toolName}.yaml");
                    }

                    if (File.Exists(toolFilePath))
                    {
                        DebugLogger.LogFile("READ", toolFilePath, "Loading tool YAML");
                        var toolYamlContent = await File.ReadAllTextAsync(toolFilePath);
                        var toolData = deserializer.Deserialize<object>(toolYamlContent);
                        toolsData.Add(toolData);
                        DebugLogger.Debug("Tools", $"📦 Loaded tool: {toolName}");
                        // Always show loaded tools message for user feedback
                        ConsoleUI.WriteBullet($"Loaded tool: {toolName}", ConsoleColor.Green);
                    }
                }
                else if (remoteTools.Contains(toolName))
                {
                    // Tool exists on server but not locally - this is okay
                    missingLocallyButRemote.Add(toolName);
                    DebugLogger.Debug("Tools", $"🌐 Tool '{toolName}' exists on server (not loading locally)");
                }
                else
                {
                    // Tool doesn't exist locally or remotely
                    completelyMissingTools.Add(toolName);
                    DebugLogger.Debug("Tools", $"⚠️  Tool '{toolName}' not found locally or on server");
                }
            }

            // Only fail if tools are completely missing (not available locally or remotely)
            if (completelyMissingTools.Count > 0)
            {
                var missingToolsList = string.Join(", ", completelyMissingTools);
                DebugLogger.Debug("Tools", $"Missing tools validation failed: {missingToolsList}");
                return (false, $"❌ Cannot apply agent '{agentName}': Referenced tools not found: {missingToolsList}. Please create the missing tools first or ensure they exist on the server.");
            }

            DebugLogger.Debug("YAML", "Using agent YAML content directly (structured format)");

            // The agent file is already in the correct structured format (api_version, kind, metadata, spec)
            // Send it directly without additional wrapping
            var wrappedYamlContent = agentYamlContent;

            DebugLogger.Debug("YAML", $"Generated YAML content size: {wrappedYamlContent.Length} characters");

            // Create the request
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply";
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
            request.Content = new StringContent(wrappedYamlContent, Encoding.UTF8, "application/yaml");

            DebugLogger.Debug("Request", $"Making apply request to {requestUrl}");
            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var localToolCount = toolsData.Count;
                var remoteToolCount = missingLocallyButRemote.Count;
                var totalToolCount = localToolCount + remoteToolCount;

                var toolsMessage = "";
                if (totalToolCount > 0)
                {
                    if (localToolCount > 0 && remoteToolCount > 0)
                    {
                        toolsMessage = $" with {localToolCount} local tool(s) and {remoteToolCount} server tool(s)";
                    }
                    else if (localToolCount > 0)
                    {
                        toolsMessage = $" and {localToolCount} referenced tool(s)";
                    }
                    else if (remoteToolCount > 0)
                    {
                        toolsMessage = $" with {remoteToolCount} server tool(s)";
                    }
                }

                stopwatch.Stop();
                DebugLogger.LogTiming("ApplyAgent", stopwatch.Elapsed);
                return (true, $"✅ Agent '{agentName}'{toolsMessage} applied successfully!");
            }
            else
            {
                stopwatch.Stop();
                DebugLogger.LogTiming("ApplyAgent (failed)", stopwatch.Elapsed);
                return (false, $"❌ Failed to apply agent: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            DebugLogger.Debug("Exception", $"ApplyAgent failed: {ex.Message}");
            DebugLogger.LogTiming("ApplyAgent (exception)", stopwatch.Elapsed);
            return (false, $"❌ Failed to apply agent: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> ListAgentsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Parse the response to get agent information from different possible structures
                var jsonDoc = JsonDocument.Parse(content);

                JsonElement agents = default;
                bool foundAgents = false;

                // Try different response structure patterns
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    // Pattern 1: { "data": { "agents": [...] } } - ExtendedAgentsListResponse style
                    if (dataElement.ValueKind == JsonValueKind.Object &&
                        dataElement.TryGetProperty("agents", out agents) && agents.ValueKind == JsonValueKind.Array)
                    {
                        foundAgents = true;
                    }
                    // Pattern 2: { "data": [...] } - PaginatedResponse style (actual current API)
                    else if (dataElement.ValueKind == JsonValueKind.Array)
                    {
                        agents = dataElement;
                        foundAgents = true;
                    }
                }
                // Pattern 3: { "agents": [...] } - Direct agents property
                else if (jsonDoc.RootElement.TryGetProperty("agents", out agents) && agents.ValueKind == JsonValueKind.Array)
                {
                    foundAgents = true;
                }
                // Pattern 4: [...] - Direct array (legacy)
                else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    agents = jsonDoc.RootElement;
                    foundAgents = true;
                }

                if (!foundAgents)
                {
                    return (false, $"Unexpected response format - no agents array found: {content}");
                }

                var agentList = new List<string>();

                foreach (var agent in agents.EnumerateArray())
                {
                    var name = agent.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Unknown" : "Unknown";

                    // Try both 'system_prompt' and 'instructions' fields
                    var systemPrompt = "";
                    if (agent.TryGetProperty("system_prompt", out var systemPromptElement))
                    {
                        systemPrompt = systemPromptElement.GetString() ?? "";
                    }
                    else if (agent.TryGetProperty("instructions", out var instructionsElement))
                    {
                        systemPrompt = instructionsElement.GetString() ?? "";
                    }

                    var handoffDescription = agent.TryGetProperty("handoffDescription", out var handoffElement) ? handoffElement.GetString() : "";
                    var createdAt = agent.TryGetProperty("created_at", out var createdElement) ? createdElement.GetString() : "";

                    var agentOutput = Helpers.ConsoleUI.CaptureOutput(() =>
                    {
                        Console.WriteLine();
                        Helpers.ConsoleUI.WriteBullet(name, ConsoleColor.White, 0);

                        if (!string.IsNullOrEmpty(handoffDescription))
                        {
                            Helpers.ConsoleUI.WriteKeyValue("  Description", handoffDescription, 13, ConsoleColor.Gray, ConsoleColor.White);
                        }

                        if (!string.IsNullOrEmpty(systemPrompt))
                        {
                            // Truncate system prompt if it's too long for display
                            var displayPrompt = systemPrompt.Length > 100 ? systemPrompt.Substring(0, 100) + "..." : systemPrompt;
                            Helpers.ConsoleUI.WriteKeyValue("  System Prompt", displayPrompt, 13, ConsoleColor.Gray, ConsoleColor.White);
                        }

                        if (!string.IsNullOrEmpty(createdAt))
                        {
                            Helpers.ConsoleUI.WriteKeyValue("  Created", createdAt, 13, ConsoleColor.Gray, ConsoleColor.White);
                        }

                        // Get tools
                        if (agent.TryGetProperty("tools", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                        {
                            var tools = toolsElement.EnumerateArray().Select(t => t.GetString()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                            if (tools.Any())
                            {
                                Helpers.ConsoleUI.WriteKeyValue("  Tools", string.Join(", ", tools), 13, ConsoleColor.Gray, ConsoleColor.White);
                            }
                        }

                        // Get handoffs
                        if (agent.TryGetProperty("handoffs", out var handoffsElement) && handoffsElement.ValueKind == JsonValueKind.Array)
                        {
                            var handoffs = handoffsElement.EnumerateArray().Select(h => h.GetString()).Where(h => !string.IsNullOrEmpty(h)).ToList();
                            if (handoffs.Any())
                            {
                                Helpers.ConsoleUI.WriteKeyValue("  Handoffs", string.Join(", ", handoffs), 13, ConsoleColor.Gray, ConsoleColor.White);
                            }
                        }
                    });
                    agentList.Add(agentOutput);
                }

                if (agents.GetArrayLength() == 0)
                {
                    agentList.Add("\nNo agents found on the server.");
                }
                else
                {
                    // Get pagination info from different response structures
                    int totalCount = agents.GetArrayLength(); // Default fallback
                    bool hasMore = false;
                    int pageSize = 50;
                    int pageIndex = 0;

                    // Try to get pagination info from PaginatedResponse structure
                    if (jsonDoc.RootElement.TryGetProperty("total_count", out var totalCountElement))
                    {
                        totalCount = totalCountElement.GetInt32();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("has_next_page", out var hasMoreElement))
                    {
                        hasMore = hasMoreElement.GetBoolean();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("page_size", out var pageSizeElement))
                    {
                        pageSize = pageSizeElement.GetInt32();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("page_index", out var pageIndexElement))
                    {
                        pageIndex = pageIndexElement.GetInt32();
                    }
                    // Legacy pagination format (only if data is an object, not an array)
                    else if (jsonDoc.RootElement.TryGetProperty("data", out var legacyDataElement) &&
                             legacyDataElement.ValueKind == JsonValueKind.Object &&
                             legacyDataElement.TryGetProperty("pagination", out var legacyPaginationElement))
                    {
                        if (legacyPaginationElement.TryGetProperty("total_count", out var legacyTotalElement))
                        {
                            totalCount = legacyTotalElement.GetInt32();
                        }
                        if (legacyPaginationElement.TryGetProperty("has_more", out var legacyHasMoreElement))
                        {
                            hasMore = legacyHasMoreElement.GetBoolean();
                        }
                        if (legacyPaginationElement.TryGetProperty("limit", out var legacyLimitElement))
                        {
                            pageSize = legacyLimitElement.GetInt32();
                        }
                        if (legacyPaginationElement.TryGetProperty("offset", out var legacyOffsetElement))
                        {
                            pageIndex = legacyOffsetElement.GetInt32() / pageSize; // Convert offset to page index
                        }
                    }

                    agentList.Add($"\nTotal: {totalCount} agent(s)");

                    // Add pagination info if available and we have actual results to show
                    if ((hasMore || pageIndex > 0) && agents.GetArrayLength() > 0)
                    {
                        var currentPageAgents = agents.GetArrayLength();
                        var actualOffset = pageIndex * pageSize;
                        var startIndex = actualOffset + 1;
                        var endIndex = actualOffset + currentPageAgents;
                        // Only show pagination if it makes sense
                        if (startIndex <= totalCount)
                        {
                            agentList.Add($"Showing agents {startIndex}-{endIndex} of {totalCount} (page {pageIndex + 1})");
                        }
                    }
                }

                return (true, string.Join("\n", agentList));
            }
            else
            {
                return (false, $"❌ Failed to list agents: {response.StatusCode} - {content}\n   Request URL: {url}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to list agents: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns a simple list of agent names from the server.
    /// </summary>
    public async Task<(bool Success, List<string> Names, string Error)> GetAgentNamesAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, new List<string>(), "Configuration not found. Please run 'srectl init' first.");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, _) = await MakeHttpRequestAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return (false, new List<string>(), $"Failed to list agents: {response.StatusCode} - {content}");
            }

            // Prefer typed deserialization matching controller's PaginatedResponse<T>
            try
            {
                // Case 1: PaginatedResponse<AgentListItem>
                var paged = JsonSerializer.Deserialize<PaginatedResponse<AgentListItem>>(content, _jsonOptions);
                if (paged?.Data != null && paged.Data.Count > 0)
                {
                    var namesTyped = paged.Data
                        .Select(a => a.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return (true, namesTyped, "");
                }

                // Case 2: bare array [ { name: ... } ]
                var bare = JsonSerializer.Deserialize<List<AgentListItem>>(content, _jsonOptions);
                if (bare != null && bare.Count > 0)
                {
                    var namesBare = bare
                        .Select(a => a.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return (true, namesBare, "");
                }
            }
            catch
            {
                // Fall back to robust JSON parsing below
            }

            // Fallback: robust parsing of multiple shapes
            try
            {
                var names = new List<string>();
                var jsonDoc = JsonDocument.Parse(content);
                JsonElement agents;
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
                {
                    agents = dataElement;
                }
                else if (jsonDoc.RootElement.TryGetProperty("agents", out var agentsElement) && agentsElement.ValueKind == JsonValueKind.Array)
                {
                    agents = agentsElement;
                }
                else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    agents = jsonDoc.RootElement;
                }
                else
                {
                    return (false, new List<string>(), "Unexpected response format for agents list");
                }

                foreach (var agent in agents.EnumerateArray())
                {
                    if (agent.TryGetProperty("name", out var nameEl))
                    {
                        var n = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                    }
                }
                return (true, names, "");
            }
            catch (Exception ex)
            {
                return (false, new List<string>(), $"Failed to parse agents list: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return (false, new List<string>(), ex.Message);
        }
    }

    public async Task<(bool Success, string Response)> ListToolsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/listTools";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Parse the response using the shared tool response parser
                var toolElements = ToolResponseParser.ParseToolElements(content);

                var toolList = new List<string>();

                if (toolElements.Length == 0)
                {
                    toolList.Add("\nNo tools found on the server.");
                }
                else
                {
                    var toolDisplayInfo = ToolResponseParser.ExtractToolDisplayInfo(toolElements);
                    toolList.AddRange(toolDisplayInfo);
                    toolList.Add($"\nTotal: {toolElements.Length} tool(s)");
                }

                return (true, string.Join("\n", toolList));
            }
            else
            {
                return (false, $"❌ Failed to list tools: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to list tools: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns a simple list of tool names from the server (prefers extended tools endpoint for rich tools).
    /// </summary>
    public async Task<(bool Success, List<string> Names, string Error)> GetToolNamesAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, new List<string>(), "Configuration not found. Please run 'srectl init' first.");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/tools";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, _) = await MakeHttpRequestAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return (false, new List<string>(), $"Failed to list tools: {response.StatusCode} - {content}");
            }

            // Prefer typed deserialization matching controller's PaginatedResponse<T>
            try
            {
                // Case 1: PaginatedResponse<ToolListItem>
                var paged = JsonSerializer.Deserialize<PaginatedResponse<ToolListItem>>(content, _jsonOptions);
                if (paged?.Data != null && paged.Data.Count > 0)
                {
                    var namesTyped = paged.Data
                        .Select(t => t.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return (true, namesTyped, "");
                }

                // Case 2: bare array [ { name: ... } ]
                var bare = JsonSerializer.Deserialize<List<ToolListItem>>(content, _jsonOptions);
                if (bare != null && bare.Count > 0)
                {
                    var namesBare = bare
                        .Select(t => t.Name)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Select(n => n!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    return (true, namesBare, "");
                }
            }
            catch
            {
                // Fall back to robust JSON parsing below
            }

            // Fallback: robust parsing of multiple shapes
            try
            {
                var names = new List<string>();
                var jsonDoc = JsonDocument.Parse(content);
                JsonElement dataEl;
                if (jsonDoc.RootElement.TryGetProperty("data", out var de))
                {
                    dataEl = de;
                }
                else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    dataEl = jsonDoc.RootElement;
                }
                else
                {
                    return (false, new List<string>(), "Unexpected response format for tools list");
                }

                foreach (var tool in dataEl.EnumerateArray())
                {
                    if (tool.ValueKind == JsonValueKind.Object)
                    {
                        if (tool.TryGetProperty("name", out var nEl))
                        {
                            var n = nEl.GetString();
                            if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                        }
                        else if (tool.TryGetProperty("tool", out var toolObj) && toolObj.ValueKind == JsonValueKind.Object && toolObj.TryGetProperty("name", out var innerName))
                        {
                            var n = innerName.GetString();
                            if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                        }
                    }
                }
                return (true, names, "");
            }
            catch (Exception ex)
            {
                return (false, new List<string>(), $"Failed to parse tools list: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return (false, new List<string>(), ex.Message);
        }
    }

    public async Task<(bool Success, string Response)> ListExtendedToolsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/tools";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Parse the response using the shared tool response parser
                var toolElements = ToolResponseParser.ParseToolElements(content);

                var toolList = new List<string>();

                if (toolElements.Length == 0)
                {
                    toolList.Add("\nNo extended tools found on the server.");
                    toolList.Add("Use 'srectl tool apply <tool-name>' to add tools to the server.");
                }
                else
                {
                    var extendedToolDisplayInfo = ToolResponseParser.ExtractExtendedToolDisplayInfo(toolElements);
                    toolList.AddRange(extendedToolDisplayInfo);

                    // Get pagination info from the original response structure
                    var jsonDoc = JsonDocument.Parse(content);
                    int totalCount = toolElements.Length; // Default fallback
                    bool hasMore = false;
                    int pageSize = 50;
                    int pageIndex = 0;

                    // Try to get pagination info from PaginatedResponse structure
                    if (jsonDoc.RootElement.TryGetProperty("total_count", out var totalCountElement))
                    {
                        totalCount = totalCountElement.GetInt32();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("has_next_page", out var hasMoreElement))
                    {
                        hasMore = hasMoreElement.GetBoolean();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("page_size", out var pageSizeElement))
                    {
                        pageSize = pageSizeElement.GetInt32();
                    }
                    if (jsonDoc.RootElement.TryGetProperty("page_index", out var pageIndexElement))
                    {
                        pageIndex = pageIndexElement.GetInt32();
                    }
                    // Legacy pagination format (only if data is an object, not an array)
                    else if (jsonDoc.RootElement.TryGetProperty("data", out var legacyDataElement) &&
                             legacyDataElement.ValueKind == JsonValueKind.Object &&
                             legacyDataElement.TryGetProperty("pagination", out var legacyPaginationElement))
                    {
                        if (legacyPaginationElement.TryGetProperty("total_count", out var legacyTotalElement))
                        {
                            totalCount = legacyTotalElement.GetInt32();
                        }
                        if (legacyPaginationElement.TryGetProperty("has_more", out var legacyHasMoreElement))
                        {
                            hasMore = legacyHasMoreElement.GetBoolean();
                        }
                        if (legacyPaginationElement.TryGetProperty("limit", out var legacyLimitElement))
                        {
                            pageSize = legacyLimitElement.GetInt32();
                        }
                        if (legacyPaginationElement.TryGetProperty("offset", out var legacyOffsetElement))
                        {
                            pageIndex = legacyOffsetElement.GetInt32() / pageSize; // Convert offset to page index
                        }
                    }

                    toolList.Add($"\nTotal: {totalCount} extended tool(s)");

                    // Add pagination info if available and we have actual results to show
                    if ((hasMore || pageIndex > 0) && toolElements.Length > 0)
                    {
                        var currentPageTools = toolElements.Length;
                        var actualOffset = pageIndex * pageSize;
                        var startIndex = actualOffset + 1;
                        var endIndex = actualOffset + currentPageTools;
                        // Only show pagination if it makes sense
                        if (startIndex <= totalCount)
                        {
                            toolList.Add($"Showing tools {startIndex}-{endIndex} of {totalCount} (page {pageIndex + 1})");
                        }
                    }
                }

                return (true, string.Join("\n", toolList));
            }
            else
            {
                return (false, $"❌ Failed to list extended tools: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to list extended tools: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> ApplyToolAsync(string toolName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            // Find tool YAML file using flexible search
            var toolFilePath = FindToolFile(toolName);
            if (toolFilePath == null)
            {
                return (false, $"Tool file not found for '{toolName}'. Searched in tools directory and subdirectories for '{toolName}.yaml'");
            }

            // Read the YAML file
            var yamlContent = await File.ReadAllTextAsync(toolFilePath);

            // Parse the tool YAML content to an object
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var toolData = deserializer.Deserialize<object>(yamlContent);

            // Create the wrapper with proper structure
            var toolWrapper = new ToolListWrapper
            {
                ApiVersion = config.ApiVersion ?? "azuresre.ai/v1",
                Metadata = new YamlMetadata
                {
                    Owner = config.Owner ?? "your-team@example.com",
                    Version = config.Version ?? "1.0.0",
                    Tags = config.Tags?.Any() == true ? config.Tags : new List<string> { "example", "demo", "generic" },
                    CreatedAt = config.CreatedAt != default(DateTime) ? config.CreatedAt.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd")
                },
                Spec = new ToolSpec { Tools = new List<object> { toolData } }
            };

            // Serialize to YAML
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var wrappedYamlContent = serializer.Serialize(toolWrapper);

            // Create the request
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply";
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
            request.Content = new StringContent(wrappedYamlContent, Encoding.UTF8, "application/yaml");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"✅ Tool '{toolName}' applied successfully!");
            }
            else
            {
                return (false, $"❌ Failed to apply tool: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply tool: {ex.Message}");
        }
    }

    public async Task<(bool Success, string GeneratedInstructions, List<string> RecommendedTools, List<string> McpTools, string ErrorMessage)> GenerateSmartAgentAsync(string agentName, string? userInstructions = null)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", new List<string>(), new List<string>(), "Configuration not found. Please run 'srectl init' first.");
            }

            // Build custom instructions
            var customInstructions = $"This is a {agentName} Agent. Create a prompt workflow for this and recommended tools.";
            if (!string.IsNullOrWhiteSpace(userInstructions))
            {
                customInstructions += $" Here are some user provided instructions to guide you: {userInstructions}";
            }

            // Create request payload
            var requestPayload = new
            {
                AgentName = agentName,
                CustomInstructions = customInstructions,
                Incidents = new object[0],
                Tools = new object[0]
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/generateInstructions";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                var generatedInstructions = root.TryGetProperty("generatedInstructions", out var instructionsElement)
                    ? instructionsElement.GetString() ?? ""
                    : "";

                var recommendedTools = new List<string>();
                if (root.TryGetProperty("tools", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in toolsElement.EnumerateArray())
                    {
                        var toolName = tool.GetString();
                        if (!string.IsNullOrEmpty(toolName))
                        {
                            recommendedTools.Add(toolName);
                        }
                    }
                }

                var mcpTools = new List<string>();
                if (root.TryGetProperty("mcpTools", out var mcpToolsElement) && mcpToolsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var tool in mcpToolsElement.EnumerateArray())
                    {
                        var toolName = tool.GetString();
                        if (!string.IsNullOrEmpty(toolName))
                        {
                            mcpTools.Add(toolName);
                        }
                    }
                }

                return (true, generatedInstructions, recommendedTools, mcpTools, string.Empty);
            }
            else
            {
                return (false, "", new List<string>(), new List<string>(), $"Failed to generate smart agent: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, "", new List<string>(), new List<string>(), $"Failed to generate smart agent: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> ApplyYamlFileAsync(string filePath)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            // Check if the YAML file exists
            if (!File.Exists(filePath))
            {
                return (false, $"YAML file not found: {filePath}");
            }

            // Read the YAML file content
            var yamlContent = await File.ReadAllTextAsync(filePath);

            if (string.IsNullOrWhiteSpace(yamlContent))
            {
                return (false, $"YAML file is empty: {filePath}");
            }

            // Create the request - send YAML directly to the same endpoint
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply";
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
            request.Content = new StringContent(yamlContent, Encoding.UTF8, "application/yaml");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"✅ YAML file '{filePath}' applied successfully!");
            }
            else
            {
                return (false, $"❌ Failed to apply YAML file: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to apply YAML file: {ex.Message}");
        }
    }

    public async Task<(bool Success, string ThreadId, string Response)> CreateThreadAsync(string message, string userId, string displayName, string? agentName = null)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", "Configuration not found. Please run 'srectl init' first.");
            }

            // Create request payload
            object requestPayload;
            if (string.IsNullOrWhiteSpace(agentName))
            {
                requestPayload = new
                {
                    startMessage = new
                    {
                        text = message,
                        userId = userId,
                        displayName = displayName,
                        agent = agentName
                    },
                    source = "Conversation"
                };
            }
            else
            {
                requestPayload = new
                {
                    startMessage = new
                    {
                        text = message,
                        userId = userId,
                        displayName = displayName,
                        agent = agentName
                    },
                    source = "Conversation"
                };
            }

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);
                var threadId = jsonDoc.RootElement.TryGetProperty("id", out var idElement)
                    ? idElement.GetString() ?? ""
                    : "";

                return (true, threadId, $"✅ Thread created successfully with ID: {threadId}");
            }
            else
            {
                return (false, "", $"❌ Failed to create thread: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, "", $"❌ Failed to create thread: {ex.Message}");
        }
    }

    public async Task<(bool Success, string MessageId, string Response)> SendMessageAsync(string threadId, string message, string userId, string displayName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", "Configuration not found. Please run 'srectl init' first.");
            }

            // Create request payload
            var requestPayload = new
            {
                text = message,
                userId = userId,
                displayName = displayName
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);
                var messageId = jsonDoc.RootElement.TryGetProperty("id", out var idElement)
                    ? idElement.GetString() ?? ""
                    : "";

                return (true, messageId, $"✅ Message sent successfully with ID: {messageId}");
            }
            else
            {
                return (false, "", $"❌ Failed to send message: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, "", $"❌ Failed to send message: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<ThreadMessage> Messages, string Response)> GetThreadMessagesAsync(string threadId, int maxRetries = 30, int delaySeconds = 2)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, new List<ThreadMessage>(), "Configuration not found. Please run 'srectl init' first.");
            }

            var messages = new List<ThreadMessage>();
            var retryCount = 0;

            // For snappy spinner animation with shimmer effect
            string[] dots = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            string[] colors = new[] { "[36m", "[96m", "[37m", "[97m" }; // Mono cyan color
            int dotIndex = 0;
            int colorIndex = 0;
            bool waitingPrinted = false;

            while (retryCount < maxRetries)
            {
                var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                var (response, content, responseTime) = await MakeHttpRequestAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(content);
                    var value = jsonDoc.RootElement.GetProperty("value");

                    messages.Clear();
                    foreach (var messageElement in value.EnumerateArray())
                    {
                        var id = messageElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
                        var text = messageElement.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? "" : "";
                        var timestamp = messageElement.TryGetProperty("timeStamp", out var timestampElement) ? timestampElement.GetDateTime() : DateTime.MinValue;

                        var authorRole = "User";
                        var authorUserId = "";
                        var authorDisplayName = "";

                        if (messageElement.TryGetProperty("author", out var authorElement))
                        {
                            authorRole = authorElement.TryGetProperty("role", out var roleElement) ? roleElement.GetString() ?? "User" : "User";
                            authorUserId = authorElement.TryGetProperty("userId", out var userIdElement) ? userIdElement.GetString() ?? "" : "";
                            authorDisplayName = authorElement.TryGetProperty("displayName", out var displayNameElement) ? displayNameElement.GetString() ?? "" : "";
                        }

                        messages.Add(new ThreadMessage(id, text, timestamp, authorRole, authorUserId, authorDisplayName));
                    }

                    // Check if we have agent responses (more than 1 message means agent has responded)
                    var agentMessages = messages.Where(m => m.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (agentMessages.Any())
                    {
                        if (waitingPrinted)
                        {
                            // Clear the waiting line
                            Console.Write("\r" + new string(' ', 30) + "\r");
                        }
                        return (true, messages, "Messages retrieved successfully");
                    }

                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        // Print or update the waiting line with blipping dots
                        string waitMsg = $"{colors[colorIndex]}{dots[dotIndex]}[0m Working...";
                        colorIndex = (colorIndex + 1) % colors.Length;
                        Console.Write($"\r{waitMsg}   ");
                        dotIndex = (dotIndex + 1) % dots.Length;
                        waitingPrinted = true;
                        await Task.Delay(150); // Much faster animation
                    }
                }
                else
                {
                    if (waitingPrinted)
                    {
                        Console.Write("\r" + new string(' ', 30) + "\r");
                    }
                    return (false, new List<ThreadMessage>(), $"Failed to get messages: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
                }
            }

            if (waitingPrinted)
            {
                Console.Write("\r" + new string(' ', 30) + "\r");
            }
            return (false, messages, "Timeout: Agent did not respond within the expected time.");
        }
        catch (Exception ex)
        {
            return (false, new List<ThreadMessage>(), $"Failed to get messages: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<ThreadMessage> Messages, string Response)> GetThreadMessagesStreamingAsync(
        string threadId,
        int maxRetries = 60,
        int delaySeconds = 2,
        int noNewMessageRetries = 3)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, new List<ThreadMessage>(), "Configuration not found. Please run 'srectl init' first.");
            }

            var allMessages = new List<ThreadMessage>();
            var lastDisplayedMessageCount = 0;
            var retryCount = 0;
            var noNewMessageCount = 0;
            var hasSeenAgentResponse = false;
            var waitingPrinted = false;

            // Enhanced shimmer utilities
            static bool UseAnsi()
            {
                if (Console.IsOutputRedirected) return false;
                if (!OperatingSystem.IsWindows()) return true;
                var noColor = Environment.GetEnvironmentVariable("NO_COLOR");
                if (!string.IsNullOrEmpty(noColor)) return false;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WT_SESSION"))) return true;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"))) return true;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ConEmuANSI"))) return true;
                return true;
            }

            bool ansi = UseAnsi();
            const string Mono = "\x1b[36m";
            const string Reset = "\x1b[0m";
            const string Dim = "\x1b[2m";
            const string Bold = "\x1b[1m";

            string RenderFrame(int idx)
            {
                if (!ansi) return new string('.', (idx % 3) + 1).PadRight(3, ' ');
                var dots = new[]
                {
                idx % 3 == 0 ? $"{Mono}{Bold}●{Reset}" : $"{Mono}{Dim}●{Reset}",
                idx % 3 == 1 ? $"{Mono}{Bold}●{Reset}" : $"{Mono}{Dim}●{Reset}",
                idx % 3 == 2 ? $"{Mono}{Bold}●{Reset}" : $"{Mono}{Dim}●{Reset}",
            };
                return string.Join("", dots);
            }

            void PrintShimmer(int frame, string label = " reasoning ")
            {
                var frameText = RenderFrame(frame);
                var spacer = "  ";
                if (ansi)
                {
                    Console.Write($"\r{Mono}{Dim}{label}{Reset}{frameText}{spacer}");
                }
                else
                {
                    Console.Write($"\r{label}{frameText}{spacer}");
                }
            }

            void ClearLine()
            {
                try
                {
                    var width = Console.IsOutputRedirected ? 80 : Math.Max(20, Console.WindowWidth - 1);
                    Console.Write("\r" + new string(' ', width) + "\r");
                }
                catch
                {
                    Console.Write("\r                                                  \r");
                }
            }

            // Show initial status
            Console.WriteLine("Conversation:");
            Console.WriteLine("═══════════════");
            Console.WriteLine();

            int frame = 0;

            // START SPINNER IMMEDIATELY - this fixes spinner not showing on early failures
            PrintShimmer(frame++, " connecting ");
            waitingPrinted = true;

            while (retryCount < maxRetries)
            {
                try
                {
                    var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages";
                    var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                    var (response, content, responseTime) = await MakeHttpRequestAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var currentMessages = ParseThreadMessages(content);
                        if (currentMessages == null)
                        {
                            // Invalid JSON response
                            if (waitingPrinted)
                            {
                                ClearLine();
                                waitingPrinted = false;
                            }
                            Console.WriteLine("⚠️  Received invalid response format from server");

                            retryCount++;
                            if (retryCount < maxRetries)
                            {
                                PrintShimmer(frame++, " retrying ");
                                waitingPrinted = true;
                                await Task.Delay(1000); // Longer delay for retry
                            }
                            continue;
                        }

                        // Sort messages by timestamp
                        currentMessages = currentMessages.OrderBy(m => m.Timestamp).ToList();

                        // Display new messages
                        if (currentMessages.Count > lastDisplayedMessageCount)
                        {
                            if (waitingPrinted)
                            {
                                ClearLine();
                                waitingPrinted = false;
                            }

                            // Display only the new messages
                            for (int i = lastDisplayedMessageCount; i < currentMessages.Count; i++)
                            {
                                var msg = currentMessages[i];
                                var roleLabel = msg.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase) ? "SRE Agent" : "You";
                                var ts = msg.Timestamp.ToString("HH:mm:ss");
                                Console.WriteLine($"{roleLabel} ({ts}):");
                                Console.WriteLine($"   {msg.Text}");
                                Console.WriteLine();
                            }

                            lastDisplayedMessageCount = currentMessages.Count;
                            allMessages = currentMessages;
                            noNewMessageCount = 0;
                        }
                        else
                        {
                            noNewMessageCount++;
                        }

                        // Check if we have agent responses
                        var agentMessages = currentMessages.Where(m => m.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (agentMessages.Any())
                        {
                            hasSeenAgentResponse = true;

                            // If we've seen agent responses and haven't received new messages for a while, assume we're done
                            if (noNewMessageCount >= noNewMessageRetries)
                            {
                                if (waitingPrinted)
                                {
                                    ClearLine();
                                }
                                return (true, allMessages, "Conversation complete");
                            }
                        }

                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            // Show appropriate shimmer based on state
                            string label = hasSeenAgentResponse ? " continuing " : " reasoning ";
                            PrintShimmer(frame++, label);
                            waitingPrinted = true;
                            await Task.Delay(150);
                        }
                    }
                    else
                    {
                        if (waitingPrinted)
                        {
                            ClearLine();
                            waitingPrinted = false;
                        }

                        // Show error and retry if we have retries left
                        Console.WriteLine($"⚠️  Request failed ({response.StatusCode}), retrying...");

                        retryCount++;
                        if (retryCount < maxRetries)
                        {
                            PrintShimmer(frame++, " retrying ");
                            waitingPrinted = true;
                            await Task.Delay(2000); // Longer delay for error retry
                        }
                        else
                        {
                            return (false, new List<ThreadMessage>(), $"Failed to get messages: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    if (waitingPrinted)
                    {
                        ClearLine();
                        waitingPrinted = false;
                    }

                    Console.WriteLine($"⚠️  Network error: {ex.Message}, retrying...");

                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        PrintShimmer(frame++, " reconnecting ");
                        waitingPrinted = true;
                        await Task.Delay(3000); // Longer delay for network errors
                    }
                    else
                    {
                        return (false, new List<ThreadMessage>(), $"Network error: {ex.Message}");
                    }
                }
            }

            if (waitingPrinted)
            {
                ClearLine();
            }

            if (hasSeenAgentResponse)
            {
                return (true, allMessages, "Conversation complete (timeout reached but messages were received)");
            }
            else
            {
                return (false, allMessages, "Timeout: Agent did not respond within the expected time.");
            }
        }
        catch (Exception ex)
        {
            return (false, new List<ThreadMessage>(), $"Failed to get messages: {ex.Message}");
        }
    }

    /// <summary>
    /// Robust JSON message parsing with error handling
    /// </summary>
    private List<ThreadMessage>? ParseThreadMessages(string content)
    {
        try
        {
            var jsonDoc = JsonDocument.Parse(content);

            // Handle empty content
            if (string.IsNullOrWhiteSpace(content))
            {
                return new List<ThreadMessage>();
            }

            // Extract messages from various response formats
            JsonElement messagesElement;
            if (jsonDoc.RootElement.TryGetProperty("value", out messagesElement) &&
                messagesElement.ValueKind == JsonValueKind.Array)
            {
                // Standard format: { "value": [...] }
            }
            else if (jsonDoc.RootElement.TryGetProperty("messages", out messagesElement) &&
                     messagesElement.ValueKind == JsonValueKind.Array)
            {
                // Alternative format: { "messages": [...] }
            }
            else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
            {
                // Direct array format: [...]
                messagesElement = jsonDoc.RootElement;
            }
            else
            {
                // Unknown format
                Console.WriteLine($"⚠️  Unexpected response format: {content.Substring(0, Math.Min(100, content.Length))}...");
                return null;
            }

            var messages = new List<ThreadMessage>();
            foreach (var messageElement in messagesElement.EnumerateArray())
            {
                try
                {
                    var id = messageElement.TryGetProperty("id", out var idElement) ?
                        idElement.GetString() ?? "" : "";
                    var text = messageElement.TryGetProperty("text", out var textElement) ?
                        textElement.GetString() ?? "" : "";
                    var timestamp = messageElement.TryGetProperty("timeStamp", out var timestampElement) ?
                        timestampElement.GetDateTime() : DateTime.MinValue;

                    var authorRole = "User";
                    var authorUserId = "";
                    var authorDisplayName = "";

                    if (messageElement.TryGetProperty("author", out var authorElement))
                    {
                        authorRole = authorElement.TryGetProperty("role", out var roleElement) ?
                            roleElement.GetString() ?? "User" : "User";
                        authorUserId = authorElement.TryGetProperty("userId", out var userIdElement) ?
                            userIdElement.GetString() ?? "" : "";
                        authorDisplayName = authorElement.TryGetProperty("displayName", out var displayNameElement) ?
                            displayNameElement.GetString() ?? "" : "";
                    }

                    // Skip empty messages
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        messages.Add(new ThreadMessage(id, text, timestamp, authorRole, authorUserId, authorDisplayName));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️  Failed to parse message: {ex.Message}");
                    // Continue processing other messages
                }
            }

            return messages;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"⚠️  JSON parsing error: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Unexpected error parsing messages: {ex.Message}");
            return null;
        }
    }

    public async Task<(bool Success, List<ThreadMessage> Messages, string Response)> TrackThreadAsync(string threadId, int maxRetries = 60, int delaySeconds = 2, int noNewMessageRetries = 3)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, new List<ThreadMessage>(), "Configuration not found. Please run 'srectl init' first.");
            }

            var allMessages = new List<ThreadMessage>();
            var lastDisplayedMessageCount = 0;
            var retryCount = 0;
            var noNewMessageCount = 0;
            var hasSeenAgentResponse = false;
            var hasDisplayedInitialMessages = false;

            // For snappy spinner animation with shimmer effect
            string[] dots = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
            string[] colors = new[] { "[36m", "[96m", "[37m", "[97m" }; // Mono cyan color
            int dotIndex = 0;
            int colorIndex = 0;
            bool waitingPrinted = false;

            Console.WriteLine("Conversation:");
            Console.WriteLine("═══════════════");
            Console.WriteLine();

            while (retryCount < maxRetries)
            {
                var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                var (response, content, responseTime) = await MakeHttpRequestAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    var jsonDoc = JsonDocument.Parse(content);
                    var value = jsonDoc.RootElement.GetProperty("value");

                    var currentMessages = new List<ThreadMessage>();
                    foreach (var messageElement in value.EnumerateArray())
                    {
                        var id = messageElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
                        var text = messageElement.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? "" : "";
                        var timestamp = messageElement.TryGetProperty("timeStamp", out var timestampElement) ? timestampElement.GetDateTime() : DateTime.MinValue;

                        var authorRole = "User";
                        var authorUserId = "";
                        var authorDisplayName = "";

                        if (messageElement.TryGetProperty("author", out var authorElement))
                        {
                            authorRole = authorElement.TryGetProperty("role", out var roleElement) ? roleElement.GetString() ?? "User" : "User";
                            authorUserId = authorElement.TryGetProperty("userId", out var userIdElement) ? userIdElement.GetString() ?? "" : "";
                            authorDisplayName = authorElement.TryGetProperty("displayName", out var displayNameElement) ? displayNameElement.GetString() ?? "" : "";
                        }

                        currentMessages.Add(new ThreadMessage(id, text, timestamp, authorRole, authorUserId, authorDisplayName));
                    }

                    // Sort messages by timestamp
                    currentMessages = currentMessages.OrderBy(m => m.Timestamp).ToList();

                    // On first run, display all existing messages
                    if (!hasDisplayedInitialMessages)
                    {
                        if (waitingPrinted)
                        {
                            Console.Write("\r" + new string(' ', 50) + "\r");
                            waitingPrinted = false;
                        }

                        if (currentMessages.Count == 0)
                        {
                            Console.WriteLine("No messages found in this thread.");
                            Console.WriteLine();
                        }
                        else
                        {
                            // Display all existing messages
                            foreach (var msg in currentMessages)
                            {
                                var roleLabel = msg.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase) ? "SRE Agent" : "You";
                                var timestamp = msg.Timestamp.ToString("HH:mm:ss");
                                Console.WriteLine($"{roleLabel} ({timestamp}):");
                                Console.WriteLine($"   {msg.Text}");
                                Console.WriteLine();
                            }
                        }

                        lastDisplayedMessageCount = currentMessages.Count;
                        allMessages = currentMessages;
                        hasDisplayedInitialMessages = true;

                        // Check if we already have agent responses
                        var agentMessages = currentMessages.Where(m => m.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase)).ToList();
                        if (agentMessages.Any())
                        {
                            hasSeenAgentResponse = true;
                        }

                        Console.WriteLine("📡 Now tracking for new messages...");
                        Console.WriteLine();
                    }
                    // Display only new messages after initial display
                    else if (currentMessages.Count > lastDisplayedMessageCount)
                    {
                        if (waitingPrinted)
                        {
                            Console.Write("\r" + new string(' ', 50) + "\r");
                            waitingPrinted = false;
                        }

                        // Display only the new messages
                        for (int i = lastDisplayedMessageCount; i < currentMessages.Count; i++)
                        {
                            var msg = currentMessages[i];
                            var roleLabel = msg.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase) ? "SRE Agent" : "You";
                            var timestamp = msg.Timestamp.ToString("HH:mm:ss");
                            Console.WriteLine($"{roleLabel} ({timestamp}):");
                            Console.WriteLine($"   {msg.Text}");
                            Console.WriteLine();
                        }

                        lastDisplayedMessageCount = currentMessages.Count;
                        allMessages = currentMessages;
                        noNewMessageCount = 0; // Reset no new message counter
                    }
                    else
                    {
                        // No new messages received
                        noNewMessageCount++;
                    }

                    // Check if we have agent responses and determine completion
                    var currentAgentMessages = currentMessages.Where(m => m.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase)).ToList();
                    if (currentAgentMessages.Any())
                    {
                        hasSeenAgentResponse = true;

                        // If we've seen agent responses and haven't received new messages for a while, assume we're done
                        if (noNewMessageCount >= noNewMessageRetries)
                        {
                            if (waitingPrinted)
                            {
                                Console.Write("\r" + new string(' ', 50) + "\r");
                            }
                            return (true, allMessages, "Thread tracking complete");
                        }
                    }

                    retryCount++;
                    if (retryCount < maxRetries)
                    {
                        // Show waiting indicator only if we haven't seen agent response yet or are still getting messages
                        if (!hasSeenAgentResponse || noNewMessageCount < noNewMessageRetries)
                        {
                            string waitMsg = !hasSeenAgentResponse ?
                                $"{colors[colorIndex]}{dots[dotIndex]}[0m" :
                                $"{colors[colorIndex]}Monitoring for additional messages{dots[dotIndex]}[0m";
                            colorIndex = (colorIndex + 1) % colors.Length;
                            Console.Write($"\r{waitMsg}   ");
                            dotIndex = (dotIndex + 1) % dots.Length;
                            waitingPrinted = true;
                        }
                        await Task.Delay(150); // Much faster animation
                    }
                }
                else
                {
                    if (waitingPrinted)
                    {
                        Console.Write("\r" + new string(' ', 50) + "\r");
                    }
                    return (false, new List<ThreadMessage>(), $"Failed to get messages: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
                }
            }

            if (waitingPrinted)
            {
                Console.Write("\r" + new string(' ', 50) + "\r");
            }

            if (hasDisplayedInitialMessages)
            {
                return (true, allMessages, "Thread tracking complete (timeout reached)");
            }
            else
            {
                return (false, allMessages, "Timeout: Failed to retrieve thread messages.");
            }
        }
        catch (Exception ex)
        {
            return (false, new List<ThreadMessage>(), $"Failed to track thread: {ex.Message}");
        }
    }

    public async Task<(bool Success, List<ThreadInfo> Threads, string Response)> ListThreadsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, new List<ThreadInfo>(), "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);
                var value = jsonDoc.RootElement.GetProperty("value");

                var threads = new List<ThreadInfo>();
                foreach (var threadElement in value.EnumerateArray())
                {
                    var id = threadElement.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
                    var title = threadElement.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? "" : "";
                    var createdAt = threadElement.TryGetProperty("createdDateTime", out var createdElement) ? createdElement.GetDateTime() : DateTime.MinValue;
                    var lastMessageAt = threadElement.TryGetProperty("lastMessageAt", out var lastElement) ? lastElement.GetDateTime() : DateTime.MinValue;

                    threads.Add(new ThreadInfo(id, title, createdAt, lastMessageAt));
                }

                return (true, threads, "Threads retrieved successfully");
            }
            else
            {
                return (false, new List<ThreadInfo>(), $"Failed to list threads: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, new List<ThreadInfo>(), $"Failed to list threads: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> DeleteThreadAsync(string threadId)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"Thread {threadId} deleted successfully");
            }
            else
            {
                return (false, $"Failed to delete thread: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete thread: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> DeleteAgentAsync(string agentName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents/{agentName}";
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"Agent '{agentName}' deleted successfully");
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Parse conflict response to show dependent agents
                try
                {
                    var conflictData = JsonSerializer.Deserialize<JsonElement>(content);
                    if (conflictData.TryGetProperty("dependentAgents", out var dependentAgentsElement))
                    {
                        var dependentAgents = dependentAgentsElement.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();

                        var agentList = dependentAgents.Any() ? string.Join(", ", dependentAgents) : "unknown agents";
                        return (false, $"Cannot delete agent '{agentName}': it is used by the following agents: {agentList}");
                    }

                    if (conflictData.TryGetProperty("message", out var messageElement))
                    {
                        return (false, messageElement.GetString() ?? $"Conflict deleting agent '{agentName}'");
                    }
                }
                catch
                {
                    // Fall back to generic conflict message if parsing fails
                }

                return (false, $"Cannot delete agent '{agentName}': it is being used by other agents or tools");
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (false, $"Agent '{agentName}' not found");
            }
            else
            {
                return (false, $"Failed to delete agent: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete agent: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Response)> DeleteToolAsync(string toolName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/tools/{toolName}";
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"Tool '{toolName}' deleted successfully");
            }
            else if (response.StatusCode == HttpStatusCode.Conflict)
            {
                // Parse conflict response to show dependent agents
                try
                {
                    var conflictData = JsonSerializer.Deserialize<JsonElement>(content);
                    if (conflictData.TryGetProperty("dependentAgents", out var dependentAgentsElement))
                    {
                        var dependentAgents = dependentAgentsElement.EnumerateArray()
                            .Select(x => x.GetString())
                            .Where(x => !string.IsNullOrEmpty(x))
                            .ToList();

                        var agentList = dependentAgents.Any() ? string.Join(", ", dependentAgents) : "unknown agents";
                        return (false, $"Cannot delete tool '{toolName}': it is used by the following agents: {agentList}");
                    }

                    if (conflictData.TryGetProperty("message", out var messageElement))
                    {
                        return (false, messageElement.GetString() ?? $"Conflict deleting tool '{toolName}'");
                    }
                }
                catch
                {
                    // Fall back to generic conflict message if parsing fails
                }

                return (false, $"Cannot delete tool '{toolName}': it is being used by agents");
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (false, $"Tool '{toolName}' not found");
            }
            else
            {
                return (false, $"Failed to delete tool: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Failed to delete tool: {ex.Message}");
        }
    }

    // Scheduled Tasks API methods
    public async Task<List<JsonNode>?> GetScheduledTasksAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                DebugLogger.Debug("No configuration found");
                return null;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks";
            DebugLogger.LogHttpRequest("GET", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonArray = JsonSerializer.Deserialize<JsonArray>(responseContent);
                return jsonArray?.Select(item => item?.AsObject()).Where(item => item != null).Cast<JsonNode>().ToList()!;
            }

            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"Error getting scheduled tasks: {ex.Message}");
            return null;
        }
    }

    public async Task<JsonNode?> GetScheduledTaskAsync(string taskId)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                DebugLogger.Debug("No configuration found");
                return null;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks/{taskId}";
            DebugLogger.LogHttpRequest("GET", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            if (response.IsSuccessStatusCode)
            {
                return JsonNode.Parse(responseContent);
            }

            return null;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"Error getting scheduled task {taskId}: {ex.Message}");
            return null;
        }
    }

    public async Task<(bool success, string message)> CreateScheduledTaskAsync(JsonNode task)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "No configuration found");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks";
            var json = task.ToJsonString();
            DebugLogger.LogHttpRequest("POST", url, "application/json", json);

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            return response.IsSuccessStatusCode 
                ? (true, "Scheduled task created successfully") 
                : (false, responseContent);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to create scheduled task: {ex.Message}");
        }
    }

    public async Task<bool> UpdateScheduledTaskAsync(string taskId, JsonNode updates)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return false;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks/{taskId}";
            var json = updates.ToJsonString();
            DebugLogger.LogHttpRequest("PUT", url, "application/json", json);

            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"Error updating scheduled task {taskId}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteScheduledTaskAsync(string taskId)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return false;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks/{taskId}";
            DebugLogger.LogHttpRequest("DELETE", url);

            var request = new HttpRequestMessage(HttpMethod.Delete, url);
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"Error deleting scheduled task {taskId}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> PauseScheduledTaskAsync(string taskId)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return false;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks/{taskId}/pause";
            DebugLogger.LogHttpRequest("POST", url);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"Error pausing scheduled task {taskId}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> ResumeScheduledTaskAsync(string taskId)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return false;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/scheduledtasks/{taskId}/resume";
            DebugLogger.LogHttpRequest("POST", url);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            
            var response = await _httpClient.SendAsync(request);
            var responseContent = await response.Content.ReadAsStringAsync();
            DebugLogger.LogHttpResponse((int)response.StatusCode, response.ReasonPhrase ?? "", responseContent);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug($"Error resuming scheduled task {taskId}: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    /// <summary>
    /// Gets the configuration for internal service use.
    /// </summary>
    public async Task<Agent.Cli.Models.CliConfiguration?> GetConfigurationAsync()
    {
        return await _configService.LoadConfigurationAsync();
    }

    /// <summary>
    /// Gets the YAML configuration for a specific agent from the remote server.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve</param>
    /// <returns>Success status, YAML content, and error message</returns>
    public async Task<(bool Success, string YamlContent, string ErrorMessage)> GetAgentConfigurationAsync(string agentName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", "Configuration not found. Please run 'srectl init' first.");
            }
            // Only collection API exists: fetch collection, find by name, convert JSON->YAML
            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var (response, content, _) = await MakeHttpRequestAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return (false, "", $"Failed to list agents: {response.StatusCode}");
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.Contains("html"))
            {
                return (false, "", "Server returned HTML instead of JSON. Check the API URL/auth.");
            }
            if (!LooksLikeJson(content))
            {
                return (false, "", "Unexpected response format for agents list");
            }

            // Parse possible shapes: { data: [...] }, { agents: [...] }, or bare array
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                JsonElement agentsArray;
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    agentsArray = dataEl;
                }
                else if (jsonDoc.RootElement.TryGetProperty("agents", out var agentsEl) && agentsEl.ValueKind == JsonValueKind.Array)
                {
                    agentsArray = agentsEl;
                }
                else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    agentsArray = jsonDoc.RootElement;
                }
                else
                {
                    return (false, "", "No agents array found in response");
                }

                foreach (var agent in agentsArray.EnumerateArray())
                {
                    if (agent.ValueKind != JsonValueKind.Object) continue;
                    if (!agent.TryGetProperty("name", out var nameEl)) continue;
                    var name = nameEl.GetString();
                    if (!string.Equals(name, agentName, StringComparison.OrdinalIgnoreCase)) continue;

                    // Convert the agent JsonElement into plain objects (no cycles), then YAML
                    var plainAgent = ConvertJsonElementToPlainObject(agent);
                    var wrapper = new AgentConfigurationWrapper
                    {
                        Spec = new AgentSpec { Agent = plainAgent! }
                    };
                    var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                        .DisableAliases()
                        .Build();
                    var yaml = serializer.Serialize(wrapper);
                    return (true, yaml, "");
                }

                return (false, "", $"Agent '{agentName}' not found on server");
            }
            catch (Exception ex)
            {
                return (false, "", $"Failed to parse agents list: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetAgentConfiguration failed: {ex.Message}");
            return (false, "", $"Failed to retrieve agent configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the YAML configuration for a specific tool from the remote server.
    /// </summary>
    /// <param name="toolName">The name of the tool to retrieve</param>
    /// <returns>Success status, YAML content, and error message</returns>
    public async Task<(bool Success, string YamlContent, string ErrorMessage)> GetToolConfigurationAsync(string toolName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", "Configuration not found. Please run 'srectl init' first.");
            }
            // Only collection API exists: fetch collection, find by name, convert JSON->YAML
            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/tools";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var (response, content, _) = await MakeHttpRequestAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return (false, "", $"Failed to list tools: {response.StatusCode}");
            }
            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.Contains("html"))
            {
                return (false, "", "Server returned HTML instead of JSON. Check the API URL/auth.");
            }
            if (!LooksLikeJson(content))
            {
                return (false, "", "Unexpected response format for tools list");
            }

            // Parse possible shapes: { data: [...] } or bare array
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                JsonElement toolsArray;
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    toolsArray = dataEl;
                }
                else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    toolsArray = jsonDoc.RootElement;
                }
                else
                {
                    return (false, "", "No tools array found in response");
                }

                foreach (var tool in toolsArray.EnumerateArray())
                {
                    if (tool.ValueKind != JsonValueKind.Object) continue;
                    // Tool name can be at top-level name or inside nested 'tool' object depending on API
                    string? name = null;
                    JsonElement? toolPayload = null;
                    if (tool.TryGetProperty("name", out var nameEl))
                    {
                        name = nameEl.GetString();
                        toolPayload = tool;
                    }
                    else if (tool.TryGetProperty("tool", out var toolObj) && toolObj.ValueKind == JsonValueKind.Object && toolObj.TryGetProperty("name", out var innerNameEl))
                    {
                        name = innerNameEl.GetString();
                        toolPayload = toolObj;
                    }

                    if (!string.Equals(name, toolName, StringComparison.OrdinalIgnoreCase)) continue;

                    // Convert the tool object to a plain tool YAML (no ToolList wrapper)
                    // We expect the payload to represent a single tool definition that includes fields like 'type', 'name', etc.
                    var payload = toolPayload ?? tool;
                    var plainTool = ConvertJsonElementToPlainObject(payload);

                    // Prune empty nodes for cleaner tool YAML; do not use agent-only visitors
                    var pruned = Agent.Cli.Helpers.YamlHelper.PruneEmptyNodes(plainTool) ?? plainTool;
                    var serializer = new YamlDotNet.Serialization.SerializerBuilder()
                        .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                        .ConfigureDefaultValuesHandling(YamlDotNet.Serialization.DefaultValuesHandling.OmitNull)
                        .DisableAliases()
                        .Build();
                    var yaml = serializer.Serialize(pruned);
                    return (true, yaml, "");
                }

                return (false, "", $"Tool '{toolName}' not found on server");
            }
            catch (Exception ex)
            {
                return (false, "", $"Failed to parse tools list: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetToolConfiguration failed: {ex.Message}");
            return (false, "", $"Failed to retrieve tool configuration: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the HTTP client for internal service use.
    /// </summary>
    public HttpClient GetHttpClient()
    {
        return _httpClient;
    }

    private static bool LooksLikeHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var trimmed = content.TrimStart();
        return trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<!doctype html", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return false;
        var t = content.TrimStart();
        return t.StartsWith("{") || t.StartsWith("[");
    }

    private static object? ConvertJsonElementToPlainObject(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>();
                foreach (var prop in element.EnumerateObject())
                {
                    dict[prop.Name] = ConvertJsonElementToPlainObject(prop.Value);
                }
                return dict;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElementToPlainObject(item));
                }
                return list;
            case JsonValueKind.String:
                return element.GetString();
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var l)) return l;
                if (element.TryGetDouble(out var d)) return d;
                return element.ToString();
            case JsonValueKind.True:
                return true;
            case JsonValueKind.False:
                return false;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return null;
            default:
                return element.ToString();
        }
    }

    private static string ConvertAgentJsonToYaml(string json)
    {
        var node = JsonNode.Parse(json);
        var wrapper = new AgentConfigurationWrapper
        {
            Spec = new AgentSpec { Agent = node! }
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return serializer.Serialize(wrapper);
    }

    private static string ConvertToolJsonToYaml(string json)
    {
        var node = JsonNode.Parse(json);
        var wrapper = new ToolListWrapper
        {
            Spec = new ToolSpec { Tools = new List<object> { node! } }
        };

        var serializer = new SerializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();
        return serializer.Serialize(wrapper);
    }

    /// <summary>
    /// Gets an access token for internal service use.
    /// </summary>
    public async Task<string?> GetAccessTokenForInternalUseAsync()
    {
        return await _tokenService.GetAccessTokenAsync();
    }

    public async Task<(bool Success, string Response)> ListDataConnectorsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/dataconnectors";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(content);

                var connectorList = new List<string>();

                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var connectors = jsonDoc.RootElement.EnumerateArray().ToArray();

                    if (connectors.Length == 0)
                    {
                        connectorList.Add("No data connectors found on the server.");
                        connectorList.Add("Data connectors are configured through server settings.");
                    }
                    else
                    {
                        foreach (var connector in connectors)
                        {
                            var name = connector.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Unknown" : "Unknown";
                            var connectorType = connector.TryGetProperty("connectorType", out var typeElement) ? typeElement.GetString() ?? "Unknown" : "Unknown";
                            var dataSource = connector.TryGetProperty("dataSource", out var dataSourceElement) ? dataSourceElement.GetString() ?? "Not specified" : "Not specified";
                            var identity = connector.TryGetProperty("identity", out var identityElement) ? identityElement.GetString() ?? "Not specified" : "Not specified";

                            var connectorOutput = Helpers.ConsoleUI.CaptureOutput(() =>
                            {
                                Console.WriteLine();
                                Helpers.ConsoleUI.WriteBullet(name, ConsoleColor.White, 0);

                                Helpers.ConsoleUI.WriteKeyValue("  Type", connectorType, 13, ConsoleColor.Gray, ConsoleColor.White);
                                Helpers.ConsoleUI.WriteKeyValue("  Data Source", dataSource, 13, ConsoleColor.Gray, ConsoleColor.White);
                                if (!string.IsNullOrEmpty(identity) && identity != "Not specified")
                                {
                                    Helpers.ConsoleUI.WriteKeyValue("  Identity", identity, 13, ConsoleColor.Gray, ConsoleColor.White);
                                }
                            });
                            connectorList.Add(connectorOutput);
                        }

                        connectorList.Add($"\nTotal: {connectors.Length} data connector(s)");
                    }
                }
                else
                {
                    connectorList.Add("\nUnexpected response format from server.");
                }

                return (true, string.Join("\n", connectorList));
            }
            else
            {
                return (false, $"❌ Failed to list data connectors: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to list data connectors: {ex.Message}");
        }
    }

    /// <summary>
    /// Uploads documents to the SRE Agent's memory storage.
    /// </summary>
    /// <param name="filePaths">List of absolute file paths to upload</param>
    /// <param name="triggerIndexing">Whether to trigger indexing after upload</param>
    /// <returns>Success status and response message</returns>
    public async Task<(bool Success, string Response)> UploadDocumentsAsync(List<string> filePaths, bool triggerIndexing = true)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            if (filePaths == null || filePaths.Count == 0)
            {
                return (false, "No files provided for upload.");
            }

            // Validate all files exist before starting upload
            var invalidFiles = filePaths.Where(file => !File.Exists(file)).ToList();
            if (invalidFiles.Any())
            {
                return (false, $"Files not found: {string.Join(", ", invalidFiles.Select(Path.GetFileName))}");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/AgentMemory/upload";

            using var multipartContent = new MultipartFormDataContent();

            // Add indexing parameter
            multipartContent.Add(new StringContent(triggerIndexing.ToString().ToLower()), "triggerIndexing");

            // Add files to the multipart content
            var fileContents = new List<IDisposable>();
            try
            {
                foreach (var filePath in filePaths)
                {
                    var fileStream = File.OpenRead(filePath);
                    fileContents.Add(fileStream);

                    var fileName = Path.GetFileName(filePath);
                    var fileContent = new StreamContent(fileStream);
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

                    multipartContent.Add(fileContent, "files", fileName);
                }

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Content = multipartContent;

                var (response, content, responseTime) = await MakeHttpRequestAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        // Try to parse the JSON response for detailed feedback
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.TryGetProperty("message", out var messageElement))
                        {
                            var message = messageElement.GetString() ?? "Upload completed successfully";
                            var indexingStatus = triggerIndexing ? " and indexing triggered" : "";
                            return (true, $"Successfully uploaded {filePaths.Count} file(s){indexingStatus}. {message}");
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, fall back to simple success message
                    }

                    var indexingSuffix = triggerIndexing ? " and indexing triggered" : "";
                    return (true, $"Successfully uploaded {filePaths.Count} file(s){indexingSuffix}.");
                }
                else
                {
                    // Try to extract error details from response
                    var errorMessage = "Upload failed";
                    try
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                        {
                            errorMessage = errorElement.GetString() ?? errorMessage;
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("detail", out var detailElement))
                        {
                            errorMessage = detailElement.ToString();
                        }
                    }
                    catch
                    {
                        // If JSON parsing fails, use the raw content
                        if (!string.IsNullOrWhiteSpace(content))
                        {
                            errorMessage = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
                        }
                    }

                    return (false, $"Failed to upload documents: {response.StatusCode} - {errorMessage}");
                }
            }
            finally
            {
                // Dispose all file streams
                foreach (var disposable in fileContents)
                {
                    disposable?.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            return (false, $"Upload failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Searches documents in the SRE Agent's memory storage.
    /// </summary>
    /// <param name="query">Search query to find relevant documents</param>
    /// <returns>Success status and response message with search results</returns>
    public async Task<(bool Success, string Response)> SearchDocumentsAsync(string query)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                return (false, "Search query cannot be empty.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/AgentMemory/documents";

            // Create request payload
            var requestPayload = new
            {
                query = query,
                limit = 10 // Default limit for search results
            };

            // Try GET method with query parameter first
            var encodedQuery = Uri.EscapeDataString(query);
            var getRequestUrl = $"{requestUrl}?query={encodedQuery}&k=10";
            var request = new HttpRequestMessage(HttpMethod.Get, getRequestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                    var searchResults = new List<string>();

                    if (jsonDoc.RootElement.TryGetProperty("results", out var resultsElement) &&
                        resultsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var results = resultsElement.EnumerateArray().ToArray();

                        if (results.Length == 0)
                        {
                            searchResults.Add("No documents found matching your query.");
                            searchResults.Add("");
                            searchResults.Add("Try:");
                            searchResults.Add("• Using different keywords");
                            searchResults.Add("• Making your query more general");
                            searchResults.Add("• Checking if documents have been uploaded and indexed");
                        }
                        else
                        {
                            for (int i = 0; i < results.Length; i++)
                            {
                                var result = results[i];
                                var title = result.TryGetProperty("title", out var titleElement) ?
                                    titleElement.GetString() ?? "Untitled" : "Untitled";
                                var content_snippet = result.TryGetProperty("content", out var contentElement) ?
                                    contentElement.GetString() ?? "" : "";
                                var score = result.TryGetProperty("score", out var scoreElement) ?
                                    scoreElement.GetDecimal() : 0m;
                                var source = result.TryGetProperty("source", out var sourceElement) ?
                                    sourceElement.GetString() ?? "Unknown" : "Unknown";

                                var resultOutput = Helpers.ConsoleUI.CaptureOutput(() =>
                                {
                                    Console.WriteLine();
                                    Helpers.ConsoleUI.WriteBullet($"Result {i + 1}: {title}", ConsoleColor.White, 0);

                                    Helpers.ConsoleUI.WriteKeyValue("  Source", source, 15, ConsoleColor.Gray, ConsoleColor.White);
                                    Helpers.ConsoleUI.WriteKeyValue("  Relevance", $"{score:F2}", 15, ConsoleColor.Gray, ConsoleColor.White);

                                    if (!string.IsNullOrEmpty(content_snippet))
                                    {
                                        // Truncate content snippet if too long
                                        var snippet = content_snippet.Length > 200 ?
                                            content_snippet.Substring(0, 200) + "..." : content_snippet;
                                        Helpers.ConsoleUI.WriteKeyValue("  Content", snippet, 15, ConsoleColor.Gray, ConsoleColor.White);
                                    }
                                });
                                searchResults.Add(resultOutput);
                            }

                            searchResults.Add($"Found {results.Length} document(s) matching your query.");
                        }
                    }
                    else if (jsonDoc.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        var message = messageElement.GetString() ?? "Search completed successfully";
                        searchResults.Add(message);
                    }
                    else
                    {
                        searchResults.Add("Search completed, but no results were found.");
                    }

                    return (true, string.Join("\n", searchResults));
                }
                catch (System.Text.Json.JsonException)
                {
                    // If JSON parsing fails, return the raw content
                    return (true, $"Search completed successfully.\n\nResponse:\n{content}");
                }
            }
            else
            {
                // Try to extract error details from response
                var errorMessage = "Search failed";
                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                    if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        errorMessage = errorElement.GetString() ?? errorMessage;
                    }
                    else if (jsonDoc.RootElement.TryGetProperty("detail", out var detailElement))
                    {
                        errorMessage = detailElement.ToString();
                    }
                }
                catch
                {
                    // If JSON parsing fails, use the raw content
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        errorMessage = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
                    }
                }

                return (false, $"Failed to search documents: {response.StatusCode} - {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Search failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Triggers reindexing of all documents in the SRE Agent's memory storage.
    /// </summary>
    /// <returns>Success status and response message</returns>
    public async Task<(bool Success, string Response)> ReindexDocumentsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/AgentMemory/rebuildIndex";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                    if (jsonDoc.RootElement.TryGetProperty("message", out var messageElement))
                    {
                        var message = messageElement.GetString() ?? "Reindexing triggered successfully";
                        return (true, $"✅ {message}");
                    }
                }
                catch
                {
                    // If JSON parsing fails, fall back to simple success message
                }

                return (true, "✅ Document reindexing triggered successfully.");
            }
            else
            {
                // Try to extract error details from response
                var errorMessage = "Reindexing failed";
                try
                {
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(content);
                    if (jsonDoc.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        errorMessage = errorElement.GetString() ?? errorMessage;
                    }
                    else if (jsonDoc.RootElement.TryGetProperty("detail", out var detailElement))
                    {
                        errorMessage = detailElement.ToString();
                    }
                }
                catch
                {
                    // If JSON parsing fails, use the raw content
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        errorMessage = content.Length > 200 ? content.Substring(0, 200) + "..." : content;
                    }
                }

                return (false, $"❌ Failed to trigger reindexing: {response.StatusCode} - {errorMessage}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Reindexing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Finds a tool YAML file by searching recursively under the tools directory.
    /// Supports flexible folder organization.
    /// </summary>
    /// <param name="toolName">The name of the tool to find</param>
    /// <returns>The full path to the tool YAML file, or null if not found</returns>
    private static string? FindToolFile(string toolName)
    {
        var toolsDir = "tools";
        if (!Directory.Exists(toolsDir))
        {
            return null;
        }

        // First, try the legacy structure: tools/{toolName}/{toolName}.yaml
        var legacyPath = Path.Combine(toolsDir, toolName, $"{toolName}.yaml");
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        // Then try the flat structure: tools/{toolName}.yaml
        var flatPath = Path.Combine(toolsDir, $"{toolName}.yaml");
        if (File.Exists(flatPath))
        {
            return flatPath;
        }

        // Finally, search recursively for any YAML file with the matching tool name
        var yamlFiles = Directory.GetFiles(toolsDir, "*.yaml", SearchOption.AllDirectories);

        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Equals(toolName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    public async Task<List<JsonNode>?> GetIncidentFiltersAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("[ERROR] Configuration not found. Please run 'srectl init' first.");
                return null;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/filters";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<List<JsonNode>>(content);
            }
            else
            {
                Console.WriteLine($"[ERROR] Failed to get incident filters: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to get incident filters: {ex.Message}");
            return null;
        }
    }

    public async Task<(bool success, string message)> CreateIncidentFilterAsync(JsonNode filter)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var filterId = filter["id"]?.ToString();
            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/filters/{filterId}";
            var request = new HttpRequestMessage(HttpMethod.Put, url);

            var json = JsonSerializer.Serialize(filter);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return (true, "Success");
            }
            else
            {
                return (false, $"API call failed with status {response.StatusCode}: {content}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Exception: {ex.Message}");
        }
    }

    public async Task<bool> UpdateIncidentFilterAsync(string filterId, JsonNode filter)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("[ERROR] Configuration not found. Please run 'srectl init' first.");
                return false;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/filters/{filterId}";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            var json = JsonSerializer.Serialize(filter);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to update incident filter: {ex.Message}");
            return false;
        }
    }

    public async Task<List<JsonNode>?> GetIncidentHandlersAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("[ERROR] Configuration not found. Please run 'srectl init' first.");
                return null;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/handlers";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<List<JsonNode>>(content);
            }
            else
            {
                Console.WriteLine($"[ERROR] Failed to get incident handlers: {response.StatusCode}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to get incident handlers: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteIncidentHandlerAsync(string handlerId)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                Console.WriteLine("[ERROR] Configuration not found. Please run 'srectl init' first.");
                return false;
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/handlers/{handlerId}";
            var request = new HttpRequestMessage(HttpMethod.Delete, url);

            
            var (response, content, responseTime) = await MakeHttpRequestAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to delete incident handler: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates YAML structure to catch common indentation mistakes
    /// </summary>
    private static List<string> ValidateYamlStructure(Dictionary<string, object> rootDocument, Dictionary<string, object> specSection)
    {
        var errors = new List<string>();

        // Define agent properties that should be under 'spec', not at root level
        var agentProperties = new HashSet<string>
        {
            "name", "system_prompt", "tools", "handoffs", "mcp_tools",
            "temperature", "max_reflection_count", "handoff_description", "common_prompts",
            "common_tools", "allow_parallel_tool_calls", "agents_as_tools", "custom_reflection_note",
            "critic_prompt_path", "critic_on_handoff", "disable_document_retrieval",
            "instructions_override", "enable_handoff_prompt_override", "handoff_prompt_override",
            "user_prompt_override", "llm_model_name", "disable_common_prompts", "agent_type",
            "parameter_extraction_agent", "orchestration_start_agents", "result_summarization_prompt",
            "next_agent_mappings", "output_type"
        };

        // Check for agent properties at root level (common indentation mistake)
        foreach (var property in agentProperties)
        {
            if (rootDocument.ContainsKey(property))
            {
                errors.Add($"Property '{property}' should be under 'spec' section, not at root level. Check indentation.");
            }
        }

        // Check if spec section is missing required properties
        if (!specSection.ContainsKey("name"))
        {
            errors.Add("Required property 'name' is missing from 'spec' section");
        }

        // Check for required system_prompt property
        if (!specSection.ContainsKey("system_prompt"))
        {
            // Check if it's at root level due to indentation error
            if (rootDocument.ContainsKey("system_prompt"))
            {
                errors.Add("Property 'system_prompt' found at root level - should be under 'spec' section. Check indentation.");
            }
            else
            {
                errors.Add("Required property 'system_prompt' is missing from 'spec' section");
            }
        }

        // Check for invalid 'instructions' property usage
        if (specSection.ContainsKey("instructions"))
        {
            errors.Add("Use 'system_prompt' instead of 'instructions' in the 'spec' section");
        }

        // Validate that spec has some content
        if (specSection.Count == 0)
        {
            errors.Add("'spec' section is empty - agent properties should be defined here");
        }

        return errors;
    }
}

// Simple wrapper models for YAML structure
public class AgentConfigurationWrapper
{
    public string ApiVersion { get; set; } = "azuresre.ai/v1";
    public string Kind { get; set; } = "AgentConfiguration";
    public YamlMetadata Metadata { get; set; } = new YamlMetadata();
    public AgentSpec Spec { get; set; } = new AgentSpec();
}

public class ToolListWrapper
{
    public string ApiVersion { get; set; } = "azuresre.ai/v1";
    public string Kind { get; set; } = "ToolList";
    public YamlMetadata Metadata { get; set; } = new YamlMetadata();
    public ToolSpec Spec { get; set; } = new ToolSpec();
}

public class YamlMetadata
{
    public string Owner { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new List<string>();
    public string UpdatedAt { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

public class AgentSpec
{
    public object Agent { get; set; } = new object();
}

public class ToolSpec
{
    public List<object> Tools { get; set; } = new List<object>();
}

// New wrapper for combined agent and tools
public class CombinedAgentWrapper
{
    public string ApiVersion { get; set; } = "azuresre.ai/v1";
    public string Kind { get; set; } = "AgentConfiguration";
    public YamlMetadata Metadata { get; set; } = new YamlMetadata();
    public CombinedAgentSpec Spec { get; set; } = new CombinedAgentSpec();
}

public class CombinedAgentSpec
{
    public object Agent { get; set; } = new object();
    public List<object> Tools { get; set; } = new List<object>();
}

public record ThreadMessage(string Id, string Text, DateTime Timestamp, string AuthorRole, string AuthorUserId, string AuthorDisplayName);
public record ThreadInfo(string Id, string Title, DateTime CreatedAt, DateTime LastMessageAt);
