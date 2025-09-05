// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Reasoning.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for storing a list of common tool names.
/// </summary>
public record CommonToolsListDocumentModel(
    string Id,
    string Name,
    List<string> CommonToolsList,
    string OperationId,
    YamlMetadata Metadata
) : ICosmosDocument
{
    public string DocumentType => "CommonToolsList";
    public string PartitionKey => Name; // Use Id as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;
}
