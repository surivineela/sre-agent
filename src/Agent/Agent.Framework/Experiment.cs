// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;

/// <summary>
/// Defines the unit of randomization for an experiment.
/// </summary>
public enum ExperimentUnit
{
    /// <summary>
    /// Experiment is assigned globally per instance. All threads within the same instance get the same variant.
    /// </summary>
    Global,

    /// <summary>
    /// Experiment is assigned per thread. Different threads can get different variants.
    /// </summary>
    PerThread
}

/// <summary>
/// An experiment definition for A/B testing. This allows you to define multiple variants of an agent configuration
/// and specify how traffic should be split between them. Each variant can override specific parts of the agent
/// configuration, such as prompts, tools, model parameters, and agent graph structure.
/// </summary>
public sealed record Experiment
{
    [YamlMember(Alias = "experiment_id")]
    public required string ExperimentId { get; init; }

    [YamlMember(Alias = "variants")]
    public required IEnumerable<Variant> Variants { get; init; }

    /// <summary>
    /// The fraction of total traffic to include in the experiment. Must be between 0 and 1.
    /// </summary>
    [YamlMember(Alias = "coverage")]
    public double Coverage { get; init; } = 1.0;

    [YamlMember(Alias = "description")]
    public string Description { get; init; } = string.Empty;

    [YamlMember(Alias = "enabled")]
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// The unit of randomization for this experiment. Defaults to Global.
    /// - Global: All threads within the same instance get the same variant
    /// - PerThread: Different threads can get different variants
    /// </summary>
    [YamlMember(Alias = "unit")]
    public ExperimentUnit Unit { get; init; } = ExperimentUnit.Global;

    public static Experiment FromYaml(string yamlContent)
    {
        var deserializer = new DeserializerBuilder()
            .IgnoreUnmatchedProperties()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<Experiment>(yamlContent);
    }
}

public sealed record Variant
{
    [YamlMember(Alias = "name")]
    public required string Name { get; init; }

    /// <summary>
    /// The fraction of experiment traffic to route to this variant. Must be between 0 and 1, and the sum of all
    /// variant splits within the experiment must be equal to 1. If not, splits will be normalized to sum to 1.
    /// </summary>
    [YamlMember(Alias = "split")]
    public double Split { get; init; }

    [YamlMember(Alias = "overlay")]
    public VariantOverlay Overlay { get; init; } = new VariantOverlay();
}

public sealed record VariantOverlay
{
    [YamlMember(Alias = "prompts")]
    public IEnumerable<PromptOverlay>? PromptOverlay { get; init; }

    [YamlMember(Alias = "tools")]
    public IEnumerable<ToolOverlay>? ToolOverlay { get; init; }

    [YamlMember(Alias = "handoffs")]
    public IEnumerable<HandoffOverlay>? HandoffOverlay { get; init; }

    [YamlMember(Alias = "agent_params")]
    public IEnumerable<ParamOverlay>? ParamOverlay { get; init; }
}

public sealed record PromptOverlay
{
    [YamlMember(Alias = "agent_names")]
    public required IEnumerable<string> AgentNames { get; init; }

    /// <summary>
    /// If set, replaces the entire system prompt for the agent.
    /// </summary>
    [YamlMember(Alias = "replace_system_prompt")]
    public string? ReplaceSystemPrompt { get; init; }

    /// <summary>
    /// Text to append to the existing system prompt.
    /// </summary>
    [YamlMember(Alias = "append_system_prompt")]
    public string? AppendSystemPrompt { get; init; }

    /// <summary>
    /// Text to prepend to the existing system prompt.
    /// </summary>
    [YamlMember(Alias = "prepend_system_prompt")]
    public string? PrependSystemPrompt { get; init; }

    /// <summary>
    /// Replaces the handoff instructions for the agent.
    /// </summary>
    [YamlMember(Alias = "handoff_instructions")]
    public string? HandoffInstructions { get; init; }

    /// <summary>
    /// Names of common prompts to add to the agent's prompt template.
    /// If `apply_standard_modifiers` is true, these will be added in addition to the common prompts configured on the base agent.
    /// Otherwise, these will replace the base agent's common prompts.
    /// </summary>
    [YamlMember(Alias = "common_prompts")]
    public IEnumerable<string>? CommonPrompts { get; init; }

    /// <summary>
    /// Whether the standard handoff instructions should be included. True by default.
    /// If `apply_standard_modifiers` is true, this value will be ignored.
    /// Only applies if `replace_system_prompt` is set.
    /// </summary>
    [YamlMember(Alias = "has_handoff_instructions")]
    public bool HasHandoffInstructions { get; init; } = true;

    /// <summary>
    /// If true, applies the standard prompt modifiers in `AgentFactory.ConfigureAgentInstructions()`, which includes
    /// adding handoff instructions, runtime-configured prompt starters/enders, and the common prompts configured on the base agent.
    /// Only applies if `replace_system_prompt` is set.
    /// True by default.
    /// </summary>
    [YamlMember(Alias = "apply_standard_modifiers")]
    public bool ApplyStandardModifiers { get; init; } = true;

    /// <summary>
    /// User prompt override for the agent.
    /// </summary>
    [YamlMember(Alias = "user_prompt_override")]
    public string? UserPromptOverride { get; init; }

    // Add more prompt fields as needed
}

public sealed record ToolOverlay
{
    [YamlMember(Alias = "agent_names")]
    public required IEnumerable<string> AgentNames { get; init; }

    [YamlMember(Alias = "replace_tools")]
    public IEnumerable<string>? ReplaceTools { get; init; }

    [YamlMember(Alias = "add_tools")]
    public IEnumerable<string>? AddTools { get; init; }

    [YamlMember(Alias = "remove_tools")]
    public IEnumerable<string>? RemoveTools { get; init; }
}

public sealed record HandoffOverlay
{
    [YamlMember(Alias = "agent_names")]
    public required IEnumerable<string> AgentNames { get; init; }

    [YamlMember(Alias = "add_handoffs")]
    public required IEnumerable<string> AddHandoffs { get; init; }

    [YamlMember(Alias = "remove_handoffs")]
    public required IEnumerable<string> RemoveHandoffs { get; init; }

    [YamlMember(Alias = "replace_handoffs")]
    public required IEnumerable<string> ReplaceHandoffs { get; init; }
}

public sealed record ParamOverlay
{
    [YamlMember(Alias = "agent_names")]
    public required IEnumerable<string> AgentNames { get; init; }

    [YamlMember(Alias = "model_name")]
    public string? ModelName { get; init; }

    [YamlMember(Alias = "reasoning_effort_level")]
    public string? ReasoningEffortLevel { get; init; }

    [YamlMember(Alias = "output_type")]
    public string? OutputType { get; init; }

    [YamlMember(Alias = "enable_skills")]
    public bool? EnableSkills { get; init; }

    [YamlMember(Alias = "add_system_skills")]
    public bool? AddSystemSkills { get; init; }

    [YamlMember(Alias = "allow_parallel_tool_calls")]
    public bool? AllowParallelToolCalls { get; init; }

    // add more model parameters as needed
}
