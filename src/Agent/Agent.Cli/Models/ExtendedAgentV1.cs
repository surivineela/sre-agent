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
    public class ExtendedAgentV1 : ResourceModel
    {
        public ExtendedAgentV1()
        {
            ApiVersion = YamlApiVersion.V1;
            Kind = "AgentConfiguration";
        }

        /// <summary>
        /// Resource metadata (owner, tags, version, timestamps).
        /// </summary>
        [YamlMember(Alias = "metadata", Order = 0)]
        public ResourceMetadataModel Metadata { get; set; } = new();

        /// <summary>
        /// Agent specification properties.
        /// </summary>
        [YamlMember(Alias = "spec")]
        public ExtendedAgentSpecV1 Spec { get; set; } = new();

        /// <summary>
        /// Parses a YAML string into an ExtendedAgentV1 object.
        /// </summary>
        /// <param name="yaml">The YAML string to parse</param>
        /// <returns>The parsed ExtendedAgentV1 object</returns>
        public static ExtendedAgentV1 ParseYaml(string yaml)
        {
            var deserializer = GetDeserializerBuilder().Build();
            return deserializer.Deserialize<ExtendedAgentV1>(yaml);
        }

        /// <summary>
        /// Loads a YAML file and parses it into an ExtendedAgentV1 object asynchronously.
        /// </summary>
        /// <param name="fileName">The file path to read and parse</param>
        /// <returns>The parsed ExtendedAgentV1 object, or null if the file doesn't exist or parsing fails</returns>
        public static async Task<ExtendedAgentV1?> LoadYamlAsync(string fileName)
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
    /// Agent specification for YAML configurations.
    /// Contains agent properties used by the CLI for creating and managing agent configurations.
    /// </summary>
    public class ExtendedAgentSpecV1
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "system_prompt", ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        public string? Instructions { get; set; }

        [YamlMember(Alias = "handoff_description")]
        public string? HandoffDescription { get; set; }

        [YamlMember(Alias = "handoffs")]
        public List<string>? Handoffs { get; set; }

        [YamlMember(Alias = "tools")]
        public List<string>? Tools { get; set; }

        [YamlMember(Alias = "allow_parallel_tool_calls")]
        public bool? AllowParallelToolCalls { get; set; }

        [YamlMember(Alias = "max_reflection_count")]
        public int? MaxReflectionCount { get; set; }

        [YamlMember(Alias = "critic_prompt_path")]
        public string? CriticPromptPath { get; set; }

        [YamlMember(Alias = "critic_on_handoff")]
        public bool? CriticOnHandoff { get; set; }

        [YamlMember(Alias = "custom_reflection_note")]
        public string? CustomReflectionNote { get; set; }

        [YamlMember(Alias = "common_prompts")]
        public List<string>? CommonPrompts { get; set; }

        [YamlMember(Alias = "temperature")]
        public float? Temperature { get; set; }

        [YamlMember(Alias = "output_type")]
        public string? OutputType { get; set; }

        [YamlMember(Alias = "vanilla_mode")]
        public bool EnableVanillaMode { get; set; }

        [YamlMember(Alias = "enable_skills")]
        public bool? EnableSkills { get; set; }

        [YamlMember(Alias = "add_system_skills")]
        public bool? AddSystemSkills { get; set; }

        [YamlMember(Alias = "allowed_skills")]
        public List<string>? AllowedSkills { get; set; }
    }
}
