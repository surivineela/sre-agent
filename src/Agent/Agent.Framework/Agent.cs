// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Agent<TContext> where TContext : class
{
    public string Name { get; set; }

    public string? HandoffDescription { get; set; }

    public string Instructions { get; set; } = "";

    /// <summary>
    /// Tools that can be automatically run by the framework without exiting the loop
    /// </summary>
    public List<AIFunction> AutoTools { get; set; } = [];

    public List<string> AutoToolNames => AutoTools.Select(t => t.Name).ToList();

    /// <summary>
    /// Tools that should not be called automatically by the framework, control will return to the caller
    /// to handle the tool call.
    /// </summary>
    public List<AIFunction> ManualTools { get; set; } = [];

    public List<string> ManualToolNames => ManualTools.Select(t => t.Name).ToList();

    public List<Handoff<TContext>> Handoffs { get; set; } = [];

    public List<string> HandoffNames => Handoffs.Select(h => h.Name).ToList();

    public IAgentHooks? Hooks { get; set; }

    public Agent(string name)
    {
        Name = name;
    }

    public virtual ChatOptions GetChatOptions(RunConfig config)
    {
        return new ChatOptions
        {
            Tools = GetAllTools(),
            ToolMode = ChatToolMode.Auto,
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["AllowParallelToolCalls"] = false
            },
            // Configure low temperature first to make the response deterministic
            // So we can refine our prompt based on a stable response.
            // After the result become stable, we can increase the temperature to get more creative responses.
            Temperature = 0.3f
        };
    }

    public virtual IChatClient GetChatClient(RunConfig config)
    {
        return config.ChatClient;
    }

    public List<AITool> GetAllTools()
    {
        return new List<AITool>(AutoTools).Concat(ManualTools).Concat(Handoffs).ToList();
    }
}

public class Agent(string name) : Agent<object>(name)
{
}
