// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins;
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
    string GetPluginCategory();

    string GetPluginResourceType();

    string GetPluginName();
}
