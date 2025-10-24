// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

/// <summary>
/// Provides agents with experiment variants applied based on the current thread context.
/// This interface enables per-thread experiment assignment while caching agent graphs
/// for efficiency.
/// </summary>
public interface IAgentProvider<TContext> where TContext : class
{
    /// <summary>
    /// Gets an agent by name with experiment variants applied based on the threadId.
    /// </summary>
    /// <param name="name">The name of the agent to retrieve</param>
    /// <param name="threadId">Optional thread identifier for per-thread experiments. If null, only global experiments are applied.</param>
    /// <returns>The agent with appropriate experiment variants applied</returns>
    Agent<TContext> GetAgent(string name, string? threadId = null);

    /// <summary>
    /// Gets the active experiment variants for a given thread context.
    /// Useful for telemetry and debugging.
    /// </summary>
    /// <param name="threadId">Optional thread identifier. If null, returns only global experiment variants.</param>
    /// <returns>A dictionary mapping experiment IDs to their active variants</returns>
    IReadOnlyDictionary<string, Variant> GetActiveVariants(string? threadId = null);
}
