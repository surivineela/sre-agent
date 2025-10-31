// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;

namespace Agent.Data.DataModels.Legacy;

/// <summary>
/// Cosmos DB document for storing a list of common tool names (Legacy).
/// </summary>
public record CommonToolsListDocumentModelLegacy(
    string Id,
    string Name,
    List<string> CommonToolsList,
    string OperationId,
    YamlMetadata Metadata
) : ICosmosDocument, ILegacyModelConverter<CommonToolsListDocumentModel>
{
    public string DocumentType => "CommonToolsList";
    public string PartitionKey => Name; // Use Id as partition key for easy querying
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

    public CommonToolListSpec ToResourceSpec()
    {
        return new CommonToolListSpec
        {
            Name = Name,
            CommonToolsList = CommonToolsList
        };
    }

    public CommonToolsListDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();
        return new CommonToolsListDocumentModel(metadata, spec);
    }
}
