// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles common-prompt-related command operations.
/// </summary>
public static class CommonPromptCommandHandlers
{
    private const string CommonPromptsDirectory = "CommonPrompts";

    /// <summary>
    /// Handles the common-prompt create command.
    /// </summary>
    public static async Task<int> HandleCreateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting common-prompt create command");

        var name = parseResult.GetValue(CommonPromptCommandOptions.Create.NameOption);
        var customPath = parseResult.GetValue(CommonPromptCommandOptions.Create.PathOption);
        var prompt = parseResult.GetValue(CommonPromptCommandOptions.Create.PromptOption);
        var owner = parseResult.GetValue(CommonPromptCommandOptions.Create.OwnerOption);
        var tags = parseResult.GetValue(CommonPromptCommandOptions.Create.TagOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, Path: {customPath ?? "default"}");

        // Create CommonPromptV2 instance
        var commonPrompt = CommonPromptHelper.CreateCommonPrompt(
            name!,
            prompt,
            owner,
            tags?.ToList());

        // Serialize to YAML
        var promptYaml = commonPrompt.ToYaml();

        // Write prompt to file
        string yamlPath;

        if (customPath != null && customPath.Length > 0)
        {
            // Use custom path: CommonPrompts/{customPath}/{name}.yaml
            var promptDir = Path.Combine(CommonPromptsDirectory, customPath);
            Directory.CreateDirectory(promptDir);
            yamlPath = Path.Combine(promptDir, $"{name}.yaml");
        }
        else if (customPath == string.Empty)
        {
            // Use flat structure: CommonPrompts/{name}.yaml
            Directory.CreateDirectory(CommonPromptsDirectory);
            yamlPath = Path.Combine(CommonPromptsDirectory, $"{name}.yaml");
        }
        else
        {
            // Use legacy structure: CommonPrompts/{name}/{name}.yaml
            var promptDir = Path.Combine(CommonPromptsDirectory, name!);
            Directory.CreateDirectory(promptDir);
            yamlPath = Path.Combine(promptDir, $"{name}.yaml");
        }

        DebugLogger.LogFile("WRITE", yamlPath, $"Common prompt YAML content size: {promptYaml.Length} characters");

        await File.WriteAllTextAsync(yamlPath, promptYaml);
        ConsoleUI.WriteStatus(true, $"Common prompt YAML created at {yamlPath}");
        Console.WriteLine();
        ConsoleUI.WriteSection("Next Steps");
        ConsoleUI.WriteCommand("Review and customize", "Edit the generated YAML file");
        ConsoleUI.WriteCommand("Apply prompt", $"srectl common-prompt apply --name {name}");

        return 0;
    }

    /// <summary>
    /// Handles the common-prompt get command to display available common prompts.
    /// </summary>
    public static async Task<int> HandleGetCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting common-prompt get command");

        var search = parseResult.GetValue(CommonPromptCommandOptions.Get.SearchOption);
        var name = parseResult.GetValue(CommonPromptCommandOptions.Get.NameOption);
        var detail = parseResult.GetValue(CommonPromptCommandOptions.Get.DetailOption);

        DebugLogger.Debug("Parameters", $"Search: {search}, Name: {name}, Detail: {detail}");

        using var apiService = new ApiService();

        var (promptsList, error) = await apiService.ListCommonPromptsAsync(search);

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        if (promptsList.Count == 0)
        {
            ConsoleUI.WriteInfo("No common prompts found on the server.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Use 'srectl common-prompt apply <prompt-name>' to add prompts to the server.", ConsoleColor.Gray);
            return 0;
        }

        // Filter by name if specified
        if (!string.IsNullOrWhiteSpace(name))
        {
            var prompt = promptsList.FirstOrDefault(p =>
                string.Equals(p.Metadata?.Name, name, StringComparison.OrdinalIgnoreCase));

            if (prompt == null)
            {
                ConsoleUI.WriteStatus(false, $"Common prompt '{name}' not found.");
                return 1;
            }

            ConsoleUI.WriteSection("Remote Common Prompt");
            Console.WriteLine(prompt.ToYaml());
            return 0;
        }

        ConsoleUI.WriteSection("Remote Common Prompts");

        for (int i = 0; i < promptsList.Count; i++)
        {
            if (detail)
            {
                var yamlOutput = promptsList[i].ToYaml();
                Console.WriteLine(yamlOutput);
                if (i < promptsList.Count - 1)
                {
                    ConsoleUI.DrawLine();
                }
            }
            else
            {
                var promptName = promptsList[i].Metadata?.Name ?? "Unknown";
                ConsoleUI.WriteBullet(promptName);
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteKeyValue("Total", $"{promptsList.Count} common prompt(s)", 0);
        return 0;
    }

    /// <summary>
    /// Handles the common-prompt apply command.
    /// </summary>
    public static async Task<int> HandleApplyCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting common-prompt apply command");

        var name = parseResult.GetValue(CommonPromptCommandOptions.Apply.NameOption);
        var dryRun = parseResult.GetValue(CommonPromptCommandOptions.Apply.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        // Find common prompt YAML file using flexible search
        var promptFilePath = CommonPromptHelper.FindCommonPrompt(name!);
        if (promptFilePath == null)
        {
            ConsoleUI.WriteStatus(false, $"Common prompt file not found for '{name}'. Searched in CommonPrompts directory and subdirectories for '{name}.yaml'");
            return 1;
        }

        // Read and parse the YAML file as CommonPromptV2
        var prompt = await CommonPromptV2.LoadYamlAsync(promptFilePath);
        if (prompt == null)
        {
            ConsoleUI.WriteStatus(false, $"Failed to parse common prompt YAML file: {promptFilePath}");
            return 1;
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyCommonPromptAsync(prompt, dryRun);

        Console.WriteLine(response);
        return success ? 0 : 1;
    }

    /// <summary>
    /// Handles the common-prompt delete command.
    /// </summary>
    public static async Task<int> HandleDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting common-prompt delete command");

        var promptName = parseResult.GetValue(CommonPromptCommandOptions.Delete.NameOption);
        var dryRun = parseResult.GetValue(CommonPromptCommandOptions.Delete.DryRunOption);

        DebugLogger.Debug("Parameters", $"PromptName: {promptName}, DryRun: {dryRun}");

        if (string.IsNullOrWhiteSpace(promptName))
        {
            ConsoleUI.WriteStatus(false, "Common prompt name is required.");
            return 1;
        }

        try
        {
            using var apiService = new ApiService();

            if (dryRun)
            {
                ConsoleUI.WriteInfo($"Validating common prompt deletion for '{promptName}' (dry run)...", ConsoleColor.Yellow);
            }
            else
            {
                ConsoleUI.WriteInfo($"Deleting common prompt '{promptName}'...", ConsoleColor.Yellow);
            }

            var (success, message) = await apiService.DeleteCommonPromptAsync(promptName, dryRun);

            if (success)
            {
                ConsoleUI.WriteStatus(true, message);

                // After successful server deletion (not dry-run), offer to clean up local files
                if (!dryRun)
                {
                    OfferLocalPromptCleanup(promptName);
                }

                return 0;
            }
            else
            {
                ConsoleUI.WriteStatus(false, message);
                return 1;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"DeleteCommonPrompt failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to delete common prompt: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Offers to clean up local common prompt files after successful server deletion.
    /// </summary>
    private static void OfferLocalPromptCleanup(string promptName)
    {
        var promptFile = CommonPromptHelper.FindCommonPrompt(promptName);

        if (promptFile == null)
        {
            return; // No local files to clean up
        }

        var promptDir = Path.GetDirectoryName(promptFile);

        Console.WriteLine();
        ConsoleUI.WriteSection("Local File Cleanup");
        ConsoleUI.WriteInfo("Local configuration files still exist:", ConsoleColor.Yellow);
        ConsoleUI.WriteBullet(promptFile, ConsoleColor.Gray);
        Console.WriteLine();

        if (ConsoleUI.Confirm("Also delete local configuration files?", false))
        {
            try
            {
                // If prompt is in its own directory (legacy structure), delete the directory
                if (promptDir != null && Path.GetFileName(promptDir) == promptName)
                {
                    Directory.Delete(promptDir, true);
                    ConsoleUI.WriteStatus(true, $"Deleted directory: {promptDir}");
                }
                else
                {
                    // Otherwise just delete the YAML file
                    File.Delete(promptFile);
                    ConsoleUI.WriteStatus(true, $"Deleted file: {promptFile}");
                }

                Console.WriteLine();
                ConsoleUI.WriteSection("Summary");
                ConsoleUI.WriteBullet($"Common prompt '{promptName}' deleted from server", ConsoleColor.Green);
                ConsoleUI.WriteBullet($"Local configuration files deleted", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Failed to delete local files: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Summary");
            ConsoleUI.WriteBullet($"Common prompt '{promptName}' deleted from server", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Local configuration files preserved: {promptFile}", ConsoleColor.Yellow);

            Console.WriteLine();
            ConsoleUI.WriteInfo($"To redeploy: srectl common-prompt apply --name {promptName}", ConsoleColor.Cyan);
            if (Path.GetFileName(promptDir) == promptName)
            {
                ConsoleUI.WriteInfo($"To delete locally: rm -rf {promptDir!.Replace('\\', '/')}", ConsoleColor.Gray);
            }
            else
            {
                ConsoleUI.WriteInfo($"To delete locally: rm {promptFile.Replace('\\', '/')}", ConsoleColor.Gray);
            }
        }
    }
}
