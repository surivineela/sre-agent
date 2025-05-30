// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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

    [YamlMember(Alias = "max_reflection_count")]
    public int MaxReflectionCount { get; set; } = 0;

    [YamlMember(Alias = "custom_reflection_note")]
    public string CustomReflectionNote { get; set; } = string.Empty;

    [YamlMember(Alias = "common_prompts")]
    public List<string> CommonPrompts { get; set; } = [];
}
