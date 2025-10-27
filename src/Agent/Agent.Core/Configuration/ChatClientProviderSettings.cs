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
        /// Comma-separated list of model deployment names to register
        /// Example: "gpt-4.1,gpt-5"
        /// </summary>
        public string ModelNames { get; set; } = string.Empty;

        /// <summary>
        /// Default model name for general-purpose tasks
        /// </summary>
        public string DefaultModelName { get; set; } = string.Empty;

        /// <summary>
        /// Model name optimized for reasoning tasks
        /// </summary>
        public string ReasoningModelName { get; set; } = string.Empty;

        /// <summary>
        /// Model name for fast responses
        /// </summary>
        public string FastModelName { get; set; } = string.Empty;

        /// <summary>
        /// Model name optimized for large context windows
        /// </summary>
        public string LargeContextModelName { get; set; } = string.Empty;

        /// <summary>
        /// Embedding model name for vector generation
        /// </summary>
        public string EmbeddingModelName { get; set; } = string.Empty;
    }
}
