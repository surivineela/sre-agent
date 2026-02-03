// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;
using Agent.Framework.Models;
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

    [YamlMember(Alias = "mcp_tools")]
    public List<string> McpTools { get; set; } = [];

    [YamlMember(Alias = "connectors")]
    public List<string> Connectors { get; set; } = [];


    [YamlMember(Alias = "allow_parallel_tool_calls")]
    public bool AllowParallelToolCalls { get; set; } = true;

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

    [YamlMember(Alias = "llm_model_name")]
    public string? LlmModelName { get; set; } = null;

    [YamlMember(Alias = "llm_scenario_type")]
    public ModelScenarioType? LlmScenarioType { get; set; } = null;

    [YamlMember(Alias = "vanilla_mode")]
    public bool EnableVanillaMode { get; set; } = false;

    // Back-compat: accept legacy disable_common_prompts field and map it to vanilla mode
    [YamlMember(Alias = "disable_common_prompts")]
    public bool LegacyDisableCommonPrompts
    {
        set => EnableVanillaMode = value;
    }

    [YamlMember(Alias = "enable_skills")]
    public bool EnableSkills { get; set; } = false;

    /// <inheritdoc/>
    [YamlMember(Alias = "add_system_skills")]
    public bool AddSystemSkills { get; set; } = false;

    /// <inheritdoc/>
    [YamlMember(Alias = "allowed_skills")]
    public List<string>? AllowedSkills { get; set; } = null;

    // === Workflow Agent Support ===

    /// <summary>
    /// Specifies the execution type of this agent.
    /// </summary>
    [YamlMember(Alias = "agent_type")]
    public AgentType AgentType { get; set; } = AgentType.Autonomous;

    // === Orchestrator Agent Properties ===

    /// <summary>
    /// Name of the agent responsible for extracting parameters from conversation history,
    /// incident data, and function metadata for RCA execution.
    /// </summary>
    [YamlMember(Alias = "parameter_extraction_agent")]
    public string? ParameterExtractionAgent { get; set; }

    /// <summary>
    /// List of agent names to start orchestration with using extracted parameters.
    /// These agents execute without conversation history.
    /// </summary>
    [YamlMember(Alias = "orchestration_start_agents")]
    public List<string> OrchestrationStartAgents { get; set; } = [];

    /// <summary>
    /// Prompt template used for summarizing results from orchestrated agents.
    /// The results are stored in-memory and summarized using LLM with this prompt.
    /// </summary>
    [YamlMember(Alias = "result_summarization_prompt")]
    public string? ResultSummarizationPrompt { get; set; }

    // === Activity Agent Properties ===

    /// <summary>
    /// Mappings that define which agents to execute next based on execution results.
    /// Used by Activity agents to dynamically select subsequent agents in the workflow.
    /// </summary>
    [YamlMember(Alias = "next_agent_mappings")]
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];

    [YamlMember(Alias = "output_type")]
    public string? OutputType { get; set; } = null;
    [YamlMember(Alias = "meta_data")]
    public YamlMetadata Metadata { get; set; } = new();

    // === Hook Configuration ===

    /// <summary>
    /// Hook configuration for this agent. Hooks can intercept events like Stop
    /// to perform validation or prevent actions.
    /// </summary>
    [YamlMember(Alias = "hooks")]
    public Dictionary<string, List<HookDefinition>>? Hooks { get; set; }

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
