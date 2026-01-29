// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

public static class ThreadCommandHandlers
{
    public static async Task<int> HandleThreadNewCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting thread new command");

        var message = parseResult.GetValue(ThreadCommandOptions.New.MessageOption);
        var agent = parseResult.GetValue(ThreadCommandOptions.New.AgentNameOption);
        var noWait = parseResult.GetValue(ThreadCommandOptions.New.NoWaitOption);
        var wait = parseResult.GetValue(ThreadCommandOptions.New.WaitOption);

        DebugLogger.Debug("Parameters", $"Agent: {agent}, Message: {(string.IsNullOrEmpty(message) ? "<none>" : "<provided>")}, NoWait: {noWait}, Wait: {wait}");

        var threadManager = new ThreadManagerService();

        // If --no-wait is specified, just send the message without starting interactive session
        if (noWait)
        {
            var error = await threadManager.SendMessageWithoutWaitAsync(threadId: null, agentName: agent, message: message!);
            if (error != null)
            {
                ConsoleUI.WriteStatus(false, error);
                return 1;
            }
            return 0;
        }

        // If --wait is specified, send message and wait for response then exit
        if (wait)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ConsoleUI.WriteStatus(false, "--wait requires --message to be specified");
                return 1;
            }

            var error = await threadManager.SendMessageAndWaitForResponseAsync(threadId: null, agentName: agent, message: message);
            if (error != null)
            {
                ConsoleUI.WriteStatus(false, error);
                return 1;
            }
            return 0;
        }

        var sessionError = await threadManager.StartChatSessionAsync(threadId: null, agentName: agent, message: message);
        if (sessionError != null)
        {
            ConsoleUI.WriteStatus(false, sessionError);
            return 1;
        }
        return 0;
    }

    public static async Task<int> HandleThreadApplyCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting thread apply command");

        try
        {
            ConsoleUI.WriteInfo($"⚠️  Warning: The 'srectl thread apply' will be deprecated soon.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo($"    Please contact HoWang/SanMeht if you depend on this command.", ConsoleColor.Yellow);
            Console.WriteLine();

            var filePath = parseResult.GetValue(ThreadCommandOptions.Apply.FileOption);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                ConsoleUI.WriteStatus(false, "--file is required");
                return 1;
            }
            if (!File.Exists(filePath))
            {
                ConsoleUI.WriteStatus(false, $"File not found: {filePath}");
                return 1;
            }

            var yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
            var des = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            var manifest = des.Deserialize<Agent.Common.Core.Manifests.ThreadManifest>(yaml);
            if (manifest == null || manifest.Spec == null || !string.Equals(manifest.Kind, "Thread", StringComparison.OrdinalIgnoreCase))
            {
                ConsoleUI.WriteStatus(false, "YAML kind must be Thread");
                return 1;
            }

            using var apiService = new ApiService();
            var userId = manifest.Spec.UserId ?? Environment.UserName;
            var displayName = manifest.Spec.DisplayName ?? Environment.UserName;
            var (thread, error) = await apiService.CreateThreadAsync(manifest.Spec.Message, userId, displayName, manifest.Spec.Agent);

            if (thread != null && string.IsNullOrEmpty(error))
            {
                ConsoleUI.WriteStatus(true, $"Thread created successfully with ID: {thread.Id}");
                var tm = new ThreadManagerService();
                return 0;
            }
            else
            {
                ConsoleUI.WriteStatus(false, error ?? "Failed to create thread");
                return 1;
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to apply thread YAML: {ex.Message}");
            return 1;
        }
    }

    public static async Task<int> HandleThreadContinueCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting thread continue command");

        var threadId = parseResult.GetValue(ThreadCommandOptions.Continue.ThreadIdOption);
        var message = parseResult.GetValue(ThreadCommandOptions.Continue.MessageOption);
        var noWait = parseResult.GetValue(ThreadCommandOptions.Continue.NoWaitOption);
        var wait = parseResult.GetValue(ThreadCommandOptions.Continue.WaitOption);

        var threadManager = new ThreadManagerService();

        // If no thread ID is provided, get the current thread ID
        if (string.IsNullOrEmpty(threadId))
        {
            threadId = await threadManager.GetCurrentThreadIdAsync();
            if (string.IsNullOrEmpty(threadId))
            {
                ConsoleUI.WriteStatus(false, "No thread ID specified and no current thread found. Use --thread-id or create a new thread with 'srectl thread new'.");
                return 1;
            }
        }

        DebugLogger.Debug("Parameters", $"ThreadId: {threadId}, Message: {(string.IsNullOrEmpty(message) ? "<none>" : "<provided>")}, NoWait: {noWait}, Wait: {wait}");

        // If --no-wait is specified, just send the message without starting interactive session
        if (noWait)
        {
            var error = await threadManager.SendMessageWithoutWaitAsync(threadId, agentName: null, message: message!);
            if (error != null)
            {
                ConsoleUI.WriteStatus(false, error);
                return 1;
            }
            return 0;
        }

        // If --wait is specified, send message and wait for response then exit
        if (wait)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                ConsoleUI.WriteStatus(false, "--wait requires --message to be specified");
                return 1;
            }

            var error = await threadManager.SendMessageAndWaitForResponseAsync(threadId, agentName: null, message: message);
            if (error != null)
            {
                ConsoleUI.WriteStatus(false, error);
                return 1;
            }
            return 0;
        }

        var sessionError = await threadManager.StartChatSessionAsync(threadId, agentName: null, message: message);
        if (sessionError != null)
        {
            ConsoleUI.WriteStatus(false, sessionError);
            return 1;
        }
        return 0;
    }

    public static async Task<int> HandleThreadListCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting thread list command");

        using var apiService = new ApiService();
        var threadManager = new ThreadManagerService();

        var (threadCollection, error) = await apiService.ListThreadsAsync();

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        if (threadCollection == null || threadCollection.Value.Count == 0)
        {
            ConsoleUI.WriteInfo("No threads found.");
            return 0;
        }

        var threads = threadCollection.Value;
        var currentThreadId = await threadManager.GetCurrentThreadIdAsync();

        // Sort by most recently modified first
        var sortedThreads = threads.OrderByDescending(t => t.ModifiedTimestamp).ToList();

        // Calculate column widths
        const int markerWidth = 2;
        const int idWidth = 36;
        const int createdWidth = 19;
        const int modifiedWidth = 19;
        const int titleWidth = 50;

        // Header
        ConsoleUI.WriteInline("  " + "ID".PadRight(idWidth) + "  ", ConsoleColor.Cyan);
        ConsoleUI.WriteInline("CreateAt".PadRight(createdWidth) + "  ", ConsoleColor.Cyan);
        ConsoleUI.WriteInline("ModifiedAt".PadRight(modifiedWidth) + "  ", ConsoleColor.Cyan);
        ConsoleUI.WriteInline("Title", ConsoleColor.Cyan);
        Console.WriteLine();

        // Separator line
        var totalWidth = markerWidth + idWidth + createdWidth + modifiedWidth + titleWidth + 6;
        ConsoleUI.DrawLine(totalWidth, ConsoleColor.DarkGray);

        // Data rows
        foreach (var thread in sortedThreads)
        {
            var isCurrent = thread.Id == currentThreadId;
            var rowColor = isCurrent ? ConsoleColor.Green : ConsoleColor.White;
            var marker = isCurrent ? "→ " : "  ";

            // Get title - take only first line and handle empty/whitespace
            var displayTitle = string.IsNullOrWhiteSpace(thread.Title) ? "(No title)" : thread.Title;

            // Split by newlines and take first line only
            var lines = displayTitle.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            displayTitle = lines.Length > 0 ? lines[0].Trim() : "(No title)";

            // Truncate if too long
            if (displayTitle.Length > titleWidth)
            {
                displayTitle = displayTitle.Substring(0, titleWidth - 3) + "...";
            }

            ConsoleUI.WriteInline(marker + thread.Id.PadRight(idWidth) + "  ", rowColor);
            ConsoleUI.WriteInline(thread.CreatedTimestamp.ToString("yyyy-MM-dd HH:mm:ss").PadRight(createdWidth) + "  ", rowColor);
            ConsoleUI.WriteInline(thread.ModifiedTimestamp.ToString("yyyy-MM-dd HH:mm:ss").PadRight(modifiedWidth) + "  ", rowColor);
            ConsoleUI.WriteInline(displayTitle, rowColor);
            Console.WriteLine();
        }

        Console.WriteLine();
        ConsoleUI.WriteInfo($"Total: {threads.Count} thread(s)");

        return 0;
    }

    public static async Task<int> HandleThreadDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting thread delete command");

        var threadId = parseResult.GetValue(ThreadCommandOptions.Delete.ThreadIdOption);

        if (string.IsNullOrWhiteSpace(threadId))
        {
            ConsoleUI.WriteStatus(false, "Thread ID is required. Use --thread-id to specify the thread to delete.");
            return 1;
        }

        using var apiService = new ApiService();
        var threadManager = new ThreadManagerService();

        ConsoleUI.WriteInfo($"Deleting thread: {threadId}");

        var (success, message) = await apiService.DeleteThreadAsync(threadId);

        if (!success)
        {
            ConsoleUI.WriteStatus(false, message);
            return 1;
        }

        ConsoleUI.WriteStatus(true, message);
        return 0;
    }

    public static async Task<int> HandleThreadTrackCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting thread track command");

        var threadId = parseResult.GetValue(ThreadCommandOptions.Track.ThreadIdOption);

        if (string.IsNullOrWhiteSpace(threadId))
        {
            ConsoleUI.WriteStatus(false, "Thread ID is required. Use --thread-id to specify the thread to track.");
            return 1;
        }

        var threadManager = new ThreadManagerService();

        var error = await threadManager.TrackChatSessionAsync(threadId);
        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }
        return 0;
    }
}
