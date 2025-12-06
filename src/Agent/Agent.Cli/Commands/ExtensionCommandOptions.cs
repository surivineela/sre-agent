// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for extension commands.
/// </summary>
public static class ExtensionCommandOptions
{
    // Global debug option for all extension commands
    public static readonly Option<bool> DebugOption = new("--debug")
    {
        Description = "Enable verbose debug logging for network calls and operations"
    };

    // Generate EV2 command options
    public static readonly Option<string> ToolsFolderOption = new("--tools-folder")
    {
        Required = true,
        Description = "Path to the tools folder containing tool configurations"
    };

    public static readonly Option<string> AgentFolderOption = new("--agent-folder")
    {
        Required = true,
        Description = "Path to the agent folder containing agent configurations"
    };

    public static readonly Option<string> OutputOption = new("--output")
    {
        Required = true,
        Description = "Output folder where generated EV2 files will be placed"
    };
}
