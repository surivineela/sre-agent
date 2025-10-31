// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework;

namespace Agent.Data.DataModels;

/// <summary>
/// New CommonPromptDocumentModel with Metadata and Spec properties (v2)
/// </summary>
public record CommonPromptDocumentModel(
    ResourceMetadata Metadata,
    CommonPromptSpec Spec
) : ICosmosDocument
{
    public string Id => Metadata.Id ?? Spec.Name;
    public string DocumentType => "CommonPrompt";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    [JsonIgnore]
    public string Name => Spec.Name;

    #region Conversion between runtime and data model
    public YamlPromptDescriptor ToYamlPromptDescriptor() => new YamlPromptDescriptor
    {
        Name = Name,
        Prompt = Spec.Prompt
    };
    #endregion
}

/// <summary>
/// Spec fields for common prompt documents
/// </summary>
public class CommonPromptSpec
{
    public string Name { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;
}
