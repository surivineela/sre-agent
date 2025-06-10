// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Framework;

namespace Agent.Core.Configuration;

public class ReadOnlyAgentModeConfigurator<TContext> : IAgentModeConfigurator<TContext>
    where TContext : class
{
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
            // logger.LogInformation("Added readonly common prompt to agent {agentName} due to ReadOnly mode.", agentDescriptor.Name);
        }
        else
        {
            // logger.LogWarning("ReadOnly mode is enabled but readonly common prompt not found for agent {agentName}.", agentDescriptor.Name);
        }
    }
}
