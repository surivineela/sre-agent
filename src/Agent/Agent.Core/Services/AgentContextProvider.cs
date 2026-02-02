// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

/// <summary>
/// Provides access to the current agent's context information by extracting it from environment variables.
/// </summary>
public class AgentContextProvider : IAgentContextProvider
{
    private readonly ILogger<AgentContextProvider> _logger;
    private readonly bool _isProduction;
    private readonly Lazy<string?> _agentResourceId;

    public AgentContextProvider(
        ILogger<AgentContextProvider> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _isProduction = environment.IsProduction();
        _agentResourceId = new Lazy<string?>(ComputeAgentResourceId);
    }

    /// <inheritdoc/>
    public Task<string?> GetAgentResourceIdAsync()
    {
        return Task.FromResult(_agentResourceId.Value);
    }

    private string? ComputeAgentResourceId()
    {
        try
        {
            var subscriptionId = AgentNameHelper.GetSubscriptionId(_isProduction);
            var resourceGroup = AgentNameHelper.GetResourceGroupName(_isProduction);
            var agentName = AgentNameHelper.GetAgentName(_isProduction);

            // Remove the prefix unique ID part (everything after the last double dashes)
            var agentNameWithoutPrefix = agentName.Contains("--")
                ? agentName.Substring(0, agentName.LastIndexOf("--"))
                : agentName;

            var agentResourceId = $"/subscriptions/{subscriptionId}/resourcegroups/{resourceGroup}/providers/microsoft.app/agents/{agentNameWithoutPrefix}";

            _logger.LogInternalInformation("Computed agent resource ID: {AgentResourceId}", agentResourceId);
            return agentResourceId;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to compute agent resource ID from environment");
            return null;
        }
    }
}
