// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework.Skills;

/// <summary>
/// Parsed result from SKILL.md frontmatter extraction.
/// </summary>
public class SkillFrontmatterResult
{
    /// <summary>
    /// The skill metadata extracted from frontmatter.
    /// Null if no valid frontmatter was found.
    /// </summary>
    public YamlSkillDescriptor? Metadata { get; set; }

    /// <summary>
    /// The markdown content after the frontmatter.
    /// If no frontmatter exists, this contains the entire file content.
    /// </summary>
    public string MarkdownContent { get; set; } = string.Empty;

    /// <summary>
    /// Whether valid frontmatter was found and parsed.
    /// Requires both metadata object and non-empty name to be considered valid.
    /// </summary>
    public bool HasFrontmatter => Metadata != null && !string.IsNullOrEmpty(Metadata.Name);

    /// <summary>
    /// Any exception encountered during parsing.
    /// Null if parsing was successful.
    /// </summary>
    public Exception? ParsingException { get; set; }
}

/// <summary>
/// Utility class for parsing YAML frontmatter from SKILL.md files.
/// </summary>
public static partial class SkillFrontmatter
{
    private const string FrontmatterDelimiter = "---";

    // Regex to match frontmatter: starts with ---, ends with --- on its own line
    // Uses NonBacktracking to prevent catastrophic backtracking on malformed input
    [GeneratedRegex(@"^---\r?\n([\s\S]*?)\r?\n---\r?\n?", RegexOptions.NonBacktracking)]
    private static partial Regex FrontmatterRegex();

    /// <summary>
    /// Parses YAML frontmatter from SKILL.md content.
    /// </summary>
    /// <param name="skillMdContent">The full content of SKILL.md file</param>
    /// <returns>Parsed result containing metadata (if found) and markdown content</returns>
    public static SkillFrontmatterResult Parse(string skillMdContent)
    {
        if (string.IsNullOrEmpty(skillMdContent))
        {
            return new SkillFrontmatterResult { MarkdownContent = string.Empty };
        }

        // Check if content starts with frontmatter delimiter
        if (!skillMdContent.TrimStart().StartsWith(FrontmatterDelimiter))
        {
            return new SkillFrontmatterResult { MarkdownContent = skillMdContent };
        }

        var match = FrontmatterRegex().Match(skillMdContent);
        if (!match.Success)
        {
            // Has opening --- but no closing ---, treat as no frontmatter
            return new SkillFrontmatterResult { MarkdownContent = skillMdContent };
        }

        var yamlContent = match.Groups[1].Value;
        var markdownContent = skillMdContent[match.Length..].TrimStart('\r', '\n');

        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var metadata = deserializer.Deserialize<YamlSkillDescriptor>(yamlContent);

            return new SkillFrontmatterResult
            {
                Metadata = metadata,
                MarkdownContent = markdownContent
            };
        }
        catch (Exception ex)
        {
            // Invalid YAML in frontmatter, treat as no frontmatter and return the exception
            return new SkillFrontmatterResult { MarkdownContent = skillMdContent, ParsingException = ex };
        }
    }

    /// <summary>
    /// Checks if SKILL.md content has valid frontmatter.
    /// </summary>
    /// <param name="skillMdContent">The full content of SKILL.md file</param>
    /// <returns>True if valid frontmatter exists</returns>
    public static bool HasValidFrontmatter(string skillMdContent)
    {
        return Parse(skillMdContent).HasFrontmatter;
    }

    /// <summary>
    /// Generates SKILL.md content with YAML frontmatter from metadata and markdown content.
    /// </summary>
    /// <param name="metadata">The skill metadata to include in frontmatter</param>
    /// <param name="markdownContent">The markdown content (without frontmatter)</param>
    /// <param name="includeFirstPartyOnly">Whether to include first_party_only in frontmatter (for system skills only)</param>
    /// <returns>Complete SKILL.md content with frontmatter</returns>
    public static string Generate(YamlSkillDescriptor metadata, string markdownContent, bool includeFirstPartyOnly = true)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var sb = new StringBuilder();
        sb.AppendLine(FrontmatterDelimiter);

        // Include nested metadata block with api_version and kind if provided (for CLI compatibility)
        if (metadata.Metadata != null &&
            (!string.IsNullOrEmpty(metadata.Metadata.ApiVersion) || !string.IsNullOrEmpty(metadata.Metadata.Kind)))
        {
            sb.AppendLine("metadata:");
            if (!string.IsNullOrEmpty(metadata.Metadata.ApiVersion))
            {
                sb.AppendLine($"  api_version: {metadata.Metadata.ApiVersion}");
            }
            if (!string.IsNullOrEmpty(metadata.Metadata.Kind))
            {
                sb.AppendLine($"  kind: {metadata.Metadata.Kind}");
            }
        }

        // Build frontmatter YAML manually for better control over formatting
        sb.AppendLine($"name: {metadata.Name}");

        // Description with literal block style for multiline
        if (!string.IsNullOrEmpty(metadata.Description))
        {
            var description = metadata.Description.TrimEnd();
            if (description.Contains('\n'))
            {
                sb.AppendLine("description: |");
                foreach (var line in description.Split('\n'))
                {
                    sb.AppendLine($"  {line.TrimEnd('\r')}");
                }
            }
            else
            {
                sb.AppendLine($"description: {description}");
            }
        }

        // Tools list
        if (metadata.Tools.Count > 0)
        {
            sb.AppendLine("tools:");
            foreach (var tool in metadata.Tools)
            {
                sb.AppendLine($"  - {tool}");
            }
        }

        // first_party_only (only for system skills, omit if false)
        if (includeFirstPartyOnly && metadata.FirstPartyOnly)
        {
            sb.AppendLine("first_party_only: true");
        }

        sb.AppendLine(FrontmatterDelimiter);

        // Add blank line between frontmatter and content if content doesn't start with one
        var content = markdownContent?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(content))
        {
            sb.AppendLine();
            sb.AppendLine(content);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates SKILL.md content with YAML frontmatter from a SkillSpec.
    /// </summary>
    /// <param name="skillSpec">The skill specification</param>
    /// <param name="includeFirstPartyOnly">Whether to include first_party_only in frontmatter</param>
    /// <returns>Complete SKILL.md content with frontmatter</returns>
    public static string Generate(SkillSpec skillSpec, bool includeFirstPartyOnly = false)
    {
        var metadata = new YamlSkillDescriptor
        {
            Name = skillSpec.Name,
            Description = skillSpec.Description,
            Tools = skillSpec.Tools,
            FirstPartyOnly = skillSpec.FirstPartyOnly
        };

        return Generate(metadata, skillSpec.SkillMdContent, includeFirstPartyOnly);
    }
}
