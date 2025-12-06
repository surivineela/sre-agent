// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
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
    }
}
