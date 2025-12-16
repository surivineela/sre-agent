// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;
using Agent.Data.Tools;
using Agent.Framework;
using Agent.Web.Models.ExtendedAgents;

namespace Agent.Web.Services;

public static class ApiToRuntimeMapper
{
    public static ToolDocumentModel ToDocumentTool(ExtendedAgentToolApiModel tool, string operationId) => tool switch
    {
        KustoToolApiModel k => new KustoToolDocumentModel(
            new ResourceMetadata
            {
                Name = k.Name,
                Tags = k.Metadata?.Tags,
                UpdatedAt = k.Metadata?.UpdatedAt,
                CreatedAt = k.Metadata?.CreatedAt
            },
            new KustoToolSpec
            {
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
                RegionalClusterGroups = k.RegionalClusterGroups,
                DisplayOptions = k.DisplayOptions,
                ToolMode = k.ToolMode
            }),
        LinkToolApiModel link => new LinkToolDocumentModel(
            new ResourceMetadata
            {
                Name = link.Name,
                Tags = link.Metadata?.Tags,
                UpdatedAt = link.Metadata?.UpdatedAt,
                CreatedAt = link.Metadata?.CreatedAt
            },
            new LinkToolSpec
            {
                Type = link.Type,
                Connector = link.Connector,
                Description = link.Description,
                Parameters = link.Parameters,
                Attributes = link.Attributes,
                Template = link.Template,
                ToolMode = link.ToolMode
            }),
        PythonToolApiModel py => new PythonToolDocumentModel(
            new ResourceMetadata
            {
                Name = py.Name,
                Tags = py.Metadata?.Tags,
                UpdatedAt = py.Metadata?.UpdatedAt,
                CreatedAt = py.Metadata?.CreatedAt
            },
            new PythonToolSpec
            {
                Type = "PythonFunctionTool",
                Description = py.Description,
                Parameters = py.Parameters,
                Attributes = py.Attributes,
                FunctionCode = py.FunctionCode,
                TimeoutSeconds = py.TimeoutSeconds,
                Dependencies = py.Dependencies,
                ToolMode = py.ToolMode
            }),
        _ => throw new NotSupportedException($"Unknown tool type for document model: {tool.Type}")
    };

    public static CommonPromptDocumentModel ToCommonPromptTool(ExtendedAgentCommonPromptApiModel prompt, string operationId)
    {
        return new CommonPromptDocumentModel(
            new ResourceMetadata
            {
                Name = prompt.Name,
                Tags = prompt.Metadata?.Tags,
                UpdatedAt = prompt.Metadata?.UpdatedAt,
                CreatedAt = prompt.Metadata?.CreatedAt
            },
            new CommonPromptSpec
            {
                Prompt = prompt.Prompt
            });
    }

    public static CommonToolsListDocumentModel ToCommonToolsList(ExtendedAgentCommonToolsListApiModel commonToolsList, string operationId)
    {
        return new CommonToolsListDocumentModel(
            new ResourceMetadata
            {
                Name = commonToolsList.Name,
                Tags = commonToolsList.Metadata?.Tags,
                UpdatedAt = commonToolsList.Metadata?.UpdatedAt,
                CreatedAt = commonToolsList.Metadata?.CreatedAt
            },
            new CommonToolListSpec
            {
                CommonToolsList = commonToolsList.Tools
            });
    }

    public static ConnectorDocumentModel ToDocumentConnector(ExtendedAgentConnectorApiModel connector, string operationId) => connector switch
    {
        KustoConnectorApiModel k => new KustoConnectorDocumentModel(
            new ResourceMetadata
            {
                Name = k.Name,
                Tags = k.Metadata?.Tags,
                UpdatedAt = k.Metadata?.UpdatedAt,
                CreatedAt = k.Metadata?.CreatedAt
            },
            new KustoConnectorSpec
            {
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

    public static PlugInConfigDocumentModel ToDocumentConfig(PluginConfigDeploymentModel config, string operationId)
    {
        var metadata = new ResourceMetadata
        {
            Name = config.Spec.PluginName,
            Tags = config.Metadata.Tags,
            UpdatedAt = config.Metadata.UpdatedAt,
            CreatedAt = config.Metadata.CreatedAt
        };

        var spec = new PluginConfigSpec
        {
            Config = config.Spec.Config
        };

        return new PlugInConfigDocumentModel(metadata, spec);
    }

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
            ToolMode = k.ToolMode,
            Mode = k.Mode,
            Function = k.Function,
            Query = k.Query,
            File = k.File,
            Database = k.Database,
            ClusterHint = k.ClusterHint,
            DisplayOptions = k.DisplayOptions
        },
        LinkToolDefinition linkToolDefinition => new LinkToolApiModel
        {
            Name = linkToolDefinition.Name,
            Type = linkToolDefinition.Type,
            Description = linkToolDefinition.Description,
            Parameters = linkToolDefinition.Parameters,
            Attributes = linkToolDefinition.Attributes,
            Metadata = linkToolDefinition.Metadata,
            ToolMode = linkToolDefinition.ToolMode,
            Template = linkToolDefinition.Template,
        },
        PythonFunctionToolDefinition py => new PythonToolApiModel
        {
            Name = py.Name,
            Type = py.Type,
            Description = py.Description,
            Parameters = py.Parameters,
            Attributes = py.Attributes,
            Metadata = py.Metadata,
            ToolMode = py.ToolMode,
            FunctionCode = py.FunctionCode,
            TimeoutSeconds = py.TimeoutSeconds,
            Dependencies = py.Dependencies,
        },
        _ => throw new NotSupportedException($"Unknown tool document type: {tool.Type}")
    };

    public static SkillDocumentModel ToDocumentSkill(SkillDeploymentModel skill, string operationId)
    {
        return new SkillDocumentModel(
            Metadata: new ResourceMetadata
            {
                Name = skill.Spec.Name,
                Tags = skill.Metadata.Tags,
                UpdatedAt = skill.Metadata.UpdatedAt,
                CreatedAt = skill.Metadata.CreatedAt
            },
            Spec: skill.Spec
        );
    }
}
