// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels.Legacy;

using Agent.Framework;
using Agent.Framework.Skills;

// This is a dummy class to leverage the LegacyDocumentModelConverter for name property migration
public record SkillDocumentModelLegacy : SkillDocumentModel, ILegacyModelConverter<SkillDocumentModel>
{
    public SkillDocumentModelLegacy(
        ResourceMetadata Metadata,
        SkillSpec Spec)
        : base(Metadata, Spec)
    {
    }

    public SkillDocumentModel ToNewModel()
    {
        return new SkillDocumentModel(Metadata, Spec);
    }
}
