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
    public const string DocumentTypeName = "PluginConfig";

    public string Id => GetId(Name);
    public string DocumentType => DocumentTypeName;
    public string PartitionKey => GetPartitionKey();
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    [JsonIgnore]
    public string Name => Metadata.Name;

    public static string GetId(string name)
    {
        return name.ToLowerInvariant();
    }

    public static string GetPartitionKey()
    {
        return DocumentTypeName;
    }
}

/// <summary>
/// Spec fields for plugin config documents
/// </summary>
public class PluginConfigSpec
{
    public IDictionary<string, object>? Config { get; set; }
}
