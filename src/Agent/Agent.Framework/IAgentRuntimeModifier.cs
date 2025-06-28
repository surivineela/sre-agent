// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public interface IAgentRuntimeModifier<TContext> where TContext : class
{
    /// <summary>
    /// Applies runtime modifications to the agent before each turn
    /// </summary>
    /// <returns>A ChatMessage explaining the agent mode change if mode was changed, null otherwise</returns>
    Task<ChatMessage?> ApplyRuntimeModificationsAsync(RunContextWrapper<TContext> contextWrapper, Agent<TContext> agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the agent mode for the thread
    /// </summary>
    /// <param name="context">The context containing thread information</param>
    /// <param name="agentMode">The agent mode to set</param>
    /// <param name="notifyUser">Whether to notify the user about the mode change</param>
    Task SetAgentMode(TContext context, string? agentMode, bool notifyUser = true);

    /// <summary>
    /// Gets current runtime modifications for the thread
    /// </summary>
    AgentRuntimeModifications? GetAgentModifications(TContext context);

    /// <summary>
    /// Gets the agent mode, returning the modified mode if it exists, otherwise the default mode from settings
    /// </summary>
    string GetThreadAgentMode(TContext context);
}

public class AgentRuntimeModifications
{
    public string? AgentMode { get; set; }
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}
