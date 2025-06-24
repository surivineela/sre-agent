// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Models;
using YamlDotNet.Serialization;

namespace Agent.Framework;

internal class YamlAgentDescriptor : IAgentDescriptor
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "system_prompt")]
    public string Instructions { get; set; } = string.Empty;

    [YamlMember(Alias = "handoff_description")]
    public string? HandoffDescription { get; set; }

    [YamlMember(Alias = "handoffs")]
    public List<string> Handoffs { get; set; } = [];

    [YamlMember(Alias = "tools")]
    public List<string> Tools { get; set; } = [];

    [YamlMember(Alias = "allow_parallel_tool_calls")]
    public bool AllowParallelToolCalls { get; set; } = false;

    [YamlMember(Alias = "agents_as_tools")]
    public List<AgentsAsTools> AgentsAsTools { get; set; } = [];

    [YamlMember(Alias = "max_reflection_count")]
    public int MaxReflectionCount { get; set; } = 0;

    [YamlMember(Alias = "critic_prompt_path")]
    public string CriticPromptPath { get; set; } = string.Empty;

    [YamlMember(Alias = "critic_on_handoff")]
    public bool CriticOnHandOff { get; set; } = false;

    [YamlMember(Alias = "custom_reflection_note")]
    public string CustomReflectionNote { get; set; } = string.Empty;

    [YamlMember(Alias = "common_prompts")]
    public List<string> CommonPrompts { get; set; } = [];

    [YamlMember(Alias = "temperature")]
    public float? Temperature { get; set; } = null;

    [YamlMember(Alias = "output_type")]
    public string? OutputType { get; set; } = null;
}
