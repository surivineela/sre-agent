// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Framework.Reasoning.Models;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Connector storage
/// </summary>
public record ExtendedAgentConnectorDocument(
    string Id,
    string Name,
    bool Enabled,
    string Type,
    string? Description,
    ConnectorAuthSettings Auth,
    YamlMetadata Metadata,
    string? YamlContent, // Store the full YAML content for polymorphic reconstruction
    DateTime? CreatedAt,
    DateTime? UpdatedAt,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgentConnector";
    public string PartitionKey => Name; // Use connector name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    // Conversion to/from domain model
    
}
