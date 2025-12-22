// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static class GlobalOptions
{
    public static readonly Option<bool> DebugOption =
        new("--debug")
        {
            Recursive = true,
            Description = "Enable debug logging"
        };

    public static readonly Option<bool> QuietOption =
        new("--quiet")
        {
            Recursive = true,
            Description = "Minimize output"
        };
}
