// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Threading.Tasks;

namespace Agent.Core.Interfaces;

/// <summary>
/// Provides access to the current agent's context information.
/// </summary>
public interface IAgentContextProvider
{
    /// <summary>
    /// Gets the ARM resource ID of the current agent.
    /// Format: /subscriptions/{subscriptionId}/resourcegroups/{resourceGroup}/providers/microsoft.app/agents/{agentName}
    /// </summary>
    /// <returns>The agent resource ID, or null if not available.</returns>
    Task<string?> GetAgentResourceIdAsync();
}
