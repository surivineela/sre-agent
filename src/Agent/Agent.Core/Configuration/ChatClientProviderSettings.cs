// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Configuration
{
    /// <summary>
    /// Configuration for chat client provider service
    /// </summary>
    public class ChatClientProviderSettings
    {
        /// <summary>
        /// Embedding model name for vector generation
        /// </summary>
        public string EmbeddingModelName { get; set; } = string.Empty;

        /// <summary>
        /// Maximum number of burst requests allowed for rate limiting.
        /// Default is 20 if not specified.
        /// </summary>
        public int MaxBurstRequests { get; set; } = 20;

        /// <summary>
        /// Configuration for model selection based on scenario types.
        /// </summary>
        public ModelScenarioConfiguration ScenarioConfiguration { get; set; } = new();
    }
}
