// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Framework.Models;
using YamlDotNet.Serialization;

namespace Agent.Data.DataModels;

/// <summary>
/// New AgentDocumentModel with Metadata and Spec properties
/// </summary>
public record AgentDocumentModel(
    ResourceMetadata Metadata,
    AgentSpec Spec
) : ICosmosDocument
{
    public const string DocumentTypeName = "ExtendedAgent";

    public string Id => GetId(Name);

    public string DocumentType => DocumentTypeName;
    public string PartitionKey => GetPartitionKey();
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    // Convenience properties for ease of access (JsonIgnore to avoid serialization conflicts)
    [JsonIgnore]
    public string Name => Metadata.Name;

    [JsonIgnore]
    public string HandoffDescription => Spec.HandoffDescription ?? string.Empty;

    [JsonIgnore]
    public List<string> Tools => Spec.Tools ?? [];

    public static string GetId(string name)
    {
        return name.ToLowerInvariant();
    }

    public static string GetPartitionKey()
    {
        return DocumentTypeName;
    }

    # region Conversion between runtime and data model
    public YamlAgentDescriptor ToYamlAgentDescriptor() => new YamlAgentDescriptor
    {
        AgentsAsTools = Spec.AgentsAsTools ?? [],
        Name = Metadata.Name,
        Instructions = Spec.Instructions ?? string.Empty,
        HandoffDescription = Spec.HandoffDescription,
        Handoffs = Spec.Handoffs ?? [],
        Tools = Spec.Tools ?? [],
        McpTools = Spec.McpTools ?? [],
        Connectors = Spec.Connectors ?? [],
        AllowParallelToolCalls = Spec.AllowParallelToolCalls ?? true,
        MaxReflectionCount = Spec.MaxReflectionCount ?? 0,
        CriticPromptPath = Spec.CriticPromptPath ?? string.Empty,
        CriticOnHandOff = Spec.CriticOnHandOff ?? false,
        CustomReflectionNote = Spec.CustomReflectionNote ?? string.Empty,
        CommonPrompts = Spec.CommonPrompts ?? [],
        CommonTools = Spec.CommonTools ?? [],
        Temperature = Spec.Temperature,
        OutputType = Spec.OutputType,
        DisableDocumentRetrieval = Spec.DisableDocumentRetrieval ?? false,
        EnableHandoffPromptOverride = Spec.EnableHandoffPromptOverride ?? false,
        UserPromptOverride = Spec.UserPromptOverride,
        LlmModelName = Spec.LlmModelName,
        EnableVanillaMode = Spec.EnableVanillaMode,

        Metadata = Metadata.ToYamlMetadata(),
        // Workflow agent properties
        AgentType = Spec.AgentType,
        ParameterExtractionAgent = Spec.ParameterExtractionAgent,
        OrchestrationStartAgents = Spec.OrchestrationStartAgents ?? [],
        ResultSummarizationPrompt = Spec.ResultSummarizationPrompt,
        NextAgentMappings = Spec.NextAgentMappings ?? [],
        EnableSkills = Spec.EnableSkills ?? false,
        AddSystemSkills = Spec.AddSystemSkills ?? false,
        AllowedSkills = Spec.AllowedSkills,
        Hooks = ConvertHooksToFramework(Spec.Hooks)
    };

    /// <summary>
    /// Converts hooks from DTO format to framework format.
    /// </summary>
    private static Dictionary<string, List<HookDefinition>>? ConvertHooksToFramework(
        Dictionary<string, List<HookDefinitionDto>>? hooks)
    {
        if (hooks == null || hooks.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, List<HookDefinition>>();
        foreach (var (eventType, definitions) in hooks)
        {
            result[eventType] = definitions.Select(dto => new HookDefinition
            {
                Type = Enum.TryParse<HookType>(dto.Type, ignoreCase: true, out var hookType)
                    ? hookType
                    : HookType.Prompt,
                Prompt = dto.Prompt,
                Command = dto.Command,
                Script = dto.Script,
                Matcher = dto.Matcher,
                Timeout = dto.Timeout,
                Model = dto.Model,
                FailMode = Enum.TryParse<HookFailMode>(dto.FailMode, ignoreCase: true, out var failMode)
                    ? failMode
                    : HookFailMode.Allow,
                MaxRejections = dto.MaxRejections
            }).ToList();
        }

        return result;
    }
    # endregion
}


/// <summary>
/// metadata fields for agent documents
/// </summary>
public class ResourceMetadata
{
    [YamlMember(Alias = "name")]
    public string Name { get; set; } = string.Empty;

    [YamlMember(Alias = "tags")]
    public List<string>? Tags { get; set; }

    [YamlMember(Alias = "updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [YamlMember(Alias = "created_at")]
    public DateTime? CreatedAt { get; set; }

    #region Conversion between runtime and data model
    public static ResourceMetadata FromYamlMetadata(YamlMetadata yamlMetadata, string name)
    {
        return new ResourceMetadata
        {
            Name = name ?? string.Empty,
            Tags = yamlMetadata.Tags,
            UpdatedAt = yamlMetadata.UpdatedAt,
            CreatedAt = yamlMetadata.CreatedAt
        };
    }

    public YamlMetadata ToYamlMetadata()
    {
        return new YamlMetadata
        {
            Tags = Tags,
            UpdatedAt = UpdatedAt,
            CreatedAt = CreatedAt
        };
    }
    #endregion
}

public class AgentSpec
{
    [YamlMember(Alias = "instructions")]
    public string? Instructions { get; set; }

    [YamlMember(Alias = "handoff_description")]
    public string? HandoffDescription { get; set; }

    [YamlMember(Alias = "handoffs")]
    public List<string>? Handoffs { get; set; }

    [YamlMember(Alias = "mcp_tools")]
    public List<string>? McpTools { get; set; }

    [YamlMember(Alias = "tools")]
    public List<string>? Tools { get; set; }

    [YamlMember(Alias = "connectors")]
    public List<string>? Connectors { get; set; }

    [YamlMember(Alias = "allow_parallel_tool_calls")]
    public bool? AllowParallelToolCalls { get; set; }

    [YamlMember(Alias = "agents_as_tools")]
    public List<AgentsAsTools>? AgentsAsTools { get; set; }

    [YamlMember(Alias = "max_reflection_count")]
    public int? MaxReflectionCount { get; set; }

    [YamlMember(Alias = "critic_prompt_path")]
    public string? CriticPromptPath { get; set; }

    [YamlMember(Alias = "critic_on_handoff")]
    public bool? CriticOnHandOff { get; set; }

    [YamlMember(Alias = "custom_reflection_note")]
    public string? CustomReflectionNote { get; set; }

    [YamlMember(Alias = "common_prompts")]
    public List<string>? CommonPrompts { get; set; }

    [YamlMember(Alias = "common_tools")]
    public List<string>? CommonTools { get; set; }

    [YamlMember(Alias = "disable_document_retrieval")]
    public bool? DisableDocumentRetrieval { get; set; }

    [YamlMember(Alias = "instructions_override")]
    public string? InstructionsOverride { get; set; }

    [YamlMember(Alias = "enable_handoff_prompt_override")]
    public bool? EnableHandoffPromptOverride { get; set; }

    [YamlMember(Alias = "handoff_prompt_override")]
    public string? HandoffPromptOverride { get; set; }

    [YamlMember(Alias = "user_prompt_override")]
    public string? UserPromptOverride { get; set; }

    [YamlMember(Alias = "temperature")]
    public float? Temperature { get; set; }

    [YamlMember(Alias = "llm_model_name")]
    public string? LlmModelName { get; set; }

    [YamlMember(Alias = "vanilla_mode")]
    public bool EnableVanillaMode { get; set; }

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

    [YamlMember(Alias = "enable_skills")]
    public bool? EnableSkills { get; set; } = null;

    [YamlMember(Alias = "add_system_skills")]
    public bool? AddSystemSkills { get; set; } = null;

    [YamlMember(Alias = "allowed_skills")]
    public List<string>? AllowedSkills { get; set; } = null;

    /// <summary>
    /// Hook configurations for this agent, organized by event type.
    /// Key is the event type name (e.g., "Stop"), value is list of hook definitions.
    /// </summary>
    [YamlMember(Alias = "hooks")]
    public Dictionary<string, List<HookDefinitionDto>>? Hooks { get; set; }
}
