// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

public static class InitCommandOptions
{
    public static readonly Option<string> ResourceUrlOption = new("--resource-url")
    {
        Required = true,
        Description = "Base URL of the SRE Agent server"
    };
}
