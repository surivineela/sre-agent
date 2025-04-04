// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration
{
    public class OpenAISettings
    {
        [Required]
        public string LLMDeploymentName { get; set; } = string.Empty;

        [Required]
        public string Endpoint { get; set; } = String.Empty;

        [Required]
        public string ApiKey { get; set; } = String.Empty;

        [Required]
        public string EmbeddingGeneratorDeploymentName { get; set; } = String.Empty;
    }
}

