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
    public static async Task<int> HandleGenerateEv2Command(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting extension generate-ev2 command");

        // Extract required options
        var toolsFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.ToolsFolderOption);
        var agentFolder = parseResult.GetValue(ExtensionCommandOptions.GenerateEv2.AgentFolderOption);
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

        DebugLogger.Debug("Parameters", $"Tools Folder: {toolsFolder}, Agent Folder: {agentFolder}, Output: {outputFolder}");

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

        // Copy and split YAML files to output directory for self-contained artifacts
        var outputAgentsFolder = Path.Combine(outputFolder!, "agents");
        var outputToolsFolder = Path.Combine(outputFolder!, "tools");

        if (Directory.Exists(agentFolder))
        {
            await CopyAndSplitYamlFiles(agentFolder!, outputAgentsFolder);
        }

        if (Directory.Exists(toolsFolder))
        {
            await CopyAndSplitYamlFiles(toolsFolder!, outputToolsFolder);
        }

        // Generate the sreagentExtensionFile.bicep dynamically
        await GenerateSreagentExtensionFile(toolsFolder!, agentFolder!, outputFolder!);

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

            // Remove .template extension if present
            if (fileName.EndsWith(".template", StringComparison.OrdinalIgnoreCase))
            {
                fileName = fileName.Substring(0, fileName.Length - ".template".Length);
            }

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
    /// Copies YAML files and splits multi-document YAML files into individual files.
    /// </summary>
    private static async Task CopyAndSplitYamlFiles(string sourceDir, string destinationDir)
    {
        if (!Directory.Exists(sourceDir))
        {
            return;
        }

        // Create the destination directory if it doesn't exist
        Directory.CreateDirectory(destinationDir);

        // Get all YAML files from source directory
        var yamlFiles = new List<string>();
        yamlFiles.AddRange(Directory.GetFiles(sourceDir, "*.yaml", SearchOption.AllDirectories));
        yamlFiles.AddRange(Directory.GetFiles(sourceDir, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            var content = await File.ReadAllTextAsync(yamlFile);

            // Check if this is a multi-document YAML file (contains document separators)
            var documents = content.Split(new[] { "\n---\n", "\r\n---\r\n", "\n---\r\n", "\r\n---\n" }, StringSplitOptions.RemoveEmptyEntries);

            if (documents.Length > 1)
            {
                // Multi-document YAML - split into individual files
                DebugLogger.Debug("YAML Split", $"Splitting {yamlFile} into {documents.Length} documents");

                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(yamlFile);
                var extension = Path.GetExtension(yamlFile);

                for (int i = 0; i < documents.Length; i++)
                {
                    var document = documents[i].Trim();
                    if (string.IsNullOrWhiteSpace(document))
                    {
                        continue;
                    }

                    // Remove leading --- if present
                    if (document.StartsWith("---"))
                    {
                        document = document.Substring(3).TrimStart();
                    }

                    var outputFileName = $"{fileNameWithoutExtension}-{i + 1}{extension}";
                    var outputFilePath = Path.Combine(destinationDir, outputFileName);

                    await File.WriteAllTextAsync(outputFilePath, document);
                    DebugLogger.Debug("YAML Split", $"Created {outputFileName}");
                }
            }
            else
            {
                // Single document YAML - copy as-is
                var fileName = Path.GetFileName(yamlFile);
                var destinationFile = Path.Combine(destinationDir, fileName);
                File.Copy(yamlFile, destinationFile, overwrite: true);
                DebugLogger.Debug("YAML Copy", $"Copied {fileName}");
            }
        }
    }

    /// <summary>
    /// Generates the sreagentExtensionFile.bicep file based on YAML files in tools and agent folders.
    /// </summary>
    private static async Task GenerateSreagentExtensionFile(string toolsFolder, string agentFolder, string outputFolder)
    {
        DebugLogger.Debug("Extension File Generation", "Starting generation of sreagentExtensionFile.bicep");

        // Get all YAML files from the OUTPUT folders (where they were copied)
        var outputAgentsFolder = Path.Combine(outputFolder, "agents");
        var outputToolsFolder = Path.Combine(outputFolder, "tools");

        var toolYamlFiles = GetYamlFiles(outputToolsFolder);
        var agentYamlFiles = GetYamlFiles(outputAgentsFolder);

        DebugLogger.Debug("YAML Discovery", $"Found {agentYamlFiles.Count} agent YAML files and {toolYamlFiles.Count} tool YAML files");

        // The bicep file will be at: output/BicepTemplates/modules/sreagentExtensionFile.bicep
        // Calculate relative paths from that location to the YAML files in the output directory
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

        // Replace placeholders in bicepparam file
        var bicepParamPath = Path.Combine(outputFolder, "BicepTemplates", "sreagentContainerAppsExtension.bicepparam");
        await ReplaceFilePlaceholders(bicepParamPath, placeholders);

        DebugLogger.Debug("EV2 Artifacts", "EV2 deployment artifacts generated successfully");
    }

}
