// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
public record ExtendedAgentToolDocument(
    string Id,
    string Name,
    string Type,
    string Connector,
    string Description,
    List<YamlParameter> Parameters,
    List<string> Attributes,
    string? YamlContent,
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgentTool";
    public string PartitionKey => Name; // Use tool name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    // Conversion to/from domain model
}
