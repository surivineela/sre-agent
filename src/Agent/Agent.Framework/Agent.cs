// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Agent<TContext>(string name) where TContext : class
{
    public string Name { get; } = name;

    public PromptText? HandoffDescription { get; set; }

    public PromptText Instructions { get; set; } = "";

    // Tools that are retrieved from the tool factory
    public List<string> FactoryTools { get; set; } = [];

    // Tools preconfigured in the agent
    public List<AIFunction> Tools { get; set; } = [];

    public List<string> StandardToolNames => Tools.Select(t => t.Name).ToList();

    public List<Handoff<TContext>> Handoffs { get; set; } = [];

    public List<AgentAsTool<TContext>> AgentsAsTools { get; set; } = [];

    public List<string> HandoffNames => Handoffs.Select(h => h.Name).ToList();

    public IAgentHooks? Hooks { get; set; }

    public int MaxReflectionCount { get; set; } = 0;

    public string CustomReflectionNote { get; set; } = string.Empty;

    //todo: map to thinking effort
    public string CriticPromptPath { get; set; } = string.Empty;

    public bool AllowParallelToolCalls { get; set; } = false;

    public virtual ChatToolMode ChatToolMode { get; set; } = ChatToolMode.Auto;

    public virtual float Temperature { get; set; } = 0.3f;

    public virtual IChatClient GetChatClient(RunConfig config)
    {
        return config.ChatClient;
    }
}

public class Agent(string name) : Agent<object>(name)
{
}
