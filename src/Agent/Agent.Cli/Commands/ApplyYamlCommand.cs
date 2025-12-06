// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles the apply-yaml command operations.
/// This is a convenience command that delegates to GeneralCommandHandlers.
/// </summary>
public static class ApplyYamlCommand
{
    /// <summary>
    /// Handles the apply-yaml command.
    /// </summary>
    public static async Task HandleCommand(ParseResult parseResult)
    {
        try
        {
            var filePath = parseResult.GetValue(ApplyYamlCommandOptions.FileOption);

            if (string.IsNullOrEmpty(filePath))
            {
                ConsoleUI.WriteStatus(false, "File path is required.");
                Environment.Exit(1);
                return;
            }

            if (!File.Exists(filePath))
            {
                ConsoleUI.WriteStatus(false, $"File not found: {filePath}");
                Environment.Exit(1);
                return;
            }

            using var apiService = new ApiService();
            var (success, response) = await apiService.ApplyYamlFileAsync(filePath);

            Console.WriteLine(response);
            Environment.Exit(success ? 0 : 1);
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Failed to apply YAML file: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
