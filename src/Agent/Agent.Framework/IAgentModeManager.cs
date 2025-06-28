// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public interface IAgentModeManager<TContext>
    where TContext : class
{
    /// <summary>
    /// Gets the default mode configurator (from DI container)
    /// </summary>
    IAgentModeConfigurator<TContext> DefaultModeConfigurator { get; }

    /// <summary>
    /// Applies a specific mode to an agent at runtime
    /// </summary>
    void ApplyModeToAgent(
        Agent<TContext> agent,
        string? agentMode);

    /// <summary>
    /// Gets the list of available agent modes
    /// </summary>
    IEnumerable<string> GetAvailableModes();
}
