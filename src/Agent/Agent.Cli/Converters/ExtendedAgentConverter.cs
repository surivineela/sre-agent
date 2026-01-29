// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Converters
{
    /// <summary>
    /// Converter for transforming ExtendedAgentV1 to ExtendedAgentV2.
    /// Handles the migration from V1 format to V2 format.
    /// </summary>
    public static class ExtendedAgentConverter
    {
        /// <summary>
        /// Converts ExtendedAgentV1 to ExtendedAgentV2.
        /// Copies all properties from V1 spec to V2 spec and preserves metadata.
        /// </summary>
        public static Models.ExtendedAgentV2 ConvertToV2(Models.ExtendedAgentV1 v1)
        {
            return new Models.ExtendedAgentV2
            {
                Metadata = new Models.ResourceMetadataModel
                {
                    Name = v1.Spec.Name,
                    Owner = v1.Metadata?.Owner,
                    Tags = v1.Metadata?.Tags
                },
                Spec = new Models.ExtendedAgentSpecV2
                {
                    Instructions = v1.Spec.Instructions,
                    HandoffDescription = v1.Spec.HandoffDescription,
                    Handoffs = v1.Spec.Handoffs,
                    Tools = v1.Spec.Tools,
                    AllowParallelToolCalls = v1.Spec.AllowParallelToolCalls,
                    MaxReflectionCount = v1.Spec.MaxReflectionCount,
                    CriticPromptPath = v1.Spec.CriticPromptPath,
                    CriticOnHandoff = v1.Spec.CriticOnHandoff,
                    CustomReflectionNote = v1.Spec.CustomReflectionNote,
                    CommonPrompts = v1.Spec.CommonPrompts,
                    Temperature = v1.Spec.Temperature,
                    OutputType = v1.Spec.OutputType,
                    EnableVanillaMode = v1.Spec.EnableVanillaMode,
                    EnableSkills = v1.Spec.EnableSkills,
                    AddSystemSkills = v1.Spec.AddSystemSkills,
                    AllowedSkills = v1.Spec.AllowedSkills
                }
            };
        }
    }
}
