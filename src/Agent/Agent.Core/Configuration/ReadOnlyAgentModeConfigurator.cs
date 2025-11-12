// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Configuration;

public class ReadOnlyAgentModeConfigurator<TContext> : IAgentModeConfigurator<TContext>
    where TContext : class
{
    private readonly ILogger<ReadOnlyAgentModeConfigurator<TContext>> _logger;

    public ReadOnlyAgentModeConfigurator(ILogger<ReadOnlyAgentModeConfigurator<TContext>> logger)
    {
        _logger = logger;
    }

    public bool AppliesToMode(string? agentMode)
    {
        return string.Equals(agentMode, "ReadOnly", StringComparison.OrdinalIgnoreCase);
    }

    public void ConfigureAgent(
        Agent<TContext> agent,
        IAgentDescriptor agentDescriptor,
        IReadOnlyDictionary<string, IPromptDescriptor> promptDescriptors)
    {
        if (promptDescriptors.TryGetValue("readonly", out var readOnlyPrompt))
        {
            agent.Instructions.AddCommonPrompt(readOnlyPrompt.Prompt);
            _logger.LogInternalInformation("Added readonly common prompt to agent {agentName} in ReadOnly mode.", agentDescriptor.Name);
        }
        else
        {
            _logger.LogInternalWarning("ReadOnly mode is enabled but readonly common prompt not found for agent {agentName}.", agentDescriptor.Name);
        }
    }
}
