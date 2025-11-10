// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Skills;

namespace Agent.Data.DataModels;

public record SkillDocumentModel(
    ResourceMetadata Metadata,
    SkillSpec Spec
) : ICosmosDocument
{
    public string Id => Metadata.Id ?? Spec.Name;
    public string DocumentType => "Skill";
    public string PartitionKey => Spec.Name;
    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    public SkillSpec ToRuntimeModel() =>
        new()
        {
            Name = Spec.Name,
            Description = Spec.Description,
            Tools = Spec.Tools,
            SkillMdContent = Spec.SkillMdContent,
            AdditionalFiles = Spec.AdditionalFiles
        };
}
