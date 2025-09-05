// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;

namespace Agent.Data;

public static class DocumentToRuntimeMapper
{
    public static YamlAgentDescriptor ToRuntimeAgent(AgentDocumentModel api) => new YamlAgentDescriptor
    {
        AgentsAsTools = api.AgentsAsTools,
        Name = api.Name,
        Instructions = api.Instructions,
        HandoffDescription = api.HandoffDescription,
        Handoffs = api.Handoffs,
        Tools = api.Tools,
        Connectors = api.Connectors,
        AllowParallelToolCalls = api.AllowParallelToolCalls,
        MaxReflectionCount = api.MaxReflectionCount,
        CriticPromptPath = api.CriticPromptPath,
        CriticOnHandOff = api.CriticOnHandOff,
        CustomReflectionNote = api.CustomReflectionNote,
        CommonPrompts = api.CommonPrompts,
        CommonTools = api.CommonTools,
        Temperature = api.Temperature,
        OutputType = api.OutputType,
        DisableDocumentRetrieval = api.DisableDocumentRetrieval,
        EnableHandoffPromptOverride = api.EnableHandoffPromptOverride,
        UserPromptOverride = api.UserPromptOverride,

        Metadata = api.Metadata,
        // Workflow agent properties
        AgentType = api.AgentType,
        ParameterExtractionAgent = api.ParameterExtractionAgent,
        OrchestrationStartAgents = api.OrchestrationStartAgents,
        ResultSummarizationPrompt = api.ResultSummarizationPrompt,
        NextAgentMappings = api.NextAgentMappings
        //  UserPromptOverride = api.UserPromptOverride,
        //Hooks = api.Hooks,
        // FactoryTools = api.FactoryTools
    };

    public static YamlToolDefinitionBase ToRuntimeTool(ToolDocumentModel tool) => tool switch
    {
        KustoToolDocumentModel k => new KustoToolDefinition
        {
            Name = k.Name,
            Type = k.Type,
            Connector = k.Connector,
            Description = k.Description,
            Parameters = k.Parameters,
            Attributes = k.Attributes,
            Metadata = k.Metadata,
            Mode = k.Mode,
            Function = k.Function,
            Query = k.Query,
            File = k.File,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
        },
        _ => throw new NotSupportedException($"Unknown tool document type: {tool.Type}")
    };

    public static YamlPromptDescriptor ToRuntimePrompt(CommonPromptDocumentModel promptDocumentModel) => new YamlPromptDescriptor
    {
        Name = promptDocumentModel.Name,
        Prompt = promptDocumentModel.Prompt
    };

    public static YamlCommonToolsDescriptor ToRuntimeToolsList(CommonToolsListDocumentModel toolsListDocumentModel) => new YamlCommonToolsDescriptor
    {
        Name = toolsListDocumentModel.Name,
        Tools = toolsListDocumentModel.CommonToolsList,
    };
}
