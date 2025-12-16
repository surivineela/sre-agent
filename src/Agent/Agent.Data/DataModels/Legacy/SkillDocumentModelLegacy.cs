// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels.Legacy;

using Agent.Framework;
using Agent.Framework.Skills;

// This is a dummy class to leverage the LegacyDocumentModelConverter for name property migration
public record SkillDocumentModelLegacy(
    ResourceMetadata Metadata,
    SkillSpec Spec
) : ICosmosDocument, ILegacyModelConverter<SkillDocumentModel>
{
    public const string DocumentTypeName = "Skill";

    public string Id => $"skill_{Spec.Name.ToLowerInvariant()}";
    public string DocumentType => DocumentTypeName;
    public string PartitionKey => Spec.Name;

    public static string ContainerName => AgentDataConfiguration.ExtendedAgentContainerName;

    public SkillDocumentModel ToNewModel()
    {
        return new SkillDocumentModel(Metadata, Spec);
    }
}
