// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Framework;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Services;

public partial class ApiService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ICliConfigurationService _configService;
    private readonly ITokenService _tokenService;
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly JsonSerializerOptions _camelCaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
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

    // Constructor for dependency injection (primarily for testing)
    public ApiService(HttpClient httpClient, ICliConfigurationService configService, ITokenService tokenService)
    {
        _httpClient = httpClient;
        _configService = configService;
        _tokenService = tokenService;
    }

    /// <summary>
    /// Recursively converts all property names in a JsonNode to camelCase.
    /// Also converts string values to proper types since YamlDotNet's untyped deserialization
    /// treats all scalars as strings (e.g., YAML 'false' becomes string "false").
    /// Uses a custom converter to avoid maintaining CLI-side POCOs that mirror API models.
    /// </summary>
    private static JsonNode? ConvertPropertyNamesToCamelCase(JsonNode? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is JsonObject obj)
        {
            var newObj = new JsonObject();
            foreach (var prop in obj)
            {
                var camelCaseKey = ToCamelCase(prop.Key);
                var convertedValue = ConvertPropertyNamesToCamelCase(prop.Value);
                newObj[camelCaseKey] = convertedValue?.DeepClone();
            }
            return newObj;
        }
        else if (node is JsonArray arr)
        {
            var newArr = new JsonArray();
            foreach (var item in arr)
            {
                var convertedItem = ConvertPropertyNamesToCamelCase(item);
                if (convertedItem != null)
                {
                    newArr.Add(convertedItem.DeepClone());
                }
            }
            return newArr;
        }
        else if (node is JsonValue val)
        {
            // YamlDotNet's untyped deserialization converts all scalars to strings.
            // Convert string representations back to proper types.
            try
            {
                var stringValue = val.GetValue<string>();

                // Convert boolean strings
                if (stringValue == "true")
                {
                    return JsonValue.Create(true);
                }
                if (stringValue == "false")
                {
                    return JsonValue.Create(false);
                }

                // Convert integer strings
                if (int.TryParse(stringValue, out var intValue))
                {
                    return JsonValue.Create(intValue);
                }

                // Convert decimal strings
                if (decimal.TryParse(stringValue, out var decimalValue))
                {
                    return JsonValue.Create(decimalValue);
                }

                // Return string as-is if not convertible
                return node.DeepClone();
            }
            catch
            {
                // Not a string type, return as-is (e.g., already a bool/number from JSON parsing)
                return node.DeepClone();
            }
        }
        else
        {
            // Other JsonValue types - return as-is
            return node.DeepClone();
        }
    }

    /// <summary>
    /// Converts a snake_case string to camelCase
    /// </summary>
    private static string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var parts = input.Split('_');
        if (parts.Length == 1)
        {
            // No underscores, just lowercase first letter
            return char.ToLowerInvariant(input[0]) + input.Substring(1);
        }

        // First part is all lowercase, rest are capitalized
        var result = parts[0].ToLowerInvariant();
        for (int i = 1; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]))
            {
                result += char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            }
        }
        return result;
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

    /// <summary>
    /// Makes an HTTP request and deserializes the response to the specified type with error handling.
    /// If T is string, returns the raw content directly without JSON deserialization.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the response to (or string for raw content)</typeparam>
    /// <param name="method">The HTTP method to use</param>
    /// <param name="relativeUrl">The relative URL path (will be appended to config.ResourceUrl)</param>
    /// <param name="requestBody">Optional request body content to send with the request</param>
    /// <returns>A tuple containing the deserialized response object, HTTP status code, and error message if any</returns>
    private async Task<(T? ResponseObject, HttpStatusCode StatusCode, string? ErrorMessage)> MakeHttpRequestAsync<T>(HttpMethod method, string relativeUrl, string? requestBody = null)
    {
        var config = await _configService.LoadConfigurationAsync();
        if (config == null)
        {
            return (default(T), HttpStatusCode.InternalServerError, "Configuration not found. Please run 'srectl init' first.");
        }

        var fullUrl = $"{config.ResourceUrl.TrimEnd('/')}/{relativeUrl.TrimStart('/')}";
        var request = new HttpRequestMessage(method, fullUrl);

        if (!string.IsNullOrEmpty(requestBody))
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        }

        var (response, content, responseTime) = await MakeHttpRequestAsync(request);

        // Check if the response is HTML instead of JSON
        var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.Contains("html"))
        {
            return (default(T), response.StatusCode, "Server returned HTML instead of JSON. Check the API URL/auth.");
        }

        // Check if the content looks like JSON
        if (!LooksLikeJson(content))
        {
            return (default(T), response.StatusCode, "Unexpected response format");
        }

        if (typeof(T) == typeof(string))
        {
            var errorMsg = !response.IsSuccessStatusCode ? $"Request failed: {response.StatusCode} - {content}" : null;
            return ((T)(object)content, response.StatusCode, errorMsg);
        }

        if (!response.IsSuccessStatusCode)
        {
            return (default(T), response.StatusCode, $"Request failed: {response.StatusCode} - {content}");
        }

        try
        {
            var deserializedObject = JsonSerializer.Deserialize<T>(content, _camelCaseJsonOptions);
            return (deserializedObject, response.StatusCode, null);
        }
        catch (JsonException ex)
        {
            return (default(T), response.StatusCode, $"Failed to deserialize response: {ex.Message}");
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

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v2/extendedAgent/agents";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, _) = await MakeHttpRequestAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return (false, new List<string>(), $"Failed to list agents: {response.StatusCode} - {content}");
            }

            try
            {
                var names = new List<string>();
                var jsonDoc = JsonDocument.Parse(content);

                // v2 API returns: { "value": [ { "name": "...", "type": "ExtendedAgent", "properties": {...} }, ... ] }
                if (!jsonDoc.RootElement.TryGetProperty("value", out var valueElement) || valueElement.ValueKind != JsonValueKind.Array)
                {
                    return (false, new List<string>(), "Unexpected response format - no 'value' array found");
                }

                foreach (var agentEnvelope in valueElement.EnumerateArray())
                {
                    if (agentEnvelope.TryGetProperty("name", out var nameEl))
                    {
                        var n = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(n)) names.Add(n);
                    }
                }
                return (true, names.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), "");
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
    /// Lists all tools using the ExtendedAgent alltools endpoint which includes both system and extended tools.
    /// </summary>
    public async Task<(bool Success, string Response)> ListAllToolsAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/alltools";
            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Parse the JSON response for the alltools endpoint
                using var jsonDoc = JsonDocument.Parse(content);
                var toolsList = new List<string>();

                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var tools = jsonDoc.RootElement.EnumerateArray().ToArray();

                    if (tools.Length == 0)
                    {
                        toolsList.Add("\nNo tools found on the server.");
                    }
                    else
                    {
                        foreach (var tool in tools)
                        {
                            var name = tool.TryGetProperty("name", out var nameElement) ? nameElement.GetString() : "Unknown";
                            var description = tool.TryGetProperty("description", out var descElement) ? descElement.GetString() : "";
                            var pluginName = tool.TryGetProperty("pluginName", out var pluginElement) ? pluginElement.GetString() : "";

                            var displayName = name ?? "Unknown";
                            if (!string.IsNullOrEmpty(pluginName))
                            {
                                displayName = $"{pluginName}.{displayName}";
                            }

                            if (!string.IsNullOrEmpty(description))
                            {
                                toolsList.Add($"  {displayName}: {description}");
                            }
                            else
                            {
                                toolsList.Add($"  {displayName}");
                            }
                        }
                        toolsList.Add($"\nTotal: {tools.Length} tool(s)");
                    }
                }
                else
                {
                    toolsList.Add("\nUnexpected response format from server.");
                }

                return (true, string.Join("\n", toolsList));
            }
            else
            {
                return (false, $"❌ Failed to list all tools: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to list all tools: {ex.Message}");
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

            // Prefer typed deserialization matching controller's response format
            try
            {
                // Case 1: New ExtendedAgentToolsResponse format: { data: { tools: { data: [...] } } }
                var jsonDoc = JsonDocument.Parse(content);
                if (jsonDoc.RootElement.TryGetProperty("data", out var outerDataElement) &&
                    outerDataElement.TryGetProperty("tools", out var toolsWrapperElement) &&
                    toolsWrapperElement.TryGetProperty("data", out var toolsDataElement))
                {
                    var tools = JsonSerializer.Deserialize<List<ToolListItem>>(toolsDataElement.GetRawText(), _jsonOptions);
                    if (tools != null && tools.Count > 0)
                    {
                        var namesNew = tools
                            .Select(t => t.Name)
                            .Where(n => !string.IsNullOrWhiteSpace(n))
                            .Select(n => n!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList();
                        return (true, namesNew, "");
                    }
                }

                // Case 2: Legacy PaginatedResponse<ToolListItem>
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

                // Case 3: bare array [ { name: ... } ]
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

                // Check for new nested structure: { data: { tools: { data: [...] } } }
                if (jsonDoc.RootElement.TryGetProperty("data", out var outerDataElement) &&
                    outerDataElement.TryGetProperty("tools", out var toolsWrapperElement) &&
                    toolsWrapperElement.TryGetProperty("data", out var toolsArrayElement))
                {
                    dataEl = toolsArrayElement;
                }
                // Check for legacy structure: { data: [...] }
                else if (jsonDoc.RootElement.TryGetProperty("data", out var de))
                {
                    dataEl = de;
                }
                // Check for bare array: [...]
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
                Incidents = Array.Empty<object>(),
                Tools = Array.Empty<object>()
            };

            var jsonContent = JsonSerializer.Serialize(requestPayload);
            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/generateInstructions";
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

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
            var request = new HttpRequestMessage(HttpMethod.Put, requestUrl)
            {
                Content = new StringContent(yamlContent, Encoding.UTF8, "application/yaml")
            };

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
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8)
            };
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
            var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
            {
                Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
            };

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
            string[] dots = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
            string[] colors = ["[36m", "[96m", "[37m", "[97m"]; // Mono cyan color
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
                    if (agentMessages.Count != 0)
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
                        currentMessages = [.. currentMessages.OrderBy(m => m.Timestamp)];

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
                        if (agentMessages.Count != 0)
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
                return [];
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
            string[] dots = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
            string[] colors = ["[36m", "[96m", "[37m", "[97m"]; // Mono cyan color
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
                    currentMessages = [.. currentMessages.OrderBy(m => m.Timestamp)];

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
                        if (agentMessages.Count != 0)
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
                    if (currentAgentMessages.Count != 0)
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

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v2/extendedAgent/agents/{agentName}";
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);

            var (response, content, responseTime) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"✅ Agent '{agentName}' deleted successfully!");
            }
            else
            {
                return (false, $"❌ Failed to delete agent: {response.StatusCode} - {content}\nRequest URL: {requestUrl}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to delete agent: {ex.Message}");
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

    #region Skill Methods

    /// <summary>
    /// Uploads a skill from a local directory to the server.
    /// </summary>
    /// <param name="skillDirectoryPath">Path to the skill directory containing metadata.yaml and SKILL.md</param>
    /// <returns>Success status and response message</returns>
    public async Task<(bool Success, string Response)> UploadSkillAsync(string skillDirectoryPath)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            // Read and combine skill files into deployment model
            var skillYaml = await YamlHelper.ReadSkillFromDirectory(skillDirectoryPath);
            if (string.IsNullOrEmpty(skillYaml))
            {
                return (false, $"Failed to read skill from directory: {skillDirectoryPath}");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply";
            var request = new HttpRequestMessage(HttpMethod.Put, url);
            request.Content = new StringContent(skillYaml, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/yaml"));

            var (response, content, _) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, "Skill uploaded successfully");
            }
            else
            {
                return (false, $"Upload failed: {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"UploadSkill failed: {ex.Message}");
            return (false, $"Failed to upload skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts an agent to a skill and saves it locally.
    /// </summary>
    /// <param name="agentName">Name of the agent to convert</param>
    /// <param name="topLevelAgents">List of top-level agent names for handoff context</param>
    /// <param name="outputPath">Output path for the generated skill</param>
    /// <returns>Success status, SkillSpec, and error message</returns>
    public async Task<(bool Success, Agent.Framework.Skills.SkillSpec? SkillSpec, string ErrorMessage)> ConvertAgentToSkillAsync(
        string agentName,
        List<string> topLevelAgents,
        string outputPath)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, null, "Configuration not found. Please run 'srectl init' first.");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents/{agentName}/convert-to-skill";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            // Create request body
            var requestBody = new
            {
                topLevelAgents = topLevelAgents ?? new List<string>()
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, _camelCaseJsonOptions),
                Encoding.UTF8,
                "application/json");

            var (response, content, _) = await MakeHttpRequestAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (false, null, $"Agent '{agentName}' not found");
                }

                return (false, null, $"Conversion failed: {response.StatusCode} - {content}");
            }

            // Deserialize the SkillSpec response
            var skillSpec = JsonSerializer.Deserialize<Agent.Framework.Skills.SkillSpec>(content, _camelCaseJsonOptions);
            if (skillSpec == null)
            {
                return (false, null, "Failed to deserialize skill response");
            }

            // Save skill to directory
            await YamlHelper.SaveSkillToDirectory(outputPath, skillSpec);

            return (true, skillSpec, "");
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ConvertAgentToSkill failed: {ex.Message}");
            return (false, null, $"Failed to convert agent to skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Lists skills from the server with pagination and search.
    /// </summary>
    /// <param name="page">Page number (1-based)</param>
    /// <param name="limit">Number of items per page</param>
    /// <param name="search">Search query for skill name or description</param>
    /// <returns>Success status, list of skills, total count, and error message</returns>
    public async Task<(bool Success, List<Agent.Framework.Skills.SkillSpec> Skills, int TotalCount, string ErrorMessage)> ListSkillsAsync(
        int page = 1,
        int limit = 50,
        string? search = null)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, [], 0, "Configuration not found. Please run 'srectl init' first.");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/skills?page={page}&limit={limit}";
            if (!string.IsNullOrWhiteSpace(search))
            {
                url += $"&search={Uri.EscapeDataString(search)}";
            }

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var (response, content, _) = await MakeHttpRequestAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return (false, [], 0, $"Failed to list skills: {response.StatusCode} - {content}");
            }

            // Parse the paginated response
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                var skills = new List<Agent.Framework.Skills.SkillSpec>();
                int totalCount = 0;
                JsonElement skillsArray;

                // Try to get total count
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    //skip here, will be assigned below
                }
                else if (jsonDoc.RootElement.TryGetProperty("totalCount", out var totalCountEl))
                {
                    totalCount = totalCountEl.GetInt32();
                }
                else if (jsonDoc.RootElement.TryGetProperty("total_count", out var totalCountSnakeEl))
                {
                    totalCount = totalCountSnakeEl.GetInt32();
                }
                else
                {
                    return (false, [], 0, "Unexpected response format for skills list");
                }

                // Get the skills array
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    skillsArray = jsonDoc.RootElement;
                    totalCount = skillsArray.GetArrayLength();
                }
                else if (jsonDoc.RootElement.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                {
                    skillsArray = dataEl;
                }
                else if (jsonDoc.RootElement.TryGetProperty("skills", out var skillsEl) && skillsEl.ValueKind == JsonValueKind.Array)
                {
                    skillsArray = skillsEl;
                }
                else
                {
                    return (false, [], 0, "Unexpected response format for skills list");
                }

                // Deserialize each skill
                foreach (var skillEl in skillsArray.EnumerateArray())
                {
                    var skillJson = skillEl.GetRawText();
                    var skill = JsonSerializer.Deserialize<Agent.Framework.Skills.SkillSpec>(skillJson, _camelCaseJsonOptions);
                    if (skill != null)
                    {
                        skills.Add(skill);
                    }
                }

                return (true, skills, totalCount, "");
            }
            catch (Exception ex)
            {
                return (false, [], 0, $"Failed to parse skills list: {ex.Message} {ex.StackTrace}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"ListSkills failed: {ex.Message}");
            return (false, [], 0, $"Failed to list skills: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a specific skill by name from the server.
    /// </summary>
    /// <param name="skillName">Name of the skill to retrieve</param>
    /// <returns>Success status, SkillSpec, and error message</returns>
    public async Task<(bool Success, Agent.Framework.Skills.SkillSpec? SkillSpec, string ErrorMessage)> GetSkillByNameAsync(string skillName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, null, "Configuration not found. Please run 'srectl init' first.");
            }

            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/skills/{skillName}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var (response, content, _) = await MakeHttpRequestAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (false, null, $"Skill '{skillName}' not found");
                }

                return (false, null, $"Failed to get skill: {response.StatusCode} - {content}");
            }

            var skillSpec = JsonSerializer.Deserialize<Agent.Framework.Skills.SkillSpec>(content, _camelCaseJsonOptions);
            if (skillSpec == null)
            {
                return (false, null, "Failed to deserialize skill response");
            }

            return (true, skillSpec, "");
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetSkillByName failed: {ex.Message}");
            return (false, null, $"Failed to retrieve skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets all skill names from the server.
    /// </summary>
    /// <returns>Success status, list of skill names, and error message</returns>
    public async Task<(bool Success, List<string> Names, string Error)> GetSkillNamesAsync()
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, [], "Configuration not found. Please run 'srectl init' first.");
            }

            // Get all skills (we'll extract names from the full list)
            var (success, skills, _, errorMessage) = await ListSkillsAsync(1, 1000, null);
            if (!success)
            {
                return (false, [], errorMessage);
            }

            var names = skills
                .Select(s => s.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return (true, names, "");
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetSkillNames failed: {ex.Message}");
            return (false, [], $"Failed to get skill names: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a skill from the server.
    /// </summary>
    /// <param name="skillName">Name of the skill to delete</param>
    /// <returns>Success status and response message</returns>
    public async Task<(bool Success, string Response)> DeleteSkillAsync(string skillName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "Configuration not found. Please run 'srectl init' first.");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/skills/{skillName}";
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUrl);

            var (response, content, _) = await MakeHttpRequestAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return (true, $"Skill '{skillName}' deleted successfully");
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (false, $"Skill '{skillName}' not found");
            }
            else
            {
                return (false, $"Failed to delete skill: {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DeleteSkill failed: {ex.Message}");
            return (false, $"Failed to delete skill: {ex.Message}");
        }
    }

    #endregion

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    /// <summary>
    /// Gets the configuration for internal service use.
    /// </summary>
    public async Task<CliConfiguration?> GetConfigurationAsync()
    {
        return await _configService.LoadConfigurationAsync();
    }

    /// <summary>
    /// Gets the YAML configuration for a specific agent from the remote server.
    /// </summary>
    /// <param name="agentName">The name of the agent to retrieve</param>
    /// <returns>Success status, YAML content, and error message</returns>
    public async Task<(bool Success, string YamlContent, string ErrorMessage)> GetExtendedAgentAsync(string agentName)
    {
        try
        {
            var config = await _configService.LoadConfigurationAsync();
            if (config == null)
            {
                return (false, "", "Configuration not found. Please run 'srectl init' first.");
            }

            // Use V2 API to fetch specific agent
            var url = $"{config.ResourceUrl.TrimEnd('/')}/api/v2/extendedAgent/agents/{agentName}";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            var (response, content, _) = await MakeHttpRequestAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return (false, "", $"Agent '{agentName}' not found on server");
                }
                return (false, "", $"Failed to get agent: {response.StatusCode}");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(mediaType) && mediaType.Contains("html"))
            {
                return (false, "", "Server returned HTML instead of JSON. Check the API URL/auth.");
            }

            if (!LooksLikeJson(content))
            {
                return (false, "", "Unexpected response format for agent");
            }

            // Parse V2 API response: { "name": "...", "type": "...", "properties": {...}, "tags": [...], "owner": "..." }
            try
            {
                var agentEnvelope = JsonSerializer.Deserialize<ApiEnvelope<ExtendedAgentSpecV2>>(content, _camelCaseJsonOptions);

                if (agentEnvelope?.Properties == null)
                {
                    return (false, "", $"Agent '{agentName}' has no properties");
                }

                // Create V2 wrapper with metadata from envelope
                var v2Agent = new ExtendedAgentV2
                {
                    Spec = agentEnvelope.Properties,
                    Metadata = new ResourceMetadataModel
                    {
                        Name = agentEnvelope.Name ?? agentName,
                        Owner = agentEnvelope.Owner ?? string.Empty,
                        Tags = agentEnvelope.Tags ?? []
                    }
                };

                // Serialize to YAML (no naming convention needed - uses explicit [YamlMember] aliases)
                var serializer = new SerializerBuilder()
                    .DisableAliases()
                    .Build();
                var yaml = serializer.Serialize(v2Agent);
                return (true, yaml, "");
            }
            catch (Exception ex)
            {
                return (false, "", $"Failed to parse agent response: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"GetExtendedAgent failed: {ex.Message}");
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

            // Parse possible shapes: new nested format, legacy format, or bare array
            try
            {
                var jsonDoc = JsonDocument.Parse(content);
                JsonElement toolsArray;

                // Check for new nested structure: { data: { tools: { data: [...] } } }
                if (jsonDoc.RootElement.TryGetProperty("data", out var outerDataElement) &&
                    outerDataElement.TryGetProperty("tools", out var toolsWrapperElement) &&
                    toolsWrapperElement.TryGetProperty("data", out var toolsArrayElement))
                {
                    toolsArray = toolsArrayElement;
                }
                // Check for legacy structure: { data: [...] }
                else if (jsonDoc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    toolsArray = dataEl;
                }
                // Check for bare array: [...]
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
                    var pruned = YamlHelper.PruneEmptyNodes(plainTool) ?? plainTool;
                    var serializer = new SerializerBuilder()
                        .WithNamingConvention(UnderscoredNamingConvention.Instance)
                        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
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

                            var connectorOutput = ConsoleUI.CaptureOutput(() =>
                            {
                                Console.WriteLine();
                                ConsoleUI.WriteBullet(name, ConsoleColor.White, 0);

                                ConsoleUI.WriteKeyValue("  Type", connectorType, 13, ConsoleColor.Gray, ConsoleColor.White);
                                ConsoleUI.WriteKeyValue("  Data Source", dataSource, 13, ConsoleColor.Gray, ConsoleColor.White);
                                if (!string.IsNullOrEmpty(identity) && identity != "Not specified")
                                {
                                    ConsoleUI.WriteKeyValue("  Identity", identity, 13, ConsoleColor.Gray, ConsoleColor.White);
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
            if (invalidFiles.Count != 0)
            {
                return (false, $"Files not found: {string.Join(", ", invalidFiles.Select(Path.GetFileName))}");
            }

            var requestUrl = $"{config.ResourceUrl.TrimEnd('/')}/api/v1/AgentMemory/upload";

            using var multipartContent = new MultipartFormDataContent
            {
                // Add indexing parameter
                { new StringContent(triggerIndexing.ToString().ToLower()), "triggerIndexing" }
            };

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

                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = multipartContent
                };

                var (response, content, responseTime) = await MakeHttpRequestAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        // Try to parse the JSON response for detailed feedback
                        var jsonDoc = JsonDocument.Parse(content);
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
                        var jsonDoc = JsonDocument.Parse(content);
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
                    var jsonDoc = JsonDocument.Parse(content);
                    var searchResults = new List<string>();

                    if (jsonDoc.RootElement.TryGetProperty("results", out var resultsElement) &&
                        resultsElement.ValueKind == JsonValueKind.Array)
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

                                var resultOutput = ConsoleUI.CaptureOutput(() =>
                                {
                                    Console.WriteLine();
                                    ConsoleUI.WriteBullet($"Result {i + 1}: {title}", ConsoleColor.White, 0);

                                    ConsoleUI.WriteKeyValue("  Source", source, 15, ConsoleColor.Gray, ConsoleColor.White);
                                    ConsoleUI.WriteKeyValue("  Relevance", $"{score:F2}", 15, ConsoleColor.Gray, ConsoleColor.White);

                                    if (!string.IsNullOrEmpty(content_snippet))
                                    {
                                        // Truncate content snippet if too long
                                        var snippet = content_snippet.Length > 200 ?
                                            content_snippet.Substring(0, 200) + "..." : content_snippet;
                                        ConsoleUI.WriteKeyValue("  Content", snippet, 15, ConsoleColor.Gray, ConsoleColor.White);
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
                catch (JsonException)
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
                    var jsonDoc = JsonDocument.Parse(content);
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
                    var jsonDoc = JsonDocument.Parse(content);
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
                    var jsonDoc = JsonDocument.Parse(content);
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
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

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
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

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
}

/// Note:
/// - there is metadata in YamlAgentDescriptor but located under spec so at the wrong level
/// - once moved, remove this local definition and reference the YamlAgentDescriptor.Metadata instead
public class YamlMetadata
{
    public string Owner { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public string UpdatedAt { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
}

/// <summary>
/// Generic structured YAML wrapper that supports any spec type (T)
/// where T could be YamlAgentDescriptor, ToolListSpec, etc.
/// </summary>
/// example YAML structure:
/// ```yaml
/// api_version: azuresre.ai/v1
/// kind: AgentConfiguration
/// metadata:
///   <metadata fields>
/// spec:
///   <spec fields>
/// <typeparam name="T">The type of the spec content</typeparam>
public class StructuredAgentYamlWrapper<T>
{
    [YamlMember(Alias = "api_version")]
    public string ApiVersion { get; set; } = "azuresre.ai/v1";

    [YamlMember(Alias = "kind")]
    public string Kind { get; set; } = "";

    [YamlMember(Alias = "metadata")]
    public YamlMetadata? Metadata { get; set; } = new YamlMetadata();

    [YamlMember(Alias = "spec")]
    public T Spec { get; set; } = default(T)!;
}

// Type-specific wrappers
/// <summary>
/// Structured YAML for agent configurations using flat spec format
/// </summary>
public class StructuredAgentYaml : StructuredAgentYamlWrapper<YamlAgentDescriptor>
{
    public StructuredAgentYaml()
    {
        Kind = "AgentConfiguration";
    }
}

/// <summary>
/// Spec class for tool lists
/// </summary>
public class ToolListSpec
{
    [YamlMember(Alias = "tools")]
    public List<object> Tools { get; set; } = [];
}

/// <summary>
/// Structured YAML for tool lists
/// </summary>
public class StructuredToolListYaml : StructuredAgentYamlWrapper<ToolListSpec>
{
    public StructuredToolListYaml()
    {
        Kind = "ToolList";
    }
}

public record ThreadMessage(string Id, string Text, DateTime Timestamp, string AuthorRole, string AuthorUserId, string AuthorDisplayName);
public record ThreadInfo(string Id, string Title, DateTime CreatedAt, DateTime LastMessageAt);

