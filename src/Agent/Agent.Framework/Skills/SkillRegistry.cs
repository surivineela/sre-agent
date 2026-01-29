// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Microsoft.Extensions.Logging;

namespace Agent.Framework.Skills;

/// <summary>
/// Registry for managing agent skills
/// </summary>
public class SkillRegistry(
    ILogger<SkillRegistry> logger,
    string systemSkillsDirectory,
    IExtensibilityLoader extensibilityLoader,
    Func<bool>? isFirstPartyTenantCheck = null) : AsyncInitializerBase, ISkillRegistry, IDisposable
{
    private readonly Dictionary<string, ISkill> _systemSkills = [];
    private readonly Dictionary<string, ISkill> _extendedSkills = [];

    private string? _extendedSkillsTempDirectory;

    private bool _isInitialized;
    private bool _disposed;

    private bool ShouldIncludeSystemSkill(ISkill skill)
    {
        if (!skill.FirstPartyOnly)
            return true;

        return isFirstPartyTenantCheck?.Invoke() ?? false;
    }

    protected override async Task InitializeAsyncCore()
    {
        if (_isInitialized)
        {
            logger.LogInternalWarning("SkillRegistry is already initialized. Skipping re-initialization.");
            return;
        }

        if (string.IsNullOrEmpty(systemSkillsDirectory) || !Directory.Exists(systemSkillsDirectory))
        {
            logger.LogInternalWarning("System skills directory '{SkillsDirectory}' does not exist. Skipping.", systemSkillsDirectory);
            _isInitialized = true;
            return;
        }

        logger.LogInternalInformation("Initializing skills from directory: {SkillsDirectory}", systemSkillsDirectory);

        var skillDirectories = Directory.GetDirectories(systemSkillsDirectory);

        foreach (var skillDir in skillDirectories)
        {
            try
            {
                await LoadSystemSkillAsync(skillDir);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load skill from directory: {SkillDirectory}", skillDir);
            }
        }

        logger.LogInternalInformation("Loaded {SkillCount} skills successfully", _systemSkills.Count);

        await LoadSkillsFromExtensibilityAsync();

        _isInitialized = true;
    }

    public async Task LoadSkillsFromExtensibilityAsync()
    {
        _extendedSkills.Clear();

        var extendedSkills = await extensibilityLoader.LoadExtendedSkillsAsync();

        if (_extendedSkillsTempDirectory == null)
        {
            _extendedSkillsTempDirectory = Path.Combine(Path.GetTempPath(), "Agent", "ExtendedSkills");
            Directory.CreateDirectory(_extendedSkillsTempDirectory);
        }
        else
        {
            // Clean up previous temp directory
            Directory.Delete(_extendedSkillsTempDirectory, recursive: true);
            Directory.CreateDirectory(_extendedSkillsTempDirectory);
        }

        foreach (var skillDescriptor in extendedSkills)
        {
            try
            {
                // write SKILL.md and additional files to temp directory
                var tempDirectory = Path.Combine(_extendedSkillsTempDirectory, skillDescriptor.Name);
                Directory.CreateDirectory(tempDirectory);

                var skillMdPath = Path.Combine(tempDirectory, $"SKILL.md");
                await File.WriteAllTextAsync(skillMdPath, skillDescriptor.SkillMdContent, CancellationToken.None);

                foreach (var additionalFile in skillDescriptor.AdditionalFiles)
                {
                    var additionalFilePath = Path.Combine(tempDirectory, additionalFile.FilePath);
                    await File.WriteAllTextAsync(additionalFilePath, additionalFile.Content, CancellationToken.None);
                }

                skillDescriptor.DirectoryPath = tempDirectory;

                _extendedSkills[skillDescriptor.Name] = Skill.FromDescriptor(skillDescriptor, tempDirectory);

                logger.LogInternalInformation("Loaded extended skill '{SkillName}' Directory '{Directory}'", skillDescriptor.Name, tempDirectory);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load extended skill '{SkillName}'", skillDescriptor.Name);
            }
        }
    }

    private async Task LoadSystemSkillAsync(string skillDirectory)
    {
        var skillMdPath = Path.Combine(skillDirectory, "SKILL.md");
        var metadataPath = Path.Combine(skillDirectory, "metadata.yaml");

        YamlSkillDescriptor? descriptor = null;

        // Try new format first: SKILL.md with frontmatter
        if (File.Exists(skillMdPath))
        {
            var skillMdContent = await File.ReadAllTextAsync(skillMdPath);
            var frontmatterResult = SkillFrontmatter.Parse(skillMdContent);

            if (frontmatterResult.ParsingException != null)
            {
                logger.LogInternalWarning(frontmatterResult.ParsingException, "Skill directory '{SkillDirectory}' encountered an error while parsing SKILL.md frontmatter: {Error}. Skipping.", skillDirectory, frontmatterResult.ParsingException.Message);
                return;
            }

            if (frontmatterResult.HasFrontmatter)
            {
                descriptor = frontmatterResult.Metadata;
                logger.LogInternalInformation("Loaded skill metadata from SKILL.md frontmatter in '{SkillDirectory}'", skillDirectory);
            }
        }

        // Fall back to old format: metadata.yaml (deprecated)
        if (descriptor == null)
        {
            if (!File.Exists(metadataPath))
            {
                logger.LogInternalWarning("Skill directory '{SkillDirectory}' does not contain valid SKILL.md frontmatter or metadata.yaml. Skipping.", skillDirectory);
                return;
            }

            var yamlContent = await File.ReadAllTextAsync(metadataPath);
            descriptor = YamlSkillDescriptor.FromYaml(yamlContent);
            logger.LogInternalWarning("Skill '{SkillDirectory}' uses deprecated metadata.yaml format. Consider migrating to SKILL.md frontmatter.", skillDirectory);
        }

        if (string.IsNullOrEmpty(descriptor.Name))
        {
            logger.LogInternalWarning("Skill in directory '{SkillDirectory}' has no name defined. Skipping.", skillDirectory);
            return;
        }

        var skill = Skill.FromDescriptor(descriptor, skillDirectory);
        _systemSkills[skill.Name] = skill;

        logger.LogInternalInformation("Loaded skill '{SkillName}' from {SkillDirectory}", skill.Name, skillDirectory);
    }

    public string GetSkillsMetadataForPrompt(bool includeSystemSkills, IReadOnlyList<string>? allowedSkills = null)
    {
        // If allowedSkills is an empty list, return no skills
        if (allowedSkills is { Count: 0 })
        {
            return "No skills available.";
        }

        List<ISkill> includedSkills = [.. _extendedSkills.Values];

        if (includeSystemSkills)
        {
            includedSkills.AddRange(_systemSkills.Values.Where(ShouldIncludeSystemSkill));
        }

        // Apply allowedSkills filter if specified
        if (allowedSkills is not null)
        {
            var allowedSet = new HashSet<string>(allowedSkills, StringComparer.OrdinalIgnoreCase);

            // Log warnings for skill names that don't exist
            var allSkillNames = includedSkills.Select(s => s.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var allowedName in allowedSkills)
            {
                if (!allSkillNames.Contains(allowedName))
                {
                    logger.LogWarning("Allowed skill '{SkillName}' does not exist in the skill registry", allowedName);
                }
            }

            includedSkills = [.. includedSkills.Where(s => allowedSet.Contains(s.Name))];
        }

        if (includedSkills.Count == 0)
        {
            return "No skills available.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("You have access to specialized skills for domain-specific tasks. When you need domain expertise, load the relevant skill using the `read_skill_file` tool.");
        sb.AppendLine();
        sb.AppendLine("Available skills:");

        foreach (var skill in includedSkills.OrderBy(s => s.Name))
        {
            sb.AppendLine($"<skill name=\"{skill.Name}\">");
            sb.AppendLine($"  <description>{skill.Description}</description>");

            if (skill.Tools.Count > 0)
            {
                sb.AppendLine("  <skill_tools>");

                foreach (var toolName in skill.Tools)
                {
                    sb.AppendLine($"    <tool>{toolName}</tool>");
                }

                sb.AppendLine("  </skill_tools>");
            }

            sb.AppendLine($"</skill>");
        }

        sb.AppendLine();
        sb.AppendLine("To activate a skill:");
        sb.AppendLine("1. Call read_skill_file(skill_name=\"<skill_name>\", file_path=\"SKILL.md\") -- You MUST read the SKILL.md file first");
        sb.AppendLine("2. The skill content will be loaded into your context");
        sb.AppendLine("3. SKILL.md may reference additional files - read them as needed using the same tool, for example: read_skill_file(skill_name=\"<skill_name>\", file_path=\"troubleshooting.md\") -- Don't attempt to read a skill file that isn't referenced in SKILL.md");

        return sb.ToString();
    }

    public string ReadSkillFile(string skillName, string filePath, bool includeSystemSkills, IReadOnlyList<string>? allowedSkills = null)
    {
        // If allowedSkills is an empty list, no skills are available
        if (allowedSkills is { Count: 0 })
        {
            return $"Error: Skill '{skillName}' not found. No skills are available (allowed_skills is empty).";
        }

        // Check if skill is in the allowed list (when specified)
        if (allowedSkills is not null)
        {
            var isAllowed = allowedSkills.Any(s => string.Equals(s, skillName, StringComparison.OrdinalIgnoreCase));
            if (!isAllowed)
            {
                var availableSkills = string.Join(", ", allowedSkills);
                return $"Error: Skill '{skillName}' is not in the allowed skills list. Available skills: {availableSkills}";
            }
        }

        ISkill? skill = null;

        if (_extendedSkills.TryGetValue(skillName, out var extSkill))
        {
            skill = extSkill;
        }
        else if (includeSystemSkills &&
                 _systemSkills.TryGetValue(skillName, out var sysSkill) &&
                 ShouldIncludeSystemSkill(sysSkill))
        {
            skill = sysSkill;
        }

        if (skill == null)
        {
            var availableSkillNames = GetAvailableSkillNames(includeSystemSkills, allowedSkills);
            var availableSkills = string.Join(", ", availableSkillNames);
            return $"Error: Skill '{skillName}' not found. Available skills: {availableSkills}";
        }

        var fullPath = Path.Combine(skill.DirectoryPath, filePath);

        if (!File.Exists(fullPath))
        {
            var hierarchicalList = GetHierarchicalFileList(skill.DirectoryPath);
            var nl = Environment.NewLine;
            return $"Error: File '{filePath}' not found in skill '{skillName}'." + nl +
                "Available files (tree):" + nl + hierarchicalList;
        }

        try
        {
            var content = File.ReadAllText(fullPath);
            logger.LogInternalInformation("Read file '{FilePath}' from skill '{SkillName}'", filePath, skillName);

            // If reading the primary skill file, prepend a tree of other available files for discoverability
            if (string.Equals(Path.GetFileName(filePath), "SKILL.md", StringComparison.OrdinalIgnoreCase))
            {
                var tree = GetHierarchicalFileList(skill.DirectoryPath);
                var nl = Environment.NewLine;
                var header = $"Files in skill '{skillName}' (tree):" + nl + tree + nl;
                return header + content;
            }

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading file '{FilePath}' from skill '{SkillName}'", filePath, skillName);
            return $"Error reading file '{filePath}' from skill '{skillName}': {ex.Message}";
        }
    }

    public ISkill? GetSkillByName(string name, bool includeSystemSkills, IReadOnlyList<string>? allowedSkills = null)
    {
        // If allowedSkills is an empty list, no skills are available
        if (allowedSkills is { Count: 0 })
        {
            return null;
        }

        // Check if skill is in the allowed list (when specified)
        if (allowedSkills is not null)
        {
            var isAllowed = allowedSkills.Any(s => string.Equals(s, name, StringComparison.OrdinalIgnoreCase));
            if (!isAllowed)
            {
                return null;
            }
        }

        if (_extendedSkills.TryGetValue(name, out var extSkill))
        {
            return extSkill;
        }

        if (includeSystemSkills &&
            _systemSkills.TryGetValue(name, out var skill) &&
            ShouldIncludeSystemSkill(skill))
        {
            return skill;
        }

        return null;
    }

    /// <summary>
    /// Gets the list of available skill names based on filtering criteria.
    /// </summary>
    private List<string> GetAvailableSkillNames(bool includeSystemSkills, IReadOnlyList<string>? allowedSkills)
    {
        var availableSkillNames = _extendedSkills.Keys.ToList();
        if (includeSystemSkills)
        {
            availableSkillNames.AddRange(_systemSkills.Values
                .Where(ShouldIncludeSystemSkill)
                .Select(s => s.Name));
        }

        // Apply allowedSkills filter if specified
        if (allowedSkills is not null)
        {
            var allowedSet = new HashSet<string>(allowedSkills, StringComparer.OrdinalIgnoreCase);
            availableSkillNames = [.. availableSkillNames.Where(n => allowedSet.Contains(n))];
        }

        return availableSkillNames;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Cleanup managed resources
            _systemSkills.Clear();
            _extendedSkills.Clear();
        }

        // Cleanup temp directory
        if (_extendedSkillsTempDirectory != null && Directory.Exists(_extendedSkillsTempDirectory))
        {
            try
            {
                Directory.Delete(_extendedSkillsTempDirectory, recursive: true);
                logger.LogInternalInformation("Cleaned up extended skills temp directory: {Directory}", _extendedSkillsTempDirectory);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to cleanup extended skills temp directory: {Directory}", _extendedSkillsTempDirectory);
            }
        }

        _disposed = true;
    }

    ~SkillRegistry()
    {
        Dispose(disposing: false);
    }

    /// <summary>
    /// Builds a hierarchical tree view of all files under the provided root skill directory.
    /// Directories are listed first (with a trailing '/'), then files, each prefixed with a dash and indentation.
    /// </summary>
    /// <param name="rootDirectory">The root directory of the skill.</param>
    /// <returns>A multi-line string representing the directory tree contents.</returns>
    private static string GetHierarchicalFileList(string rootDirectory)
    {
        var sb = new StringBuilder();

        void Recurse(string currentDir, int indent)
        {
            // List directories first
            foreach (var dir in Directory.GetDirectories(currentDir).OrderBy(Path.GetFileName))
            {
                var dirName = Path.GetFileName(dir);
                sb.Append(' ', indent * 2).Append("- ").Append(dirName).Append('/').AppendLine();
                Recurse(dir, indent + 1);
            }

            // Then list files
            foreach (var file in Directory.GetFiles(currentDir).OrderBy(Path.GetFileName))
            {
                var fileName = Path.GetFileName(file);
                if (string.Equals(fileName, "metadata.yaml", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // skip metadata.yaml files
                }
                sb.Append(' ', indent * 2).Append("- ").Append(fileName).AppendLine();
            }
        }

        try
        {
            Recurse(rootDirectory, 0);
        }
        catch (Exception ex)
        {
            // If traversal fails, return a simplified flat list instead
            return $"(Failed to build tree: {ex.Message})" + Environment.NewLine +
                   string.Join(Environment.NewLine, Directory.GetFiles(rootDirectory).Select(f => "- " + Path.GetFileName(f)));
        }

        return sb.ToString();
    }
}
