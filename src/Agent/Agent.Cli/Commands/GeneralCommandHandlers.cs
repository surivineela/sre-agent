using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles general CLI commands like init and list.
/// </summary>
public static class GeneralCommandHandlers
{
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
                Console.WriteLine("❌ Invalid URL format provided.");
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

            // Copy example files
            await ExampleFileManager.CopyExampleFilesAsync();

            // Create instructions.md file in .github folder
            await InstructionsFileService.CreateInstructionsFileAsync();

            Console.WriteLine($"✅ SREAgent CLI initialized successfully!");
            Console.WriteLine($"   Resource URL: {resourceUrl}");
            Console.WriteLine($"   Auth Required: {config.AuthRequired}");
            Console.WriteLine($"   Created directories: agents/, tools/, .github/");
            Console.WriteLine($"   Added example files: example_agent.yaml, example_tool.yaml");
            Console.WriteLine($"   Created comprehensive instructions file: .github/instructions.md");

            // Test connection
            Console.WriteLine("\n🔄 Testing connection...");
            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(resourceUrl);
            Console.WriteLine(response);

            // Exit with appropriate code, but don't fail initialization for connection issues
            if (!success)
            {
                Console.WriteLine("⚠️  Note: Initialization completed successfully, but connection test failed.");
                Console.WriteLine("   You can still use srectl commands that don't require server connection.");
            }

            Environment.Exit(0); // Always exit successfully if initialization steps completed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Initialization failed: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the list agents command.
    /// </summary>
    public static async Task HandleListAgentsCommand(ParseResult parseResult)
    {
        using var apiService = new ApiService();
        var (success, response) = await apiService.ListAgentsAsync();

        Console.WriteLine(response);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the list tools command.
    /// </summary>
    public static async Task HandleListToolsCommand(ParseResult parseResult)
    {
        using var apiService = new ApiService();
        var (success, response) = await apiService.ListToolsAsync();

        Console.WriteLine(response);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the list extended-tools command.
    /// </summary>
    public static async Task HandleListExtendedToolsCommand(ParseResult parseResult)
    {
        using var apiService = new ApiService();
        var (success, response) = await apiService.ListExtendedToolsAsync();

        Console.WriteLine(response);
        Environment.Exit(success ? 0 : 1);
    }

    /// <summary>
    /// Handles the list data-connectors command.
    /// </summary>
    public static async Task HandleListDataConnectorsCommand(ParseResult parseResult)
    {
        using var apiService = new ApiService();
        var (success, response) = await apiService.ListDataConnectorsAsync();

        Console.WriteLine(response);
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
                Console.WriteLine("❌ File path is required.");
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"❌ File not found: {filePath}");
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
            Console.WriteLine($"❌ Failed to apply YAML file: {ex.Message}");
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
            LoggingService.Info("🤖 Starting interactive chat session...");
            LoggingService.Info("Type 'exit', 'quit', '/exit', or '/quit' to end the session, or press Ctrl+C");
            LoggingService.Info("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            using var apiService = new ApiService();
            string? currentThreadId = null;
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
                                Console.WriteLine($"\n🤖 SRE Agent: {text}");
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
                    LoggingService.Info("\n👋 Goodbye! Chat session ended.");
                    break;
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

                        var (threadSuccess, threadId, response) = await apiService.CreateThreadAsync(userInput, "cli-user", "CLI User");

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
                    Console.WriteLine($"\n🤖 SRE Agent: {msg.Text}");
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
    /// Handles the agent-busy scenario by polling and printing any new agent messages without changing the thread.
    /// </summary>
    private static async Task HandleAgentBusyAsync(ApiService apiService, string threadId, HashSet<string> printedMessageIds)
    {
        // Poll a few times to allow the in-flight response to complete
        const int attempts = 5;
        for (int i = 0; i < attempts; i++)
        {
            await PrintNewAgentMessagesInOrderAsync(apiService, threadId, printedMessageIds);
            await Task.Delay(10000);
        }
    }

    /// <summary>
    /// Waits until no new streaming messages have arrived for quietMs, up to timeoutMs.
    /// </summary>
    private static async Task WaitForStreamingQuietAsync(Func<DateTime?> lastMessageAtProvider, int quietMs, int timeoutMs)
    {
        var start = DateTime.UtcNow;
        DateTime? last = lastMessageAtProvider();
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            var current = lastMessageAtProvider();
            if (current != null)
            {
                // If we haven't seen a new message for quietMs, we consider it done
                if ((DateTime.UtcNow - current.Value).TotalMilliseconds >= quietMs)
                {
                    return;
                }
            }
            await Task.Delay(150);
            last = current;
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
        string[] dots = new[] { "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏" };
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
}
