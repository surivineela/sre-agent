using System.CommandLine;
using System.CommandLine.Parsing;
using Agent.Cli.Services;

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