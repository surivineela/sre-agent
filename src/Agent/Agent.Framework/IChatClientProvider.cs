// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Provides access to different AI models for various scenarios
/// </summary>
public interface IChatClientProvider
{
    /// <summary>
    /// Checks if a model is supported
    /// </summary>
    /// <param name="modelName">The name of the model</param>
    /// <returns>True if the model is supported; otherwise, false</returns>
    bool IsModelSupported(string modelName);

    /// <summary>
    /// Gets the list of supported models
    /// </summary>
    /// <returns>List of supported model names</returns>
    IList<string> GetSupportedModels();

    /// <summary>
    /// Gets a model by its deployment name
    /// </summary>
    /// <param name="keyName">The key name of the model</param>
    /// <returns>The chat client for the specified model</returns>
    T GetModelByKey<T>(string keyName) where T : notnull;

    public IChatClient DefaultModel { get; }
    public IChatClient ReasoningModel { get; }
    public IChatClient FastModel { get; }
    public IChatClient LargeContextModel { get; }
    public IEmbeddingGenerator<string, Embedding<float>> EmbeddingModel { get; }
}
