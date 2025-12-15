// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels.Legacy;

/// <summary>
/// Cosmos DB document for Extended Agent CommonPrompt storage (Legacy)
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using Agent.Framework;

public record CommonPromptDocumentModelLegacy(
    string Id,
    string Name,
    string Prompt,
    YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument, ILegacyModelConverter<CommonPromptDocumentModel>
{
    public string DocumentType => CommonPromptDocumentModel.DocumentTypeName;
    public string PartitionKey => Name; // Use common prompt name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    public ResourceMetadata ToResourceMetadata()
    {
        return new ResourceMetadata
        {
            Name = Name,
            Tags = Metadata?.Tags,
            UpdatedAt = Metadata?.UpdatedAt,
            CreatedAt = Metadata?.CreatedAt
        };
    }

    public CommonPromptSpec ToResourceSpec()
    {
        return new CommonPromptSpec
        {
            Prompt = Prompt
        };
    }

    public CommonPromptDocumentModel ToNewModel()
    {
        var metadata = ToResourceMetadata();
        var spec = ToResourceSpec();
        return new CommonPromptDocumentModel(metadata, spec);
    }
}
