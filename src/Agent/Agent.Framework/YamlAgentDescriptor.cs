// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Models;
using Agent.Framework.Reasoning.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;

public class YamlAgentDescriptor : IAgentDescriptor
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

    [YamlMember(Alias = "connectors")]
    public List<string> Connectors { get; set; } = [];


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

    [YamlMember(Alias = "common_tools")]
    public List<string> CommonTools { get; set; } = [];

    [YamlMember(Alias = "disable_document_retrieval")]
    public bool DisableDocumentRetrieval { get; set; } = false;

    [YamlMember(Alias = "instructions_override")]
    public string? InstructionsOverride { get; set; } = null;

    [YamlMember(Alias = "enable_handoff_prompt_override")]
    public bool EnableHandoffPromptOverride { get; set; } = false;

        [YamlMember(Alias = "handoff_prompt_override")]
    public string? HandoffPromptOverride { get; set; } = null;

    [YamlMember(Alias = "user_prompt_override")]
    public string? UserPromptOverride { get; set; } = null;

    [YamlMember(Alias = "temperature")]
    public float? Temperature { get; set; } = null;

    [YamlMember(Alias = "output_type")]
    public string? OutputType { get; set; } = null;
    [YamlMember(Alias = "meta_data")]
    public YamlMetadata Metadata { get; set; } = new();

    public static YamlAgentDescriptor FromYaml(string yamlContent)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        try
        {
            return deserializer.Deserialize<YamlAgentDescriptor>(yamlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to deserialize YAML: {ex.Message}", ex);
        }
    }

   
}
