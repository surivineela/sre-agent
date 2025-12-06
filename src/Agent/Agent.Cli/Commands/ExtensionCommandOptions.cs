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
    // ============================================================
    // Extension GenerateEv2 Command Options
    // ============================================================

    public static class GenerateEv2
    {
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
}
