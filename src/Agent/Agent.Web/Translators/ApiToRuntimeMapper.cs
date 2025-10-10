// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Data.Tools;
using Agent.Plugins.Tools;
using Agent.Framework.Reasoning.Models;
using Agent.Web.Models.ExtendedAgents;

namespace Agent.Web.Services;

public static class ApiToRuntimeMapper
{
    public static YamlToolDefinitionBase ToRuntimeTool(ExtendedAgentToolApiModel tool) => tool switch
    {
        KustoToolApiModel k => new KustoToolDefinition
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
            ClusterHint = k.ClusterHint
        },
        LinkToolApiModel link => new LinkToolDefinition
        {
            Name = link.Name,
            Type = link.Type,
            Description = link.Description,
            Parameters = link.Parameters,
            Attributes = link.Attributes,
            Metadata = link.Metadata,
            Template = link.Template,
        },
        _ => throw new NotSupportedException($"Unknown tool type: {tool.Type}")
    };

    public static DataConnectorDefinitionBase ToRuntimeConnector(ExtendedAgentConnectorApiModel connector) => connector switch
    {
        KustoConnectorApiModel k => new KustoConnector
        {
            Name = k.Name,
            Enabled = k.Enabled,
            Type = k.Type,
            Description = k.Description,
            Auth = k.Auth,
            Metadata = k.Metadata,
            ClusterUrl = k.ClusterUri,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            RegionalClusterGroups = k.RegionalClusterGroups
        },
        _ => throw new NotSupportedException($"Unknown connector type: {connector.Type}")
    };

    public static YamlAgentDescriptor ToRuntimeAgent(ExtendedAgentApiModel api) => new YamlAgentDescriptor
    {
        AgentsAsTools = api.AgentsAsTools,
        Name = api.Name,
        Instructions = api.Instructions,
        HandoffDescription = api.HandoffDescription,
        Handoffs = api.Handoffs,
        Tools = api.Tools,
        Connectors = api.Connectors,
        // Workflow agent properties
        AgentType = api.AgentType,
        ParameterExtractionAgent = api.ParameterExtractionAgent,
        OrchestrationStartAgents = api.OrchestrationStartAgents,
        ResultSummarizationPrompt = api.ResultSummarizationPrompt,
        NextAgentMappings = api.NextAgentMappings
    };

    public static ExtendedAgentApiModel ToApiAgent(YamlAgentDescriptor runtime) => new ExtendedAgentApiModel
    {
        Name = runtime.Name,
        Instructions = runtime.Instructions,
        HandoffDescription = runtime.HandoffDescription,
        Handoffs = runtime.Handoffs,
        Tools = runtime.Tools,
        Connectors = runtime.Connectors,
        AllowParallelToolCalls = runtime.AllowParallelToolCalls,
        AgentsAsTools = runtime.AgentsAsTools,
        MaxReflectionCount = runtime.MaxReflectionCount,
        CriticPromptPath = runtime.CriticPromptPath,
        CriticOnHandOff = runtime.CriticOnHandOff,
        CustomReflectionNote = runtime.CustomReflectionNote,
        CommonPrompts = runtime.CommonPrompts,
        Temperature = runtime.Temperature,
        OutputType = runtime.OutputType,
        // Workflow agent properties
        AgentType = runtime.AgentType,
        ParameterExtractionAgent = runtime.ParameterExtractionAgent,
        OrchestrationStartAgents = runtime.OrchestrationStartAgents,
        ResultSummarizationPrompt = runtime.ResultSummarizationPrompt,
        NextAgentMappings = runtime.NextAgentMappings
    };

    public static ToolDocumentModel ToDocumentTool(ExtendedAgentToolApiModel tool, string operationId) => tool switch
    {
        KustoToolApiModel k => new KustoToolDocumentModel(
            id: $"tool_{k.Name}",
            name: k.Name,
            type: k.Type,
            connector: k.Connector,
            description: k.Description,
            parameters: k.Parameters,
            attributes: k.Attributes,
            metadata: k.Metadata,

            operationId: operationId)
        {
            Mode = k.Mode,
            Function = k.Function,
            Query = k.Query,
            File = k.File,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            RegionalClusterGroups = k.RegionalClusterGroups
        },
        LinkToolApiModel link => new LinkToolDocumentModel(
            id: $"tool_{link.Name}",
            name: link.Name,
            type: link.Type,
            connector: link.Connector,
            description: link.Description,
            parameters: link.Parameters,
            attributes: link.Attributes,
            metadata: link.Metadata,

            operationId: operationId)
        {
            Template = link.Template,
        },
        _ => throw new NotSupportedException($"Unknown tool type for document model: {tool.Type}")
    };

    public static CommonPromptDocumentModel ToCommonPromptTool(ExtendedAgentCommonPromptApiModel prompt, string operationId)
    {
        {
            return new CommonPromptDocumentModel(
                  Id: $"commonprompt_{prompt.Name}",
                  Name: prompt.Name,
                  Prompt: prompt.Prompt,
                  Metadata: prompt.Metadata,
                  OperationId: operationId);
        }
        ;
    }

    public static CommonToolsListDocumentModel ToCommonToolsList(ExtendedAgentCommonToolsListApiModel commonToolsList, string operationId)
    {
        {
            return new CommonToolsListDocumentModel(
                  Id: $"commontoolslist_{commonToolsList.Name}",
                  Name: commonToolsList.Name,
                  CommonToolsList: commonToolsList.Tools,
                  Metadata: commonToolsList.Metadata,
                  OperationId: operationId);
        }
        ;
    }

    public static ConnectorDocumentModel ToDocumentConnector(ExtendedAgentConnectorApiModel connector, string operationId) => connector switch
    {
        KustoConnectorApiModel k => new KustoConnectorDocumentModel(
            id: $"connector_{k.Name}",
            name: k.Name,
            type: k.Type,
            metadata: k.Metadata,
            description: k.Description ?? string.Empty,
            auth: k.Auth,
            enabled: k.Enabled,
            operationId: operationId)
        {
            ClusterUrl = k.ClusterUri,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            RegionalClusterGroups = k.RegionalClusterGroups
        },
        _ => throw new NotSupportedException($"Unknown connector type for document model: {connector.Type}")
    };

    public static AgentDocumentModel ToDocumentAgent(ExtendedAgentApiModel api, string operationId)
    {
        return new AgentDocumentModel(
            Id: $"agent_{api.Name}",
            Name: api.Name,
            Instructions: api.Instructions,
            HandoffDescription: api.HandoffDescription,
            Handoffs: api.Handoffs,
            Tools: api.Tools,
            Connectors: api.Connectors,
            AllowParallelToolCalls: api.AllowParallelToolCalls,
            AgentsAsTools: api.AgentsAsTools,
            MaxReflectionCount: api.MaxReflectionCount,
            CriticPromptPath: api.CriticPromptPath,
            CriticOnHandOff: api.CriticOnHandOff,
            CustomReflectionNote: api.CustomReflectionNote,
            CommonPrompts: api.CommonPrompts,

            CommonTools: api.CommonTools,
            DisableDocumentRetrieval: api.DisableDocumentRetrieval,
            EnableHandoffPromptOverride: api.EnableHandoffPromptOverride,
            UserPromptOverride: api.UserPromptOverride,
            HandoffPromptOverride: api.HandoffPromptOverride,
            InstructionsOverride: api.InstructionsOverride,
            Temperature: api.Temperature,
            // Workflow agent properties
            AgentType: api.AgentType,
            ParameterExtractionAgent: api.ParameterExtractionAgent,
            OrchestrationStartAgents: api.OrchestrationStartAgents,
            ResultSummarizationPrompt: api.ResultSummarizationPrompt,
            NextAgentMappings: api.NextAgentMappings,
            OutputType: api.OutputType,
            Metadata: api.Metadata,
            OperationId: operationId);
    }

    public static PlugInConfigDocumentModel ToDocumentConfig(PluginConfigDeploymentModel config, string operationId) => new
      PlugInConfigDocumentModel(
           Id: $"config_{config.Spec.PluginName}",
           Name: $"config_{config.Spec.PluginName}",
         Config: config.Spec.Config,
           Metadata: config.Metadata,
           OperationId: operationId);

    public static ExtendedAgentToolApiModel ToApiTool(ToolDocumentModel tool) => tool switch
    {
        KustoToolDocumentModel k => new KustoToolApiModel
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
            ClusterHint = k.ClusterHint
        },
        LinkToolDocumentModel linkToolDefinition => new LinkToolApiModel
        {
            Name = linkToolDefinition.Name,
            Type = linkToolDefinition.Type,
            Description = linkToolDefinition.Description,
            Parameters = linkToolDefinition.Parameters,
            Attributes = linkToolDefinition.Attributes,
            Metadata = linkToolDefinition.Metadata,
            Template = linkToolDefinition.Template,
        },
        _ => throw new NotSupportedException($"Unknown tool document type: {tool.Type}")
    };

    public static ExtendedAgentToolApiModel ToApiTool(YamlToolDefinitionBase tool) => tool switch
    {
        KustoToolDefinition k => new KustoToolApiModel
        {
            Name = k.Name,
            Type = k.Type,
            Connector = k.Connector,
            Description = k.Description,
            Parameters = k.Parameters,
            Attributes = k.Attributes,
            Metadata = k.Metadata,
            //Mode = (KustoExecutionMode)k.Mode,
            Function = k.Function,
            Query = k.Query,
            File = k.File,
            Database = k.Database,
            ClusterHint = k.ClusterHint
        },
        LinkToolDefinition linkToolDefinition => new LinkToolApiModel
        {
            Name = linkToolDefinition.Name,
            Type = linkToolDefinition.Type,
            Description = linkToolDefinition.Description,
            Parameters = linkToolDefinition.Parameters,
            Attributes = linkToolDefinition.Attributes,
            Metadata = linkToolDefinition.Metadata,
            Template = linkToolDefinition.Template,
        },
        _ => throw new NotSupportedException($"Unknown tool document type: {tool.Type}")
    };

    public static ExtendedAgentConnectorApiModel ToApiConnector(ConnectorDocumentModel connector) => connector switch
    {
        KustoConnectorDocumentModel k => new KustoConnectorApiModel
        {
            Name = k.Name,
            Type = k.Type,
            Enabled = k.Enabled,
            Description = k.Description,
            Auth = k.Auth,
            Metadata = k.Metadata,
            ClusterUri = k.ClusterUrl,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            //RegionalClusterGroups = k.RegionalClusterGroups
        },
        _ => throw new NotSupportedException($"Unknown connector document type: {connector.Type}")
    };

    public static ExtendedAgentConnectorApiModel ToApiConnector(DataConnectorDefinitionBase connector) => connector switch
    {
        KustoConnector k => new KustoConnectorApiModel
        {
            Name = k.Name,
            Type = k.Type,
            Enabled = k.Enabled,
            Description = k.Description,
            Auth = k.Auth,
            Metadata = k.Metadata,
            ClusterUri = k.ClusterUrl,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            RegionalClusterGroups = k.RegionalClusterGroups
        },
        _ => throw new NotSupportedException($"Unknown connector document type: {connector.Type}")
    };

    public static ExtendedAgentApiModel ToApiAgent(AgentDocumentModel doc) => new ExtendedAgentApiModel
    {
        Name = doc.Name,
        Instructions = doc.Instructions,
        HandoffDescription = doc.HandoffDescription,
        Handoffs = doc.Handoffs,
        Tools = doc.Tools,
        Connectors = doc.Connectors,
        AllowParallelToolCalls = doc.AllowParallelToolCalls,
        AgentsAsTools = doc.AgentsAsTools,
        MaxReflectionCount = doc.MaxReflectionCount,
        CriticPromptPath = doc.CriticPromptPath,
        CriticOnHandOff = doc.CriticOnHandOff,
        CustomReflectionNote = doc.CustomReflectionNote,
        CommonPrompts = doc.CommonPrompts,
        Temperature = doc.Temperature,
        OutputType = doc.OutputType,
        // Workflow agent properties
        AgentType = doc.AgentType,
        ParameterExtractionAgent = doc.ParameterExtractionAgent,
        OrchestrationStartAgents = doc.OrchestrationStartAgents,
        ResultSummarizationPrompt = doc.ResultSummarizationPrompt,
        NextAgentMappings = doc.NextAgentMappings
    };

    public static ToolDocumentModel ToDocumentTool(YamlToolDefinitionBase runtime, string operationId)
        => ToDocumentTool(ToApiTool(runtime), operationId);

    public static ConnectorDocumentModel ToDocumentConnector(DataConnectorDefinitionBase runtime, string operationId)
        => ToDocumentConnector(ToApiConnector(runtime), operationId);

    public static AgentDocumentModel ToDocumentAgent(YamlAgentDescriptor runtime, string operationId)
        => ToDocumentAgent(ToApiAgent(runtime), operationId);

    public static YamlToolDefinitionBase ToRuntimeTool(ToolDocumentModel doc)
        => ToRuntimeTool(ToApiTool(doc));

    public static DataConnectorDefinitionBase ToRuntimeConnector(ConnectorDocumentModel doc)
        => ToRuntimeConnector(ToApiConnector(doc));

    public static YamlAgentDescriptor ToRuntimeAgent(AgentDocumentModel doc)
        => ToRuntimeAgent(ToApiAgent(doc));
}
