// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Agent<TContext> where TContext : class
{
    public string Name { get; set; }

    public PromptText? HandoffDescription { get; set; }

    public PromptText Instructions { get; set; } = "";

    // Tools that are retrieved from the tool factory
    public List<string> FactoryTools { get; set; } = [];

    // Tools preconfigured in the agent
    public List<AIFunction> Tools { get; set; } = [];

    public List<string> StandardToolNames => Tools.Select(t => t.Name).ToList();

    public List<Handoff<TContext>> Handoffs { get; set; } = [];

    public List<string> HandoffNames => Handoffs.Select(h => h.Name).ToList();

    public IAgentHooks? Hooks { get; set; }

    public int MaxReflectionCount { get; set; } = 0;

    public string CustomReflectionNote { get; set; } = string.Empty;

    //todo: map to thinking effort
    public string CriticPromptPath { get; set; } = string.Empty;

    public bool AllowParallelToolCalls { get; set; } = false;

    public virtual ChatToolMode ChatToolMode { get; set; } = ChatToolMode.Auto;

    public virtual float Temperature { get; set; } = 0.3f;

    public Agent(string name)
    {
        Name = name;
    }

    public virtual IChatClient GetChatClient(RunConfig config)
    {
        var innerClient = config.ChatClient;
        if (AllowParallelToolCalls)
        {
            return new ChatClientBuilder(innerClient)
                .UseFunctionInvocation()
                .Build();
        }
        else
        {
            return innerClient;
        }
    }
}

public class Agent(string name) : Agent<object>(name)
{
}
