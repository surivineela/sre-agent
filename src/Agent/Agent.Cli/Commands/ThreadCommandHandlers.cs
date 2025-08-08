using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

public static class ThreadCommandHandlers
{
    public static async Task HandleThreadNewCommand(ParseResult parseResult)
    {
        try
        {
            var message = parseResult.GetValue(AgentCommandOptions.ThreadMessageOption);
            var userId = parseResult.GetValue(AgentCommandOptions.ThreadUserIdOption) ?? Environment.UserName;
            var displayName = parseResult.GetValue(AgentCommandOptions.ThreadDisplayNameOption) ?? Environment.UserName;
            var wait = parseResult.GetValue(AgentCommandOptions.ThreadWaitOption);
            var noWait = parseResult.GetValue(AgentCommandOptions.ThreadNoWaitOption);

            // Default behavior is to wait unless --no-wait is specified
            // If --wait was explicitly provided, respect its value, otherwise default to true
            var shouldWait = !noWait;

            if (string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine("Message is required.");
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Sending message to SRE Agent...");
            Console.WriteLine($"Message: {message}");
            Console.WriteLine($"User: {displayName} ({userId})");
            Console.WriteLine();

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            // Step 1: Create a new thread
            Console.WriteLine("Creating new thread...");
            var (createSuccess, threadId, createResponse) = await apiService.CreateThreadAsync(message, userId, displayName);
            
            if (!createSuccess)
            {
                Console.WriteLine(createResponse);
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"Thread created: {threadId}");

            // Store the thread locally
            await threadManager.AddThreadAsync(threadId, message);

            // Step 2: Wait for agent response if requested (default is true unless --no-wait)
            if (shouldWait)
            {
                Console.WriteLine("Waiting for SRE Agent response...");
                Console.WriteLine();
                
                var (getSuccess, messages, getResponse) = await apiService.GetThreadMessagesStreamingAsync(threadId);
                
                if (!getSuccess)
                {
                    Console.WriteLine(getResponse);
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"Conversation complete! Thread ID: {threadId}");
                Console.WriteLine($"You can continue this conversation using 'srectl thread continue'");
            }
            else
            {
                Console.WriteLine($"Message sent successfully! Thread ID: {threadId}");
                Console.WriteLine($"Use 'srectl thread continue' to see the agent's response or continue the conversation.");
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to send message: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleThreadContinueCommand(ParseResult parseResult)
    {
        try
        {
            var threadId = parseResult.GetValue(AgentCommandOptions.ThreadIdOption);
            var message = parseResult.GetValue(AgentCommandOptions.ThreadMessageOptionalOption);
            var userId = parseResult.GetValue(AgentCommandOptions.ThreadUserIdOption) ?? Environment.UserName;
            var displayName = parseResult.GetValue(AgentCommandOptions.ThreadDisplayNameOption) ?? Environment.UserName;
            var wait = parseResult.GetValue(AgentCommandOptions.ThreadWaitOption);
            var noWait = parseResult.GetValue(AgentCommandOptions.ThreadNoWaitOption);

            // Default behavior is to wait unless --no-wait is specified
            // If --wait was explicitly provided, respect its value, otherwise default to true
            var shouldWait = !noWait;

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            // Get thread ID from local storage if not provided
            if (string.IsNullOrWhiteSpace(threadId))
            {
                threadId = await threadManager.GetCurrentThreadIdAsync();
                if (string.IsNullOrWhiteSpace(threadId))
                {
                    Console.WriteLine("No thread ID provided and no current thread found. Use 'srectl thread new' to create a new thread or provide --thread-id.");
                    Environment.Exit(1);
                    return;
                }
            }

            Console.WriteLine($"Continuing thread: {threadId}");

            // If message is provided, send it first
            if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine($"Sending message: {message}");
                Console.WriteLine($"User: {displayName} ({userId})");
                Console.WriteLine();

                var (sendSuccess, messageId, sendResponse) = await apiService.SendMessageAsync(threadId, message, userId, displayName);
                if (!sendSuccess)
                {
                    Console.WriteLine(sendResponse);
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"Message sent: {messageId}");
            }

            // Update thread last used
            await threadManager.UpdateThreadLastUsedAsync(threadId);

            // Get and display messages
            if (shouldWait)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    Console.WriteLine("Waiting for SRE Agent response...");
                    Console.WriteLine();
                }
                
                var (getSuccess, messages, getResponse) = await apiService.GetThreadMessagesStreamingAsync(threadId);
                
                if (!getSuccess)
                {
                    Console.WriteLine(getResponse);
                    Environment.Exit(1);
                    return;
                }

                Console.WriteLine($"Conversation complete! Thread ID: {threadId}");
                Console.WriteLine($"You can continue this conversation using 'srectl thread continue'");
            }
            else if (!string.IsNullOrWhiteSpace(message))
            {
                Console.WriteLine($"Message sent successfully! Thread ID: {threadId}");
                Console.WriteLine($"Use 'srectl thread continue' to see the agent's response.");
            }
            else
            {
                // Just show the conversation without waiting for new messages
                var (getSuccess, messages, getResponse) = await apiService.GetThreadMessagesAsync(threadId, maxRetries: 1);
                
                if (!getSuccess)
                {
                    Console.WriteLine(getResponse);
                    Environment.Exit(1);
                    return;
                }

                // Display the conversation
                Console.WriteLine("Conversation:");
                Console.WriteLine("═══════════════");
                Console.WriteLine();

                foreach (var msg in messages.OrderBy(m => m.Timestamp))
                {
                    var roleLabel = msg.AuthorRole.Equals("SREAgent", StringComparison.OrdinalIgnoreCase) ? "SRE Agent" : "You";
                    var timestamp = msg.Timestamp.ToString("HH:mm:ss");
                    Console.WriteLine($"{roleLabel} ({timestamp}):");
                    Console.WriteLine($"   {msg.Text}");
                    Console.WriteLine();
                }

                Console.WriteLine($"Thread ID: {threadId}");
                Console.WriteLine($"Use 'srectl thread continue --message \"your message\"' to continue the conversation.");
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to continue thread: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleThreadListCommand(ParseResult parseResult)
    {
        try
        {
            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            Console.WriteLine("Fetching threads...");

            var (success, threads, response) = await apiService.ListThreadsAsync();
            
            if (!success)
            {
                Console.WriteLine(response);
                Environment.Exit(1);
                return;
            }

            if (threads.Count == 0)
            {
                Console.WriteLine("No threads found.");
                Environment.Exit(0);
                return;
            }

            Console.WriteLine("Threads:");
            Console.WriteLine("═══════════════");
            Console.WriteLine();

            var currentThreadId = await threadManager.GetCurrentThreadIdAsync();

            foreach (var thread in threads.OrderByDescending(t => t.LastMessageAt))
            {
                var marker = thread.Id == currentThreadId ? "→ " : "  ";
                var title = string.IsNullOrWhiteSpace(thread.Title) ? "No title" : thread.Title;
                if (title.Length > 50)
                {
                    title = title.Substring(0, 47) + "...";
                }

                Console.WriteLine($"{marker}{thread.Id}");
                Console.WriteLine($"   Title: {title}");
                Console.WriteLine($"   Created: {thread.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine($"   Last Message: {thread.LastMessageAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }

            Console.WriteLine($"Total: {threads.Count} thread(s)");
            if (!string.IsNullOrWhiteSpace(currentThreadId))
            {
                Console.WriteLine($"Current thread: {currentThreadId} (marked with →)");
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to list threads: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleThreadDeleteCommand(ParseResult parseResult)
    {
        try
        {
            var threadId = parseResult.GetValue(AgentCommandOptions.ThreadIdRequiredOption);

            if (string.IsNullOrWhiteSpace(threadId))
            {
                Console.WriteLine("Thread ID is required. Use --thread-id to specify the thread to delete.");
                Environment.Exit(1);
                return;
            }

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            Console.WriteLine($"Deleting thread: {threadId}");

            var (success, response) = await apiService.DeleteThreadAsync(threadId);
            
            if (!success)
            {
                Console.WriteLine(response);
                Environment.Exit(1);
                return;
            }

            // Remove from local storage as well
            await threadManager.DeleteThreadAsync(threadId);

            Console.WriteLine(response);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to delete thread: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleThreadTrackCommand(ParseResult parseResult)
    {
        try
        {
            var threadId = parseResult.GetValue(AgentCommandOptions.ThreadIdRequiredOption);

            if (string.IsNullOrWhiteSpace(threadId))
            {
                Console.WriteLine("Thread ID is required. Use --thread-id to specify the thread to track.");
                Environment.Exit(1);
                return;
            }

            using var apiService = new ApiService();
            var threadManager = new ThreadManagerService();

            Console.WriteLine($"Tracking thread: {threadId}");
            Console.WriteLine("Press Ctrl+C to stop tracking...");
            Console.WriteLine();

            var (success, messages, response) = await apiService.TrackThreadAsync(threadId);
            
            if (!success)
            {
                Console.WriteLine(response);
                Environment.Exit(1);
                return;
            }

            // Update thread last used
            await threadManager.UpdateThreadLastUsedAsync(threadId);

            Console.WriteLine($"Thread tracking complete! Thread ID: {threadId}");
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to track thread: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
