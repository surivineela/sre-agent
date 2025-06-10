// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Framework;

namespace Agent.Core.Configuration;
public class DefaultAgentModeConfigurator<TContext> : IAgentModeConfigurator<TContext>
    where TContext : class
{
    public bool AppliesToMode(string? agentMode) => true; // This might be true for a default catch-all

    public void ConfigureAgent(
        Agent<TContext> agent,
        IAgentDescriptor agentDescriptor,
        IReadOnlyDictionary<string, IPromptDescriptor> promptDescriptors)
    {
        //  do nothing now

    }
}
