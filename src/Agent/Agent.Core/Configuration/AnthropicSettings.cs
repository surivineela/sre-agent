// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class AnthropicSettings : IValidatableObject
    {
        public string ApiKey { get; set; } = string.Empty;
        // For Microsoft Foundry, the BaseUrl looks like https://<your-foundry-instance>.services.ai.azure.com/anthropic/
        public string BaseUrl { get; set; } = string.Empty;
        public int MaxRetries { get; set; } = 3;
        public bool ExtendedThinkingEnabled { get; set; } = false;
        // Only enable interleaved thinking when ExtendedThinkingEnabled is also true
        public bool InterleavedThinkingEnabled { get; set; } = false;
        public int MaxOutputTokens { get; set; } = 10000;
        // ThinkingBudgetTokens must be less than MaxOutputTokens
        public int ThinkingBudgetTokens { get; set; } = 2000;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Only validate when BaseUrl is configured (i.e., Anthropic is being used)
            if (!string.IsNullOrEmpty(BaseUrl))
            {
                // Rule 1: If InterleavedThinkingEnabled is true, ExtendedThinkingEnabled must also be true
                if (InterleavedThinkingEnabled && !ExtendedThinkingEnabled)
                {
                    yield return new ValidationResult(
                        "InterleavedThinkingEnabled requires ExtendedThinkingEnabled to be true.",
                        new[] { nameof(InterleavedThinkingEnabled), nameof(ExtendedThinkingEnabled) });
                }

                // Rule 2: MaxOutputTokens must be greater than ThinkingBudgetTokens
                if (MaxOutputTokens <= ThinkingBudgetTokens)
                {
                    yield return new ValidationResult(
                        $"MaxOutputTokens ({MaxOutputTokens}) must be greater than ThinkingBudgetTokens ({ThinkingBudgetTokens}).",
                        new[] { nameof(MaxOutputTokens), nameof(ThinkingBudgetTokens) });
                }
            }
        }
    }
}
