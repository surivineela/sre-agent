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
    public const string DocumentTypeName = "CommonPrompt";

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
    public string Prompt { get; set; } = string.Empty;
}
