// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles the apply-yaml command operations.
/// This is a convenience command that delegates to GeneralCommandHandlers.
/// </summary>
public static class ApplyYamlCommand
{
    /// <summary>
    /// Handles the apply-yaml command by delegating to the general command handler.
    /// </summary>
    public static async Task HandleApplyYamlCommand(ParseResult parseResult)
    {
        await GeneralCommandHandlers.HandleApplyYamlCommand(parseResult);
    }
}
