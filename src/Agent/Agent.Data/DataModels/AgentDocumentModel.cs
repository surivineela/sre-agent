using System.Text.Json.Serialization;
using Agent.Framework;
using Agent.Framework.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// New AgentDocumentModel with Metadata and Spec properties
/// </summary>
public record AgentDocumentModel(
    ResourceMetadata Metadata,
    AgentSpec Spec
) : ICosmosDocument
{
    public string Id => Metadata.Id ?? Spec.Name;

    public string DocumentType => "ExtendedAgent";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    // Convenience properties for ease of access (JsonIgnore to avoid serialization conflicts)
    [JsonIgnore]
    public string Name => Spec.Name;

    [JsonIgnore]
    public string HandoffDescription => Spec.HandoffDescription ?? string.Empty;

    [JsonIgnore]
    public List<string> Tools => Spec.Tools ?? new List<string>();

    # region Conversion between runtime and data model
    public YamlAgentDescriptor ToYamlAgentDescriptor() => new YamlAgentDescriptor
    {
        AgentsAsTools = Spec.AgentsAsTools ?? new List<AgentsAsTools>(),
        Name = Spec.Name,
        Instructions = Spec.Instructions ?? string.Empty,
        HandoffDescription = Spec.HandoffDescription,
        Handoffs = Spec.Handoffs ?? new List<string>(),
        Tools = Spec.Tools ?? new List<string>(),
        McpTools = Spec.McpTools ?? new List<string>(),
        Connectors = Spec.Connectors ?? new List<string>(),
        AllowParallelToolCalls = Spec.AllowParallelToolCalls ?? false,
        MaxReflectionCount = Spec.MaxReflectionCount ?? 0,
        CriticPromptPath = Spec.CriticPromptPath ?? string.Empty,
        CriticOnHandOff = Spec.CriticOnHandOff ?? false,
        CustomReflectionNote = Spec.CustomReflectionNote ?? string.Empty,
        CommonPrompts = Spec.CommonPrompts ?? new List<string>(),
        CommonTools = Spec.CommonTools ?? new List<string>(),
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
        OrchestrationStartAgents = Spec.OrchestrationStartAgents ?? new List<string>(),
        ResultSummarizationPrompt = Spec.ResultSummarizationPrompt,
        NextAgentMappings = Spec.NextAgentMappings ?? new List<NextAgentMapping>()
    };
    # endregion
}


/// <summary>
/// metadata fields for agent documents
/// </summary>
public class ResourceMetadata
{
    public string? Id { get; set; }

    public string? OperationId { get; set; }
    public string? Owner { get; set; }

    public string? Version { get; set; }

    public List<string>? Tags { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    #region Conversion between runtime and data model
    public static ResourceMetadata FromYamlMetadata(YamlMetadata yamlMetadata, string id, string operationId)
    {
        return new ResourceMetadata
        {
            Id = id,
            OperationId = operationId,
            Owner = yamlMetadata.Owner,
            Version = yamlMetadata.Version,
            Tags = yamlMetadata.Tags,
            UpdatedAt = yamlMetadata.UpdatedAt,
            CreatedAt = yamlMetadata.CreatedAt
        };
    }

    public YamlMetadata ToYamlMetadata()
    {
        return new YamlMetadata
        {
            Owner = Owner,
            Version = Version,
            Tags = Tags,
            UpdatedAt = UpdatedAt,
            CreatedAt = CreatedAt
        };
    }
    #endregion
}

public class AgentSpec
{
    public string Name { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? HandoffDescription { get; set; }
    public List<string>? Handoffs { get; set; }
    public List<string>? McpTools { get; set; }
    public List<string>? Tools { get; set; }
    public List<string>? Connectors { get; set; }
    public bool? AllowParallelToolCalls { get; set; }
    public List<AgentsAsTools>? AgentsAsTools { get; set; }
    public int? MaxReflectionCount { get; set; }
    public string? CriticPromptPath { get; set; }
    public bool? CriticOnHandOff { get; set; }
    public string? CustomReflectionNote { get; set; }
    public List<string>? CommonPrompts { get; set; }
    public List<string>? CommonTools { get; set; }
    public bool? DisableDocumentRetrieval { get; set; }
    public string? InstructionsOverride { get; set; }
    public bool? EnableHandoffPromptOverride { get; set; }
    public string? HandoffPromptOverride { get; set; }
    public string? UserPromptOverride { get; set; }
    public float? Temperature { get; set; }
    public string? LlmModelName { get; set; }
    public bool EnableVanillaMode { get; set; }

    // === Workflow Agent Support ===

    /// <summary>
    /// Specifies the execution type of this agent.
    /// </summary>
    public AgentType AgentType { get; set; } = AgentType.Autonomous;

    // === Orchestrator Agent Properties ===

    /// <summary>
    /// Name of the agent responsible for extracting parameters from conversation history,
    /// incident data, and function metadata for RCA execution.
    /// </summary>
    public string? ParameterExtractionAgent { get; set; }

    /// <summary>
    /// List of agent names to start orchestration with using extracted parameters.
    /// These agents execute without conversation history.
    /// </summary>
    public List<string> OrchestrationStartAgents { get; set; } = [];

    /// <summary>
    /// Prompt template used for summarizing results from orchestrated agents.
    /// The results are stored in-memory and summarized using LLM with this prompt.
    /// </summary>
    public string? ResultSummarizationPrompt { get; set; }

    // === Activity Agent Properties ===

    /// <summary>
    /// Mappings that define which agents to execute next based on execution results.
    /// Used by Activity agents to dynamically select subsequent agents in the workflow.
    /// </summary>
    public List<NextAgentMapping> NextAgentMappings { get; set; } = [];

    public string? OutputType { get; set; } = null;
}
