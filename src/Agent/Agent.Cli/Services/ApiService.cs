using Azure.Core;
using Azure.Identity;
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
                // Parse the response to get agent names
                var jsonDoc = JsonDocument.Parse(content);
                var agents = jsonDoc.RootElement.GetProperty("data");
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
            else
            {
                return (false, $"❌ Connection failed: {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Connection failed: {ex.Message}");
        }
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

            // Read the YAML file
            var yamlContent = await File.ReadAllTextAsync(agentFilePath);

            // Parse the agent YAML content to an object
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var agentData = deserializer.Deserialize<object>(yamlContent);

            // Create the wrapper with proper structure
            var agentWrapper = new AgentConfigurationWrapper
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
                Spec = new AgentSpec { Agent = agentData }
            };

            // Serialize to YAML
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            var wrappedYamlContent = serializer.Serialize(agentWrapper);

            // Create the request
            var request = new HttpRequestMessage(HttpMethod.Put, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply");
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
                return (true, $"✅ Agent '{agentName}' applied successfully!");
            }
            else
            {
                return (false, $"❌ Failed to apply agent: {response.StatusCode} - {content}");
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

            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/agents");

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
                // Parse the response to get agent information
                var jsonDoc = JsonDocument.Parse(content);
                var agents = jsonDoc.RootElement.GetProperty("data");
                
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
                   
                    var totalCount = jsonDoc.RootElement.TryGetProperty("totalcount", out var totalElement) ? totalElement.GetInt32() : agents.GetArrayLength();
                    agentList.Add($"\nTotal: {totalCount} agent(s)");
                }

                return (true, string.Join("\n", agentList));
            }
            else
            {
                return (false, $"❌ Failed to list agents: {response.StatusCode} - {content}");
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

            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/listTools");

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
                // Parse the tools array response
                var tools = JsonSerializer.Deserialize<JsonElement[]>(content) ?? [];
                
                var toolList = new List<string>();
                toolList.Add("🔧 Available Tools:");
                toolList.Add("==================");
                
                foreach (var tool in tools)
                {
                    var name = tool.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "Unknown" : "Unknown";
                    var category = tool.TryGetProperty("category", out var categoryElement) ? categoryElement.GetString() ?? "" : "";
                    var description = tool.TryGetProperty("description", out var descElement) ? descElement.GetString() ?? "" : "";
                    var pluginName = tool.TryGetProperty("pluginName", out var pluginElement) ? pluginElement.GetString() ?? "" : "";
                    
                    toolList.Add($"\n🛠️  {name}");
                    if (!string.IsNullOrEmpty(category))
                    {
                        toolList.Add($"   Category: {category}");
                    }
                    if (!string.IsNullOrEmpty(description))
                    {
                        toolList.Add($"   Description: {description}");
                    }
                    if (!string.IsNullOrEmpty(pluginName))
                    {
                        toolList.Add($"   Plugin: {pluginName}");
                    }
                    
                    // Get parameters
                    if (tool.TryGetProperty("parameters", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Array)
                    {
                        var parameters = paramsElement.EnumerateArray().Select(p => p.GetString()).Where(p => !string.IsNullOrEmpty(p)).ToList();
                        if (parameters.Any())
                        {
                            toolList.Add($"   Parameters: {string.Join(", ", parameters)}");
                        }
                        else
                        {
                            toolList.Add("   Parameters: None");
                        }
                    }
                }

                if (tools.Length == 0)
                {
                    toolList.Add("\nNo tools found on the server.");
                }
                else
                {
                    toolList.Add($"\nTotal: {tools.Length} tool(s)");
                }

                return (true, string.Join("\n", toolList));
            }
            else
            {
                return (false, $"❌ Failed to list tools: {response.StatusCode} - {content}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"❌ Failed to list tools: {ex.Message}");
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
            var request = new HttpRequestMessage(HttpMethod.Put, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply");
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
                return (false, $"❌ Failed to apply tool: {response.StatusCode} - {content}");
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
            var request = new HttpRequestMessage(HttpMethod.Post, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/incidentplayground/generateInstructions");
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
                return (false, "", new List<string>(), $"Failed to generate smart agent: {response.StatusCode} - {content}");
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
            var request = new HttpRequestMessage(HttpMethod.Put, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/extendedAgent/apply");
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
                return (false, $"❌ Failed to apply YAML file: {response.StatusCode} - {content}");
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
            var request = new HttpRequestMessage(HttpMethod.Post, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads");
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
                return (false, "", $"❌ Failed to create thread: {response.StatusCode} - {content}");
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
            var request = new HttpRequestMessage(HttpMethod.Post, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages");
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
                return (false, "", $"❌ Failed to send message: {response.StatusCode} - {content}");
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
                var request = new HttpRequestMessage(HttpMethod.Get, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}/messages");

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
                    return (false, new List<ThreadMessage>(), $"Failed to get messages: {response.StatusCode} - {content}");
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

            var request = new HttpRequestMessage(HttpMethod.Get, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads");

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
                return (false, new List<ThreadInfo>(), $"Failed to list threads: {response.StatusCode} - {content}");
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

            var request = new HttpRequestMessage(HttpMethod.Delete, $"{config.ResourceUrl.TrimEnd('/')}/api/v1/threads/{threadId}");

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
                return (false, $"Failed to delete thread: {response.StatusCode} - {content}");
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

public record ThreadMessage(string Id, string Text, DateTime Timestamp, string AuthorRole, string AuthorUserId, string AuthorDisplayName);
public record ThreadInfo(string Id, string Title, DateTime CreatedAt, DateTime LastMessageAt);
