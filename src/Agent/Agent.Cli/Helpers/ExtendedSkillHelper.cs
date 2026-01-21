// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Models;
using Agent.Framework.Skills;

namespace Agent.Cli.Helpers;

/// <summary>
/// Helper class for creating and managing ExtendedSkillV2 instances.
/// </summary>
public static class ExtendedSkillHelper
{
    /// <summary>
    /// Default folder for skill directories.
    /// </summary>
    public const string DefaultSkillsFolder = "skills";

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
    /// Gets all local skill directories by searching recursively under the skills directory.
    /// Only includes directories that are valid skills (IsValidSkillDirectory returns true).
    /// </summary>
    /// <returns>List of skill directory names (without path)</returns>
    public static List<string> GetLocalSkills()
    {
        var localSkills = new List<string>();

        if (!Directory.Exists(DefaultSkillsFolder))
        {
            return localSkills;
        }

        var directories = Directory.GetDirectories(DefaultSkillsFolder, "*", SearchOption.AllDirectories);

        foreach (var dir in directories)
        {
            if (IsValidSkillDirectory(dir))
            {
                var dirName = Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(dirName))
                {
                    localSkills.Add(dirName);
                }
            }
        }

        return localSkills;
    }

    /// <summary>
    /// Finds a skill directory by searching recursively under the skills directory.
    /// </summary>
    /// <param name="skillName">The name of the skill to find</param>
    /// <returns>The full path to the skill directory, or null if not found</returns>
    public static string? FindSkillDirectory(string skillName)
    {
        if (!Directory.Exists(DefaultSkillsFolder))
        {
            return null;
        }

        // First, try the flat structure: skills/{skillName}
        var skillPath = Path.Combine(DefaultSkillsFolder, skillName);
        if (Directory.Exists(skillPath) && IsValidSkillDirectory(skillPath))
        {
            return skillPath;
        }

        // Search recursively for any directory with matching name that contains valid skill files
        var directories = Directory.GetDirectories(DefaultSkillsFolder, "*", SearchOption.AllDirectories);

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
    /// Validates if a directory contains valid skill files.
    /// Supports both new format (SKILL.md with frontmatter) and old format (metadata.yaml + SKILL.md).
    /// </summary>
    /// <param name="directoryPath">Path to the skill directory</param>
    /// <returns>True if the directory contains valid skill files, false otherwise</returns>
    public static bool IsValidSkillDirectory(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return false;
        }

        var skillMdPath = Path.Combine(directoryPath, SkillContentFileName);

        // SKILL.md must always exist
        if (!File.Exists(skillMdPath))
        {
            return false;
        }

        // Check new format: SKILL.md with frontmatter
        try
        {
            var skillMdContent = File.ReadAllText(skillMdPath);
            if (SkillFrontmatter.HasValidFrontmatter(skillMdContent))
            {
                return true;
            }
        }
        catch (Exception ex)
        {
            // If we can't read the file, fall through to old format check
            DebugLogger.Debug("SkillValidation", $"Failed to read SKILL.md in {directoryPath}: {ex.Message}");
        }

        // Check old format: metadata.yaml + SKILL.md
        var metadataPath = Path.Combine(directoryPath, MetadataFileName);
        return File.Exists(metadataPath);
    }

    /// <summary>
    /// Checks if a skill directory uses the new frontmatter format.
    /// </summary>
    /// <param name="directoryPath">Path to the skill directory</param>
    /// <returns>True if SKILL.md has valid frontmatter, false otherwise</returns>
    public static bool UsesFrontmatterFormat(string directoryPath)
    {
        var skillMdPath = Path.Combine(directoryPath, SkillContentFileName);
        if (!File.Exists(skillMdPath))
        {
            return false;
        }

        try
        {
            var skillMdContent = File.ReadAllText(skillMdPath);
            return SkillFrontmatter.HasValidFrontmatter(skillMdContent);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("SkillValidation", $"Failed to read SKILL.md in {directoryPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Generates SKILL.md template content for a new skill with frontmatter.
    /// </summary>
    /// <param name="skillName">The name of the skill</param>
    /// <param name="description">Optional description for the skill</param>
    /// <returns>The SKILL.md content as a string with frontmatter</returns>
    public static string CreateSkillContent(string skillName, string? description = null)
    {
        var defaultDescription = "Brief description of what this skill does and when to use it.";
        var metadata = new Agent.Framework.Skills.YamlSkillDescriptor
        {
            Metadata = new Agent.Framework.Skills.YamlSkillMetadata
            {
                ApiVersion = YamlApiVersion.V2.Value,
                Kind = Models.ResourceModel.ResourceKind.SkillV2
            },
            Name = skillName,
            Description = description ?? defaultDescription,
            Tools = []
        };

        var markdownBody = $@"# {skillName}

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

        // Generate the SKILL.md with frontmatter (includeFirstPartyOnly: false for user skills)
        return SkillFrontmatter.Generate(metadata, markdownBody, includeFirstPartyOnly: false);
    }

    /// <summary>
    /// Represents the format used for skill metadata storage.
    /// </summary>
    public enum SkillMetadataFormat
    {
        /// <summary>
        /// New format: metadata stored in SKILL.md YAML frontmatter.
        /// </summary>
        Frontmatter,

        /// <summary>
        /// Legacy format: metadata stored in separate metadata.yaml file.
        /// </summary>
        MetadataYaml,

        /// <summary>
        /// Unknown or invalid format.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Detects the metadata format used by a skill directory.
    /// </summary>
    /// <param name="directoryPath">Path to the skill directory</param>
    /// <returns>The detected SkillMetadataFormat</returns>
    public static SkillMetadataFormat DetectSkillFormat(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return SkillMetadataFormat.Unknown;
        }

        var skillMdPath = Path.Combine(directoryPath, SkillContentFileName);

        // Check new format first: SKILL.md with frontmatter
        if (File.Exists(skillMdPath))
        {
            try
            {
                var skillMdContent = File.ReadAllText(skillMdPath);
                if (SkillFrontmatter.HasValidFrontmatter(skillMdContent))
                {
                    return SkillMetadataFormat.Frontmatter;
                }
            }
            catch (Exception ex)
            {
                // Fall through to check old format
                DebugLogger.Debug("SkillValidation", $"Failed to read SKILL.md in {directoryPath}: {ex.Message}");
            }
        }

        // Check old format: metadata.yaml
        var metadataPath = Path.Combine(directoryPath, MetadataFileName);
        if (File.Exists(metadataPath))
        {
            return SkillMetadataFormat.MetadataYaml;
        }

        return SkillMetadataFormat.Unknown;
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
        catch (Exception ex)
        {
            DebugLogger.Debug("SkillValidation", $"Failed to detect version for {metadataFilePath}: {ex.Message}");
            return null;
        }
    }
}
