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
    IReadOnlyList<string> GetAvailableModels();

    /// <summary>
    /// Gets a model by its deployment name
    /// </summary>
    /// <param name="keyName">The key name of the model</param>
    /// <returns>The chat client for the specified model</returns>
    T GetModelByKey<T>(string keyName) where T : notnull;

    /// <summary>
    /// Gets the best available model for a specific scenario type based on priority configuration
    /// </summary>
    /// <param name="scenarioType">The scenario type to get the model for</param>
    /// <returns>The chat client for the best matching model</returns>
    IChatClient GetBestModelByScenario(ModelScenarioType scenarioType);

    public IChatClient GeneralPurposeModel { get; }
    public IChatClient ReasoningHeavyModel { get; }
    public IChatClient ReasoningFastModel { get; }
    public IChatClient SmallFastModel { get; }
    public IChatClient EvalModel { get; }
    public IChatClient LargeContextModel { get; }
    public IEmbeddingGenerator<string, Embedding<float>> EmbeddingModel { get; }
}
