// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// Base class for YAML resource models.
    /// Contains common envelope fields for YAML file serialization.
    /// </summary>
    public class ResourceModel
    {
        /// <summary>
        /// Standard resource kind values used in YAML files.
        /// </summary>
        public static class ResourceKind
        {
            public const string ExtendedAgentToolV2 = "ExtendedAgentTool";
            public const string ExtendedAgentV2 = "ExtendedAgent";
            public const string CommonPromptV2 = "CommonPrompt";
            public const string SkillV2 = "Skill";
        }

        [YamlMember(Alias = "api_version", Order = -2)]
        public string ApiVersion { get; set; } = string.Empty;

        [YamlMember(Alias = "kind", Order = -1)]
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Normalizes string properties in the model to ensure clean YAML serialization.
        /// Override in subclasses to normalize specific properties.
        /// </summary>
        public virtual void Normalize()
        {
            // Base implementation does nothing - subclasses override to normalize their properties
        }

        /// <summary>
        /// Normalizes a string by trimming trailing whitespace from each line and the end.
        /// This ensures YamlDotNet uses literal block style (|) instead of quoted style.
        /// </summary>
        /// <param name="input">The string to normalize</param>
        /// <returns>Normalized string with trailing whitespace removed</returns>
        protected static string? NormalizeString(string? input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Split into lines, trim trailing whitespace from each line
            var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].TrimEnd(' ', '\t');
            }

            // Join back together and trim any final trailing newlines/whitespace
            return string.Join("\n", lines).TrimEnd();
        }

        /// <summary>
        /// Gets a standard DeserializerBuilder with common configuration.
        /// This ensures consistency across all YAML deserialization operations.
        /// </summary>
        /// <returns>A configured DeserializerBuilder instance</returns>
        public static DeserializerBuilder GetDeserializerBuilder()
        {
            return new DeserializerBuilder()
                .IgnoreUnmatchedProperties();
        }

        /// <summary>
        /// Serializes this ResourceModel to a YAML string using standard serialization settings.
        /// Automatically normalizes the model before serialization to ensure clean formatting.
        /// </summary>
        /// <returns>YAML string representation of this object</returns>
        public string ToYaml()
        {
            // Normalize string properties by calling the model's Normalize method
            Normalize();

            var serializer = new SerializerBuilder()
                .DisableAliases()
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                .Build();

            return serializer.Serialize(this);
        }

        /// <summary>
        /// Serializes this ResourceModel to a YAML file asynchronously.
        /// Automatically normalizes the model before serialization to ensure clean formatting.
        /// </summary>
        /// <param name="fileName">The file path where the YAML content will be saved</param>
        /// <returns>A task representing the asynchronous save operation</returns>
        public async Task SaveYamlAsync(string fileName)
        {
            var yaml = ToYaml();
            await File.WriteAllTextAsync(fileName, yaml);
        }
    }
}
