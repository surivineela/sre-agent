// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Framework.Skills;

namespace Agent.Data.DataModels;

public record SkillDocumentModel(
    ResourceMetadata Metadata,
    SkillSpec Spec
) : ICosmosDocument
{
    public const string DocumentTypeName = "Skill";

    public string Id => GetId(Name);
    public string DocumentType => DocumentTypeName;
    public string PartitionKey => GetPartitionKey();

    [JsonIgnore]
    public string Name => Metadata.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    public static string GetId(string name)
    {
        return name.ToLowerInvariant();
    }

    public static string GetPartitionKey()
    {
        return DocumentTypeName;
    }

    public SkillSpec ToRuntimeModel() =>
        new()
        {
            Name = Metadata.Name,
            Description = Spec.Description,
            Tools = Spec.Tools,
            SkillMdContent = Spec.SkillMdContent,
            AdditionalFiles = Spec.AdditionalFiles
        };
}
