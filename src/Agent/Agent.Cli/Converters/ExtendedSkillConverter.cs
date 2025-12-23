// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Converters
{
    /// <summary>
    /// Converter for transforming ExtendedSkillV1 to ExtendedSkillV2.
    /// Handles the migration from V1 format (flat structure) to V2 format (kind/metadata/spec structure).
    /// </summary>
    public static class ExtendedSkillConverter
    {
        /// <summary>
        /// Converts ExtendedSkillV1 to ExtendedSkillV2.
        /// Maps V1 flat properties to V2 metadata and spec structure.
        /// </summary>
        public static Models.ExtendedSkillV2 ConvertToV2(Models.ExtendedSkillV1 v1)
        {
            return new Models.ExtendedSkillV2
            {
                Metadata = new Models.ResourceMetadataModel
                {
                    Name = v1.Name
                },
                Spec = new Models.SkillSpecV2
                {
                    Description = v1.Description,
                    Tools = v1.Tools,
                    SkillContent = v1.SkillMdContent,
                    AdditionalFiles = v1.AdditionalFiles?.Select(f => new Models.SkillAdditionalFileV2
                    {
                        FilePath = f.FilePath,
                        Content = f.Content
                    }).ToList()
                }
            };
        }
    }
}
