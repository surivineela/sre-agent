// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Reflection;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles general CLI commands like init and list.
/// </summary>
public static class GeneralCommandHandlers
{
    /// <summary>
    /// Sync remote agents and tools into local agents/ and tools/ directories.
    /// </summary>
    public static async Task HandleSyncCommand(ParseResult parseResult)
    {
        try
        {
            ConsoleUI.WriteSection("Sync from remote");

            // Ensure configuration exists
            var configService = new CliConfigurationService();
            var config = await configService.LoadConfigurationAsync();
            if (config == null)
            {
                ConsoleUI.WriteStatus(false, "No configuration found. Run 'srectl init --resource-url <url>' first.");
                Environment.Exit(1);
                return;
            }

            // Ensure folders exist
            Directory.CreateDirectory("agents");
            Directory.CreateDirectory("tools");
            Directory.CreateDirectory("skills");

            using var api = new ApiService();

            // Fetch names
            ConsoleUI.WriteInfo("Fetching remote inventory (agents, tools, skills)...", ConsoleColor.Cyan);
            var (agentsOk, agentNames, agentsMsg) = await api.GetAgentNamesAsync();
            var (toolsOk, toolNames, toolsMsg) = await api.GetToolNamesAsync();
            var (skillsOk, skillNames, skillsMsg) = await api.GetSkillNamesAsync();

            if (!agentsOk && !toolsOk && !skillsOk)
            {
                ConsoleUI.WriteStatus(false, "Failed to list agents, tools, and skills from server.");
                if (!string.IsNullOrWhiteSpace(agentsMsg)) ConsoleUI.WriteBullet($"Agents: {agentsMsg}", ConsoleColor.Yellow);
                if (!string.IsNullOrWhiteSpace(toolsMsg)) ConsoleUI.WriteBullet($"Tools: {toolsMsg}", ConsoleColor.Yellow);
                if (!string.IsNullOrWhiteSpace(skillsMsg)) ConsoleUI.WriteBullet($"Skills: {skillsMsg}", ConsoleColor.Yellow);
                Environment.Exit(1);
                return;
            }

            int syncedAgents = 0, syncedTools = 0, syncedSkills = 0;

            // Sync agents
            if (agentsOk && agentNames.Count > 0)
            {
                ConsoleUI.WriteSection($"Agents ({agentNames.Count})");
                foreach (var name in agentNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n))
                {
                    try
                    {
                        var (ok, yaml, err) = await api.GetAgentConfigurationAsync(name);
                        if (!ok || string.IsNullOrWhiteSpace(yaml))
                        {
                            ConsoleUI.WriteBullet($"Skip {name}: {err}", ConsoleColor.Yellow);
                            continue;
                        }

                        var dir = Path.Combine("agents", name);
                        Directory.CreateDirectory(dir);
                        var path = Path.Combine(dir, $"{name}.yaml");
                        await File.WriteAllTextAsync(path, yaml, System.Text.Encoding.UTF8);
                        ConsoleUI.WriteBullet($"Wrote {path}", ConsoleColor.Green);
                        syncedAgents++;
                    }
                    catch (Exception ex)
                    {
                        ConsoleUI.WriteBullet($"Failed {name}: {ex.Message}", ConsoleColor.Yellow);
                    }
                }
            }
            else
            {
                ConsoleUI.WriteInfo("No agents found on remote or failed to fetch.", ConsoleColor.Yellow);
            }

            // Sync tools
            if (toolsOk && toolNames.Count > 0)
            {
                ConsoleUI.WriteSection($"Tools ({toolNames.Count})");
                foreach (var name in toolNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n))
                {
                    try
                    {
                        var (ok, yaml, err) = await api.GetToolConfigurationAsync(name);
                        if (!ok || string.IsNullOrWhiteSpace(yaml))
                        {
                            ConsoleUI.WriteBullet($"Skip {name}: {err}", ConsoleColor.Yellow);
                            continue;
                        }

                        // Parse the YAML content into the correct tool type and use WriteToolYamlFile
                        try
                        {
                            // Parse YAML to get tool type
                            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                                .IgnoreUnmatchedProperties()
                                .Build();

                            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(yaml);
                            var toolType = yamlDict.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : null;

                            // Deserialize YAML to the correct derived type to preserve all fields (like query)
                            YamlToolDefinitionBase tool = toolType switch
                            {
                                "KustoTool" => deserializer.Deserialize<KustoToolDefinition>(yaml),
                                "LinkTool" => deserializer.Deserialize<LinkToolDefinition>(yaml),
                                _ => throw new NotSupportedException($"Unknown tool type: {toolType}")
                            };

                            // Extract metadata from tool level for document level (proper architecture)
                            var toolMetadata = tool.Metadata;

                            // Use WriteToolYamlFile to create structured YAML with wrapper, passing extracted metadata
                            YamlHelper.WriteToolYamlFile("tools", name, tool, toolMetadata);
                            ConsoleUI.WriteBullet($"Wrote tools/{name}/{name}.yaml", ConsoleColor.Green);
                        }
                        catch (Exception parseEx)
                        {
                            ConsoleUI.WriteBullet($"Failed to parse tool {name}: {parseEx.Message}", ConsoleColor.Yellow);
                            continue;
                        }
                        syncedTools++;
                    }
                    catch (Exception ex)
                    {
                        ConsoleUI.WriteBullet($"Failed {name}: {ex.Message}", ConsoleColor.Yellow);
                    }
                }
            }
            else
            {
                ConsoleUI.WriteInfo("No tools found on remote or failed to fetch.", ConsoleColor.Yellow);
            }

            // Sync skills
            if (skillsOk && skillNames.Count > 0)
            {
                ConsoleUI.WriteSection($"Skills ({skillNames.Count})");
                foreach (var name in skillNames.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n))
                {
                    try
                    {
                        var (ok, skillSpec, err) = await api.GetSkillByNameAsync(name);
                        if (!ok || skillSpec == null)
                        {
                            ConsoleUI.WriteBullet($"Skip {name}: {err}", ConsoleColor.Yellow);
                            continue;
                        }

                        // Save skill to directory
                        var skillPath = Path.Combine("skills", name);
                        await YamlHelper.SaveSkillToDirectory(skillPath, skillSpec);
                        ConsoleUI.WriteBullet($"Wrote {skillPath}/metadata.yaml and {skillPath}/SKILL.md", ConsoleColor.Green);
                        syncedSkills++;
                    }
                    catch (Exception ex)
                    {
                        ConsoleUI.WriteBullet($"Failed {name}: {ex.Message}", ConsoleColor.Yellow);
                    }
                }
            }
            else
            {
                ConsoleUI.WriteInfo("No skills found on remote or failed to fetch.", ConsoleColor.Yellow);
            }

            Console.WriteLine();
            ConsoleUI.WriteSection("Summary");
            ConsoleUI.WriteKeyValue("Agents", syncedAgents.ToString(), 10);
            ConsoleUI.WriteKeyValue("Tools", syncedTools.ToString(), 10);
            ConsoleUI.WriteKeyValue("Skills", syncedSkills.ToString(), 10);
            Console.WriteLine();
            ConsoleUI.WriteInfo("Tip: edit YAML locally and use 'srectl apply-yaml --file <path>' or 'srectl agent/tool/skill apply' to push updates.", ConsoleColor.Gray);

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Sync failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the init command with a specific resource URL.
    /// </summary>
    public static async Task HandleInitCommandWithResourceUrl(string resourceUrl)
    {
        try
        {
            // Validate URL format
            if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
            {
                ConsoleUI.WriteStatus(false, "Invalid URL format provided.");
                Environment.Exit(1);
                return;
            }

            // Create configuration
            var config = new CliConfiguration
            {
                ResourceUrl = resourceUrl,
                AuthRequired = !CliConfigurationService.IsLocalhost(resourceUrl),
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Save configuration
            var configService = new CliConfigurationService();
            await configService.SaveConfigurationAsync(config);

            // Create directory structure
            Directory.CreateDirectory("agents");
            Directory.CreateDirectory("tools");
            Directory.CreateDirectory("skills");
            Directory.CreateDirectory("scheduledtasks");

            // Copy example files
            await ExampleFileManager.CopyExampleFilesAsync();

            // Create instructions.md file in .github folder
            await InstructionsFileService.CreateInstructionsFileAsync();

            ConsoleUI.WriteStatus(true, "SREAgent CLI initialized successfully!");
            ConsoleUI.WriteBullet($"Resource URL: {resourceUrl}", ConsoleColor.White);
            ConsoleUI.WriteBullet($"Auth Required: {config.AuthRequired}", ConsoleColor.White);
            ConsoleUI.WriteBullet("Created directories: agents/, tools/, skills/, scheduledtasks/, .github/", ConsoleColor.White);
            ConsoleUI.WriteBullet("Added example files: example_agent.yaml, example_tool.yaml", ConsoleColor.White);
            ConsoleUI.WriteBullet("Created comprehensive instructions file: .github/instructions.md", ConsoleColor.White);

            // Test connection
            Console.WriteLine();
            ConsoleUI.WriteBullet("Testing connection...", ConsoleColor.Cyan);
            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(resourceUrl);
            Console.WriteLine(response);

            // Exit with appropriate code, but don't fail initialization for connection issues
            if (!success)
            {
                ConsoleUI.WriteBullet("Note: Initialization completed successfully, but connection test failed.", ConsoleColor.Yellow);
                ConsoleUI.WriteBullet("You can still use srectl commands that don't require server connection.", ConsoleColor.White);
            }

            Environment.Exit(0); // Always exit successfully if initialization steps completed
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Initialization failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the list agents command.
    /// </summary>
    public static async Task HandleListAgentsCommand(ParseResult parseResult)
    {
        // Ensure debug/quiet flags are honored for this handler
        CommandExecutionContext.Initialize(parseResult);
        var showAll = parseResult.GetValue(AgentCommandOptions.AllOption);

        DebugLogger.Debug("Command", "Starting agent list command");
        DebugLogger.Debug("Parameters", $"ShowAll: {showAll}");

        using var apiService = new ApiService();

        if (showAll)
        {
            // Show all agents with full details (existing behavior)
            var (success, response, _) = await apiService.ListAgentsAsync();

            if (success)
            {
                ConsoleUI.WriteSection("All Available Agents");

                if (DebugLogger.IsDebugEnabled)
                {
                    DebugLogger.Debug("API Response", "Successfully retrieved complete agent list from server");
                }

                Console.WriteLine(response);
            }
            else
            {
                if (DebugLogger.IsDebugEnabled)
                {
                    DebugLogger.Debug("API Error", "Failed to retrieve agent list from server");
                    DebugLogger.Debug("Error Response", response);
                }
                Console.WriteLine(response);
            }
            Environment.Exit(success ? 0 : 1);
        }
        else
        {
            // Show only remote extended agents (simplified view)
            var (success, response, _) = await apiService.ListAgentsAsync();

            if (success)
            {
                ConsoleUI.WriteSection("Remote Extended Agents");

                if (DebugLogger.IsDebugEnabled)
                {
                    DebugLogger.Debug("API Response", "Successfully retrieved agent list from server");
                }

                // For simplified view, try to extract just the agent names from the response
                try
                {
                    // Extract agent names from the formatted response
                    var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var agentNames = new List<string>();

                    foreach (var line in lines)
                    {
                        // Look for lines that start with bullet point (agent names)
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("• ") && !trimmed.Contains("System Prompt:") &&
                            !trimmed.Contains("Description:") && !trimmed.Contains("Tools:") &&
                            !trimmed.Contains("Created:") && !trimmed.Contains("Handoffs:") &&
                            !trimmed.StartsWith("• Total:"))
                        {
                            var agentName = trimmed.Substring(2).Trim();
                            if (!string.IsNullOrEmpty(agentName))
                            {
                                agentNames.Add(agentName);
                            }
                        }
                    }

                    if (agentNames.Count != 0)
                    {
                        foreach (var name in agentNames.OrderBy(n => n))
                        {
                            ConsoleUI.WriteBullet(name);
                        }
                        Console.WriteLine($"\nTotal: {agentNames.Count} extended agent(s)");
                    }
                    else
                    {
                        ConsoleUI.WriteInfo("No extended agents found.", ConsoleColor.Yellow);
                    }
                }
                catch (Exception ex)
                {
                    if (DebugLogger.IsDebugEnabled)
                    {
                        DebugLogger.Debug("Parser Error", $"Failed to parse agent names: {ex.Message}");
                    }
                    // Fall back to showing the full response
                    Console.WriteLine(response);
                }
            }
            else
            {
                if (DebugLogger.IsDebugEnabled)
                {
                    DebugLogger.Debug("API Error", "Failed to retrieve agent list from server");
                    DebugLogger.Debug("Error Response", response);
                }
                Console.WriteLine(response);
            }
            Environment.Exit(success ? 0 : 1);
        }
    }

    /// <summary>
    /// Handles the list tools command.
    /// </summary>
    public static async Task HandleListToolsCommand(ParseResult parseResult)
    {
        // Ensure debug/quiet flags are honored for this handler
        CommandExecutionContext.Initialize(parseResult);

        DebugLogger.Debug("Command", "Starting tool list command");

        using var apiService = new ApiService();
        var (success, response) = await apiService.ListAllToolsAsync();

        if (success)
        {
            ConsoleUI.WriteSection("Available Tools");

            if (DebugLogger.IsDebugEnabled)
            {
                DebugLogger.Debug("API Response", "Successfully retrieved tool list from server");
            }

            Console.WriteLine(response);
        }
        else
        {
            if (DebugLogger.IsDebugEnabled)
            {
                DebugLogger.Debug("API Error", "Failed to retrieve tool list from server");
                DebugLogger.Debug("Error Response", response);
            }
            Console.WriteLine(response);
        }
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the list extended-tools command.
    /// </summary>
    public static async Task HandleListExtendedToolsCommand(ParseResult parseResult)
    {
        // Ensure debug/quiet flags are honored for this handler
        CommandExecutionContext.Initialize(parseResult);

        DebugLogger.Debug("Command", "Starting extended tools list command");

        using var apiService = new ApiService();
        var (success, response) = await apiService.ListExtendedToolsAsync();

        if (success)
        {
            ConsoleUI.WriteSection("Extended Tools");

            if (DebugLogger.IsDebugEnabled)
            {
                DebugLogger.Debug("API Response", "Successfully retrieved extended tools list from server");
            }

            Console.WriteLine(response);
        }
        else
        {
            if (DebugLogger.IsDebugEnabled)
            {
                DebugLogger.Debug("API Error", "Failed to retrieve extended tools list from server");
                DebugLogger.Debug("Error Response", response);
            }
            Console.WriteLine(response);
        }
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the list data-connectors command.
    /// </summary>
    public static async Task HandleListDataConnectorsCommand(ParseResult parseResult)
    {
        // Ensure debug/quiet flags are honored for this handler
        CommandExecutionContext.Initialize(parseResult);

        DebugLogger.Debug("Command", "Starting data connectors list command");

        using var apiService = new ApiService();
        var (success, response) = await apiService.ListDataConnectorsAsync();

        if (success)
        {
            ConsoleUI.WriteSection("Data Connectors");

            if (DebugLogger.IsDebugEnabled)
            {
                DebugLogger.Debug("API Response", "Successfully retrieved data connectors list from server");
            }

            Console.WriteLine(response);
        }
        else
        {
            if (DebugLogger.IsDebugEnabled)
            {
                DebugLogger.Debug("API Error", "Failed to retrieve data connectors list from server");
                DebugLogger.Debug("Error Response", response);
            }
            Console.WriteLine(response);
        }
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the apply-yaml command.
    /// </summary>
    public static async Task HandleApplyYamlCommand(ParseResult parseResult)
    {
        try
        {
            var filePath = parseResult.GetValue(AgentCommandOptions.ApplyYamlFileOption);

            if (string.IsNullOrEmpty(filePath))
            {
                ConsoleUI.WriteStatus(false, "File path is required.");
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(filePath))
            {
                ConsoleUI.WriteStatus(false, $"File not found: {filePath}");
                Environment.Exit(1);
                return;
            }

            using var apiService = new ApiService();
            var (success, response) = await apiService.ApplyYamlFileAsync(filePath);

            Console.WriteLine(response);
            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to apply YAML file: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the chat command for interactive mode.
    /// </summary>
    public static async Task HandleChatCommand(ParseResult parseResult)
    {
        try
        {
            LoggingService.Initialize(parseResult);

            var agentName = parseResult.GetValue(AgentCommandOptions.ChatAgentNameOption);

            LoggingService.Info("Starting interactive chat session...");
            if (!string.IsNullOrWhiteSpace(agentName))
            {
                LoggingService.Info($"Using agent: {agentName}");
            }
            LoggingService.Info("Type 'exit', 'quit', '/exit', or '/quit' to end the session, or press Ctrl+C");
            LoggingService.Info("Commands: /agent <name> (switch agents), /clear (start new thread)");
            LoggingService.Info("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            using var apiService = new ApiService();
            string? currentThreadId = null;
            string? currentAgentName = agentName; // Track current agent
            // Track which agent messages we've already printed to avoid duplicates
            var printedMessageIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Try to establish streaming connection (best-effort)
            StreamingHubClient? streaming = null;
            bool streamingConnected = false;
            DateTime? lastStreamMessageAt = null;
            bool isAgentWorking = false;
            try
            {
                streaming = new StreamingHubClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var connected = await streaming.ConnectAsync(cts.Token);
                if (connected)
                {
                    LoggingService.Debug("SignalR streaming connected");
                    streamingConnected = true;
                    streaming.OnMessageUpdate((msg) =>
                    {
                        try
                        {
                            if (msg == null)
                            {
                                return;
                            }
                            var threadId = msg.additionalProperties?.threadId;
                            if (!string.IsNullOrEmpty(threadId) && currentThreadId != null && !string.Equals(threadId, currentThreadId, StringComparison.OrdinalIgnoreCase))
                            {
                                // Ignore messages for other threads
                                return;
                            }

                            // Filter out user messages completely - only process agent messages
                            var isUserMsg = msg.role != null && msg.role.Equals("user", StringComparison.OrdinalIgnoreCase);
                            if (isUserMsg)
                            {
                                return; // Skip user messages completely to avoid echoing
                            }

                            // Only react to assistant/agent messages
                            var isAgentMsg = (msg.role != null && msg.role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                                             || (msg.authorName != null && msg.authorName.Equals("SREAgent", StringComparison.OrdinalIgnoreCase));
                            if (!isAgentMsg)
                            {
                                return;
                            }

                            lastStreamMessageAt = DateTime.UtcNow;
                            var text = StreamingMessageUtils.ExtractText(msg);
                            var messageId = msg?.additionalProperties?.messageId;
                            var key = !string.IsNullOrWhiteSpace(messageId) ? messageId! : $"stream|{text?.GetHashCode() ?? 0}";
                            if (!string.IsNullOrWhiteSpace(text) && printedMessageIds.Add(key))
                            {
                                Console.WriteLine($"\nSRE Agent: {text}");
                            }
                            // Agent still working unless finishReason indicates stop
                            var finished = string.Equals(msg?.finishReason, "stop", StringComparison.OrdinalIgnoreCase) || (msg?.additionalProperties?.isCancelled == true);
                            isAgentWorking = !finished;
                        }
                        catch { /* swallow printing errors in background handler */ }
                    });
                }
                else
                {
                    await streaming.DisposeAsync();
                    streaming = null;
                }
            }
            catch
            {
                if (streaming != null) await streaming.DisposeAsync();
                streaming = null;
            }

            while (true)
            {
                // Get user input
                Console.Write("\n💬 You: ");
                var userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                    continue;

                // Check for exit commands
                if (IsExitCommand(userInput))
                {
                    LoggingService.Info("\nGoodbye! Chat session ended.");
                    break;
                }

                // Check for /clear command - starts a new thread
                if (userInput.Trim().Equals("/clear", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleUI.WriteInfo("Starting new thread...");
                    currentThreadId = null;
                    printedMessageIds.Clear();
                    ConsoleUI.WriteStatus(true, "New thread started. Send a message to begin.");
                    continue;
                }

                // Check for /agent command - switches to a specific agent without creating thread immediately
                if (userInput.Trim().StartsWith("/agent", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = userInput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length == 1) // Just "/agent" without agent name
                    {
                        Console.WriteLine();
                        ConsoleUI.WriteSection("Available agents:");

                        // Get list of agents
                        var (agentListSuccess, agentListResponse, _) = await apiService.ListAgentsAsync();
                        if (agentListSuccess)
                        {
                            Console.WriteLine(agentListResponse);
                        }
                        else
                        {
                            ConsoleUI.WriteStatus(false, "Failed to fetch agent list");
                        }

                        Console.WriteLine();
                        ConsoleUI.WriteInfo("Usage: /agent <agent-name>");
                        ConsoleUI.WriteInfo("Example: /agent echo-agent");
                        continue;
                    }
                    else if (parts.Length >= 2)
                    {
                        var newAgentName = parts[1];

                        // Switch to the new agent but don't create thread until first message is sent
                        currentAgentName = newAgentName;
                        currentThreadId = null; // Clear current thread to start fresh
                        printedMessageIds.Clear();

                        ConsoleUI.WriteStatus(true, $"• Switched to agent: {newAgentName}");
                        ConsoleUI.WriteInfo("Type your first message to create a new thread with this agent.");

                        continue;
                    }
                }

                try
                {
                    if (currentThreadId == null)
                    {
                        // Start new thread
                        LoggingService.Debug("Creating new thread...");

                        // Show spinner immediately when sending the request
                        isAgentWorking = true;
                        lastStreamMessageAt = null;

                        var (threadSuccess, threadId, response) = await apiService.CreateThreadAsync(userInput, "cli-user", "CLI User", currentAgentName);

                        if (threadSuccess && !string.IsNullOrEmpty(threadId))
                        {
                            currentThreadId = threadId;
                            printedMessageIds.Clear();
                            LoggingService.Debug($"Thread created: {currentThreadId}");

                            // Wait for agent response to complete
                            await WaitForCompletionAsync(streamingConnected, () => lastStreamMessageAt, () => isAgentWorking, apiService, currentThreadId, printedMessageIds);
                        }
                        else
                        {
                            isAgentWorking = false; // Stop spinner on error
                            LoggingService.Error($"Failed to create thread: {response}");
                            continue;
                        }
                    }
                    else
                    {
                        // Continue existing thread
                        LoggingService.Debug($"Continuing thread: {currentThreadId}");

                        // Show spinner immediately when sending the request
                        isAgentWorking = true;
                        lastStreamMessageAt = null;

                        var (success, messageId, response) = await apiService.SendMessageAsync(currentThreadId, userInput, "cli-user", "CLI User");

                        if (success)
                        {
                            // Wait for agent response to complete
                            await WaitForCompletionAsync(streamingConnected, () => lastStreamMessageAt, () => isAgentWorking, apiService, currentThreadId, printedMessageIds);
                        }
                        else
                        {
                            LoggingService.Error($"Failed to send message: {response}");

                            // If agent is busy (422 / UnprocessableEntity), wait on the same thread instead of starting a new one
                            var lower = response?.ToLowerInvariant() ?? string.Empty;
                            if (lower.Contains("unprocessableentity") || lower.Contains("agent is currently busy") || lower.Contains("busy"))
                            {
                                LoggingService.Info("Agent is busy processing. Waiting for the current response to complete...");
                                await WaitForCompletionAsync(streamingConnected, () => lastStreamMessageAt, () => isAgentWorking, apiService, currentThreadId!, printedMessageIds);
                                // stay on same thread
                                continue;
                            }

                            // If the thread no longer exists (404), start over; otherwise, keep the thread
                            if (lower.Contains("404") || lower.Contains("notfound") || lower.Contains("not found"))
                            {
                                currentThreadId = null;
                                printedMessageIds.Clear();
                                LoggingService.Info("The conversation thread was not found. Starting a new thread...");
                                continue;
                            }

                            // For other errors, stop spinner and keep the same thread and let the user retry
                            isAgentWorking = false;
                            LoggingService.Info("You can retry once the agent finishes or type a new message.");
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoggingService.Error($"Error during chat: {ex.Message}");
                    LoggingService.Debug($"Exception details: {ex}");
                    currentThreadId = null; // Reset thread on error
                }
            }

            if (streaming != null)
            {
                await streaming.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            LoggingService.Error($"Failed to start chat session: {ex.Message}");
            LoggingService.Debug($"Exception details: {ex}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Checks if the user input is an exit command.
    /// </summary>
    private static bool IsExitCommand(string input)
    {
        var exitCommands = new[] { "exit", "quit", "/exit", "/quit" };
        return exitCommands.Contains(input.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Fetches thread messages and prints only new agent messages in timestamp order.
    /// Uses ApiService.GetThreadMessagesAsync to wait until at least one agent message is available,
    /// then prints all unseen agent messages sorted by time.
    /// </summary>
    private static async Task PrintNewAgentMessagesInOrderAsync(ApiService apiService, string threadId, HashSet<string> printedMessageIds)
    {
        // Try a few rounds to capture late messages; stop when no new messages are found
        const int rounds = 2; // small follow-up to catch stragglers
        for (int i = 0; i < rounds; i++)
        {
            var (messagesSuccess, messages, _) = await apiService.GetThreadMessagesAsync(threadId, 30, 2);
            if (!messagesSuccess || messages == null || messages.Count == 0)
            {
                return;
            }

            var orderedAgentMessages = messages
                .Where(m => m.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase))
                .OrderBy(m => m.Timestamp)
                .ToList();

            var printedAny = false;
            foreach (var msg in orderedAgentMessages)
            {
                var key = GetMessageKey(msg);
                if (printedMessageIds.Add(key))
                {
                    Console.WriteLine($"\nSRE Agent: {msg.Text}");
                    printedAny = true;
                }
            }

            // If we didn't print anything new this round, we're done
            if (!printedAny)
            {
                return;
            }

            // Brief pause before a quick second pass to catch follow-up chunks
            await Task.Delay(500);
        }
    }

    /// <summary>
    /// Unified waiting function that keeps a spinner up until the agent is done.
    /// For streaming: waits until isAgentWorking becomes false with a quiet buffer.
    /// For HTTP: polls and prints messages while spinning.
    /// </summary>
    private static async Task WaitForCompletionAsync(
        bool streamingConnected,
        Func<DateTime?> lastStreamAt,
        Func<bool> isAgentWorking,
        ApiService api,
        string threadId,
        HashSet<string> printedIds)
    {
        string[] dots = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];
        var i = 0;
        var started = DateTime.UtcNow;

        if (streamingConnected)
        {
            // Streaming mode: wait for agent to signal completion
            while (true)
            {
                var last = lastStreamAt();
                var working = isAgentWorking();

                Console.Write($"\r{dots[i++ % dots.Length]} Working...");
                await Task.Delay(150);

                // If agent is no longer working, wait for a quiet period to ensure we got all messages
                if (!working)
                {
                    // Wait for quiet period after agent signals done
                    if (last != null && (DateTime.UtcNow - last.Value).TotalMilliseconds >= 1000)
                    {
                        Console.Write("\r" + new string(' ', 30) + "\r");
                        return;
                    }
                    // If no messages received yet but agent says it's done, wait a bit more
                    if (last == null && (DateTime.UtcNow - started).TotalMilliseconds >= 2000)
                    {
                        Console.Write("\r" + new string(' ', 30) + "\r");
                        return;
                    }
                }

                // Safety timeout: don't wait forever (5 minutes max)
                if ((DateTime.UtcNow - started).TotalMinutes >= 5)
                {
                    Console.Write("\r" + new string(' ', 30) + "\r");
                    LoggingService.Debug("Timeout waiting for agent response");
                    return;
                }
            }
        }
        else
        {
            // HTTP fallback with spinner and polling
            bool hasReceivedResponse = false;
            int stableAttempts = 0;
            const int maxStableAttempts = 3; // Number of consecutive stable checks

            for (int attempt = 0; attempt < 60; attempt++)
            {
                Console.Write($"\r{dots[i++ % dots.Length]} Working...");

                var beforeCount = printedIds.Count;
                await PrintNewAgentMessagesInOrderAsync(api, threadId, printedIds);
                var afterCount = printedIds.Count;

                // Check if we got new messages
                if (afterCount > beforeCount)
                {
                    hasReceivedResponse = true;
                    stableAttempts = 0; // Reset stability counter
                }
                else if (hasReceivedResponse)
                {
                    stableAttempts++;
                    // If we've received messages and now it's stable for several attempts, we're done
                    if (stableAttempts >= maxStableAttempts)
                    {
                        Console.Write("\r" + new string(' ', 30) + "\r");
                        return;
                    }
                }

                await Task.Delay(500);
            }
            Console.Write("\r" + new string(' ', 30) + "\r");
        }
    }

    private static string GetMessageKey(ThreadMessage msg)
    {
        // Prefer stable ID if present, otherwise fall back to timestamp+hash
        if (!string.IsNullOrWhiteSpace(msg.Id))
        {
            return msg.Id;
        }
        var textHash = msg.Text?.GetHashCode() ?? 0;
        return $"{msg.Timestamp:O}|{msg.AuthorRole}|{textHash}";
    }

    /// <summary>
    /// Handles the version command to display version information with banner.
    /// </summary>
    public static Task HandleVersionCommand(ParseResult parseResult)
    {
        StandardHelpFormatter.ShowSrectlHeader();

        // Get version information from assembly
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString() ?? "Unknown";
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? version;

        ConsoleUI.WriteKeyValue("Version", informationalVersion, 15, ConsoleColor.Cyan, ConsoleColor.White);
        ConsoleUI.WriteKeyValue("Runtime", Environment.Version.ToString(), 15, ConsoleColor.Cyan, ConsoleColor.White);
        ConsoleUI.WriteKeyValue("Platform", Environment.OSVersion.Platform.ToString(), 15, ConsoleColor.Cyan, ConsoleColor.White);
        ConsoleUI.WriteKeyValue("Architecture", Environment.Is64BitProcess ? "x64" : "x86", 15, ConsoleColor.Cyan, ConsoleColor.White);

        Console.WriteLine();
        ConsoleUI.WriteSection("Build Information");
        // In single-file publish, Assembly.Location is empty. Use Environment.ProcessPath or AppContext.BaseDirectory.
        string? processPath = Environment.ProcessPath;
        DateTime buildDate;
        string locationDisplay;

        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            buildDate = File.GetCreationTime(processPath);
            locationDisplay = processPath;
        }
        else
        {
            var baseDir = AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
            {
                buildDate = Directory.GetCreationTime(baseDir);
                locationDisplay = baseDir;
            }
            else
            {
                buildDate = DateTime.MinValue;
                locationDisplay = "Unknown";
            }
        }

        ConsoleUI.WriteBullet($"Built: {buildDate:yyyy-MM-dd HH:mm:ss}", ConsoleColor.Gray);
        ConsoleUI.WriteBullet($"Location: {locationDisplay}", ConsoleColor.Gray);

        Console.WriteLine();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the status command with debug support.
    /// </summary>
    public static async Task HandleStatusCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting status command");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        ConsoleUI.WriteSection("Workspace Status Check");
        ConsoleUI.DrawLine();

        var configService = new CliConfigurationService();
        var hasConfig = await configService.HasValidConfigurationAsync();

        var configStatus = hasConfig ? "Configured" : "Not configured";
        ConsoleUI.WriteStatus(hasConfig, $"Configuration: {configStatus}");

        if (hasConfig)
        {
            try
            {
                var config = await configService.LoadConfigurationAsync();
                ConsoleUI.WriteBullet($"Server URL: {config?.ResourceUrl ?? "Unknown"}");
                ConsoleUI.WriteBullet($"Auth Required: {config?.AuthRequired ?? false}");

                DebugLogger.LogConfig("ResourceUrl", config?.ResourceUrl ?? "Unknown");
                DebugLogger.LogConfig("AuthRequired", (config?.AuthRequired ?? false).ToString());
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, "Configuration file corrupted");
                DebugLogger.Debug("Exception", $"Configuration error: {ex.Message}");
            }
        }

        var agentCount = Directory.Exists("agents") ? Directory.GetDirectories("agents").Length : 0;
        var toolCount = Directory.Exists("tools") ? Directory.GetDirectories("tools").Length : 0;

        ConsoleUI.WriteInfo($"Agents: {agentCount} configured");
        ConsoleUI.WriteInfo($"Tools: {toolCount} configured");

        DebugLogger.Debug("Local Files", $"Found {agentCount} agent directories and {toolCount} tool directories");

        bool serverConnected = false;
        bool remoteAgentsAvailable = false;

        if (hasConfig)
        {
            ProgressService.AnimatedSpinner.Start("Testing server connection");

            try
            {
                var config = await configService.LoadConfigurationAsync();
                if (config == null)
                {
                    ProgressService.AnimatedSpinner.Stop();
                    ProgressService.ShowError("No configuration found. Please run 'srectl init' first.");
                    return;
                }

                using var apiService = new ApiService();
                var (success, response) = await apiService.TestConnectionAsync(config.ResourceUrl);

                ProgressService.AnimatedSpinner.Stop();
                if (success)
                {
                    serverConnected = true;
                    ConsoleUI.WriteStatus(true, "Server Connection: Connected");

                    var (agentsSuccess, agentsResponse, _) = await apiService.ListAgentsAsync();
                    var (toolsSuccess, toolsResponse) = await apiService.ListToolsAsync();

                    remoteAgentsAvailable = agentsSuccess;
                    var remoteToolsAvailable = toolsSuccess;
                    if (agentsSuccess) ConsoleUI.WriteBullet("Remote Agents: Available");
                    if (toolsSuccess) ConsoleUI.WriteBullet("Remote Tools: Available");

                    DebugLogger.Debug("API Response", $"Connection test successful: {response}");
                    DebugLogger.Debug("API Response", $"Agents check success: {agentsSuccess}");
                    DebugLogger.Debug("API Response", $"Tools check success: {toolsSuccess}");
                }
                else
                {
                    ConsoleUI.WriteStatus(false, "Server Connection: Failed");
                    ConsoleUI.WriteBullet($"Error: {response}");

                    DebugLogger.Debug("API Response", $"Connection test failed: {response}");
                }
            }
            catch (Exception ex)
            {
                ProgressService.AnimatedSpinner.Stop();
                ConsoleUI.WriteStatus(false, "Server Connection: Failed");
                ConsoleUI.WriteBullet($"Error: {ex.Message}");

                DebugLogger.Debug("Exception", $"Connection test exception: {ex.Message}");
            }
        }

        stopwatch.Stop();
        ProgressService.ShowTiming("Status check", stopwatch.Elapsed);

        DebugLogger.LogTiming("StatusCommand", stopwatch.Elapsed);

        Console.WriteLine();
        ConsoleUI.WriteSection("Suggested next steps");

        if (!hasConfig)
        {
            ConsoleUI.WriteBullet("srectl init --resource-url <your-server>");
        }
        else if (agentCount == 0)
        {
            ConsoleUI.WriteBullet("srectl agent create --name <agent-name>");
        }
        else if (!serverConnected)
        {
            ConsoleUI.WriteBullet("Check server URL and network connectivity");
            ConsoleUI.WriteBullet("Verify authentication with 'az login'");
        }
        else
        {
            ConsoleUI.WriteBullet("srectl chat");
            ConsoleUI.WriteBullet("srectl agent test --name <agent>");
            if (remoteAgentsAvailable)
            {
                ConsoleUI.WriteBullet("srectl list agents  # List remote agents from server");
            }
            else
            {
                ConsoleUI.WriteBullet("srectl agent list  # List local agents");
            }
        }
    }
}
