// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for the help command.
/// </summary>
public static class HelpCommandOptions
{
    public static readonly Option<string?> OutputOption = new("--output", "-o")
    {
        Description = "Output help to a markdown file"
    };
}
