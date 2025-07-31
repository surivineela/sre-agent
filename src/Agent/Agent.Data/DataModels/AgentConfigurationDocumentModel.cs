// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
/// <summary>
/// A factory for creating generic CosmosDocument wrappers from specific domain models.
/// </summary>
using System.Collections.Generic;
using System.ComponentModel;
using Agent.Framework;
using Agent.Framework.Reasoning.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

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
