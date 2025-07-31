// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Microsoft.Extensions.AI;

/// <summary>
/// Defines the contract for a deferred tool function.
/// </summary>
public interface IDeferredToolFunction
{
    /// <summary>
    /// Creates and returns the executable AIFunction.
    /// </summary>
    /// <param name="threadId">The optional thread ID for context.</param>
    /// <returns>An <see cref="AIFunction"/>.</returns>
    AIFunction GetToolFunction(Guid? threadId = null);
    
    /// <summary>
    /// Creates and returns the executable AIFunction with agent mode support.
    /// </summary>
    /// <param name="threadId">The optional thread ID for context.</param>
    /// <param name="agentMode">The agent mode (e.g., "Chat", "ReadOnly", "Review", "Autonomous").</param>
    /// <returns>An <see cref="AIFunction"/>.</returns>
    AIFunction GetToolFunction(Guid? threadId, string? agentMode);
    
    string GetPluginCategory();
    string GetPluginResourceType();
    string GetPluginName();
}
