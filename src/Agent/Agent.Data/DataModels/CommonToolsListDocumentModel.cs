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
    public string Id => Metadata.Id ?? Spec.Name;
    public string DocumentType => "CommonToolsList";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    [JsonIgnore]
    public string Name => Spec.Name;

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
    public string Name { get; set; } = string.Empty;

    public List<string> CommonToolsList { get; set; } = new();
}
