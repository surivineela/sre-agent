// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles extension-related command operations.
/// </summary>
public static class ExtensionCommandHandlers
{
    /// <summary>
    /// Bicep file indentation (2 spaces).
    /// </summary>
    private const string BicepIndent = "  ";

    /// <summary>
    /// Holds information about a skill directory and its files.
    /// </summary>
    private class SkillInfo
    {
        public string DirectoryPath { get; set; } = string.Empty;
        public List<string> AdditionalFiles { get; set; } = new List<string>();
    }
    /// <summary>
    /// Handles the extension generate-ev2 command.
    /// </summary>
    public static async Task<int> HandleGenerateEv2Command(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting extension generate-ev2 command");

        // Extract required options
        var toolsFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ToolsFolderOption);
        var agentFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.AgentFolderOption);
        var skillsFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.SkillsFolderOption);
        var scheduledTasksFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ScheduledTasksFolderOption);
        var incidentFilterFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.IncidentFilterFolderOption);
        var outputFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.OutputOption);

        // Extract optional EV2 deployment options
        var serviceIdentifier = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ServiceIdentifierOption);
        var serviceGroup = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ServiceGroupOption);
        var environment = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.EnvironmentOption);
        var tenantId = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.TenantIdOption);
        var subscriptionKey = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.SubscriptionKeyOption);
        var subscriptionId = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.SubscriptionIdOption);
        var resourceGroup = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ResourceGroupOption);
        var agentName = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.AgentNameOption);

        DebugLogger.Debug("Parameters", $"Tools Folder: {toolsFolder}, Agent Folder: {agentFolder}, Skill Folder: {skillsFolder}, Scheduled Tasks Folder: {scheduledTasksFolder}, Incident Filter Folder: {incidentFilterFolder}, Output: {outputFolder}");

        // Determine if EV2 deployment artifacts should be generated
        var generateEv2Artifacts = !string.IsNullOrWhiteSpace(serviceIdentifier) &&
                                   !string.IsNullOrWhiteSpace(serviceGroup) &&
                                   !string.IsNullOrWhiteSpace(environment) &&
                                   !string.IsNullOrWhiteSpace(tenantId) &&
                                   !string.IsNullOrWhiteSpace(subscriptionKey) &&
                                   !string.IsNullOrWhiteSpace(subscriptionId) &&
                                   !string.IsNullOrWhiteSpace(resourceGroup) &&
                                   !string.IsNullOrWhiteSpace(agentName);

        // Validate required input folders exist
        if (!Directory.Exists(toolsFolder))
        {
            ConsoleUI.WriteStatus(false, $"Tools folder does not exist: {toolsFolder}");
            return 1;
        }

        if (!Directory.Exists(agentFolder))
        {
            ConsoleUI.WriteStatus(false, $"Agent folder does not exist: {agentFolder}");
            return 1;
        }

        // Get the Templates/Ev2 directory path relative to the CLI assembly
        var templatePath = GetTemplatesEv2Path();
        if (string.IsNullOrEmpty(templatePath) || !Directory.Exists(templatePath))
        {
            ConsoleUI.WriteStatus(false, $"Templates/Ev2 directory not found at: {templatePath}");
            return 1;
        }

        ConsoleUI.WriteSection("Generating EV2 deployment files");
        ConsoleUI.WriteBullet($"Tools folder: {toolsFolder}");
        ConsoleUI.WriteBullet($"Agent folder: {agentFolder}");
        if (!string.IsNullOrWhiteSpace(skillsFolder))
        {
            ConsoleUI.WriteBullet($"Skills folder: {skillsFolder}");
        }
        if (!string.IsNullOrWhiteSpace(scheduledTasksFolder))
        {
            ConsoleUI.WriteBullet($"Scheduled Tasks folder: {scheduledTasksFolder}");
        }
        if (!string.IsNullOrWhiteSpace(incidentFilterFolder))
        {
            ConsoleUI.WriteBullet($"Incident Filter folder: {incidentFilterFolder}");
        }
        ConsoleUI.WriteBullet($"Output folder: {outputFolder}");
        if (generateEv2Artifacts)
        {
            ConsoleUI.WriteBullet("EV2 artifacts: enabled");
        }
        DebugLogger.Debug("Template Source", $"Using template path: {templatePath}");

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(outputFolder!);

        // Copy files from Templates/Ev2 to the output folder
        if (generateEv2Artifacts)
        {
            // Copy everything from Templates/Ev2
            await CopyDirectory(templatePath, outputFolder!);
        }
        else
        {
            // Only copy BicepTemplates folder
            var bicepTemplatesSource = Path.Combine(templatePath, "BicepTemplates");
            var bicepTemplatesDestination = Path.Combine(outputFolder!, "BicepTemplates");

            if (Directory.Exists(bicepTemplatesSource))
            {
                await CopyDirectory(bicepTemplatesSource, bicepTemplatesDestination);
            }
        }

        // Generate the sreagentExtensionFile.bicep dynamically
        await GenerateSreagentExtensionFile(toolsFolder!, agentFolder!, outputFolder!, skillsFolder, scheduledTasksFolder, incidentFilterFolder);

        // Generate ARM templates from Bicep files
        await GenerateArmTemplates(outputFolder!);

        // Generate EV2 artifacts only if all EV2 options were provided
        if (generateEv2Artifacts)
        {
            await GenerateEv2Artifacts(outputFolder!, serviceIdentifier!, serviceGroup!, environment!, tenantId!, subscriptionKey!, subscriptionId!, resourceGroup!, agentName!);
        }

        ConsoleUI.WriteStatus(true, $"Successfully generated EV2 files in {outputFolder}");
        return 0;
    }

    /// <summary>
    /// Gets the path to the Templates/Ev2 directory.
    /// </summary>
    private static string GetTemplatesEv2Path()
    {
        try
        {
            // Get the application base directory (works for single-file apps)
            var assemblyDirectory = AppContext.BaseDirectory;

            // Look for Templates/Ev2 relative to the assembly location
            var templatesPath = Path.Combine(assemblyDirectory, "Templates", "Ev2");

            if (Directory.Exists(templatesPath))
            {
                return templatesPath;
            }

            // If not found, try looking relative to the project structure (for development scenarios)
            var currentDirectory = Directory.GetCurrentDirectory();
            var projectRelativePath = Path.Combine(currentDirectory, "Templates", "Ev2");

            if (Directory.Exists(projectRelativePath))
            {
                return projectRelativePath;
            }

            // Try one more location - relative to the source code structure
            var sourceRelativePath = Path.Combine(assemblyDirectory, "..", "..", "..", "Templates", "Ev2");
            var normalizedPath = Path.GetFullPath(sourceRelativePath);

            if (Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Recursively copies a directory and all its contents.
    /// </summary>
    private static async Task CopyDirectory(string sourceDir, string destinationDir)
    {
        // Create the destination directory if it doesn't exist
        Directory.CreateDirectory(destinationDir);

        // Copy all files in the current directory
        var files = Directory.GetFiles(sourceDir);
        foreach (var file in files)
        {
            var fileName = Path.GetFileName(file);

            var destinationFile = Path.Combine(destinationDir, fileName);

            DebugLogger.Debug("File Copy", $"Copying {file} to {destinationFile}");
            File.Copy(file, destinationFile, overwrite: true);
        }

        // Recursively copy all subdirectories
        var subdirectories = Directory.GetDirectories(sourceDir);
        foreach (var subdirectory in subdirectories)
        {
            var subdirectoryName = Path.GetFileName(subdirectory);
            var destinationSubdirectory = Path.Combine(destinationDir, subdirectoryName);

            await CopyDirectory(subdirectory, destinationSubdirectory);
        }
    }

    /// <summary>
    /// Generates the sreagentExtensionFile.bicep file based on YAML files in tools, agent, and skill folders.
    /// </summary>
    private static async Task GenerateSreagentExtensionFile(string toolsFolder, string agentFolder, string outputFolder, string? skillFolder, string? scheduledTaskFolder, string? incidentFilterFolder)
    {
        DebugLogger.Debug("Extension File Generation", "Starting generation of sreagentExtensionFile.bicep");

        // Get all YAML files from tools and agent folders
        var allToolYamlFiles = GetYamlFiles(toolsFolder);
        var allAgentYamlFiles = GetYamlFiles(agentFolder);
        var allScheduledTaskYamlFiles = scheduledTaskFolder != null ? GetYamlFiles(scheduledTaskFolder) : new List<string>();
        var allIncidentFilterYamlFiles = incidentFilterFolder != null ? GetYamlFiles(incidentFilterFolder) : new List<string>();

        DebugLogger.Debug("YAML Discovery", $"Found {allAgentYamlFiles.Count} agent YAML files and {allToolYamlFiles.Count} tool YAML files and {allScheduledTaskYamlFiles.Count} scheduledtask YAML files and {allIncidentFilterYamlFiles.Count} incident filter YAML files.");

        // Validate and filter tool YAML files
        var toolYamlFiles = new List<string>();
        foreach (var toolFile in allToolYamlFiles)
        {
            var version = ExtendedToolHelper.DetectVersion(toolFile);
            if (version == null)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(toolFile)}' - not a valid tool file or unsupported version", ConsoleColor.Yellow);
                DebugLogger.Debug("YAML Validation", $"Skipped tool file: {toolFile} - invalid or unsupported version");
                continue;
            }

            if (version != YamlApiVersion.V2)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(toolFile)}' - only V2 format is supported for EV2 extensions", ConsoleColor.Yellow);
                DebugLogger.Debug("YAML Validation", $"Skipped tool file: {toolFile} - version {version} not supported (V2 required)");
                continue;
            }

            toolYamlFiles.Add(toolFile);
            DebugLogger.Debug("YAML Validation", $"Validated tool file: {toolFile} - version {version}");
        }

        // Validate and filter agent YAML files
        var agentYamlFiles = new List<string>();
        foreach (var agentFile in allAgentYamlFiles)
        {
            try
            {
                var yamlContent = await File.ReadAllTextAsync(agentFile);
                var version = ExtendedAgentHelper.DetectVersion(yamlContent);
                if (version == null)
                {
                    ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(agentFile)}' - not a valid agent file or unsupported version", ConsoleColor.Yellow);
                    DebugLogger.Debug("YAML Validation", $"Skipped agent file: {agentFile} - invalid or unsupported version");
                    continue;
                }

                if (version != YamlApiVersion.V2)
                {
                    ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(agentFile)}' - only V2 format is supported for EV2 extensions", ConsoleColor.Yellow);
                    DebugLogger.Debug("YAML Validation", $"Skipped agent file: {agentFile} - version {version} not supported (V2 required)");
                    continue;
                }

                agentYamlFiles.Add(agentFile);
                DebugLogger.Debug("YAML Validation", $"Validated agent file: {agentFile} - version {version}");
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(agentFile)}' - failed to read file: {ex.Message}", ConsoleColor.Yellow);
                DebugLogger.Debug("YAML Validation", $"Skipped agent file: {agentFile} - read error: {ex.Message}");
            }
        }

        // get and validate scheduled task YAML files
        var scheduledTaskFiles = new List<string>();
        foreach (var taskFile in allScheduledTaskYamlFiles)
        {
            try
            {
                var yamlContent = await File.ReadAllTextAsync(taskFile);
                // var version = ExtendedAgentHelper.DetectVersion(yamlContent);
                // TODO: validate and use V2 version
                scheduledTaskFiles.Add(taskFile);
                DebugLogger.Debug("YAML Validation", $"Validated task file: {taskFile}");
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(taskFile)}' - failed to read file: {ex.Message}", ConsoleColor.Yellow);
                DebugLogger.Debug("YAML Validation", $"Skipped task file: {taskFile} - read error: {ex.Message}");
            }
        }

        // get and validate incident filter YAML files
        var incidentFilterFiles = new List<string>();
        foreach (var filterFile in allIncidentFilterYamlFiles)
        {
            try
            {
                var yamlContent = await File.ReadAllTextAsync(filterFile);
                var version = IncidentFilterHelper.DetectVersion(yamlContent);
                if (version == null)
                {
                    ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(filterFile)}' - not a valid filter file or unsupported version", ConsoleColor.Yellow);
                    DebugLogger.Debug("YAML Validation", $"Skipped filter file: {filterFile} - invalid or unsupported version");
                    continue;
                }

                if (version != YamlApiVersion.V2)
                {
                    ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(filterFile)}' - only V2 format is supported for EV2 extensions", ConsoleColor.Yellow);
                    DebugLogger.Debug("YAML Validation", $"Skipped filter file: {filterFile} - version {version} not supported (V2 required)");
                    continue;
                }
                incidentFilterFiles.Add(filterFile);
                DebugLogger.Debug("YAML Validation", $"Validated incident filter file: {filterFile}");
            }
            catch (Exception ex)
            {
                ConsoleUI.WriteStatus(false, $"Warning: Skipping '{Path.GetFileName(filterFile)}' - failed to read file: {ex.Message}", ConsoleColor.Yellow);
                DebugLogger.Debug("YAML Validation", $"Skipped filter file: {filterFile} - read error: {ex.Message}");
            }
        }

        // Validate and discover skill directories with their files
        var skillInfoList = new List<SkillInfo>();
        DebugLogger.Debug("YAML Validation", $"Validating skill files in {skillFolder}, {string.IsNullOrWhiteSpace(skillFolder)}, {Directory.Exists(skillFolder)}");
        if (!string.IsNullOrWhiteSpace(skillFolder) && Directory.Exists(skillFolder))
        {
            // Get all subdirectories in the skills folder
            var allDirectories = Directory.GetDirectories(skillFolder, "*", SearchOption.AllDirectories);
            DebugLogger.Debug("YAML Validation", $"Validating skill files in {allDirectories}");
            foreach (var dir in allDirectories)
            {
                var metadataPath = Path.Combine(dir, "metadata.yaml");
                var skillMdPath = Path.Combine(dir, "SKILL.md");

                DebugLogger.Debug("YAML Validation", $"{metadataPath}, {skillMdPath}");

                if (File.Exists(metadataPath) && File.Exists(skillMdPath))
                {
                    // Get all files in the skill directory
                    var allFiles = Directory.GetFiles(dir, "*", SearchOption.TopDirectoryOnly);

                    // Filter out metadata.yaml and SKILL.md to get additional files
                    var additionalFiles = allFiles
                        .Where(f =>
                        {
                            var fileName = Path.GetFileName(f);
                            return !fileName.Equals("metadata.yaml", StringComparison.OrdinalIgnoreCase) &&
                                   !fileName.Equals("SKILL.md", StringComparison.OrdinalIgnoreCase);
                        })
                        .ToList();

                    skillInfoList.Add(new SkillInfo
                    {
                        DirectoryPath = dir,
                        AdditionalFiles = additionalFiles
                    });

                    DebugLogger.Debug("Skill Discovery", $"Found skill directory: {dir} with {additionalFiles.Count} additional files");
                }
            }
        }
        DebugLogger.Debug("YAML Validation", $"Validated {agentYamlFiles.Count} agent YAML files, {toolYamlFiles.Count} tool YAML files, {scheduledTaskFiles.Count} scheduled task YAML files, {incidentFilterFiles.Count} incident filter YAML files, and {skillInfoList.Count} skill directories");

        // The bicep file will be at: output/BicepTemplates/modules/sreagentExtensionFile.bicep
        // Calculate relative paths from that location to the YAML files
        var bicepFileDirectory = Path.Combine(outputFolder, "BicepTemplates", "modules");
        var agentRelativePaths = agentYamlFiles.Select(file => GetRelativePath(bicepFileDirectory, file)).ToList();
        var toolRelativePaths = toolYamlFiles.Select(file => GetRelativePath(bicepFileDirectory, file)).ToList();
        var scheduledTaskRelativePaths = scheduledTaskFiles.Select(file => GetRelativePath(bicepFileDirectory, file)).ToList();
        var incidentFilterRelativePaths = incidentFilterFiles.Select(file => GetRelativePath(bicepFileDirectory, file)).ToList();

        // Read the template file and replace placeholders
        var bicepOutputPath = Path.Combine(bicepFileDirectory, "sreagentExtensionFile.bicep");
        var templateContent = await File.ReadAllTextAsync(bicepOutputPath);

        var bicepContent = ReplacePlaceholders(templateContent, agentRelativePaths, toolRelativePaths, skillInfoList, scheduledTaskRelativePaths, incidentFilterRelativePaths, bicepFileDirectory);

        // Write the updated content back
        await File.WriteAllTextAsync(bicepOutputPath, bicepContent);
        DebugLogger.Debug("Extension File Generation", $"Generated sreagentExtensionFile.bicep at {bicepOutputPath}");
    }

    /// <summary>
    /// Gets all YAML files recursively from a directory.
    /// </summary>
    private static List<string> GetYamlFiles(string directory)
    {
        var yamlFiles = new List<string>();

        if (!Directory.Exists(directory))
        {
            DebugLogger.Debug("YAML Discovery", $"Error reading directory {directory}: Directory does not exist");
            return yamlFiles;
        }

        try
        {
            // Search for .yaml and .yml files recursively
            yamlFiles.AddRange(Directory.GetFiles(directory, "*.yaml", SearchOption.AllDirectories));
            yamlFiles.AddRange(Directory.GetFiles(directory, "*.yml", SearchOption.AllDirectories));
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("YAML Discovery", $"Error reading directory {directory}: {ex.Message}");
        }

        return yamlFiles;
    }

    /// <summary>
    /// Calculates the relative path from one directory to another file.
    /// </summary>
    private static string GetRelativePath(string fromDirectory, string toFile)
    {
        var fromUri = new Uri(Path.GetFullPath(fromDirectory) + Path.DirectorySeparatorChar);
        var toUri = new Uri(Path.GetFullPath(toFile));
        var relativeUri = fromUri.MakeRelativeUri(toUri);
        var relativePath = Uri.UnescapeDataString(relativeUri.ToString());

        // Convert to forward slashes for Bicep compatibility
        return relativePath.Replace('\\', '/');
    }

    /// <summary>
    /// Replaces placeholders in the template with actual YAML file references.
    /// </summary>
    private static string ReplacePlaceholders(string templateContent, List<string> agentPaths, List<string> toolPaths, List<SkillInfo> skillInfoList, List<string> scheduledTaskPaths, List<string> incidentFilterPaths, string bicepFileDirectory)
    {
        // Build agent YAML files list
        var agentFilesContent = new System.Text.StringBuilder();
        if (agentPaths.Any())
        {
            foreach (var path in agentPaths)
            {
                agentFilesContent.AppendLine($"{BicepIndent}loadYamlContent('{path}')");
            }
            // Remove the last newline
            agentFilesContent.Length -= Environment.NewLine.Length;
        }
        else
        {
            agentFilesContent.Append($"{BicepIndent}// No agent YAML files found");
        }
        templateContent = templateContent.Replace($"{BicepIndent}// {{{{AGENT_YAML_FILES}}}}", agentFilesContent.ToString());

        // Build tool YAML files list
        var toolFilesContent = new System.Text.StringBuilder();
        if (toolPaths.Any())
        {
            foreach (var path in toolPaths)
            {
                toolFilesContent.AppendLine($"{BicepIndent}loadYamlContent('{path}')");
            }
            // Remove the last newline
            toolFilesContent.Length -= Environment.NewLine.Length;
        }
        else
        {
            toolFilesContent.Append($"{BicepIndent}// No tool YAML files found");
        }
        templateContent = templateContent.Replace($"{BicepIndent}// {{{{TOOL_YAML_FILES}}}}", toolFilesContent.ToString());


        // Build scheduled task YAML files list
        var taskFileContent = new System.Text.StringBuilder();
        if (scheduledTaskPaths.Any())
        {
            foreach (var path in scheduledTaskPaths)
            {
                taskFileContent.AppendLine($"{BicepIndent}loadYamlContent('{path}')");
            }
            // Remove the last newline
            taskFileContent.Length -= Environment.NewLine.Length;
        }
        else
        {
            taskFileContent.Append($"{BicepIndent}// No scheduled task YAML files found");
        }
        templateContent = templateContent.Replace($"{BicepIndent}// {{{{SCHEDULED_TASK_YAML_FILES}}}}", taskFileContent.ToString());

        // Build incident filter YAML files list
        var incidentFilterFileContent = new System.Text.StringBuilder();
        if (incidentFilterPaths.Any())
        {
            foreach (var path in incidentFilterPaths)
            {
                incidentFilterFileContent.AppendLine($"{BicepIndent}loadYamlContent('{path}')");
            }
            // Remove the last newline
            incidentFilterFileContent.Length -= Environment.NewLine.Length;
        }
        else
        {
            incidentFilterFileContent.Append($"{BicepIndent}// No incident filter YAML files found");
        }
        templateContent = templateContent.Replace($"{BicepIndent}// {{{{INCIDENT_FILTER_YAML_FILES}}}}", incidentFilterFileContent.ToString());

        // Build skill entries
        var skillFilesContent = new System.Text.StringBuilder();
        if (skillInfoList.Any())
        {
            for (int i = 0; i < skillInfoList.Count; i++)
            {
                var skillInfo = skillInfoList[i];
                var skillDirRelativePath = GetRelativePath(bicepFileDirectory, skillInfo.DirectoryPath);

                skillFilesContent.AppendLine($"{BicepIndent}{{");
                skillFilesContent.AppendLine($"{BicepIndent}{BicepIndent}metadata: loadYamlContent('{skillDirRelativePath}/metadata.yaml')");
                skillFilesContent.AppendLine($"{BicepIndent}{BicepIndent}skillContent: loadTextContent('{skillDirRelativePath}/SKILL.md')");
                skillFilesContent.AppendLine($"{BicepIndent}{BicepIndent}additionalFiles: [");

                // Add additional files
                if (skillInfo.AdditionalFiles.Any())
                {
                    for (int j = 0; j < skillInfo.AdditionalFiles.Count; j++)
                    {
                        var additionalFile = skillInfo.AdditionalFiles[j];
                        var fileName = Path.GetFileName(additionalFile);
                        var fileRelativePath = GetRelativePath(bicepFileDirectory, additionalFile);

                        skillFilesContent.AppendLine($"{BicepIndent}{BicepIndent}{BicepIndent}{{");
                        skillFilesContent.AppendLine($"{BicepIndent}{BicepIndent}{BicepIndent}{BicepIndent}fileName: '{fileName}'");
                        skillFilesContent.Append($"{BicepIndent}{BicepIndent}{BicepIndent}{BicepIndent}content: loadTextContent('{fileRelativePath}')");
                        skillFilesContent.AppendLine();
                        skillFilesContent.Append($"{BicepIndent}{BicepIndent}{BicepIndent}}}");

                        // Add comma if not the last additional file
                        if (j < skillInfo.AdditionalFiles.Count - 1)
                        {
                            skillFilesContent.AppendLine();
                        }
                    }
                    skillFilesContent.AppendLine();
                }

                skillFilesContent.Append($"{BicepIndent}{BicepIndent}]");
                skillFilesContent.AppendLine();
                skillFilesContent.Append($"{BicepIndent}}}");

                // Add comma if not the last skill
                if (i < skillInfoList.Count - 1)
                {
                    skillFilesContent.AppendLine();
                }
            }
        }
        else
        {
            skillFilesContent.Append($"{BicepIndent}// No skill directories found");
        }
        templateContent = templateContent.Replace($"{BicepIndent}// {{{{SKILL_YAML_FILES}}}}", skillFilesContent.ToString());
        return templateContent;
    }

    /// <summary>
    /// Generates ARM templates from Bicep files by calling PowerShell script.
    /// </summary>
    private static async Task GenerateArmTemplates(string outputFolder)
    {
        DebugLogger.Debug("ARM Generation", "Starting ARM template generation from Bicep files");

        // Convert to absolute paths
        var absoluteOutputFolder = Path.GetFullPath(outputFolder);
        var bicepTemplatesPath = Path.Combine(absoluteOutputFolder, "BicepTemplates");
        var armTemplatesPath = Path.Combine(absoluteOutputFolder, "ArmTemplates");

        if (!Directory.Exists(bicepTemplatesPath))
        {
            DebugLogger.Debug("ARM Generation", "BicepTemplates folder not found, skipping ARM generation");
            return;
        }

        // Get the PowerShell script path
        var scriptPath = Path.Combine(GetTemplatesEv2Path(), "ConvertTo-ArmTemplate.ps1");
        if (!File.Exists(scriptPath))
        {
            DebugLogger.Debug("ARM Generation", $"PowerShell script not found at {scriptPath}");
            return;
        }

        DebugLogger.Debug("ARM Generation", $"Executing PowerShell script: {scriptPath}");

        var scriptArgs = $"-BicepTemplatesPath \"{bicepTemplatesPath}\" -ArmTemplatesPath \"{armTemplatesPath}\"";
        var (exitCode, output, error) = await ProcessHelper.ExecutePowerShellScriptAsync(
            scriptPath,
            scriptArgs,
            absoluteOutputFolder);

        if (!string.IsNullOrWhiteSpace(output))
        {
            DebugLogger.Debug("ARM Generation", output);
        }

        if (exitCode != 0)
        {
            DebugLogger.Debug("ARM Generation", $"Script exited with code {exitCode}");
            if (!string.IsNullOrWhiteSpace(error))
            {
                DebugLogger.Debug("ARM Generation", $"Error: {error}");
            }
        }
        else
        {
            DebugLogger.Debug("ARM Generation", "ARM templates generated successfully");
        }
    }

    /// <summary>
    /// Replaces placeholders in a file with provided values.
    /// </summary>
    /// <param name="filePath">Path to the file to process</param>
    /// <param name="placeholders">Dictionary of placeholder keys and their replacement values</param>
    private static async Task ReplaceFilePlaceholders(string filePath, Dictionary<string, string> placeholders)
    {
        if (!File.Exists(filePath))
        {
            DebugLogger.Debug("Placeholder Replacement", $"File not found: {filePath}");
            return;
        }

        var content = await File.ReadAllTextAsync(filePath);

        foreach (var placeholder in placeholders)
        {
            var placeholderPattern = "{{" + placeholder.Key + "}}";
            content = content.Replace(placeholderPattern, placeholder.Value);
        }

        await File.WriteAllTextAsync(filePath, content);
        DebugLogger.Debug("Placeholder Replacement", $"Updated: {filePath}");
    }

    /// <summary>
    /// Generates EV2 deployment artifacts (serviceModel.json, rolloutSpec.json, Deploy-Extension.ps1).
    /// </summary>
    private static async Task GenerateEv2Artifacts(
        string outputFolder,
        string serviceIdentifier,
        string serviceGroup,
        string environment,
        string tenantId,
        string subscriptionKey,
        string subscriptionId,
        string resourceGroup,
        string agentName)
    {
        DebugLogger.Debug("EV2 Artifacts", "Starting generation of EV2 deployment artifacts");

        // Build placeholder dictionary with all values
        var placeholders = new Dictionary<string, string>
        {
            { "SERVICE_IDENTIFIER", serviceIdentifier },
            { "SERVICE_GROUP", serviceGroup },
            { "ENVIRONMENT", environment },
            { "TENANT_ID", tenantId },
            { "SUBSCRIPTION_KEY", subscriptionKey },
            { "SUBSCRIPTION_ID", subscriptionId },
            { "RESOURCE_GROUP", resourceGroup },
            { "AGENT_NAME", agentName }
        };

        // Replace placeholders in serviceModel.json
        var serviceModelPath = Path.Combine(outputFolder, "serviceModel.json");
        await ReplaceFilePlaceholders(serviceModelPath, placeholders);

        // Replace placeholders in serviceGroupSpecification.json
        var serviceGroupSpecPath = Path.Combine(outputFolder, "serviceGroupSpecification.json");
        await ReplaceFilePlaceholders(serviceGroupSpecPath, placeholders);

        // Replace placeholders in configurationSettings.jsonc
        var configSettingsPath = Path.Combine(outputFolder, "configurationSettings.jsonc");
        await ReplaceFilePlaceholders(configSettingsPath, placeholders);

        DebugLogger.Debug("EV2 Artifacts", "EV2 deployment artifacts generated successfully");
    }
}
