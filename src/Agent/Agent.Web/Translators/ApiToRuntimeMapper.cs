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

    public static SkillDocumentModel ToDocumentSkill(SkillDeploymentModel skill, string operationId)
    {
        return new SkillDocumentModel(
            Metadata: new ResourceMetadata
            {
                Id = $"skill_{skill.Spec.Name}",
                OperationId = operationId,
                Owner = skill.Metadata.Owner,
                Version = skill.Metadata.Version,
                Tags = skill.Metadata.Tags,
                UpdatedAt = skill.Metadata.UpdatedAt,
                CreatedAt = skill.Metadata.CreatedAt
            },
            Spec: skill.Spec
        );
    }
}
