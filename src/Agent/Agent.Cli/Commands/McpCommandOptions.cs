// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for mcp commands.
/// The MCP start command provides an MCP server interface for building agents.
/// </summary>
public static class McpCommandOptions
{
    // ============================================================
    // MCP Start Command Options
    // ============================================================

    public static class Start
    {
        public static readonly Option<bool> VerboseOption = new("--verbose", "-v")
        {
            Description = "Enable verbose output for debugging (logs to stderr)"
        };
    }

    // ============================================================
    // MCP Info Command Options
    // ============================================================

    public static class Info
    {
        public static readonly Option<string> TopicOption = new("--topic")
        {
            Description = "Specific topic to get info about (triggers, subagents, tools, scheduled-tasks, incident-handlers)"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Show all available information"
        };
    }
}

/// <summary>
/// Parsed options for the 'mcp start' command.
/// </summary>
public class McpStartOptions
{
    public bool Verbose { get; set; } = false;
}

/// <summary>
/// Parsed options for the 'mcp info' command.
/// </summary>
public class McpInfoOptions
{
    public string? Topic { get; set; }
    public bool ShowAll { get; set; } = false;
}
