// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static class GlobalOptions
{
    public static readonly Option<bool> Debug =
        new("--debug")
        {
            Description = "Enable debug logging"
        };

    public static readonly Option<bool> Quiet =
        new("--quiet")
        {
            Description = "Minimize output"
        };
}
