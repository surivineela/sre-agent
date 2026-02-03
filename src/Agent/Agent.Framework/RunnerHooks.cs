// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.TaskTool;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class RunHooks<TContext> where TContext : class
{
    public delegate Task AgentStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent);
    public delegate Task AgentEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, object? result);
    public delegate Task ToolStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, FunctionCallContent functionCallContent, AIFunction tool, IEnumerable<KeyValuePair<string, object?>>? arguments);
    public delegate Task ToolEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, FunctionCallContent functionCallContent, AIFunction tool, object? result);
    public delegate Task HandoffDelegate(RunContextWrapper<TContext> context, Agent<TContext> fromAgent, Agent<TContext> toAgent, string handoffReasoning);
    public delegate Task<List<AIFunction>> ResolveFactoryToolsDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, IEnumerable<string> additionalToolNames);
    public delegate Task ModelGenerationStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, IEnumerable<ChatMessage> chatMessages, ChatOptions chatOption);
    public delegate Task ModelGenerationEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, ChatResponse? response);
    public delegate Task ModelGenerationErrorDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, Exception error);
    public delegate Task SummarizerStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent);
    public delegate Task SummarizerEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, string extractedUserIntent);
    public delegate Task CriticStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, int currentTurn);
    public delegate Task CriticEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, string userQuery, string criticResult, bool wasApproved);
    public delegate Task InputInjectionDelegate(RunContextWrapper<TContext> context, ChatMessage injectedMessage, string injectionSource);
    public delegate Task CompactionStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent);
    public delegate Task CompactionEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent);
    public delegate Task TaskToolGroupStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecutionGroup group);
    public delegate Task TaskToolGroupEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecutionGroup group);
    public delegate Task TaskToolExecutionStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecution execution);
    public delegate Task TaskToolExecutionEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecution execution);
    public delegate Task TaskToolInvocationStartDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, string executionId, string toolName, string? description);
    public delegate Task TaskToolInvocationEndDelegate(RunContextWrapper<TContext> context, Agent<TContext> agent, string executionId, string toolName, bool success, string? output = null);
    public event AgentStartDelegate? AgentStart;
    public event AgentEndDelegate? AgentEnd;
    public event ToolStartDelegate? ToolStart;
    public event ToolEndDelegate? ToolEnd;
    public event HandoffDelegate? Handoff;
    public event ResolveFactoryToolsDelegate? ResolveFactoryTools;
    public event ModelGenerationStartDelegate? ModelGenerationStart;
    public event ModelGenerationEndDelegate? ModelGenerationEnd;
    public event ModelGenerationErrorDelegate? ModelGenerationError;
    public event SummarizerStartDelegate? SummarizerStart;
    public event SummarizerEndDelegate? SummarizerEnd;
    public event CriticStartDelegate? CriticStart;
    public event CriticEndDelegate? CriticEnd;
    public event InputInjectionDelegate? InputInjection;
    public event CompactionStartDelegate? CompactionStart;
    public event CompactionEndDelegate? CompactionEnd;
    public event TaskToolGroupStartDelegate? TaskToolGroupStart;
    public event TaskToolGroupEndDelegate? TaskToolGroupEnd;
    public event TaskToolExecutionStartDelegate? TaskToolExecutionStart;
    public event TaskToolExecutionEndDelegate? TaskToolExecutionEnd;
    public event TaskToolInvocationStartDelegate? TaskToolInvocationStart;
    public event TaskToolInvocationEndDelegate? TaskToolInvocationEnd;
    public Task OnAgentStart(RunContextWrapper<TContext> context, Agent<TContext> agent) => AgentStart?.Invoke(context, agent) ?? Task.CompletedTask;
    public Task OnAgentEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, object? result) => AgentEnd?.Invoke(context, agent, result) ?? Task.CompletedTask;
    public Task OnToolStart(RunContextWrapper<TContext> context, Agent<TContext> agent, FunctionCallContent functionCallContent, AIFunction tool, IEnumerable<KeyValuePair<string, object?>>? arguments) => ToolStart?.Invoke(context, agent, functionCallContent, tool, arguments) ?? Task.CompletedTask;
    public Task OnToolEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, FunctionCallContent functionCallContent, AIFunction tool, object? result) => ToolEnd?.Invoke(context, agent, functionCallContent, tool, result) ?? Task.CompletedTask;
    public Task OnHandoff(RunContextWrapper<TContext> context, Agent<TContext> fromAgent, Agent<TContext> toAgent, string handoffReasoning) => Handoff?.Invoke(context, fromAgent, toAgent, handoffReasoning) ?? Task.CompletedTask;
    public Task<List<AIFunction>> OnResolveFactoryTools(RunContextWrapper<TContext> context, Agent<TContext> agent, IEnumerable<string> additionalToolNames) => ResolveFactoryTools?.Invoke(context, agent, additionalToolNames) ?? Task.FromResult(new List<AIFunction>());
    public Task OnModelGenerationStart(RunContextWrapper<TContext> context, Agent<TContext> agent, IEnumerable<ChatMessage> chatMessages, ChatOptions chatOption) => ModelGenerationStart?.Invoke(context, agent, chatMessages, chatOption) ?? Task.CompletedTask;
    public Task OnModelGenerationEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, ChatResponse? response) => ModelGenerationEnd?.Invoke(context, agent, response) ?? Task.CompletedTask;
    public Task OnModelGenerationError(RunContextWrapper<TContext> context, Agent<TContext> agent, Exception error) => ModelGenerationError?.Invoke(context, agent, error) ?? Task.CompletedTask;
    public Task OnSummarizerStart(RunContextWrapper<TContext> context, Agent<TContext> agent) => SummarizerStart?.Invoke(context, agent) ?? Task.CompletedTask;
    public Task OnSummarizerEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, string extractedUserIntent) => SummarizerEnd?.Invoke(context, agent, extractedUserIntent) ?? Task.CompletedTask;
    public Task OnCriticStart(RunContextWrapper<TContext> context, Agent<TContext> agent, int currentTurn) => CriticStart?.Invoke(context, agent, currentTurn) ?? Task.CompletedTask;
    public Task OnCriticEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, string userQuery, string criticResult, bool wasApproved) => CriticEnd?.Invoke(context, agent, userQuery, criticResult, wasApproved) ?? Task.CompletedTask;
    public Task OnInputInjection(RunContextWrapper<TContext> context, ChatMessage injectedMessage, string injectionSource) => InputInjection?.Invoke(context, injectedMessage, injectionSource) ?? Task.CompletedTask;
    public Task OnCompactionStart(RunContextWrapper<TContext> context, Agent<TContext> agent) => CompactionStart?.Invoke(context, agent) ?? Task.CompletedTask;
    public Task OnCompactionEnd(RunContextWrapper<TContext> context, Agent<TContext> agent) => CompactionEnd?.Invoke(context, agent) ?? Task.CompletedTask;
    public Task OnTaskToolGroupStart(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecutionGroup group) => TaskToolGroupStart?.Invoke(context, agent, group) ?? Task.CompletedTask;
    public Task OnTaskToolGroupEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecutionGroup group) => TaskToolGroupEnd?.Invoke(context, agent, group) ?? Task.CompletedTask;
    public Task OnTaskToolExecutionStart(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecution execution) => TaskToolExecutionStart?.Invoke(context, agent, execution) ?? Task.CompletedTask;
    public Task OnTaskToolExecutionEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, TaskToolExecution execution) => TaskToolExecutionEnd?.Invoke(context, agent, execution) ?? Task.CompletedTask;
    public Task OnTaskToolInvocationStart(RunContextWrapper<TContext> context, Agent<TContext> agent, string executionId, string toolName, string? description) => TaskToolInvocationStart?.Invoke(context, agent, executionId, toolName, description) ?? Task.CompletedTask;
    public Task OnTaskToolInvocationEnd(RunContextWrapper<TContext> context, Agent<TContext> agent, string executionId, string toolName, bool success, string? output = null) => TaskToolInvocationEnd?.Invoke(context, agent, executionId, toolName, success, output) ?? Task.CompletedTask;
}
