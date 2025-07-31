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
using System.Collections.Generic;
using Agent.Framework.Reasoning.Models;

public record   ToolDocumentModel(
    string Id,
    string Name,
    string Type,
    string Connector,
    string Description,
    List<YamlParameter> Parameters,
    List<string> Attributes,
    
YamlMetadata Metadata,
    string OperationId
) : ICosmosDocument
{
    public string DocumentType => "ExtendedAgentTool";
    public string PartitionKey => Name; // Use tool name as partition key for easy querying
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

}
