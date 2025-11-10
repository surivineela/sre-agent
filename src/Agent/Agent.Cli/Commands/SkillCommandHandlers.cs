// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles skill-related command operations.
/// </summary>
public static class SkillCommandHandlers
{
    /// <summary>
    /// Handles the skill upload command.
    /// </summary>
    public static async Task HandleUploadCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill upload command");

        try
        {
            // Extract options
            var singlePath = parseResult.GetValue(SkillCommandOptions.UploadPathOption);
            var folderPath = parseResult.GetValue(SkillCommandOptions.UploadFolderOption);

            DebugLogger.Debug("Parameters", $"Path: {singlePath ?? "none"}, Folder: {folderPath ?? "none"}");

            // Validate mutually exclusive options
            if (!string.IsNullOrEmpty(singlePath) && !string.IsNullOrEmpty(folderPath))
            {
                ConsoleUI.WriteStatus(false, "Error: --path and --folder options are mutually exclusive. Use only one.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrEmpty(singlePath) && string.IsNullOrEmpty(folderPath))
            {
                ConsoleUI.WriteStatus(false, "Error: Either --path or --folder must be specified.");
                Environment.Exit(1);
                return;
            }

            List<string> skillDirectoriesToUpload;

            if (!string.IsNullOrEmpty(singlePath))
            {
                // Single skill directory upload
                DebugLogger.Debug("SkillProcessing", $"Processing single skill directory: {singlePath}");
                skillDirectoriesToUpload = await GetSingleSkillDirectory(singlePath);
            }
            else
            {
                // Multiple skill directories upload
                DebugLogger.Debug("SkillProcessing", $"Processing skill folder: {folderPath}");
                skillDirectoriesToUpload = await GetSkillDirectoriesFromFolder(folderPath!);
            }

            if (skillDirectoriesToUpload.Count == 0)
            {
                ConsoleUI.WriteStatus(false, "No valid skill directories found to upload.");
                ConsoleUI.WriteInfo("A valid skill directory must contain 'metadata.yaml' and 'SKILL.md' files.", ConsoleColor.Yellow);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteInfo($"Found {skillDirectoriesToUpload.Count} skill(s) to upload", ConsoleColor.Green);
            foreach (var skillDir in skillDirectoriesToUpload)
            {
                ConsoleUI.WriteBullet(Path.GetFileName(skillDir.TrimEnd(Path.DirectorySeparatorChar)), ConsoleColor.Gray, 3);
            }

            // Upload each skill
            using var apiService = new ApiService();
            int successCount = 0;
            int failCount = 0;

            foreach (var skillDir in skillDirectoriesToUpload)
            {
                var skillName = Path.GetFileName(skillDir.TrimEnd(Path.DirectorySeparatorChar));
                DebugLogger.Debug("SkillUpload", $"Uploading skill: {skillName}");

                var (success, response) = await apiService.UploadSkillAsync(skillDir);

                if (success)
                {
                    ConsoleUI.WriteStatus(true, $"Uploaded skill '{skillName}' successfully");
                    successCount++;
                }
                else
                {
                    ConsoleUI.WriteStatus(false, $"Failed to upload skill '{skillName}': {response}");
                    failCount++;
                }
            }

            Console.WriteLine();
            ConsoleUI.WriteInfo($"Upload complete: {successCount} succeeded, {failCount} failed",
                failCount == 0 ? ConsoleColor.Green : ConsoleColor.Yellow);

            if (failCount > 0)
            {
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"SkillUpload failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the skill convert command (convert agent to skill).
    /// </summary>
    public static async Task HandleConvertCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill convert command");

        try
        {
            var agentName = parseResult.GetValue(SkillCommandOptions.ConvertAgentNameOption);
            var topLevelAgents = parseResult.GetValue(SkillCommandOptions.ConvertTopLevelAgentsOption);
            var outputPath = parseResult.GetValue(SkillCommandOptions.ConvertOutputPathOption);

            DebugLogger.Debug("Parameters", $"AgentName: {agentName}, TopLevelAgents: {topLevelAgents?.Length ?? 0}, OutputPath: {outputPath ?? "default"}");

            // Set default output path if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine("skills", agentName!);
            }

            ProgressService.AnimatedSpinner.Start($"Converting agent '{agentName}' to skill");

            using var apiService = new ApiService();
            var (success, skillSpec, errorMessage) = await apiService.ConvertAgentToSkillAsync(
                agentName!,
                topLevelAgents?.ToList() ?? [],
                outputPath);

            ProgressService.AnimatedSpinner.Stop();

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Conversion failed: {errorMessage}");
                ProgressService.ShowError("Conversion failed",
                [
                    $"Ensure agent '{agentName}' exists on the server",
                    "Check server connection with --debug",
                    "Verify you have permissions to access the agent"
                ]);
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Successfully converted agent '{agentName}' to skill '{skillSpec?.Name}'");
            Console.WriteLine();
            ConsoleUI.WriteSection("Skill Details:");
            ConsoleUI.WriteKeyValue("Name", skillSpec?.Name ?? "N/A", 20);
            ConsoleUI.WriteKeyValue("Description", skillSpec?.Description ?? "N/A", 20);
            ConsoleUI.WriteKeyValue("Tools", string.Join(", ", skillSpec?.Tools ?? []), 20);
            ConsoleUI.WriteKeyValue("Output Path", outputPath, 20);

            Console.WriteLine();
            ConsoleUI.WriteSection("Next Steps");
            ConsoleUI.WriteBullet($"Review the generated skill at: {outputPath}", ConsoleColor.Cyan);
            ConsoleUI.WriteBullet($"Edit SKILL.md and metadata.yaml as needed", ConsoleColor.Cyan);
            ConsoleUI.WriteBullet($"Upload the skill: srectl skill upload --path {outputPath}", ConsoleColor.Cyan);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"SkillConvert failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the skill list command.
    /// </summary>
    public static async Task HandleListCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill list command");

        try
        {
            var limit = parseResult.GetValue(SkillCommandOptions.ListLimitOption);
            var page = parseResult.GetValue(SkillCommandOptions.ListPageOption);
            var search = parseResult.GetValue(SkillCommandOptions.ListSearchOption);

            DebugLogger.Debug("Parameters", $"Limit: {limit}, Page: {page}, Search: {search ?? "none"}");

            using var apiService = new ApiService();
            var (success, skills, totalCount, errorMessage) = await apiService.ListSkillsAsync(page, limit, search);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to retrieve skills: {errorMessage}");
                Environment.Exit(1);
                return;
            }

            if (skills.Count == 0)
            {
                ConsoleUI.WriteInfo("No skills found.", ConsoleColor.Yellow);
                Console.WriteLine();
                ConsoleUI.WriteSection("Next Steps");
                ConsoleUI.WriteBullet("Create a new skill: srectl skill create --name <name>", ConsoleColor.Cyan);
                ConsoleUI.WriteBullet("Create a skill by converting an agent: srectl skill convert --agent-name <name>", ConsoleColor.Cyan);
                ConsoleUI.WriteBullet("Upload a skill from a directory: srectl skill upload --path <directory>", ConsoleColor.Cyan);
                return;
            }

            ConsoleUI.WriteSection($"Skills (Page {page}, {skills.Count} of {totalCount} total):");
            Console.WriteLine();

            foreach (var skill in skills)
            {
                ConsoleUI.WriteInfo($"• {skill.Name}", ConsoleColor.Cyan);
                if (!string.IsNullOrEmpty(skill.Description))
                {
                    ConsoleUI.WriteBullet($"Description: {skill.Description}", ConsoleColor.Gray, 3);
                }

                if (skill.Tools?.Count > 0)
                {
                    ConsoleUI.WriteBullet($"Tools: {string.Join(", ", skill.Tools)}", ConsoleColor.Gray, 3);
                }

                Console.WriteLine();
            }

            // Show pagination info if there are more pages
            var totalPages = (int)Math.Ceiling((double)totalCount / limit);
            if (totalPages > 1)
            {
                Console.WriteLine();
                ConsoleUI.WriteInfo($"Page {page} of {totalPages}", ConsoleColor.Gray);
                if (page < totalPages)
                {
                    ConsoleUI.WriteCommand("View next page", $"srectl skill list --page {page + 1} --limit {limit}");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"SkillList failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the skill download command.
    /// </summary>
    public static async Task HandleDownloadCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill download command");

        try
        {
            var skillName = parseResult.GetValue(SkillCommandOptions.DownloadNameOption);
            var outputPath = parseResult.GetValue(SkillCommandOptions.DownloadOutputPathOption);

            DebugLogger.Debug("Parameters", $"SkillName: {skillName}, OutputPath: {outputPath ?? "default"}");

            // Set default output path if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine("skills", skillName!);
            }

            ProgressService.AnimatedSpinner.Start($"Downloading skill '{skillName}'");

            using var apiService = new ApiService();
            var (success, skillSpec, errorMessage) = await apiService.GetSkillByNameAsync(skillName!);

            ProgressService.AnimatedSpinner.Stop();

            if (!success || skillSpec == null)
            {
                ConsoleUI.WriteStatus(false, $"Download failed: {errorMessage}");
                ProgressService.ShowError("Download failed",
                [
                    $"Ensure skill '{skillName}' exists on the server",
                    "Check server connection with --debug",
                    "List available skills: srectl skill list"
                ]);
                Environment.Exit(1);
                return;
            }

            // Save skill to directory
            DebugLogger.Debug("SkillDownload", $"Saving skill to: {outputPath}");
            await YamlHelper.SaveSkillToDirectory(outputPath, skillSpec);

            ConsoleUI.WriteStatus(true, $"Successfully downloaded skill '{skillName}' to {outputPath}");
            Console.WriteLine();
            ConsoleUI.WriteSection("Downloaded Files:");
            ConsoleUI.WriteBullet($"{Path.Combine(outputPath, "metadata.yaml")}", ConsoleColor.Green, 3);
            ConsoleUI.WriteBullet($"{Path.Combine(outputPath, "SKILL.md")}", ConsoleColor.Green, 3);

            if (skillSpec != null && skillSpec.AdditionalFiles?.Count > 0)
            {
                foreach (var file in skillSpec.AdditionalFiles)
                {
                    ConsoleUI.WriteBullet($"{Path.Combine(outputPath, file.FileName)}", ConsoleColor.Green, 3);
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"SkillDownload failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the skill delete command.
    /// </summary>
    public static async Task HandleDeleteCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill delete command");

        try
        {
            var skillName = parseResult.GetValue(SkillCommandOptions.DeleteNameOption);

            DebugLogger.Debug("Parameters", $"SkillName: {skillName}");

            ConsoleUI.WriteInfo($"Deleting skill '{skillName}'...", ConsoleColor.Yellow);

            using var apiService = new ApiService();
            var (success, response) = await apiService.DeleteSkillAsync(skillName!);

            if (success)
            {
                ConsoleUI.WriteStatus(true, $"Skill '{skillName}' deleted successfully");
            }
            else
            {
                ConsoleUI.WriteStatus(false, $"Failed to delete skill: {response}");
                Environment.Exit(1);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"SkillDelete failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the skill create command.
    /// </summary>
    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill create command");

        try
        {
            var skillName = parseResult.GetValue(SkillCommandOptions.CreateNameOption);
            var outputPath = parseResult.GetValue(SkillCommandOptions.CreateOutputPathOption);

            DebugLogger.Debug("Parameters", $"SkillName: {skillName}, OutputPath: {outputPath ?? "default"}");

            // Set default output path if not provided
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine("skills", skillName!);
            }

            // Check if directory already exists
            if (Directory.Exists(outputPath))
            {
                ConsoleUI.WriteStatus(false, $"Directory already exists: {outputPath}");
                ConsoleUI.WriteInfo("Please choose a different name or remove the existing directory.", ConsoleColor.Yellow);
                Environment.Exit(1);
                return;
            }

            // Create directory
            Directory.CreateDirectory(outputPath);
            DebugLogger.Debug("SkillCreate", $"Created directory: {outputPath}");

            // Create metadata.yaml with basic template
            var metadataContent = GenerateMetadataTemplate(skillName!);
            var metadataPath = Path.Combine(outputPath, "metadata.yaml");
            await File.WriteAllTextAsync(metadataPath, metadataContent, System.Text.Encoding.UTF8);
            DebugLogger.Debug("SkillCreate", $"Created metadata.yaml: {metadataPath}");

            // Create SKILL.md with basic template
            var skillMdContent = GenerateSkillMdTemplate(skillName!);
            var skillMdPath = Path.Combine(outputPath, "SKILL.md");
            await File.WriteAllTextAsync(skillMdPath, skillMdContent, System.Text.Encoding.UTF8);
            DebugLogger.Debug("SkillCreate", $"Created SKILL.md: {skillMdPath}");

            ConsoleUI.WriteStatus(true, $"Successfully created skill '{skillName}' at {outputPath}");
            Console.WriteLine();
            ConsoleUI.WriteSection("Created Files:");
            ConsoleUI.WriteBullet($"{metadataPath}", ConsoleColor.Green, 3);
            ConsoleUI.WriteBullet($"{skillMdPath}", ConsoleColor.Green, 3);

            Console.WriteLine();
            ConsoleUI.WriteSection("Next Steps");
            ConsoleUI.WriteBullet($"Edit {Path.Combine(outputPath, "metadata.yaml")} to update the description", ConsoleColor.Cyan);
            ConsoleUI.WriteBullet($"Edit {Path.Combine(outputPath, "SKILL.md")} to add skill instructions and capabilities", ConsoleColor.Cyan);
            ConsoleUI.WriteBullet("Add additional markdown files as needed for specialized scenarios, and reference them in SKILL.md", ConsoleColor.Cyan);
            ConsoleUI.WriteBullet($"Upload the skill: srectl skill upload --path {outputPath}", ConsoleColor.Cyan);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"SkillCreate failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Generates a basic metadata.yaml template for a new skill.
    /// </summary>
    private static string GenerateMetadataTemplate(string skillName)
    {
        return $@"name: {skillName}
description: |
  Brief description of what this skill does and when to use it.
  Update this with specific capabilities and use cases.
";
    }

    /// <summary>
    /// Generates a basic SKILL.md template for a new skill.
    /// </summary>
    private static string GenerateSkillMdTemplate(string skillName)
    {
        return $@"# {skillName}

## Overview
Provide a clear overview of what this skill does and when it should be used.

## Capabilities
- List the main capabilities of this skill
- What problems does it solve?
- What can it help with?

## Instructions
Provide detailed instructions for using this skill:

1. When to use this skill
2. How to approach tasks with this skill
3. Best practices and guidelines
4. Any constraints or limitations

## Example Workflows

### Example 1: [Task Name]
- Goal: Describe what the user wants to accomplish
- Steps:
  1. First step
  2. Second step
  3. Third step
- Expected outcome: What should happen

### Example 2: [Another Task]
- Goal: Another use case
- Steps:
  1. Step one
  2. Step two
- Expected outcome: Result

## Related Skills
- List any related skills that might be used together with this one
- When to handoff or use other skills

## Additional Resources
- Links to documentation
- Related runbooks
- Other helpful information
";
    }

    /// <summary>
    /// Gets and validates a single skill directory.
    /// </summary>
    private static Task<List<string>> GetSingleSkillDirectory(string skillPath)
    {
        var directories = new List<string>();

        try
        {
            // Convert relative path to absolute if needed
            var absolutePath = Path.IsPathRooted(skillPath) ? skillPath : Path.GetFullPath(skillPath);

            if (!Directory.Exists(absolutePath))
            {
                throw new DirectoryNotFoundException($"Skill directory not found: {skillPath}");
            }

            // Validate skill directory structure
            if (!YamlHelper.IsValidSkillDirectory(absolutePath))
            {
                throw new ArgumentException($"Invalid skill directory: {skillPath}. Must contain 'metadata.yaml' and 'SKILL.md'");
            }

            directories.Add(absolutePath);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("SkillValidation", $"Skill directory validation failed: {ex.Message}");
            throw;
        }

        return Task.FromResult(directories);
    }

    /// <summary>
    /// Gets all skill directories from a parent folder (non-recursive, skills should not be nested).
    /// </summary>
    private static Task<List<string>> GetSkillDirectoriesFromFolder(string folderPath)
    {
        var directories = new List<string>();

        try
        {
            // Convert relative path to absolute if needed
            var absolutePath = Path.IsPathRooted(folderPath) ? folderPath : Path.GetFullPath(folderPath);

            if (!Directory.Exists(absolutePath))
            {
                throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
            }

            // Find all immediate subdirectories that contain metadata.yaml (non-recursive)
            var allSubdirectories = Directory.GetDirectories(absolutePath, "*", SearchOption.TopDirectoryOnly);

            foreach (var dir in allSubdirectories)
            {
                if (YamlHelper.IsValidSkillDirectory(dir))
                {
                    directories.Add(dir);
                    DebugLogger.Debug("SkillValidation", $"Found valid skill directory: {dir}");
                }
            }

            DebugLogger.Debug("SkillValidation", $"Found {directories.Count} valid skill directories");
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("SkillValidation", $"Folder processing failed: {ex.Message}");
            throw;
        }

        return Task.FromResult(directories);
    }
}
