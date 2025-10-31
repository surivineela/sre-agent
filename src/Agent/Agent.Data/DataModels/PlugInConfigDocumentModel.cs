// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for Extended Agent Tool storage
/// </summary>
public record PlugInConfigDocumentModel(
    ResourceMetadata Metadata,
    PluginConfigSpec Spec
) : ICosmosDocument
{
    public string Id => Metadata.Id ?? Spec.Name;
    public string DocumentType => "PluginConfig";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    [JsonIgnore]
    public string Name => Spec.Name;
}

/// <summary>
/// Spec fields for plugin config documents
/// </summary>
public class PluginConfigSpec
{
    public string Name { get; set; } = string.Empty;

    public IDictionary<string, object>? Config { get; set; }
}
