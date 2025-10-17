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
/// <summary>
/// A factory for creating flattened Cosmos DB documents as dictionaries.
/// </summary>
public class AgentConfigurationDocumentModel
{
    public required string Id { get; set; }                      // Same as agent name
    public required string ApiVersion { get; set; }              // From YAML
    public required AgentDocumentModel Agent { get; set; }          // High-level metadata
    public required List<ToolDocumentModel> Tools { get; set; }  // Flattened tool properties
    public required List<ConnectorDocumentModel> Connectors { get; set; } // Flattened connector properties
}
