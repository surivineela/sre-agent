// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Helpers;
using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for skill configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class ExtendedSkillV2 : ResourceModel
    {
        public ExtendedSkillV2()
        {
            ApiVersion = YamlApiVersion.V2;
            Kind = ResourceKind.SkillV2;
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Skill specification properties.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public SkillSpecV2 Spec { get; set; } = new();

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// Removes trailing whitespace from description and skill content fields.
        /// </summary>
        public override void Normalize()
        {
            if (Spec != null)
            {
                Spec.Description = NormalizeString(Spec.Description);
                Spec.SkillContent = NormalizeString(Spec.SkillContent);

                if (Spec.AdditionalFiles != null)
                {
                    foreach (var file in Spec.AdditionalFiles)
                    {
                        file.Content = NormalizeString(file.Content);
                    }
                }
            }
        }

        /// <summary>
        /// Parses a YAML string into an ExtendedSkillV2 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedSkillV2 object</returns>
        public static ExtendedSkillV2 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedSkillV2>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedSkillV2 object asynchronously.
        /// Also loads the SKILL.md content and any additional files referenced in the metadata.
        /// </summary>
        /// <param name="fileName">The file path to the metadata.yaml to read and parse</param>
        /// <returns>A tuple containing the parsed ExtendedSkillV2 object and an error message (if any)</returns>
        public static async Task<(ExtendedSkillV2? Skill, string? Error)> LoadYamlAsync(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    return (null, $"File not found: {fileName}");
                }

                var yamlContent = await File.ReadAllTextAsync(fileName);
                var skill = ParseYaml(yamlContent);

                if (skill == null)
                {
                    return (null, "Failed to parse YAML file");
                }

                // Get the skill directory from the metadata.yaml path
                var skillDirectory = Path.GetDirectoryName(fileName);
                if (string.IsNullOrEmpty(skillDirectory))
                {
                    return (null, "Invalid file path - cannot determine skill directory");
                }

                // Load SKILL.md content
                var skillMdPath = Path.Combine(skillDirectory, ExtendedSkillHelper.SkillContentFileName);
                if (!File.Exists(skillMdPath))
                {
                    return (null, $"{ExtendedSkillHelper.SkillContentFileName} not found in skill directory: {skillDirectory}");
                }
                skill.Spec.SkillContent = await File.ReadAllTextAsync(skillMdPath);

                // Auto-discover all additional files in the directory (excluding metadata.yaml and SKILL.md)
                var allFiles = Directory.GetFiles(skillDirectory, "*", SearchOption.AllDirectories);
                skill.Spec.AdditionalFiles = [];

                foreach (var filePath in allFiles)
                {
                    var fileNameInLoop = Path.GetFileName(filePath);

                    // Skip metadata.yaml and SKILL.md
                    if (string.Equals(fileNameInLoop, ExtendedSkillHelper.MetadataFileName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(fileNameInLoop, ExtendedSkillHelper.SkillContentFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Get relative path from skill directory
                    var relativePath = Path.GetRelativePath(skillDirectory, filePath);
                    var content = await File.ReadAllTextAsync(filePath);

                    skill.Spec.AdditionalFiles.Add(new SkillAdditionalFileV2
                    {
                        FilePath = relativePath,
                        Content = content
                    });
                }

                return (skill, null);
            }
            catch (Exception ex)
            {
                return (null, $"Failed to load skill: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves the skill to a directory with metadata.yaml, SKILL.md, and additional files.
        /// </summary>
        /// <param name="directoryPath">The directory path where the skill files will be saved</param>
        public async Task SaveAsync(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
            {
                throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));
            }

            // Create directory if it doesn't exist
            Directory.CreateDirectory(directoryPath);

            // Save metadata.yaml
            var metadataPath = Path.Combine(directoryPath, ExtendedSkillHelper.MetadataFileName);
            await SaveYamlAsync(metadataPath);

            // Save SKILL.md
            var skillMdPath = Path.Combine(directoryPath, ExtendedSkillHelper.SkillContentFileName);
            if (!string.IsNullOrEmpty(Spec.SkillContent))
            {
                await File.WriteAllTextAsync(skillMdPath, Spec.SkillContent, System.Text.Encoding.UTF8);
            }

            // Save additional files
            if (Spec.AdditionalFiles != null)
            {
                foreach (var file in Spec.AdditionalFiles)
                {
                    if (string.IsNullOrWhiteSpace(file.FilePath) || string.IsNullOrWhiteSpace(file.Content))
                    {
                        continue;
                    }

                    var filePath = Path.Combine(directoryPath, file.FilePath);
                    var fileDirectory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(fileDirectory))
                    {
                        Directory.CreateDirectory(fileDirectory);
                    }
                    await File.WriteAllTextAsync(filePath, file.Content, System.Text.Encoding.UTF8);
                }
            }
        }
    }

    /// <summary>
    /// Skill specification for YAML configurations (V2).
    /// Contains skill properties used by the CLI for creating and managing skill configurations.
    /// </summary>
    public class SkillSpecV2
    {
        [YamlMember(Alias = "description", Order = 0, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Description { get; set; }

        [YamlMember(Alias = "tools", Order = 1, DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
        public List<string>? Tools { get; set; } = [];

        [YamlIgnore]
        public string? SkillContent { get; set; }

        [YamlIgnore]
        public List<SkillAdditionalFileV2>? AdditionalFiles { get; set; } = [];
    }

    /// <summary>
    /// Additional file specification for skills.
    /// </summary>
    public class SkillAdditionalFileV2
    {
        [YamlIgnore]
        public string? FilePath { get; set; }

        [YamlIgnore]
        public string? Content { get; set; }
    }
}
