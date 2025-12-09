// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles extension-related command operations.
/// </summary>
public static class ExtensionCommandHandlers
{
    /// <summary>
    /// Handles the extension generate-ev2 command.
    /// </summary>
    public static async Task HandleGenerateEv2Command(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting extension generate-ev2 command");

        // Extract options
        var toolsFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ToolsFolderOption);
        var agentFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.AgentFolderOption);
        var outputFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.OutputOption);

        DebugLogger.Debug("Parameters", $"Tools Folder: {toolsFolder}, Agent Folder: {agentFolder}, Output: {outputFolder}");

        // Validate required parameters (should already be enforced by Required=true)
        if (string.IsNullOrWhiteSpace(toolsFolder) ||
            string.IsNullOrWhiteSpace(agentFolder) ||
            string.IsNullOrWhiteSpace(outputFolder))
        {
            ConsoleUI.WriteStatus(false, "Error: All parameters (--tools-folder, --agent-folder, --output) are required.");
            Environment.Exit(1);
            return;
        }

        // Validate input folders exist
        if (!Directory.Exists(toolsFolder))
        {
            ConsoleUI.WriteStatus(false, $"Error: Tools folder does not exist: {toolsFolder}");
            Environment.Exit(1);
            return;
        }

        if (!Directory.Exists(agentFolder))
        {
            ConsoleUI.WriteStatus(false, $"Error: Agent folder does not exist: {agentFolder}");
            Environment.Exit(1);
            return;
        }

        // Get the Templates/Ev2 directory path relative to the CLI assembly
        var templatePath = GetTemplatesEv2Path();
        if (string.IsNullOrEmpty(templatePath) || !Directory.Exists(templatePath))
        {
            ConsoleUI.WriteStatus(false, $"Error: Templates/Ev2 directory not found at: {templatePath}");
            Environment.Exit(1);
            return;
        }

        ConsoleUI.WriteSection($"Generating EV2 deployment files");
        ConsoleUI.WriteBullet($"Tools folder: {toolsFolder}");
        ConsoleUI.WriteBullet($"Agent folder: {agentFolder}");
        ConsoleUI.WriteBullet($"Output folder: {outputFolder}");
        DebugLogger.Debug("Template Source", $"Using template path: {templatePath}");

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(outputFolder);

        // Copy all files from Templates/Ev2 to the output folder
        await CopyDirectory(templatePath, outputFolder);

        // Generate the sreagentExtensionFile.bicep dynamically
        await GenerateSreagentExtensionFile(toolsFolder, agentFolder, outputFolder);

        // Generate ARM templates from Bicep files
        await GenerateArmTemplates(outputFolder);

        ConsoleUI.WriteStatus(true, $"Successfully generated EV2 files in {outputFolder}");
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
    /// Generates the sreagentExtensionFile.bicep file based on YAML files in tools and agent folders.
    /// </summary>
    private static async Task GenerateSreagentExtensionFile(string toolsFolder, string agentFolder, string outputFolder)
    {
        DebugLogger.Debug("Extension File Generation", "Starting generation of sreagentExtensionFile.bicep");

        // Get all YAML files from tools and agent folders
        var toolYamlFiles = GetYamlFiles(toolsFolder);
        var agentYamlFiles = GetYamlFiles(agentFolder);

        DebugLogger.Debug("YAML Discovery", $"Found {agentYamlFiles.Count} agent YAML files and {toolYamlFiles.Count} tool YAML files");

        // The bicep file will be at: output/BicepTemplates/modules/sreagentExtensionFile.bicep
        // Calculate relative paths from that location to the YAML files
        var bicepFileDirectory = Path.Combine(outputFolder, "BicepTemplates", "modules");
        var agentRelativePaths = agentYamlFiles.Select(file => GetRelativePath(bicepFileDirectory, file)).ToList();
        var toolRelativePaths = toolYamlFiles.Select(file => GetRelativePath(bicepFileDirectory, file)).ToList();

        // Read the template file and replace placeholders
        var bicepOutputPath = Path.Combine(bicepFileDirectory, "sreagentExtensionFile.bicep");
        var templateContent = await File.ReadAllTextAsync(bicepOutputPath);

        var bicepContent = ReplacePlaceholders(templateContent, agentRelativePaths, toolRelativePaths);

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
    private static string ReplacePlaceholders(string templateContent, List<string> agentPaths, List<string> toolPaths)
    {
        // Build agent YAML files list
        var agentFilesContent = new System.Text.StringBuilder();
        if (agentPaths.Any())
        {
            foreach (var path in agentPaths)
            {
                agentFilesContent.AppendLine($"  loadYamlContent('{path}')");
            }
            // Remove the last newline
            agentFilesContent.Length -= Environment.NewLine.Length;
        }
        else
        {
            agentFilesContent.Append("  // No agent YAML files found");
        }
        templateContent = templateContent.Replace("  // {{AGENT_YAML_FILES}}", agentFilesContent.ToString());

        // Build tool YAML files list
        var toolFilesContent = new System.Text.StringBuilder();
        if (toolPaths.Any())
        {
            foreach (var path in toolPaths)
            {
                toolFilesContent.AppendLine($"  loadYamlContent('{path}')");
            }
            // Remove the last newline
            toolFilesContent.Length -= Environment.NewLine.Length;
        }
        else
        {
            toolFilesContent.Append("  // No tool YAML files found");
        }
        templateContent = templateContent.Replace("  // {{TOOL_YAML_FILES}}", toolFilesContent.ToString());

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
        var scriptPath = Path.Combine(GetTemplatesEv2Path(), "generate-arm-templates.ps1");
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

}
