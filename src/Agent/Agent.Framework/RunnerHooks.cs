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

    public Func<RunContextWrapper<TContext>, Agent<TContext>, AIFunction, IEnumerable<KeyValuePair<string, object?>>?, Task> OnToolStart { get; set; } =
        (context, agent, tool, arguments) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, AIFunction, object?, Task> OnToolEnd { get; set; } =
        (context, agent, tool, result) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, Agent<TContext>, string, Task> OnHandoff { get; set; } =
        (context, fromAgent, toAgent, handoffReasoning) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, Task<List<AIFunction>>> ResolveFactoryTools { get; set; } =
        (context, agent) => Task.FromResult<List<AIFunction>>([]);

    public Func<RunContextWrapper<TContext>, Agent<TContext>, IEnumerable<ChatMessage>, ChatOptions, Task> OnModelGenerationStart { get; set; } =
        (context, agent, chatMessages, chatOption) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, ChatResponse, Task> OnModelGenerationEnd { get; set; } =
        (context, agent, response) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, Task> OnSummarizerStart { get; set; } =
        (context, agent) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, string, Task> OnSummarizerEnd { get; set; } =
        (context, agent, extractedUserIntent) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, int, Task> OnCriticStart { get; set; } =
        (context, agent, currentTurn) => Task.CompletedTask;

    public Func<RunContextWrapper<TContext>, Agent<TContext>, string, string, bool, Task> OnCriticEnd { get; set; } =
        (context, agent, userQuery, criticResult, wasApproved) => Task.CompletedTask;

    //todo: convert to enum - critic or reasoningloophandler
    public Func<RunContextWrapper<TContext>, ChatMessage, string, Task> OnInputInjection { get; set; } =
        (context, injectedMessage, injectionSource) => Task.CompletedTask;
}
