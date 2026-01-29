// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
    /// <summary>
    /// CLI YAML wrapper for agent configurations.
    /// Adds YAML envelope fields (api_version, kind) for file serialization.
    /// </summary>
    public class ExtendedAgentV2 : ResourceModel
    {
        public ExtendedAgentV2()
        {
            ApiVersion = YamlApiVersion.V2;
            Kind = ResourceKind.ExtendedAgentV2;
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Agent specification properties.
        /// </summary>
        [YamlMember(Alias = "spec", Order = 1)]
        public ExtendedAgentSpecV2 Spec { get; set; } = new();

        /// <summary>
        /// Normalizes string properties to ensure clean YAML literal block formatting.
        /// Removes trailing whitespace from instructions and other text fields.
        /// </summary>
        public override void Normalize()
        {
            if (Spec != null)
            {
                Spec.Instructions = NormalizeString(Spec.Instructions);
                Spec.HandoffDescription = NormalizeString(Spec.HandoffDescription);
                Spec.CustomReflectionNote = NormalizeString(Spec.CustomReflectionNote);
            }
        }

        /// <summary>
        /// Parses a YAML string into an ExtendedAgentV2 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedAgentV2 object</returns>
        public static ExtendedAgentV2 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedAgentV2>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedAgentV2 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedAgentV2 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedAgentV2?> LoadYamlAsync(string fileName)
        {
            try
            {
                if (!File.Exists(fileName))
                {
                    return null;
                }

                var yamlContent = await File.ReadAllTextAsync(fileName);
                return ParseYaml(yamlContent);
            }
            catch
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Agent specification for YAML configurations (V2).
    /// Contains agent properties used by the CLI for creating and managing agent configurations.
    /// </summary>
    public class ExtendedAgentSpecV2
    {
        [YamlMember(Alias = "instructions", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Instructions { get; set; }

        [YamlMember(Alias = "handoffDescription")]
        public string? HandoffDescription { get; set; }

        private List<string> _handoffs = new();
        [YamlMember(Alias = "handoffs")]
        public List<string>? Handoffs
        {
            get => _handoffs;
            set => _handoffs = value ?? new List<string>();
        }

        private List<string> _tools = new();
        [YamlMember(Alias = "tools")]
        public List<string>? Tools
        {
            get => _tools;
            set => _tools = value ?? new List<string>();
        }

        [YamlMember(Alias = "allowParallelToolCalls")]
        public bool? AllowParallelToolCalls { get; set; }

        [YamlMember(Alias = "maxReflectionCount")]
        public int? MaxReflectionCount { get; set; }

        [YamlMember(Alias = "criticPromptPath")]
        public string? CriticPromptPath { get; set; }

        [YamlMember(Alias = "criticOnHandoff")]
        public bool? CriticOnHandoff { get; set; }

        [YamlMember(Alias = "customReflectionNote")]
        public string? CustomReflectionNote { get; set; }

        [YamlMember(Alias = "commonPrompts")]
        public List<string>? CommonPrompts { get; set; }

        [YamlMember(Alias = "temperature")]
        public float? Temperature { get; set; }

        [YamlMember(Alias = "outputType")]
        public string? OutputType { get; set; }

        [YamlMember(Alias = "enableVanillaMode")]
        public bool EnableVanillaMode { get; set; }

        [YamlMember(Alias = "enableSkills")]
        public bool? EnableSkills { get; set; }

        [YamlMember(Alias = "addSystemSkills")]
        public bool? AddSystemSkills { get; set; }

        [YamlMember(Alias = "allowedSkills")]
        public List<string>? AllowedSkills { get; set; }
    }
}
