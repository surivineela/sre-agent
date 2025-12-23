// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Cli.Helpers;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML model for skill configurations (V1).
    /// Matches the legacy SkillSpec structure exactly without kind/metadata wrappers.
    /// </summary>
    public class ExtendedSkillV1
    {
        [YamlMember(Alias = "name", Order = 0)]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "description", Order = 1, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string Description { get; set; } = string.Empty;

        [YamlMember(Alias = "tools", Order = 2, DefaultValuesHandling = DefaultValuesHandling.OmitEmptyCollections)]
        public List<string> Tools { get; set; } = [];

        [YamlIgnore]
        public string SkillMdContent { get; set; } = string.Empty;

        [YamlIgnore]
        public List<SkillAdditionalFileV1> AdditionalFiles { get; set; } = [];

        [YamlIgnore]
        public string DirectoryPath { get; set; } = string.Empty;

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// Removes trailing whitespace from description and skill content fields.
        /// </summary>
        public void Normalize()
        {
            Description = NormalizeString(Description);
            SkillMdContent = NormalizeString(SkillMdContent);

            if (AdditionalFiles != null)
            {
                foreach (var file in AdditionalFiles)
                {
                    file.Content = NormalizeString(file.Content);
                }
            }
        }

        /// <summary>
        /// Normalizes a string by removing trailing whitespace from each line.
        /// </summary>
        private static string NormalizeString(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var lines = value.Split('\n');
            return string.Join('\n', lines.Select(line => line.TrimEnd()));
        }

        /// <summary>
        /// Parses a YAML string into an ExtendedSkillV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedSkillV1 object</returns>
        public static ExtendedSkillV1 ParseYaml(string yaml)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            return deserializer.Deserialize<ExtendedSkillV1>(yaml);
        }

        /// <summary>
        /// Serializes the ExtendedSkillV1 object to YAML string.
        /// </summary>
        /// <returns>The YAML string representation</returns>
        public string ToYaml()
        {
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitDefaults)
                .Build();
            return serializer.Serialize(this);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedSkillV1 object asynchronously.
        /// Also loads the SKILL.md content and any additional files referenced in the metadata.
        /// </summary>
        /// <param name="fileName">The file path to the metadata.yaml to read and parse</param>
        /// <returns>A tuple containing the parsed ExtendedSkillV1 object and an error message (if any)</returns>
        public static async Task<(ExtendedSkillV1? Skill, string? Error)> LoadYamlAsync(string fileName)
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
                skill.SkillMdContent = await File.ReadAllTextAsync(skillMdPath);

                // Auto-discover all additional files in the directory (excluding metadata.yaml and SKILL.md)
                var allFiles = Directory.GetFiles(skillDirectory, "*", SearchOption.AllDirectories);
                skill.AdditionalFiles = [];

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

                    skill.AdditionalFiles.Add(new SkillAdditionalFileV1
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
    }

    /// <summary>
    /// Additional file specification for skills (V1).
    /// </summary>
    public class SkillAdditionalFileV1
    {
        [YamlIgnore]
        public string FilePath { get; set; } = string.Empty;

        [YamlIgnore]
        public string Content { get; set; } = string.Empty;
    }
}
