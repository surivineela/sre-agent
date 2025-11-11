// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Defines different AI model scenario types based on task requirements
/// </summary>
public enum ModelScenarioType
{
    /// <summary>
    /// Complex, multi-step reasoning where robustness > latency
    /// Prioritize depth, reliability, and chain-of-thought quality
    /// </summary>
    ReasoningHeavy,

    /// <summary>
    /// Good reasoning with low latency where speed > peak accuracy
    /// Models tuned for fast inference, "mini" variants trade accuracy for speed
    /// </summary>
    ReasoningFast,

    /// <summary>
    /// Mixed tasks with balanced accuracy, cost, and latency
    /// Chat-style models provide the best everyday balance
    /// </summary>
    GeneralPurpose,

    /// <summary>
    /// Lowest cost/latency with acceptable accuracy
    /// "Mini" models first; larger models drop in priority
    /// </summary>
    SmallFast,

    /// <summary>
    /// Handling long documents with stability over long inputs
    /// Prioritize models positioned for strong long-context stability
    /// </summary>
    LongContext,

    /// <summary>
    /// Grading, review, rubric-based assessment
    /// Consistency and calibration; speed is less critical
    /// </summary>
    Eval,

    /// <summary>
    /// Vector representations for semantic search/retrieval
    /// Dedicated embedding models
    /// </summary>
    Embedding
}
