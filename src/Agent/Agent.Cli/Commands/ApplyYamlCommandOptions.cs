// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for apply-yaml command.
/// </summary>
public static class ApplyYamlCommandOptions
{
    public static readonly Option<string> FileOption = new("--file")
    {
        Description = "Path to the YAML file to apply",
        Required = true
    };
}
