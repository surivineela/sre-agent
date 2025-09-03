using System;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Linq;
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
        // Try different possible debug options in order of preference
        
        // 1. Try GlobalOptions.Debug first (most common)
        try
        {
            var globalDebug = parseResult.GetValue(GlobalOptions.Debug);
            return globalDebug;
        }
        catch { /* Ignore if option not available */ }
        
        // 2. Try AgentCommandOptions.DebugOption
        try
        {
            var agentDebug = parseResult.GetValue(AgentCommandOptions.DebugOption);
            return agentDebug;
        }
        catch { /* Ignore if option not available */ }
        
        // 3. Try ToolCommandOptions.DebugOption
        try
        {
            var toolDebug = parseResult.GetValue(ToolCommandOptions.DebugOption);
            return toolDebug;
        }
        catch { /* Ignore if option not available */ }
        
        // 4. Try DocumentCommandOptions.DebugOption
        try
        {
            var docDebug = parseResult.GetValue(DocumentCommandOptions.DebugOption);
            return docDebug;
        }
        catch { /* Ignore if option not available */ }
        
        // 5. Look for --debug flag directly in the command line tokens
        if (parseResult.Tokens.Any(token => 
            token.Value == "--debug" || token.Value == "-d"))
        {
            return true;
        }
        
        // Default to false if no debug flag found
        return false;
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
