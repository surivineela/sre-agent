// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Data.Tools;
using Agent.Plugins.Connector;
using Agent.Web.Models.ExtendedAgents;
using Agent.Framework.Models;

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
        McpTools = api.McpTools,
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
        McpTools = runtime.McpTools,
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
            new ResourceMetadata
            {
                Id = $"tool_{k.Name}",
                OperationId = operationId,
                Owner = k.Metadata?.Owner,
                Version = k.Metadata?.Version,
                Tags = k.Metadata?.Tags,
                UpdatedAt = k.Metadata?.UpdatedAt,
                CreatedAt = k.Metadata?.CreatedAt
            },
            new KustoToolSpec
            {
                Name = k.Name,
                Type = k.Type,
                Connector = k.Connector,
                Description = k.Description,
                Parameters = k.Parameters,
                Attributes = k.Attributes,
                Mode = k.Mode,
                Function = k.Function,
                Query = k.Query,
                File = k.File,
                Database = k.Database,
                ClusterHint = k.ClusterHint,
                RegionalClusterGroups = k.RegionalClusterGroups
            }),
        LinkToolApiModel link => new LinkToolDocumentModel(
            new ResourceMetadata
            {
                Id = $"tool_{link.Name}",
                OperationId = operationId,
                Owner = link.Metadata?.Owner,
                Version = link.Metadata?.Version,
                Tags = link.Metadata?.Tags,
                UpdatedAt = link.Metadata?.UpdatedAt,
                CreatedAt = link.Metadata?.CreatedAt
            },
            new LinkToolSpec
            {
                Name = link.Name,
                Type = link.Type,
                Connector = link.Connector,
                Description = link.Description,
                Parameters = link.Parameters,
                Attributes = link.Attributes,
                Template = link.Template
            }),
        _ => throw new NotSupportedException($"Unknown tool type for document model: {tool.Type}")
    };

    public static CommonPromptDocumentModel ToCommonPromptTool(ExtendedAgentCommonPromptApiModel prompt, string operationId)
    {
        return new CommonPromptDocumentModel(
            new ResourceMetadata
            {
                Id = $"commonprompt_{prompt.Name}",
                OperationId = operationId,
                Owner = prompt.Metadata?.Owner,
                Version = prompt.Metadata?.Version,
                Tags = prompt.Metadata?.Tags,
                UpdatedAt = prompt.Metadata?.UpdatedAt,
                CreatedAt = prompt.Metadata?.CreatedAt
            },
            new CommonPromptSpec
            {
                Name = prompt.Name,
                Prompt = prompt.Prompt
            });
    }

    public static CommonToolsListDocumentModel ToCommonToolsList(ExtendedAgentCommonToolsListApiModel commonToolsList, string operationId)
    {
        return new CommonToolsListDocumentModel(
            new ResourceMetadata
            {
                Id = $"commontoolslist_{commonToolsList.Name}",
                OperationId = operationId,
                Owner = commonToolsList.Metadata?.Owner,
                Version = commonToolsList.Metadata?.Version,
                Tags = commonToolsList.Metadata?.Tags,
                UpdatedAt = commonToolsList.Metadata?.UpdatedAt,
                CreatedAt = commonToolsList.Metadata?.CreatedAt
            },
            new CommonToolListSpec
            {
                Name = commonToolsList.Name,
                CommonToolsList = commonToolsList.Tools
            });
    }

    public static ConnectorDocumentModel ToDocumentConnector(ExtendedAgentConnectorApiModel connector, string operationId) => connector switch
    {
        KustoConnectorApiModel k => new KustoConnectorDocumentModel(
            new ResourceMetadata
            {
                Id = $"connector_{k.Name}",
                OperationId = operationId,
                Owner = k.Metadata?.Owner,
                Version = k.Metadata?.Version,
                Tags = k.Metadata?.Tags,
                UpdatedAt = k.Metadata?.UpdatedAt,
                CreatedAt = k.Metadata?.CreatedAt
            },
            new KustoConnectorSpec
            {
                Name = k.Name,
                Type = k.Type,
                Description = k.Description ?? string.Empty,
                Auth = k.Auth,
                Enabled = k.Enabled,
                ClusterUrl = k.ClusterUri,
                Database = k.Database,
                ClusterHint = k.ClusterHint,
                RegionalClusterGroups = k.RegionalClusterGroups
            }),
        _ => throw new NotSupportedException($"Unknown connector type for document model: {connector.Type}")
    };

    public static AgentDocumentModel ToDocumentAgent(ExtendedAgentApiModel api, string operationId)
    {
        var spec = new AgentSpec
        {
            Name = api.Name,
            Instructions = api.Instructions,
            HandoffDescription = api.HandoffDescription,
            Handoffs = api.Handoffs,
            Tools = api.Tools,
            McpTools = api.McpTools,
            Connectors = api.Connectors,
            AllowParallelToolCalls = api.AllowParallelToolCalls,
            AgentsAsTools = api.AgentsAsTools,
            MaxReflectionCount = api.MaxReflectionCount,
            CriticPromptPath = api.CriticPromptPath,
            CriticOnHandOff = api.CriticOnHandOff,
            CustomReflectionNote = api.CustomReflectionNote,
            CommonPrompts = api.CommonPrompts,
            CommonTools = api.CommonTools,
            DisableDocumentRetrieval = api.DisableDocumentRetrieval,
            EnableHandoffPromptOverride = api.EnableHandoffPromptOverride,
            UserPromptOverride = api.UserPromptOverride,
            HandoffPromptOverride = api.HandoffPromptOverride,
            InstructionsOverride = api.InstructionsOverride,
            Temperature = api.Temperature,
            // Workflow agent properties
            AgentType = api.AgentType,
            ParameterExtractionAgent = api.ParameterExtractionAgent,
            OrchestrationStartAgents = api.OrchestrationStartAgents,
            ResultSummarizationPrompt = api.ResultSummarizationPrompt,
            NextAgentMappings = api.NextAgentMappings,
            OutputType = api.OutputType
        };

        var metadata = ResourceMetadata.FromYamlMetadata(api.Metadata, $"agent_{api.Name}", operationId);

        return new AgentDocumentModel(
            Metadata: metadata,
            Spec: spec
        );
    }

    public static PlugInConfigDocumentModel ToDocumentConfig(PluginConfigDeploymentModel config, string operationId)
    {
        var metadata = new ResourceMetadata
        {
            Id = $"config_{config.Spec.PluginName}",
            OperationId = operationId,
            Owner = config.Metadata.Owner,
            Version = config.Metadata.Version,
            Tags = config.Metadata.Tags,
            UpdatedAt = config.Metadata.UpdatedAt,
            CreatedAt = config.Metadata.CreatedAt
        };

        var spec = new PluginConfigSpec
        {
            Name = config.Spec.PluginName,
            Config = config.Spec.Config
        };

        return new PlugInConfigDocumentModel(metadata, spec);
    }

    public static ExtendedAgentToolApiModel ToApiTool(ToolDocumentModel tool) => tool switch
    {
        KustoToolDocumentModel k => new KustoToolApiModel
        {
            Name = k.Name,
            Type = k.Type,
            Connector = k.Spec.Connector ?? string.Empty,
            Description = k.Spec.Description,
            Parameters = k.Spec.Parameters ?? new List<YamlParameter>(),
            Attributes = k.Spec.Attributes ?? new List<string>(),
            Metadata = k.Metadata.ToYamlMetadata(),
            Mode = k.Spec.Mode,
            Function = k.Spec.Function,
            Query = k.Spec.Query,
            File = k.Spec.File,
            Database = k.Spec.Database,
            ClusterHint = k.Spec.ClusterHint
        },
        LinkToolDocumentModel linkToolDefinition => new LinkToolApiModel
        {
            Name = linkToolDefinition.Name,
            Type = linkToolDefinition.Type,
            Description = linkToolDefinition.Spec.Description,
            Parameters = linkToolDefinition.Spec.Parameters ?? new List<YamlParameter>(),
            Attributes = linkToolDefinition.Spec.Attributes ?? new List<string>(),
            Metadata = linkToolDefinition.Metadata.ToYamlMetadata(),
            Template = linkToolDefinition.Spec.Template,
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
            Mode = k.Mode,
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
            Enabled = k.Spec.Enabled,
            Description = k.Spec.Description,
            Auth = k.Spec.Auth ?? new ConnectorAuthSettings(),
            Metadata = k.Metadata.ToYamlMetadata(),
            ClusterUri = k.Spec.ClusterUrl,
            Database = k.Spec.Database,
            ClusterHint = k.Spec.ClusterHint,
            //RegionalClusterGroups = k.Spec.RegionalClusterGroups
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
        Name = doc.Spec.Name,
        Instructions = doc.Spec.Instructions ?? string.Empty,
        HandoffDescription = doc.Spec.HandoffDescription,
        Handoffs = doc.Spec.Handoffs ?? new List<string>(),
        Tools = doc.Spec.Tools ?? new List<string>(),
        McpTools = doc.Spec.McpTools ?? new List<string>(),
        Connectors = doc.Spec.Connectors ?? new List<string>(),
        AllowParallelToolCalls = doc.Spec.AllowParallelToolCalls ?? false,
        AgentsAsTools = doc.Spec.AgentsAsTools ?? new List<AgentsAsTools>(),
        MaxReflectionCount = doc.Spec.MaxReflectionCount ?? 0,
        CriticPromptPath = doc.Spec.CriticPromptPath ?? string.Empty,
        CriticOnHandOff = doc.Spec.CriticOnHandOff ?? false,
        CustomReflectionNote = doc.Spec.CustomReflectionNote ?? string.Empty,
        CommonPrompts = doc.Spec.CommonPrompts ?? new List<string>(),
        Temperature = doc.Spec.Temperature,
        OutputType = doc.Spec.OutputType,
        // Workflow agent properties
        AgentType = doc.Spec.AgentType,
        ParameterExtractionAgent = doc.Spec.ParameterExtractionAgent,
        OrchestrationStartAgents = doc.Spec.OrchestrationStartAgents,
        ResultSummarizationPrompt = doc.Spec.ResultSummarizationPrompt,
        NextAgentMappings = doc.Spec.NextAgentMappings
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
