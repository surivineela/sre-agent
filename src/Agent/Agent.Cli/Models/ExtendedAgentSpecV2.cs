// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Cli.Models
{
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

        [YamlMember(Alias = "handoffs")]
        public List<string>? Handoffs { get; set; }

        [YamlMember(Alias = "tools")]
        public List<string>? Tools { get; set; }

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
    }
}
