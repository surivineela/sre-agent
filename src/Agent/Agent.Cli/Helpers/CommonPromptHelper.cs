// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for creating and managing CommonPromptV2 instances.
/// </summary>
public static class CommonPromptHelper
{
    /// <summary>
    /// Creates a CommonPromptV2 instance with the provided parameters.
    /// </summary>
    public static CommonPromptV2 CreateCommonPrompt(
        string name,
        string? prompt = null,
        string? owner = null,
        List<string>? tags = null)
    {
        var defaultPrompt = "# PLACEHOLDER: Modify this prompt with your actual common prompt content";

        var commonPrompt = new CommonPromptV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = name,
                Owner = owner,
                Tags = tags ?? new List<string>()
            },
            Spec = new CommonPromptSpecV2
            {
                Prompt = prompt ?? defaultPrompt
            }
        };

        return commonPrompt;
    }

    /// <summary>
    /// Finds a common prompt YAML file by searching recursively under the CommonPrompts directory.
    /// Supports flexible folder organization.
    /// </summary>
    /// <param name="promptName">The name of the common prompt to find</param>
    /// <returns>The full path to the common prompt YAML file, or null if not found</returns>
    public static string? FindCommonPrompt(string promptName)
    {
        const string commonPromptsDir = "CommonPrompts";
        if (!Directory.Exists(commonPromptsDir))
        {
            return null;
        }

        // First, try the legacy structure: CommonPrompts/{promptName}/{promptName}.yaml
        var legacyPath = Path.Combine(commonPromptsDir, promptName, $"{promptName}.yaml");
        if (File.Exists(legacyPath))
        {
            return legacyPath;
        }

        // Then try the flat structure: CommonPrompts/{promptName}.yaml
        var flatPath = Path.Combine(commonPromptsDir, $"{promptName}.yaml");
        if (File.Exists(flatPath))
        {
            return flatPath;
        }

        // Finally, search recursively for any YAML file with the matching prompt name
        var yamlFiles = Directory.GetFiles(commonPromptsDir, "*.yaml", SearchOption.AllDirectories);

        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            if (fileName.Equals(promptName, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }
}
