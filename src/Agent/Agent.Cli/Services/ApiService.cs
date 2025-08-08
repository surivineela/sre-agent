using Azure.Core;
using Azure.Identity;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Agent.Cli.Services;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Services;

public class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CliConfigurationService _configService;

    public ApiService()
    {
        _httpClient = new HttpClient();
        _configService = new CliConfigurationService();
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            var credential = new AzureCliCredential();
            var token = await credential.GetTokenAsync(new TokenRequestContext(new[] { "https://azuresre.dev/.default" }));
            return token.Token;
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool Success, string Response)> TestConnectionAsync(string resourceUrl)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var request = new HttpRequestMessage(HttpMethod.Get, $"{resourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents");

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(resourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    // Parse the response to get agent names using robust parsing
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
                        return (true, "✅ Connection successful! (Unexpected response format)");
                    }
                    
                    var agentNames = new List<string>();
                    
                    foreach (var agent in agents.EnumerateArray())
                    {
                        if (agent.TryGetProperty("name", out var nameElement))
                        {
                            agentNames.Add(nameElement.GetString() ?? "");
                        }
                    }

                    if (agentNames.Count == 0)
                    {
                        return (true, "✅ Connection successful! No agents found.");
                    }
                    else
                    {
                        return (true, $"✅ Connection successful! Found {agentNames.Count} agents: {string.Join(", ", agentNames)}");
                    }
                }
                catch (JsonException)
                {
                    return (true, "✅ Connection successful! (Server returned non-JSON response)");
                }
            }
            else
            {
                return (false, FormatConnectionError(response, content, resourceUrl));
            }
        }
        catch (HttpRequestException ex)
        {
            return (false, $"❌ Network connection failed: {ex.Message}\n   Check if the URL is correct and accessible: {resourceUrl}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            return (false, $"❌ Connection timed out: {resourceUrl}\n   The server may be unreachable or overloaded.");
        }
        catch (JsonException ex)
        {
            return (false, $"❌ Invalid JSON response from server: {ex.Message}\n   The server may have returned an error page instead of expected data.\n   This often indicates authentication or permission issues.");
        }
        catch (Exception ex)
        {
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
            errorMessage += "\n   This typically indicates authentication or authorization issues.";
            
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
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            // Check if agent YAML file exists
            var agentFilePath = Path.Combine("agents", $"{agentName}.yaml");
            if (!File.Exists(agentFilePath))
            {
                // Try the subdirectory structure
                var agentFilePathSubdir = Path.Combine("agents", agentName, $"{agentName}.yaml");
                if (!File.Exists(agentFilePathSubdir))
                {
                    return (false, $"Agent file not found: {agentFilePath} or {agentFilePathSubdir}");
                }
                agentFilePath = agentFilePathSubdir;
            }

            // Read the agent YAML file
            var agentYamlContent = await File.ReadAllTextAsync(agentFilePath);

            // Parse the agent YAML to extract the tools list
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            
            // Parse agent as dynamic object to extract tools list
            var agentDynamic = deserializer.Deserialize<Dictionary<string, object>>(agentYamlContent);
            var agentData = deserializer.Deserialize<object>(agentYamlContent);

            // Extract tools list from agent
            var toolNames = new List<string>();
            if (agentDynamic.TryGetValue("tools", out var toolsObj) && toolsObj is List<object> toolsList)
            {
                toolNames = toolsList.Cast<string>().ToList();
            }

            // Get available tools from local and remote sources
            var toolAvailabilityService = new ToolAvailabilityService(this);
            var (localTools, remoteTools, errors) = await toolAvailabilityService.GetAvailableToolsAsync();

            // DEBUG: Log the results
            Console.WriteLine($"🔍 DEBUG: Found {localTools.Count} local tools: {string.Join(", ", localTools.Take(5))}...");
            Console.WriteLine($"🔍 DEBUG: Found {remoteTools.Count} remote tools: {string.Join(", ", remoteTools.Take(5))}...");
            if (errors.Any())
            {
                Console.WriteLine($"🔍 DEBUG: Errors: {string.Join("; ", errors)}");
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
                        var toolYamlContent = await File.ReadAllTextAsync(toolFilePath);
                        var toolData = deserializer.Deserialize<object>(toolYamlContent);
                        toolsData.Add(toolData);
                        Console.WriteLine($"📦 Loaded tool: {toolName}");
                    }
                }
                else if (remoteTools.Contains(toolName))
                {
                    // Tool exists on server but not locally - this is okay
                    missingLocallyButRemote.Add(toolName);
                    Console.WriteLine($"🌐 Tool '{toolName}' exists on server (not loading locally)");
                }
                else
                {
                    // Tool doesn't exist locally or remotely
                    completelyMissingTools.Add(toolName);
                    Console.WriteLine($"⚠️  Tool '{toolName}' not found locally or on server");
                }
            }

            // Only fail if tools are completely missing (not available locally or remotely)
            if (completelyMissingTools.Count > 0)
            {
                var missingToolsList = string.Join(", ", completelyMissingTools);
                return (false, $"❌ Cannot apply agent '{agentName}': Referenced tools not found: {missingToolsList}. Please create the missing tools first or ensure they exist on the server.");
            }

            // Create the combined wrapper with agent and tools
            var combinedWrapper = new CombinedAgentWrapper
            {
                ApiVersion = config.ApiVersion ?? "agent.platform.ai/v1",
                Kind = "AgentConfiguration",
                Metadata = new YamlMetadata
                {
                    Owner = config.Owner ?? "your-team@example.com",
                    Version = config.Version ?? "1.0.0",
                    Tags = config.Tags?.Any() == true ? config.Tags : new List<string> { "example", "demo", "generic" },
                    CreatedAt = config.CreatedAt != default(DateTime) ? config.CreatedAt.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    UpdatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd")
                },
                Spec = new CombinedAgentSpec 
                { 
                    Agent = agentData,
                    Tools = toolsData
                }
            };

            // Serialize to YAML
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var wrappedYamlContent = serializer.Serialize(combinedWrapper);

            // Create the request
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply";
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
            request.Content = new StringContent(wrappedYamlContent, Encoding.UTF8, "application/yaml");

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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
                
                return (true, $"✅ Agent '{agentName}'{toolsMessage} applied successfully!");
            }
            else
            {
                return (false, $"❌ Failed to apply agent: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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
                    return (false, $"❌ Unexpected response format - no agents array found: {content}");
                }
                
                var agentList = new List<string>();
                agentList.Add("📋 Available Agents:");
                agentList.Add("==================");
                
                foreach (var agent in agents.EnumerateArray())
                {
                    var name = agent.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
                    var instructions = agent.TryGetProperty("instructions", out var instructionsElement) ? instructionsElement.GetString() : "";
                    var handoffDescription = agent.TryGetProperty("handoffDescription", out var handoffElement) ? handoffElement.GetString() : "";
                    var createdAt = agent.TryGetProperty("created_at", out var createdElement) ? createdElement.GetString() : "";
                    
                    agentList.Add($"\n🤖 {name}");
                    if (!string.IsNullOrEmpty(handoffDescription))
                    {
                        agentList.Add($"   Description: {handoffDescription}");
                    }
                    if (!string.IsNullOrEmpty(createdAt))
                    {
                        agentList.Add($"   Created: {createdAt}");
                    }
                    
                    // Get tools
                    if (agent.TryGetProperty("tools", out var toolsElement) && toolsElement.ValueKind == JsonValueKind.Array)
                    {
                        var tools = toolsElement.EnumerateArray().Select(t => t.GetString()).Where(t => !string.IsNullOrEmpty(t)).ToList();
                        if (tools.Any())
                        {
                            agentList.Add($"   Tools: {string.Join(", ", tools)}");
                        }
                    }
                    
                    // Get handoffs
                    if (agent.TryGetProperty("handoffs", out var handoffsElement) && handoffsElement.ValueKind == JsonValueKind.Array)
                    {
                        var handoffs = handoffsElement.EnumerateArray().Select(h => h.GetString()).Where(h => !string.IsNullOrEmpty(h)).ToList();
                        if (handoffs.Any())
                        {
                            agentList.Add($"   Handoffs: {string.Join(", ", handoffs)}");
                        }
                    }
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
                    
                    // Add pagination info if available
                    if (hasMore || pageIndex > 0)
                    {
                        var currentPageAgents = agents.GetArrayLength();
                        var startIndex = pageIndex * pageSize + 1;
                        var endIndex = startIndex + currentPageAgents - 1;
                        agentList.Add($"Showing agents {startIndex}-{endIndex} of {totalCount} (page {pageIndex + 1})");
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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Parse the response using the shared tool response parser
                var toolElements = ToolResponseParser.ParseToolElements(content);
                
                var toolList = new List<string>();
                toolList.Add("🔧 Available Tools:");
                toolList.Add("==================");
                
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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                // Parse the response using the shared tool response parser
                var toolElements = ToolResponseParser.ParseToolElements(content);
                
                var toolList = new List<string>();
                toolList.Add("🔧 Extended Tools:");
                toolList.Add("==================");
                
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
                    
                    // Add pagination info if available
                    if (hasMore || pageIndex > 0)
                    {
                        var currentPageTools = toolElements.Length;
                        var startIndex = pageIndex * pageSize + 1;
                        var endIndex = startIndex + currentPageTools - 1;
                        toolList.Add($"Showing tools {startIndex}-{endIndex} of {totalCount} (page {pageIndex + 1})");
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

            // Check if tool YAML file exists
            var toolFilePath = Path.Combine("tools", $"{toolName}.yaml");
            if (!File.Exists(toolFilePath))
            {
                // Try the subdirectory structure
                var toolFilePathSubdir = Path.Combine("tools", toolName, $"{toolName}.yaml");
                if (!File.Exists(toolFilePathSubdir))
                {
                    return (false, $"Tool file not found: {toolFilePath} or {toolFilePathSubdir}");
                }
                toolFilePath = toolFilePathSubdir;
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
                ApiVersion = config.ApiVersion ?? "agent.platform.ai/v1",
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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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

    public async Task<(bool Success, string GeneratedInstructions, List<string> RecommendedTools, string ErrorMessage)> GenerateSmartAgentAsync(string agentName, string? userInstructions = null)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", new List<string>(), "Configuration not found. Please run 'srectl init' first.");
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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "", new List<string>(), "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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

                return (true, generatedInstructions, recommendedTools, "");
            }
            else
            {
                return (false, "", new List<string>(), $"Failed to generate smart agent: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, "", new List<string>(), $"Failed to generate smart agent: {ex.Message}");
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

            // Create the request
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply";
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl);
            request.Content = new StringContent(yamlContent, Encoding.UTF8, "application/yaml");

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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

    public async Task<(bool Success, string ThreadId, string Response)> CreateThreadAsync(string message, string userId, string displayName)
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
                startMessage = new
                {
                    text = message,
                    userId = userId,
                    displayName = displayName
                },
                source = "Conversation"
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "", "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "", "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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

            // For blipping dots animation
            string[] dots = new[] {".", "..", "..."};
            int dotIndex = 0;
            bool waitingPrinted = false;

            while (retryCount < maxRetries)
            {
                var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages";
                var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

                // Add auth header if not localhost
                if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
                {
                    var token = await GetAccessTokenAsync();
                    if (string.IsNullOrEmpty(token))
                    {
                        return (false, new List<ThreadMessage>(), "Failed to get access token. Please run 'az login' first.");
                    }
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

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
                        string waitMsg = $"Waiting{dots[dotIndex]}";
                        Console.Write($"\r{waitMsg}   ");
                        dotIndex = (dotIndex + 1) % dots.Length;
                        waitingPrinted = true;
                        await Task.Delay(delaySeconds * 1000);
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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, new List<ThreadInfo>(), "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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

            // Add auth header if not localhost
            if (!CliConfigurationService.IsLocalhost(config.ResourceUrl))
            {
                var token = await GetAccessTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Failed to get access token. Please run 'az login' first.");
                }
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

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
    /// Gets the HTTP client for internal service use.
    /// </summary>
    public HttpClient GetHttpClient()
    {
        return _httpClient;
    }

    /// <summary>
    /// Gets an access token for internal service use.
    /// </summary>
    public async Task<string?> GetAccessTokenForInternalUseAsync()
    {
        return await GetAccessTokenAsync();
    }
}

// Simple wrapper models for YAML structure
public class AgentConfigurationWrapper
{
    public string ApiVersion { get; set; } = "agent.platform.ai/v1";
    public string Kind { get; set; } = "AgentConfiguration";
    public YamlMetadata Metadata { get; set; } = new YamlMetadata();
    public AgentSpec Spec { get; set; } = new AgentSpec();
}

public class ToolListWrapper
{
    public string ApiVersion { get; set; } = "agent.platform.ai/v1";
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
    public string ApiVersion { get; set; } = "agent.platform.ai/v1";
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
