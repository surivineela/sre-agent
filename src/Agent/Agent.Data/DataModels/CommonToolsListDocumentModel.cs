// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// Cosmos DB document for storing a list of common tool names.
/// </summary>
public record CommonToolsListDocumentModel(
    ResourceMetadata Metadata,
    CommonToolListSpec Spec
) : ICosmosDocument
{
    public const string DocumentTypeName = "CommonToolsList";

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

    #region Conversion between runtime and data model
    public YamlCommonToolsDescriptor ToRuntimeToolsList() => new YamlCommonToolsDescriptor
    {
        Name = Name,
        Tools = Spec.CommonToolsList,
    };
    #endregion
}

/// <summary>
/// Spec fields for common tool list documents
/// </summary>
public class CommonToolListSpec
{
    public List<string> CommonToolsList { get; set; } = new();
}
