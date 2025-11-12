// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Extensions for Agent to enable using an agent as a tool by another agent.
/// </summary>
public static class AgentExtensions
{
    /// <summary>
    /// Creates an AIFunction that allows an agent to be used as a tool by another agent.
    /// </summary>
    /// <typeparam name="TContext">The context type of the agent.</typeparam>
    /// <param name="agent">The agent to be used as a tool.</param>
    /// <param name="name">Optional custom name for the tool. Defaults to the agent's name converted to lowercase with spaces replaced by underscores.</param>
    /// <param name="description">Optional custom description for the tool. Defaults to a standard description with the agent's name.</param>
    /// <param name="maxTurns">Maximum number of turns the agent can take. Defaults to 10.</param>
    /// <returns>An AIFunction that can be added to another agent's tools.</returns>
    public static AgentAsTool<TContext> AsTool<TContext>(
        this Agent<TContext> agent,
        string inputDescription,
        string? name = null,
        string? description = null,
        int maxTurns = 10
    ) where TContext : class
    {
        return AgentAsTool<TContext>.Create(
            agent: agent,
            toolNameOverride: name,
            toolDescriptionOverride: description,
            maxTurns: maxTurns,
            inputDescription: inputDescription
        );
    }
}
