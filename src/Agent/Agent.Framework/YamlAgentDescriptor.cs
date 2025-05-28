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

    [YamlMember(Alias = "auto_tools")]
    public List<string> AutoTools { get; set; } = [];

    [YamlMember(Alias = "manual_tools")]
    public List<string> ManualTools { get; set; } = [];

    [YamlMember(Alias = "max_reflection_count")]
    public int MaxReflectionCount { get; set; } = 0;
}
