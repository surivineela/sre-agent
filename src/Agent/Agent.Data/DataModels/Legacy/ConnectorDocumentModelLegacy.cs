// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels.Legacy;

using System.Text.Json.Serialization;

/// <summary>
/// Cosmos DB document for Extended Agent Connector storage (Legacy)
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using Agent.Framework;

/// <summary>
/// A factory for creating flattened Cosmos DB documents as dictionaries.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(KustoConnectorDocumentModelLegacy), "Kusto")]
public record ConnectorDocumentModelLegacy(
    string Id,
    string Name,
    [property: JsonIgnore] string Type,
    string Description,
    YamlMetadata Metadata,
    ConnectorAuthSettings Auth,
    bool Enabled,
    string OperationId
) : ICosmosDocument, ILegacyModelConverter<ConnectorDocumentModel>
{
    public string DocumentType => "ExtendedAgentConnector";
    public string PartitionKey => Name; // Use connector name as partition key for easy querying
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

    public ConnectorSpec ToResourceSpec()
    {
        return new ConnectorSpec
        {
            Name = Name,
            Type = Type,
            Description = Description,
            Auth = Auth,
            Enabled = Enabled
        };
    }

    public virtual ConnectorDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();
        return new ConnectorDocumentModel(metadata, spec);
    }
}

