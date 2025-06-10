// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework;

public interface IAgentModeConfigurator<TContext>
    where TContext : class
{
    public bool AppliesToMode(string? agentMode);

    // apply agent mode to agent
    public void ConfigureAgent(
        Agent<TContext> agent,
        IAgentDescriptor agentDescriptor,
        IReadOnlyDictionary<string, IPromptDescriptor> promptDescriptors
    );
}
