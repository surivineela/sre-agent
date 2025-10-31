// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels.Legacy;

/// <summary>
/// Cosmos DB document for Extended Agent Plugin Config storage (Legacy)
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using System.Collections.Generic;
using Agent.Framework;

public record PlugInConfigDocumentModelLegacy(
    string Id,
    string Name,
    IDictionary<string, object> Config,
    YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument, ILegacyModelConverter<PlugInConfigDocumentModel>
{
    public string DocumentType => "PluginConfig";
    public string PartitionKey => Name; // Use tool name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

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

    public PluginConfigSpec ToResourceSpec()
    {
        return new PluginConfigSpec
        {
            Name = Name,
            Config = Config
        };
    }

    public PlugInConfigDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();
        return new PlugInConfigDocumentModel(metadata, spec);
    }
}
