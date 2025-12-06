// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Commands;

namespace Agent.Cli.Helpers;

/// <summary>
/// Provides centralized execution context for commands, automatically handling debug mode and other global options.
/// </summary>
public static class CommandExecutionContext
{
    /// <summary>
    /// Initializes the command execution context, setting up debug logging and other global options.
    /// Call this at the beginning of every command handler.
    /// </summary>
    /// <param name="parseResult">The parse result from the command line parser</param>
    public static void Initialize(ParseResult parseResult)
    {
        // Try to get debug flag from various possible option locations
        var debug = TryGetDebugFlag(parseResult);
        // Set debug mode globally
        DebugLogger.SetDebugMode(debug);

        // Show debug activation message if enabled
        if (debug)
        {
            ConsoleUI.WriteInfo($"DEBUG MODE ACTIVATED: {DateTime.Now:HH:mm:ss.fff}", ConsoleColor.DarkGray);
        }
        // Log command execution start
        DebugLogger.Debug("Command", $"Starting command execution at {DateTime.Now:HH:mm:ss.fff}");
    }

    /// <summary>
    /// Try to extract the debug flag from various possible option sources in the parse result.
    /// This handles the case where different command groups might use different option objects.
    /// </summary>
    private static bool TryGetDebugFlag(ParseResult parseResult)
    {
        // Prefer a resilient approach: OR across all potential sources and avoid early returns.
        // This handles duplicate --debug options attached to different command trees.

        bool enabled = false;

        // 1) Raw token presence (works even if bound to a different Option instance)
        if (parseResult.Tokens.Any(t => t.Value == "--debug" || t.Value == "-d"))
        {
            enabled = true;
        }

        // 2) Global option instance
        try { enabled |= parseResult.GetValue(GlobalOptions.DebugOption); } catch { /* ignore */ }

        return enabled;
    }

    /// <summary>
    /// Checks if debug mode is currently enabled.
    /// </summary>
    public static bool IsDebugEnabled => DebugLogger.IsDebugEnabled;

    /// <summary>
    /// Logs the completion of a command execution.
    /// </summary>
    /// <param name="commandName">The name of the command that completed</param>
    /// <param name="success">Whether the command completed successfully</param>
    public static void LogCompletion(string commandName, bool success = true)
    {
        var status = success ? "SUCCESS" : "FAILED";
        DebugLogger.Debug("Command", $"Command '{commandName}' completed with status: {status} at {DateTime.Now:HH:mm:ss.fff}");
    }
}
