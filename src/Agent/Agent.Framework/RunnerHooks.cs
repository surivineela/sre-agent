// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class RunHooks<TContext> where TContext : class
{
    public Func<RunContextWrapper<TContext>, Agent<TContext>, Task> OnAgentStart { get; set; } =
        (context, agent) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, object?, Task> OnAgentEnd { get; set; } =
        (context, agent, result) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, AITool, Task> OnToolStart { get; set; } =
        (context, agent, tool) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, AITool, object?, Task> OnToolEnd { get; set; } =
        (context, agent, tool, result) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, Agent<TContext>, Task> OnHandoff { get; set; } =
        (context, fromAgent, toAgent) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, List<string>, Task<List<AIFunction>>> ResolveFactoryTools { get; set; } =
        (context, agent, toolNames) => Task.FromResult<List<AIFunction>>([]);
}
