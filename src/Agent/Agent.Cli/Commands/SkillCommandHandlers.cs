// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles skill-related command operations.
/// </summary>
public static class SkillCommandHandlers
{
    /// <summary>
    /// Handles the skill apply command.
    /// </summary>
    public static async Task<int> HandleApplyCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting skill apply command");

        var name = parseResult.GetValue(SkillCommandOptions.Apply.NameOption);
        var dryRun = parseResult.GetValue(SkillCommandOptions.Apply.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        // Check if 'upload' alias was used and show deprecation warning
        var commandToken = parseResult.Tokens.FirstOrDefault(t => t.Type == System.CommandLine.Parsing.TokenType.Command && t.Value == "upload");
        if (commandToken != null)
        {
            ConsoleUI.WriteInfo("⚠️  Warning: 'srectl skill upload' is deprecated and will be removed in a future release.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("    Please use 'srectl skill apply' instead.", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        // Find skill directory using flexible search
        var skillDirectory = ExtendedSkillHelper.FindSkillDirectory(name!);
        if (skillDirectory == null)
        {
            ConsoleUI.WriteStatus(false, $"Skill directory not found for '{name}'. Searched in skills directory and subdirectories.");
            return 1;
        }

        // Detect skill version
        var metadataPath = Path.Combine(skillDirectory, ExtendedSkillHelper.MetadataFileName);
        var detectedVersion = ExtendedSkillHelper.DetectVersion(metadataPath);

        if (detectedVersion == null)
        {
            ConsoleUI.WriteStatus(false, $"Unable to detect skill format. The metadata.yaml file may be invalid or corrupted.");
            return 1;
        }

        ExtendedSkillV2 skill;

        // Handle V1 format - convert to V2
        if (detectedVersion == YamlApiVersion.V1)
        {
            ConsoleUI.WriteInfo($"⚠️  Warning: Skill YAML is using V1 format.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo($"    Consider migrating to V2 format by running: srectl skill migrate --name [skill name]", ConsoleColor.Yellow);
            Console.WriteLine();

            var (v1Skill, v1Error) = await ExtendedSkillV1.LoadYamlAsync(metadataPath);
            if (v1Skill == null || !string.IsNullOrEmpty(v1Error))
            {
                ConsoleUI.WriteStatus(false, v1Error ?? $"Failed to load V1 skill: {metadataPath}");
                return 1;
            }

            // Convert V1 to V2
            skill = Converters.ExtendedSkillConverter.ConvertToV2(v1Skill);
        }
        else // V2 format
        {
            var (v2Skill, v2Error) = await ExtendedSkillV2.LoadYamlAsync(metadataPath);
            if (v2Skill == null || !string.IsNullOrEmpty(v2Error))
            {
                ConsoleUI.WriteStatus(false, v2Error ?? $"Failed to load skill: {metadataPath}");
                return 1;
            }
            skill = v2Skill;
        }

        // Check if --name parameter differs from the name in YAML
        var yamlName = skill.Metadata?.Name;
        if (!string.IsNullOrEmpty(yamlName) && !string.Equals(name, yamlName, StringComparison.OrdinalIgnoreCase))
        {
            ConsoleUI.WriteBullet($"Warning: --name parameter '{name}' differs from YAML metadata.name '{yamlName}'", ConsoleColor.Yellow);
            ConsoleUI.WriteBullet($"Using name from YAML: '{yamlName}'", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyExtendedSkillAsync(skill, dryRun);

        Console.WriteLine(response);
        return success ? 0 : 1;
    }

    /// <summary>
    /// Handles the skill convert command (convert agent to skill).
    /// </summary>
    public static async Task HandleConvertCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill convert command");

        try
        {
            var agentName = parseResult.GetValue(SkillCommandOptions.Convert.AgentNameOption);
            var topLevelAgents = parseResult.GetValue(SkillCommandOptions.Convert.TopLevelAgentsOption);
            var outputPath = parseResult.GetValue(SkillCommandOptions.Convert.OutputPathOption);

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
            ConsoleUI.WriteBullet($"Apply the skill: srectl skill apply --name {skillSpec?.Name}", ConsoleColor.Cyan);
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
    public static async Task<int> HandleListCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting skill list command");

        var search = parseResult.GetValue(SkillCommandOptions.List.SearchOption);
        var name = parseResult.GetValue(SkillCommandOptions.List.NameOption);
        var detail = parseResult.GetValue(SkillCommandOptions.List.DetailOption);

        DebugLogger.Debug("Parameters", $"Search: {search}, Name: {name}, Detail: {detail}");

        using var apiService = new ApiService();

        var (skillsList, error) = await apiService.ListExtendedSkillsAsync(search);

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        if (skillsList.Count == 0)
        {
            ConsoleUI.WriteInfo("No extended skills found on the server.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Use 'srectl skill apply --name <skill-name>' to add skills to the server.", ConsoleColor.Gray);
            return 1;
        }

        // Filter by name if specified
        if (!string.IsNullOrWhiteSpace(name))
        {
            var skill = skillsList.FirstOrDefault(s =>
                string.Equals(s.Metadata?.Name, name, StringComparison.OrdinalIgnoreCase));

            if (skill == null)
            {
                ConsoleUI.WriteStatus(false, $"Skill '{name}' not found.");
                return 1;
            }

            ConsoleUI.WriteSection("Remote Extended Skill");
            Console.WriteLine(skill.ToYaml());
            return 0;
        }

        ConsoleUI.WriteSection("Remote Extended Skills");

        for (int i = 0; i < skillsList.Count; i++)
        {
            if (detail)
            {
                Console.WriteLine(skillsList[i].ToYaml());
                Console.WriteLine();
                if (i < skillsList.Count - 1)
                {
                    ConsoleUI.DrawLine(80, ConsoleColor.DarkGray);
                }
            }
            else
            {
                ConsoleUI.WriteBullet(skillsList[i].Metadata?.Name ?? "<unnamed>", ConsoleColor.Cyan);
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteKeyValue("Total", $"{skillsList.Count} extended skill(s)", 0);
        return 0;
    }

    /// <summary>
    /// Handles the skill sync command.
    /// </summary>
    public static async Task<int> HandleSyncCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting skill sync command");

        var skillName = parseResult.GetValue(SkillCommandOptions.Sync.NameOption);
        var all = parseResult.GetValue(SkillCommandOptions.Sync.AllOption);
        var path = parseResult.GetValue(SkillCommandOptions.Sync.PathOption);

        DebugLogger.Debug("Parameters", $"Name: {skillName ?? "none"}, All: {all}, Path: {path ?? "default"}");

        // Check if 'download' alias was used and show deprecation warning
        var commandToken = parseResult.Tokens.FirstOrDefault(t => t.Type == System.CommandLine.Parsing.TokenType.Command && t.Value == "download");
        if (commandToken != null)
        {
            ConsoleUI.WriteInfo("⚠️  Warning: 'srectl skill download' is deprecated and will be removed in a future release.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("    Please use 'srectl skill sync' instead.", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        // Check if '--output-path' alias was used and show deprecation warning
        var outputPathToken = parseResult.Tokens.FirstOrDefault(t => t.Type == System.CommandLine.Parsing.TokenType.Option && t.Value == "--output-path");
        if (outputPathToken != null)
        {
            ConsoleUI.WriteInfo("⚠️  Warning: '--output-path' is deprecated and will be removed in a future release.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("    Please use '--path' instead.", ConsoleColor.Yellow);
            Console.WriteLine();
        }

        using var apiService = new ApiService();

        // Fetch skills based on --name or --all and build dictionary of path -> skill
        Dictionary<string, ExtendedSkillV2> skillsToSync = [];

        if (all)
        {
            // Set default base path for --all
            var basePath = string.IsNullOrEmpty(path) ? ExtendedSkillHelper.DefaultSkillFolder : path;

            ProgressService.AnimatedSpinner.Start("Fetching skills from server");
            var (skillsList, error) = await apiService.ListExtendedSkillsAsync(null);
            ProgressService.AnimatedSpinner.Stop();

            if (error != null)
            {
                ConsoleUI.WriteStatus(false, $"Failed to fetch skills: {error}");
                return 1;
            }

            if (skillsList.Count == 0)
            {
                ConsoleUI.WriteInfo("No skills found on the server.", ConsoleColor.Yellow);
                return 1;
            }

            ConsoleUI.WriteSection($"Syncing {skillsList.Count} skill(s) from server");
            Console.WriteLine();

            // Build dictionary with path as key - each skill gets its own subdirectory
            foreach (var skill in skillsList)
            {
                var name = skill.Metadata?.Name;
                if (!string.IsNullOrEmpty(name))
                {
                    var skillPath = Path.Combine(basePath, name);
                    skillsToSync[skillPath] = skill;
                }
            }
        }
        else
        {
            ProgressService.AnimatedSpinner.Start($"Syncing skill '{skillName}'");
            var (skill, error) = await apiService.GetExtendedSkillAsync(skillName!);
            ProgressService.AnimatedSpinner.Stop();

            if (skill == null || error != null)
            {
                ConsoleUI.WriteStatus(false, $"Sync failed: {error}");
                return 1;
            }

            // For --name: if path is provided, use it directly; otherwise use skills/<name>
            string skillPath;
            if (string.IsNullOrEmpty(path))
            {
                skillPath = Path.Combine(ExtendedSkillHelper.DefaultSkillFolder, skill.Metadata?.Name ?? skillName!);
            }
            else
            {
                skillPath = path;
            }

            skillsToSync[skillPath] = skill;
        }

        // Sync all skills
        int successCount = 0;
        int errorCount = 0;

        foreach (var (skillPath, skill) in skillsToSync)
        {
            var name = skill.Metadata?.Name;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            try
            {
                DebugLogger.Debug("SkillSync", $"Saving skill '{name}' to: {skillPath}");
                await skill.SaveAsync(skillPath);

                ConsoleUI.WriteStatus(true, $"Successfully synced skill '{name}' to {skillPath}");
                Console.WriteLine();
                ConsoleUI.WriteSection("Synced Files:");
                ConsoleUI.WriteBullet($"{Path.Combine(skillPath, ExtendedSkillHelper.MetadataFileName)}", ConsoleColor.Green, 3);
                ConsoleUI.WriteBullet($"{Path.Combine(skillPath, ExtendedSkillHelper.SkillContentFileName)}", ConsoleColor.Green, 3);

                if (skill.Spec.AdditionalFiles != null && skill.Spec.AdditionalFiles.Count > 0)
                {
                    foreach (var file in skill.Spec.AdditionalFiles)
                    {
                        if (!string.IsNullOrEmpty(file.FilePath))
                        {
                            ConsoleUI.WriteBullet($"{Path.Combine(skillPath, file.FilePath)}", ConsoleColor.Green, 3);
                        }
                    }
                }

                Console.WriteLine();

                successCount++;
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Failed to sync skill '{name}': {ex.Message}");
                errorCount++;
            }
        }

        // Show summary if syncing multiple skills
        if (all)
        {
            ConsoleUI.WriteSection("Sync Summary");
            ConsoleUI.WriteKeyValue("Total", skillsToSync.Count.ToString());
            ConsoleUI.WriteKeyValue("Success", successCount.ToString(), valueColor: ConsoleColor.Green);
            ConsoleUI.WriteKeyValue("Errors", errorCount.ToString(), valueColor: errorCount > 0 ? ConsoleColor.Red : ConsoleColor.Gray);
        }

        return errorCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Handles the skill delete command.
    /// </summary>
    public static async Task<int> HandleDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting skill delete command");

        var skillName = parseResult.GetValue(SkillCommandOptions.Delete.NameOption);
        var dryRun = parseResult.GetValue(SkillCommandOptions.Delete.DryRunOption);

        DebugLogger.Debug("Parameters", $"SkillName: {skillName}, DryRun: {dryRun}");

        // Null check (validation should have caught this, but be defensive)
        if (string.IsNullOrWhiteSpace(skillName))
        {
            throw new InvalidOperationException("Skill name validation should have been caught by the validator.");
        }

        using var apiService = new ApiService();

        if (dryRun)
        {
            ConsoleUI.WriteInfo($"Validating skill deletion for '{skillName}' (dry run)...", ConsoleColor.Yellow);
        }
        else
        {
            ConsoleUI.WriteInfo($"Deleting skill '{skillName}'...", ConsoleColor.Yellow);
        }

        var (success, message) = await apiService.DeleteExtendedSkillAsync(skillName, dryRun);

        if (success)
        {
            ConsoleUI.WriteStatus(true, message);

            // After successful server deletion (not dry-run), offer to clean up local files
            if (!dryRun)
            {
                OfferLocalSkillCleanup(skillName);
            }

            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, message);
            return 1;
        }
    }

    /// <summary>
    /// Handles the skill create command.
    /// </summary>
    public static async Task<int> HandleCreateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting skill create command");

        var skillName = parseResult.GetValue(SkillCommandOptions.Create.NameOption);
        var description = parseResult.GetValue(SkillCommandOptions.Create.DescriptionOption);

        // Validate skill name
        if (string.IsNullOrWhiteSpace(skillName))
        {
            ConsoleUI.WriteStatus(false, "Skill name is required");
            return 1;
        }

        var outputPath = Path.Combine(ExtendedSkillHelper.DefaultSkillFolder, skillName);

        DebugLogger.Debug("Parameters", $"SkillName: {skillName}, OutputPath: {outputPath}");

        // Check if directory already exists
        if (Directory.Exists(outputPath))
        {
            ConsoleUI.WriteStatus(false, $"Directory already exists: {outputPath}");
            ConsoleUI.WriteInfo("Please choose a different name or remove the existing directory.", ConsoleColor.Yellow);
            return 1;
        }

        // Create directory
        Directory.CreateDirectory(outputPath);
        DebugLogger.Debug("SkillCreate", $"Created directory: {outputPath}");

        // Create ExtendedSkillV2 model for metadata.yaml
        var extendedSkill = ExtendedSkillHelper.CreateSkill(skillName, description);

        // Write metadata.yaml
        var metadataPath = Path.Combine(outputPath, ExtendedSkillHelper.MetadataFileName);
        var metadataYaml = extendedSkill.ToYaml();
        await File.WriteAllTextAsync(metadataPath, metadataYaml, System.Text.Encoding.UTF8, cancellationToken);
        DebugLogger.Debug("SkillCreate", $"Created {ExtendedSkillHelper.MetadataFileName}: {metadataPath}");

        // Create SKILL.md with template
        var skillMdContent = ExtendedSkillHelper.CreateSkillContent(skillName);
        var skillMdPath = Path.Combine(outputPath, ExtendedSkillHelper.SkillContentFileName);
        await File.WriteAllTextAsync(skillMdPath, skillMdContent, System.Text.Encoding.UTF8, cancellationToken);
        DebugLogger.Debug("SkillCreate", $"Created SKILL.md: {skillMdPath}");

        ConsoleUI.WriteStatus(true, $"Successfully created skill '{skillName}' at {outputPath}");
        Console.WriteLine();
        ConsoleUI.WriteSection("Created Files:");
        ConsoleUI.WriteBullet(metadataPath, ConsoleColor.Green, 3);
        ConsoleUI.WriteBullet(skillMdPath, ConsoleColor.Green, 3);

        Console.WriteLine();
        ConsoleUI.WriteSection("Next Steps");
        ConsoleUI.WriteBullet("Edit metadata.yaml", "Update the description and add tools if needed", ConsoleColor.Cyan);
        ConsoleUI.WriteBullet("Edit SKILL.md", "Add skill instructions and capabilities", ConsoleColor.Cyan);
        ConsoleUI.WriteBullet("Add additional files", "Include specialized markdown files and reference them in SKILL.md", ConsoleColor.Cyan);
        ConsoleUI.WriteBullet("Apply the skill", $"srectl skill apply --name {skillName}", ConsoleColor.Cyan);

        return 0;
    }

    /// <summary>
    /// Offers to clean up local skill files after successful server deletion.
    /// </summary>
    private static void OfferLocalSkillCleanup(string skillName)
    {
        var skillDirectory = ExtendedSkillHelper.FindSkillDirectory(skillName);

        if (skillDirectory == null)
        {
            return; // No local files to clean up
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Local File Cleanup");
        ConsoleUI.WriteInfo("Local configuration files still exist:", ConsoleColor.Yellow);
        ConsoleUI.WriteBullet(skillDirectory, ConsoleColor.Gray);
        Console.WriteLine();

        if (ConsoleUI.Confirm("Also delete local configuration files?", false))
        {
            try
            {
                Directory.Delete(skillDirectory, recursive: true);
                DebugLogger.Debug("Cleanup", $"Deleted local skill directory: {skillDirectory}");

                Console.WriteLine();
                ConsoleUI.WriteSection("Summary");
                ConsoleUI.WriteBullet($"Skill '{skillName}' deleted from server", ConsoleColor.Green);
                ConsoleUI.WriteBullet($"Local configuration files deleted: {skillDirectory}", ConsoleColor.Green);
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
            ConsoleUI.WriteBullet($"Skill '{skillName}' deleted from server", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Local configuration files preserved: {skillDirectory}", ConsoleColor.Yellow);

            Console.WriteLine();
            ConsoleUI.WriteInfo($"To reapply: srectl skill apply --name {skillName}", ConsoleColor.Cyan);
            ConsoleUI.WriteInfo($"To delete locally: rm -rf {skillDirectory.Replace('\\', '/')}", ConsoleColor.Gray);
        }
    }

    /// <summary>
    /// Handles the skill migrate command to migrate V1 skills to V2 format.
    /// </summary>
    public static async Task<int> HandleMigrateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting skill migrate command");

        var skillName = parseResult.GetValue(SkillCommandOptions.Migrate.NameOption);
        var migrateAll = parseResult.GetValue(SkillCommandOptions.Migrate.AllOption);
        var dryRun = parseResult.GetValue(SkillCommandOptions.Migrate.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {skillName ?? "none"}, All: {migrateAll}, DryRun: {dryRun}");

        var skillsDirectory = ExtendedSkillHelper.DefaultSkillFolder;

        if (!Directory.Exists(skillsDirectory))
        {
            ConsoleUI.WriteStatus(false, "No skills directory found.");
            return 1;
        }

        List<string> directoriesToMigrate = [];

        if (migrateAll)
        {
            // Find all directories that contain valid skill files
            var allDirs = Directory.GetDirectories(skillsDirectory, "*", SearchOption.AllDirectories);
            foreach (var dir in allDirs)
            {
                if (ExtendedSkillHelper.IsValidSkillDirectory(dir))
                {
                    directoriesToMigrate.Add(dir);
                }
            }

            // Also check root skills directory
            if (ExtendedSkillHelper.IsValidSkillDirectory(skillsDirectory))
            {
                directoriesToMigrate.Add(skillsDirectory);
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(skillName))
            {
                ConsoleUI.WriteStatus(false, "Skill name is required when not using --all");
                return 1;
            }

            var skillDir = ExtendedSkillHelper.FindSkillDirectory(skillName);
            if (skillDir == null)
            {
                ConsoleUI.WriteStatus(false, $"Skill directory not found for '{skillName}'");
                ConsoleUI.WriteInfo($"Expected: skills/{skillName}/ with metadata.yaml and SKILL.md", ConsoleColor.Gray);
                return 1;
            }
            directoriesToMigrate.Add(skillDir);
        }

        if (directoriesToMigrate.Count == 0)
        {
            ConsoleUI.WriteStatus(false, "No skill directories found to migrate.");
            return 1;
        }

        ConsoleUI.WriteSection($"Migrating {directoriesToMigrate.Count} skill(s) from V1 to V2{(dryRun ? " (DRY RUN)" : "")}");
        Console.WriteLine();

        int migratedCount = 0;
        int skippedCount = 0;
        int errorCount = 0;

        foreach (var skillDir in directoriesToMigrate)
        {
            try
            {
                var dirName = Path.GetFileName(skillDir);
                var metadataPath = Path.Combine(skillDir, ExtendedSkillHelper.MetadataFileName);

                if (!File.Exists(metadataPath))
                {
                    ConsoleUI.WriteBullet($"{dirName}: No {ExtendedSkillHelper.MetadataFileName} found", ConsoleColor.Yellow);
                    skippedCount++;
                    continue;
                }

                var content = await File.ReadAllTextAsync(metadataPath);

                // Try to detect if this is V1 or V2
                var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                    .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build();

                var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(content);
                var apiVersion = yamlDict.TryGetValue("api_version", out var apiObj) ? apiObj?.ToString() : null;
                var kind = yamlDict.TryGetValue("kind", out var kindObj) ? kindObj?.ToString() : null;

                // Check if already V2
                if (string.Equals(apiVersion, Models.YamlApiVersion.V2, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(kind, Models.ResourceModel.ResourceKind.SkillV2, StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleUI.WriteBullet($"{dirName}: Already V2 format", ConsoleColor.Gray);
                    skippedCount++;
                    continue;
                }

                // Check if this is V1 format (no api_version/kind or V1)
                bool isV1 = string.IsNullOrEmpty(apiVersion) || string.IsNullOrEmpty(kind) ||
                           string.Equals(apiVersion, Models.YamlApiVersion.V1, StringComparison.OrdinalIgnoreCase);

                if (!isV1)
                {
                    ConsoleUI.WriteBullet($"{dirName}: Unknown format", ConsoleColor.Yellow);
                    skippedCount++;
                    continue;
                }

                // Parse as V1
                var v1Skill = ExtendedSkillV1.ParseYaml(content);
                if (v1Skill == null)
                {
                    ConsoleUI.WriteBullet($"{dirName}: Failed to parse V1 format", ConsoleColor.Red);
                    errorCount++;
                    continue;
                }

                // Load SKILL.md content
                var skillMdPath = Path.Combine(skillDir, ExtendedSkillHelper.SkillContentFileName);
                if (File.Exists(skillMdPath))
                {
                    v1Skill.SkillMdContent = await File.ReadAllTextAsync(skillMdPath);
                }

                // Load additional files content if they exist
                if (v1Skill.AdditionalFiles != null && v1Skill.AdditionalFiles.Count > 0)
                {
                    foreach (var additionalFile in v1Skill.AdditionalFiles)
                    {
                        if (string.IsNullOrWhiteSpace(additionalFile.FilePath))
                        {
                            continue;
                        }

                        var additionalFilePath = Path.Combine(skillDir, additionalFile.FilePath);
                        if (File.Exists(additionalFilePath))
                        {
                            additionalFile.Content = await File.ReadAllTextAsync(additionalFilePath);
                        }
                    }
                }

                // Convert to V2
                var v2Skill = Converters.ExtendedSkillConverter.ConvertToV2(v1Skill);

                if (dryRun)
                {
                    ConsoleUI.WriteBullet($"{dirName}: Would migrate to V2", ConsoleColor.Green);
                }
                else
                {
                    // Save V2 metadata.yaml (overwrites original)
                    await v2Skill.SaveYamlAsync(metadataPath);
                    ConsoleUI.WriteBullet($"{dirName}: Migrated to V2", ConsoleColor.Green);
                }

                migratedCount++;
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteBullet($"{Path.GetFileName(skillDir)}: Error - {ex.Message}", ConsoleColor.Red);
                errorCount++;
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteSection("Migration Summary");
        ConsoleUI.WriteKeyValue("Total Skills", directoriesToMigrate.Count.ToString());
        ConsoleUI.WriteKeyValue("Migrated", migratedCount.ToString(), valueColor: ConsoleColor.Green);
        ConsoleUI.WriteKeyValue("Skipped", skippedCount.ToString(), valueColor: ConsoleColor.Gray);
        ConsoleUI.WriteKeyValue("Errors", errorCount.ToString(), valueColor: errorCount > 0 ? ConsoleColor.Red : ConsoleColor.Gray);

        if (dryRun && migratedCount > 0)
        {
            Console.WriteLine();
            ConsoleUI.WriteInfo("This was a dry run. No files were modified.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Run without --dry-run to apply changes.", ConsoleColor.Yellow);
        }

        if (migratedCount > 0 && !dryRun)
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Next Steps");
            ConsoleUI.WriteBullet("Apply migrated skills", "srectl skill apply --name <skill-name>", ConsoleColor.Cyan);
            ConsoleUI.WriteBullet("Or apply all", "srectl sync", ConsoleColor.Cyan);
        }

        return errorCount > 0 ? 1 : 0;
    }
}
