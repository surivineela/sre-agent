using Agent.Framework;
using Agent.Framework.Models;

namespace Agent.Data.DataModels.Legacy;

public record AgentDocumentModelLegacy(
    string Id,
    string Name,
    string Instructions,
    string? HandoffDescription,
    List<string> Handoffs,
    List<string> Tools,
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
    string OperationId,
    bool? EnableSkills,
    bool? AddSystemSkills
) : ICosmosDocument, ILegacyModelConverter<AgentDocumentModel>
{
    public string DocumentType => "ExtendedAgent";
    public string PartitionKey => Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    public AgentSpec ToResourceSpec()
    {
        return new AgentSpec
        {
            Name = Name,
            Instructions = Instructions,
            HandoffDescription = HandoffDescription,
            Handoffs = Handoffs,
            Tools = Tools,
            Connectors = Connectors,
            AllowParallelToolCalls = AllowParallelToolCalls,
            AgentsAsTools = AgentsAsTools,
            MaxReflectionCount = MaxReflectionCount,
            CriticPromptPath = CriticPromptPath,
            CriticOnHandOff = CriticOnHandOff,
            CustomReflectionNote = CustomReflectionNote,
            CommonPrompts = CommonPrompts,
            DisableDocumentRetrieval = DisableDocumentRetrieval,
            EnableHandoffPromptOverride = EnableHandoffPromptOverride,
            UserPromptOverride = UserPromptOverride,
            HandoffPromptOverride = HandoffPromptOverride,
            InstructionsOverride = InstructionsOverride,
            CommonTools = CommonTools,
            Temperature = Temperature,
            AgentType = AgentType,
            ParameterExtractionAgent = ParameterExtractionAgent,
            OrchestrationStartAgents = OrchestrationStartAgents,
            ResultSummarizationPrompt = ResultSummarizationPrompt,
            NextAgentMappings = NextAgentMappings,
            OutputType = OutputType,
            EnableSkills = EnableSkills ?? false,
            AddSystemSkills = AddSystemSkills ?? false
        };
    }


    public ResourceMetadata ToResourceMetadata()
    {
        return new ResourceMetadata
        {
            Id = Id,
            OperationId = OperationId,
            Owner = Metadata?.Owner,
            Version = Metadata?.Version,
            Tags = Metadata?.Tags,
            UpdatedAt = Metadata?.UpdatedAt,
            CreatedAt = Metadata?.CreatedAt
        };
    }

    public AgentDocumentModel ToNewModel() => ToAgentDocumentModel();

    public AgentDocumentModel ToAgentDocumentModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();

        return new AgentDocumentModel(
            Metadata: metadata,
            Spec: spec
        );
    }
}
