// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Cli.Converters
{
    /// <summary>
    /// Converter for transforming common prompt configurations from V1 to V2 format.
    /// Handles migration of CommonPromptV1 (multiple prompts per file) to CommonPromptV2 (single prompt per file).
    /// </summary>
    public static class CommonPromptConverter
    {
        /// <summary>
        /// Converts a CommonPromptV1 to a list of CommonPromptV2 objects.
        /// Each prompt in the V1 common_prompts list becomes a separate V2 CommonPrompt.
        /// </summary>
        /// <param name="v1">The V1 common prompt to convert</param>
        /// <returns>List of CommonPromptV2 objects</returns>
        public static List<Models.CommonPromptV2> ConvertToV2(Models.CommonPromptV1 v1)
        {
            if (v1 == null) throw new ArgumentNullException(nameof(v1));

            var result = new List<Models.CommonPromptV2>();

            if (v1.Spec?.CommonPrompts == null || v1.Spec.CommonPrompts.Count == 0)
            {
                return result;
            }

            foreach (var promptItem in v1.Spec.CommonPrompts)
            {
                if (promptItem == null || string.IsNullOrWhiteSpace(promptItem.Name))
                {
                    continue;
                }

                var v2Prompt = new Models.CommonPromptV2
                {
                    Metadata = new Models.ResourceMetadataModel
                    {
                        Name = promptItem.Name,
                        Owner = v1.Metadata?.Owner,
                        Tags = v1.Metadata?.Tags != null ? new List<string>(v1.Metadata.Tags) : null
                    },
                    Spec = new Models.CommonPromptSpecV2
                    {
                        Prompt = promptItem.Prompt
                    }
                };

                result.Add(v2Prompt);
            }

            return result;
        }
    }
}
