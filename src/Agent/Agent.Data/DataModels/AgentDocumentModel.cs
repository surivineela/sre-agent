using System.ComponentModel;
using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Framework.Models;
using Newtonsoft.Json;

public record AgentDocumentModel(
    string Id,
    string Name,
    string Instructions,
    string? HandoffDescription,
    List<string> Handoffs,
    List<string> Tools,
    [property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    [property: DefaultValue(null)]
    List<string>? McpTools,
    List<string> Connectors,
    bool AllowParallelToolCalls,
    List<AgentsAsTools> AgentsAsTools,
    int MaxReflectionCount,
    string CriticPromptPath,
    bool CriticOnHandOff,
    string CustomReflectionNote,
    List<string> CommonPrompts,
    bool DisableDocumentRetrieval,
    bool EnableHandoffPromptOverride,
    string? UserPromptOverride,
    string? HandoffPromptOverride,
    string? InstructionsOverride,
    List<string> CommonTools,
    float? Temperature,
    // Workflow agent properties
    AgentType AgentType,
    string? ParameterExtractionAgent,
    List<string> OrchestrationStartAgents,
    string? ResultSummarizationPrompt,
    List<NextAgentMapping> NextAgentMappings,
    string? OutputType,
    YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgent";
    public string PartitionKey => Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;
}
