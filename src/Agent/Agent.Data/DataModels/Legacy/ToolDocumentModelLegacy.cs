// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels.Legacy;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage (Legacy)
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Agent.Framework;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KustoToolDocumentModelLegacy), "KustoTool")]
[JsonDerivedType(typeof(LinkToolDocumentModelLegacy), "LinkTool")]
public record ToolDocumentModelLegacy(
    string Id,
    string Name,
    string Type,
    string Connector,
    string Description,
    List<YamlParameter> Parameters,
    List<string> Attributes,

YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument, ILegacyModelConverter<ToolDocumentModel>
{
    public string DocumentType => "ExtendedAgentTool";
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

    public ToolSpec ToResourceSpec()
    {
        return new ToolSpec
        {
            Name = Name,
            Type = Type,
            Connector = Connector,
            Description = Description,
            Parameters = Parameters,
            Attributes = Attributes
        };
    }

    public virtual ToolDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();
        return new ToolDocumentModel(metadata, spec);
    }
}
