using Agent.Data;
using Agent.Data.DataModels;
using Agent.Framework.Models;
using Agent.Framework.Reasoning.Models;

public record AgentDocumentModel(
    string Id,
    string Name,
    string Instructions,
    string? HandoffDescription,
    List<string> Handoffs,
    List<string> Tools,
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
    string? OutputType,
 YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgent";
    public string PartitionKey => Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;
}
