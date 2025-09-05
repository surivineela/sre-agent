// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent CommonPrompt storage
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using Agent.Framework.Reasoning.Models;

public record CommonPromptDocumentModel(
    string Id,
    string Name,
    string Prompt,
    YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "CommonPrompt";
    public string PartitionKey => Name; // Use common prompt name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;
}
