// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

public static class InitCommandHandler
{
    public static Task<int> HandleCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        var resourceUrl = parseResult.GetValue(InitCommandOptions.ResourceUrlOption);

        return HandleCommand(resourceUrl);
    }

    /// <summary>
    /// Handles the init command with a specific resource URL.
    /// </summary>
    public static async Task<int> HandleCommand(string? resourceUrl)
    {
        try
        {
            // Validate URL format
            if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out _))
            {
                ConsoleUI.WriteStatus(false, "Invalid URL format provided.");
                return 1;
            }

            // Create configuration
            var config = new CliConfiguration
            {
                ResourceUrl = resourceUrl,
                AuthRequired = !CliConfigurationService.IsLocalhost(resourceUrl),
                LastUpdated = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Save configuration
            var configService = new CliConfigurationService();
            await configService.SaveConfigurationAsync(config);

            // Create directory structure
            Directory.CreateDirectory("agents");
            Directory.CreateDirectory("tools");
            Directory.CreateDirectory("skills");
            Directory.CreateDirectory("scheduledtasks");

            // Copy example files
            await ExampleFileManager.CopyExampleFilesAsync();

            // Create instructions.md file in .github folder
            await InstructionsFileService.CreateInstructionsFileAsync();

            ConsoleUI.WriteStatus(true, "SREAgent CLI initialized successfully!");
            ConsoleUI.WriteBullet($"Resource URL: {resourceUrl}", ConsoleColor.White);
            ConsoleUI.WriteBullet($"Auth Required: {config.AuthRequired}", ConsoleColor.White);
            ConsoleUI.WriteBullet("Created directories: agents/, tools/, skills/, scheduledtasks/, .github/", ConsoleColor.White);
            ConsoleUI.WriteBullet("Added example files: example_agent.yaml, example_tool.yaml", ConsoleColor.White);
            ConsoleUI.WriteBullet("Created comprehensive instructions file: .github/instructions.md", ConsoleColor.White);

            // Test connection
            Console.WriteLine();
            ConsoleUI.WriteBullet("Testing connection...", ConsoleColor.Cyan);
            using var apiService = new ApiService();
            var (success, response) = await apiService.TestConnectionAsync(resourceUrl);
            Console.WriteLine(response);

            // Exit with appropriate code, but don't fail initialization for connection issues
            if (!success)
            {
                ConsoleUI.WriteBullet("Note: Initialization completed successfully, but connection test failed.", ConsoleColor.Yellow);
                ConsoleUI.WriteBullet("You can still use srectl commands that don't require server connection.", ConsoleColor.White);
            }

            return 0; // Always exit successfully if initialization steps completed
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Initialization failed: {ex.Message}");
            return 1;
        }
    }
}
