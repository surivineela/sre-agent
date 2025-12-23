// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for creating and managing ExtendedSkillV2 instances.
/// </summary>
public static class ExtendedSkillHelper
{
    /// <summary>
    /// Default folder for skill directories.
    /// </summary>
    public const string DefaultSkillFolder = "skills";

    /// <summary>
    /// The filename for skill metadata.
    /// </summary>
    public const string MetadataFileName = "metadata.yaml";

    /// <summary>
    /// The filename for skill content.
    /// </summary>
    public const string SkillContentFileName = "SKILL.md";

    /// <summary>
    /// Creates an ExtendedSkillV2 instance with the provided parameters.
    /// </summary>
    /// <param name="name">The name of the skill</param>
    /// <param name="description">Optional description for the skill. If not provided, uses a default template.</param>
    /// <param name="tools">Optional list of tool names that the skill can use</param>
    /// <returns>A new ExtendedSkillV2 instance</returns>
    public static ExtendedSkillV2 CreateSkill(
        string name,
        string? description = null,
        List<string>? tools = null)
    {
        var defaultDescription = "Brief description of what this skill does and when to use it.\nUpdate this with specific capabilities and use cases.";

        var skill = new ExtendedSkillV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = name
            },
            Spec = new SkillSpecV2
            {
                Description = description ?? defaultDescription,
                Tools = tools ?? []
            }
        };

        return skill;
    }

    /// <summary>
    /// Finds a skill directory by searching recursively under the skills directory.
    /// </summary>
    /// <param name="skillName">The name of the skill to find</param>
    /// <returns>The full path to the skill directory, or null if not found</returns>
    public static string? FindSkillDirectory(string skillName)
    {
        if (!Directory.Exists(DefaultSkillFolder))
        {
            return null;
        }

        // First, try the flat structure: skills/{skillName}
        var skillPath = Path.Combine(DefaultSkillFolder, skillName);
        if (Directory.Exists(skillPath) && IsValidSkillDirectory(skillPath))
        {
            return skillPath;
        }

        // Search recursively for any directory with matching name that contains valid skill files
        var directories = Directory.GetDirectories(DefaultSkillFolder, "*", SearchOption.AllDirectories);

        foreach (var dir in directories)
        {
            var dirName = Path.GetFileName(dir);
            if (dirName.Equals(skillName, StringComparison.OrdinalIgnoreCase) &&
                IsValidSkillDirectory(dir))
            {
                return dir;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates if a directory contains the required skill files (metadata.yaml and SKILL.md).
    /// </summary>
    /// <param name="directoryPath">Path to the skill directory</param>
    /// <returns>True if the directory contains required skill files, false otherwise</returns>
    public static bool IsValidSkillDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        var metadataPath = Path.Combine(directoryPath, MetadataFileName);
        var skillMdPath = Path.Combine(directoryPath, SkillContentFileName);

        return File.Exists(metadataPath) && File.Exists(skillMdPath);
    }

    /// <summary>
    /// Generates SKILL.md template content for a new skill.
    /// </summary>
    /// <param name="skillName">The name of the skill</param>
    /// <returns>The SKILL.md content as a string</returns>
    public static string CreateSkillContent(string skillName)
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
    /// Detects the YAML API version of a skill metadata file.
    /// </summary>
    /// <param name="metadataFilePath">Path to the metadata.yaml file</param>
    /// <returns>The detected YamlApiVersion (V1 or V2), or null if not a valid skill metadata file</returns>
    public static YamlApiVersion? DetectVersion(string metadataFilePath)
    {
        // Check if the file name is metadata.yaml
        if (!string.Equals(Path.GetFileName(metadataFilePath), MetadataFileName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!File.Exists(metadataFilePath))
        {
            return null;
        }

        try
        {
            var content = File.ReadAllText(metadataFilePath);

            // Use YamlDotNet to parse as a dictionary to inspect structure
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var yamlDict = deserializer.Deserialize<Dictionary<string, object>>(content);

            if (yamlDict == null)
            {
                return null;
            }

            // Check for V2 format: must have both api_version and kind
            var apiVersion = yamlDict.TryGetValue("api_version", out var apiObj) ? apiObj?.ToString() : null;
            var kind = yamlDict.TryGetValue("kind", out var kindObj) ? kindObj?.ToString() : null;

            if (!string.IsNullOrEmpty(apiVersion) && !string.IsNullOrEmpty(kind))
            {
                // Check if it matches V2 values
                if (string.Equals(apiVersion, YamlApiVersion.V2.Value, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(kind, ResourceModel.ResourceKind.SkillV2, StringComparison.OrdinalIgnoreCase))
                {
                    return YamlApiVersion.V2;
                }
            }

            // Check for V1 format: must have name and/or description at root level
            var hasName = yamlDict.ContainsKey("name");
            var hasDescription = yamlDict.ContainsKey("description");

            if (hasName || hasDescription)
            {
                return YamlApiVersion.V1;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
