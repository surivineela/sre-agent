// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles the apply-yaml command operations.
/// Supports multi-document YAML files (separated by ---) similar to Kubernetes manifests.
/// </summary>
public static class ApplyYamlCommandHandlers
{
    /// <summary>
    /// Handles the apply-yaml command.
    /// </summary>
    public static async Task<int> HandleCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting apply-yaml command");

        var filePath = parseResult.GetValue(ApplyYamlCommandOptions.FileOption);

        DebugLogger.Debug("Parameters", $"FilePath: {filePath}");

        if (!File.Exists(filePath))
        {
            ConsoleUI.WriteStatus(false, $"File not found: {filePath}");
            return 1;
        }

        try
        {
            var fileContent = await File.ReadAllTextAsync(filePath);

            // Split YAML documents by '---' separator (like Kubernetes manifests)
            var yamlDocuments = SplitYamlDocuments(fileContent);

            DebugLogger.Debug("YAML Processing", $"Found {yamlDocuments.Count} YAML document(s) in file");

            if (yamlDocuments.Count == 0)
            {
                ConsoleUI.WriteStatus(false, "No valid YAML documents found in file");
                return 1;
            }

            using var apiService = new ApiService();
            var overallSuccess = true;
            var appliedCount = 0;
            var failedCount = 0;
            var isSingleDocument = yamlDocuments.Count == 1;

            for (var i = 0; i < yamlDocuments.Count; i++)
            {
                var yamlDocument = yamlDocuments[i];
                var documentNumber = i + 1;

                // Only show document section header for multi-document files
                if (!isSingleDocument)
                {
                    Console.WriteLine();
                    ConsoleUI.WriteSection($"Document {documentNumber}/{yamlDocuments.Count}", ConsoleColor.Cyan, topMargin: false, bottomMargin: false);
                    Console.WriteLine();
                }

                var (success, message) = await ProcessYamlDocumentAsync(apiService, yamlDocument, documentNumber, filePath);

                if (success)
                {
                    appliedCount++;
                    ConsoleUI.WriteStatus(true, message, ConsoleColor.Green);
                }
                else
                {
                    failedCount++;
                    overallSuccess = false;
                    ConsoleUI.WriteStatus(false, message, ConsoleColor.Red);
                }
            }

            // Only show summary for multi-document files
            if (!isSingleDocument)
            {
                Console.WriteLine();
                ConsoleUI.DrawLine(60, ConsoleColor.Gray);
                ConsoleUI.WriteInfo($"Summary: {appliedCount} succeeded, {failedCount} failed out of {yamlDocuments.Count} total", ConsoleColor.Cyan);
            }

            return overallSuccess ? 0 : 1;
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"apply-yaml command failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to apply YAML file: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Splits a YAML file content into individual documents using '---' separator.
    /// </summary>
    private static List<string> SplitYamlDocuments(string fileContent)
    {
        var documents = new List<string>();
        var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var currentDocument = new List<string>();

        foreach (var line in lines)
        {
            // YAML document separator
            if (line.Trim() == "---")
            {
                // Save previous document if it has content
                if (currentDocument.Count > 0)
                {
                    var documentContent = string.Join(Environment.NewLine, currentDocument).Trim();
                    if (!string.IsNullOrWhiteSpace(documentContent))
                    {
                        documents.Add(documentContent);
                    }
                    currentDocument.Clear();
                }
                continue;
            }

            currentDocument.Add(line);
        }

        // Add the last document
        if (currentDocument.Count > 0)
        {
            var documentContent = string.Join(Environment.NewLine, currentDocument).Trim();
            if (!string.IsNullOrWhiteSpace(documentContent))
            {
                documents.Add(documentContent);
            }
        }

        return documents;
    }

    /// <summary>
    /// Processes a single YAML document and applies it to the server.
    /// </summary>
    private static async Task<(bool Success, string Message)> ProcessYamlDocumentAsync(
        ApiService apiService,
        string yamlContent,
        int documentNumber,
        string yamlFilePath)
    {
        try
        {
            // Try to deserialize as ResourceModel to detect the resource type
            var deserializer = ResourceModel.GetDeserializerBuilder().Build();
            var resourceModel = deserializer.Deserialize<ResourceModel>(yamlContent);

            if (resourceModel == null)
            {
                return (false, $"Failed to parse YAML document {documentNumber} - invalid format");
            }

            var apiVersion = YamlApiVersion.Parse(resourceModel.ApiVersion);
            var kind = resourceModel.Kind;

            DebugLogger.Debug("Resource Detection", $"Document {documentNumber}: Kind='{kind}', ApiVersion='{resourceModel.ApiVersion}'");

            // Check for V2 ExtendedAgentTool
            if (string.Equals(kind, ResourceModel.ResourceKind.ExtendedAgentToolV2, StringComparison.OrdinalIgnoreCase))
            {
                if (apiVersion == YamlApiVersion.V2)
                {
                    var tool = ExtendedToolV2.ParseYaml(yamlContent);
                    if (tool == null)
                    {
                        return (false, $"Failed to parse tool YAML (kind: {kind})");
                    }

                    var (success, message) = await apiService.ApplyExtendedToolAsync(tool, dryRun: false);
                    return (success, message);
                }
                else
                {
                    return (false, $"Unsupported API version '{resourceModel.ApiVersion}' for tool '{kind}'. Expected '{YamlApiVersion.V2}'. Please migrate to V2 format.");
                }
            }

            // Check for V2 ExtendedAgent
            if (string.Equals(kind, ResourceModel.ResourceKind.ExtendedAgentV2, StringComparison.OrdinalIgnoreCase))
            {
                if (apiVersion == YamlApiVersion.V2)
                {
                    var agent = ExtendedAgentV2.ParseYaml(yamlContent);
                    if (agent == null)
                    {
                        return (false, $"Failed to parse agent YAML (kind: {kind})");
                    }

                    var (success, message) = await apiService.ApplyExtendedAgentAsync(agent, dryRun: false);
                    return (success, message);
                }
                else
                {
                    return (false, $"Unsupported API version '{resourceModel.ApiVersion}' for agent '{kind}'. Expected '{YamlApiVersion.V2}'. Please migrate to V2 format.");
                }
            }

            // Check for V2 CommonPrompt
            if (string.Equals(kind, ResourceModel.ResourceKind.CommonPromptV2, StringComparison.OrdinalIgnoreCase))
            {
                if (apiVersion == YamlApiVersion.V2)
                {
                    var prompt = CommonPromptV2.ParseYaml(yamlContent);
                    if (prompt == null)
                    {
                        return (false, $"Failed to parse common prompt YAML (kind: {kind})");
                    }

                    var (success, message) = await apiService.ApplyCommonPromptAsync(prompt, dryRun: false);
                    return (success, message);
                }
                else
                {
                    return (false, $"Unsupported API version '{resourceModel.ApiVersion}' for common prompt '{kind}'. Expected '{YamlApiVersion.V2}'. Please migrate to V2 format.");
                }
            }

            // Check for V2 Skill
            if (string.Equals(kind, ResourceModel.ResourceKind.SkillV2, StringComparison.OrdinalIgnoreCase))
            {
                if (apiVersion == YamlApiVersion.V2)
                {
                    // For skills, we need to load from the actual file path because skills require
                    // loading SKILL.md and additional files from the skill directory
                    var yamlDirectory = Path.GetDirectoryName(yamlFilePath);
                    if (string.IsNullOrEmpty(yamlDirectory))
                    {
                        return (false, "Failed to determine directory from YAML file path");
                    }

                    // Determine the metadata.yaml path:
                    // If the YAML file is named metadata.yaml, use it directly
                    // Otherwise, assume it's in the current directory
                    string metadataPath;
                    if (Path.GetFileName(yamlFilePath).Equals("metadata.yaml", StringComparison.OrdinalIgnoreCase))
                    {
                        metadataPath = yamlFilePath;
                    }
                    else
                    {
                        // For multi-document YAML files, we cannot determine which skill directory to use
                        return (false, "Skill resources in multi-document YAML files are not supported. Please use separate metadata.yaml files or use 'srectl skill apply' command.");
                    }

                    var (skill, error) = await ExtendedSkillV2.LoadYamlAsync(metadataPath);
                    if (skill == null || !string.IsNullOrEmpty(error))
                    {
                        return (false, error ?? "Failed to load skill");
                    }

                    var (success, message) = await apiService.ApplyExtendedSkillAsync(skill, dryRun: false);
                    return (success, message);
                }
                else
                {
                    return (false, $"Unsupported API version '{resourceModel.ApiVersion}' for skill '{kind}'. Expected '{YamlApiVersion.V2}'. Please migrate to V2 format.");
                }
            }

            // Unknown resource type
            return (false, $"Unsupported resource kind '{kind}' with api_version '{resourceModel.ApiVersion}'");
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"Failed to process YAML document {documentNumber}: {ex.Message}");
            return (false, $"Failed to process document {documentNumber}: {ex.Message}");
        }
    }
}
