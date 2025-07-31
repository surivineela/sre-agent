// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using Agent.Framework.Reasoning.Models;

/// <summary>
/// A factory for creating flattened Cosmos DB documents as dictionaries.
/// </summary>
public record ConnectorDocumentModel(
    string Id,
    string Name,
    string Type,
    string Description,
    YamlMetadata Metadata,
    ConnectorAuthSettings Auth,
    bool Enabled,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgentConnector";
    public string PartitionKey => Name; // Use connector name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;


}

