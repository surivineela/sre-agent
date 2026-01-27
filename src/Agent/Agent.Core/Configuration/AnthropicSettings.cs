// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class AnthropicSettings : IValidatableObject
    {
        public string ApiKey { get; set; } = string.Empty;
        // Anthropic API endpoint base URL.
        // Examples:
        // - Direct Anthropic: https://api.anthropic.com/
        // - Microsoft Foundry proxy: https://<your-foundry-instance>.services.ai.azure.com/anthropic/
        public string BaseUrl { get; set; } = string.Empty;
        // Optional API version passed as a query parameter (apiVersion=...)
        // Obsolete: This is no longer needed as the Anthropic C# SDK hard codes it to be 2023-06-01, https://github.com/anthropics/anthropic-sdk-csharp/blob/main/src/Anthropic/Core/ParamsBase.cs#L30.
        public string ApiVersion { get; set; } = string.Empty;
        public int MaxRetries { get; set; } = 3;
        public bool ExtendedThinkingEnabled { get; set; } = true;
        // Only enable interleaved thinking when ExtendedThinkingEnabled is also true
        public bool InterleavedThinkingEnabled { get; set; } = true;
        public int MaxOutputTokens { get; set; } = 30000;
        // ThinkingBudgetTokens must be less than MaxOutputTokens
        public int ThinkingBudgetTokens { get; set; } = 4000;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            // Only validate when BaseUrl is configured (i.e., Anthropic is being used)
            if (!string.IsNullOrEmpty(BaseUrl))
            {
                if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out _))
                {
                    yield return new ValidationResult(
                        $"BaseUrl must be an absolute URI when configured (value: '{BaseUrl}').",
                        new[] { nameof(BaseUrl) });
                }

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
