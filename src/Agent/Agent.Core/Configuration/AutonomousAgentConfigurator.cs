// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Configuration;

public class AutonomousAgentModeConfigurator<TContext> : IAgentModeConfigurator<TContext>
    where TContext : class
{
    private readonly ILogger<AutonomousAgentModeConfigurator<TContext>> _logger;

    public AutonomousAgentModeConfigurator(ILogger<AutonomousAgentModeConfigurator<TContext>> logger)
    {
        _logger = logger;
    }

    public bool AppliesToMode(string? agentMode)
    {
        return string.Equals(agentMode, "Autonomous", StringComparison.OrdinalIgnoreCase);
    }

    public void ConfigureAgent(
        Agent<TContext> agent,
        IAgentDescriptor agentDescriptor,
        IReadOnlyDictionary<string, IPromptDescriptor> promptDescriptors)
    {
        if (promptDescriptors.TryGetValue("autonomous", out var autonomousPrompt))
        {
            agent.Instructions.AddCommonPrompt(autonomousPrompt.Prompt);
            _logger.LogInternalInformation("Added autonomous common prompt to agent {agentName} due in autonomous mode.", agentDescriptor.Name);
        }
        else
        {
            _logger.LogInternalWarning("Autonomous mode is enabled but autonomous common prompt not found for agent {agentName}.", agentDescriptor.Name);
        }
    }
}

