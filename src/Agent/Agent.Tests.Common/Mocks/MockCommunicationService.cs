// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common.Mocks;

public class MockCommunicationService : IAgentOutboundCommunicationService
{
    private readonly ILogger? _logger;
    public MockCommunicationService(ILogger? logger)
    {
        _logger = logger;
    }

    public List<string> Messages { get; } = new List<string>();

    public Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Approval id: {approval.Id}, Approval status: {approval.Status}");
        Messages.Add(approval.Description);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> AppendAgentImageMessage(Guid threadId, string message, Guid messageId = default)
    {
        Messages.Add(message);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task AppendAgentManualToolCallMessage(Guid threadId, List<ManualToolCall>? manualToolCalls, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        Messages.AddRange(manualToolCalls?.Select(call => call.FunctionCall.Name) ?? Enumerable.Empty<string>());
        return Task.FromResult(Guid.NewGuid());
    }

    public Task AppendAgentManualToolCallResult(Guid threadId, List<ManualToolCallResult>? manualToolCallResults, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        Messages.AddRange(manualToolCallResults?.Select(result => result.FunctionCall.Name) ?? Enumerable.Empty<string>());
        return Task.CompletedTask;
    }

    public Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger?.LogInternalInformation($"Mock: Streaming message for thread {threadId} with type {type}: {message}");
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task AppendAgentTaskUpdate(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger?.LogInternalInformation($"Mock: Task update for thread {threadId}: {taskData}");
        Messages.Add(taskData);
        return Task.CompletedTask;
    }

    public Task AppendTodoPlanUpdate(Guid threadId, string todoPlanData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger?.LogInternalInformation($"Mock: Todo plan update for thread {threadId}: {todoPlanData}");
        Messages.Add(todoPlanData);
        return Task.CompletedTask;
    }

    public Task AppendAgentToolCallMessage(Guid threadId, FunctionCallContent functionCall, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        Messages.Add(functionCall.Name);
        return Task.CompletedTask;
    }

    public Task AppendAgentToolCallResult(Guid threadId, FunctionResultContent result, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        Messages.Add(result.Result?.ToString() ?? string.Empty);
        return Task.CompletedTask;
    }

    public Task AppendUserStreamMessage(Guid threadId, string displayName, string message, Guid messageId, string? userId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task NotifyAzCliUpdate(Guid threadId, AzCliExecution execution, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Command: {execution.Command}, Status: {execution.Status}");
        Messages.Add(execution.Command);
        return Task.CompletedTask;
    }

    public Task NotifyKubectlUpdate(Guid threadId, KubectlExecution execution, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Command: {execution.Command}, Status: {execution.Status}");
        Messages.Add(execution.Command);
        return Task.CompletedTask;
    }

    public Task NotifyPsqlUpdate(Guid threadId, PsqlExecution execution, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Command: {execution.Command}, Status: {execution.Status}");
        Messages.Add(execution.Command);
        return Task.CompletedTask;
    }

    public Task NotifyApprovalUpdate(Guid threadId, Approval approval, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, ApprovalId: {approval.Id}, Status: {approval.Status}");
        Messages.Add(approval.Description);
        return Task.CompletedTask;
    }

    public Task NotifyGenericAgentMessage(Guid threadId, Message message, StreamMessageType? type)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Message: {message.Text}");
        Messages.Add(message.Text);
        return Task.CompletedTask;
    }

    public Task NotifyThreadEvent(Guid threadId, Core.Models.Api.v1.Thread thread)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}");
        return Task.CompletedTask;
    }

    public Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message, Guid? messageId = null, StreamMessageType? type = null, bool isComplete = true)
    {
        _logger?.LogInternalInformation($"ThreadId: {context.ThreadId}, Message: {message.Text}");
        Messages.Add(message.Text);
        return Task.CompletedTask;
    }

    public Task SignalProcessingComplete(Guid threadId, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}");
        return Task.CompletedTask;
    }

    public Task HandleAgentTaskMemoryResult(Guid threadId, string chatMessageContent)
    {
        _logger?.LogInternalInformation($"Mock: Handling agent task Memory result for thread {threadId}, Content: {chatMessageContent}");
        Messages.Add($"AgentTask_Memory:{chatMessageContent}");
        return Task.CompletedTask;
    }

    public Task UpdateThreadWithAgentMessageAsync(Guid? threadId, ChatMessage message, Guid? messageId = null, StreamMessageType? type = null)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Message: {message.Text}");
        Messages.Add(message.Text);
        return Task.CompletedTask;
    }

    public Task UpdateThreadWithAgentMessageAsync(Guid? threadId, ChatMessage message, AgentTaskInfo? agentTaskInfo, TodoInfo? todoInfo, Guid? messageId = null, StreamMessageType? type = null)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Message: {message.Text}");
        Messages.Add(message.Text);
        return Task.CompletedTask;
    }

    public Task UpdateThreadWithAgentMessageAsync(Guid? threadId, ChatMessage message, AgentTaskInfo? agentTaskInfo, Approval? approval, Guid? messageId = null, StreamMessageType? type = null)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Message: {message.Text}, Approval: {approval?.Id}");
        Messages.Add(message.Text);
        return Task.CompletedTask;
    }

    public Task NotifyIncidentStatusMetrics(Guid threadId, IncidentStatusMetrics metrics, Guid? messageId = null)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, ActiveCount: {metrics.ActiveCount}, MitigatedCount: {metrics.MitigatedCount}, ResolvedCount: {metrics.ResolvedCount}");
        Messages.Add($"Incident metrics: Active {metrics.ActiveCount}, Mitigated {metrics.MitigatedCount}, Resolved {metrics.ResolvedCount}");
        return Task.CompletedTask;
    }

    public Task HandleAgentTaskAzCliResult(Guid threadId, AzCliExecution execution)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, Command: {execution.Command}, Status: {execution.Status}");
        Messages.Add(execution.Command);
        return Task.CompletedTask;
    }

    public Task HandleAgentTaskKustoResult(Guid threadId, string chatMessageContent)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, MessageContent: {chatMessageContent}");
        Messages.Add(chatMessageContent);
        return Task.CompletedTask;
    }

    public Task<Guid> AppendAgentMemorySearchMessage(Guid threadId, MemorySearchResult memorySearchResult, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, MemorySearchResult: {memorySearchResult}");
        Messages.Add(memorySearchResult?.ToString() ?? string.Empty);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> AppendAgentKnowledgeGraphSearchMessage(Guid threadId, KnowledgeGraphSearchResult knowledgeGraphSearchResult, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, KnowledgeGraphSearchResult: {knowledgeGraphSearchResult}");
        Messages.Add(knowledgeGraphSearchResult?.ToString() ?? string.Empty);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> AppendAgentGrepSearchMessage(Guid threadId, GrepSearchResult grepSearchResult, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, GrepSearchResult: {grepSearchResult}");
        Messages.Add(grepSearchResult?.ToString() ?? string.Empty);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> AppendAgentUserQuestionMessage(Guid threadId, UserQuestion userQuestion, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, UserQuestion: {userQuestion?.Question}");
        Messages.Add(userQuestion?.Question ?? string.Empty);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task NotifyUserQuestionUpdate(Guid threadId, UserQuestion userQuestion, Guid messageId)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, UserQuestion update: {userQuestion?.Question}, Status: {userQuestion?.Status}");
        Messages.Add($"UserQuestion update: {userQuestion?.Question}");
        return Task.CompletedTask;
    }

    public Task NotifyIntermediateUpdate(Guid threadId, string message, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, intermediate message {message}");
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task NotifyMcpToolExecution(Guid threadId, McpToolExecution execution, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, MCP tool execution {execution.FullToolName}, status {execution.Status}");
        Messages.Add($"MCP Tool: {execution.FullToolName}");
        return Task.CompletedTask;
    }

    public Task<Guid> AppendAgentReadFileMessage(Guid threadId, ReadFileResult readFileResult, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, ReadFileResult: {readFileResult?.FilePath}");
        Messages.Add(readFileResult?.FilePath ?? string.Empty);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task<Guid> AppendAgentTerminalMessage(Guid threadId, TerminalExecutionResult terminalResult, Guid messageId = default)
    {
        _logger?.LogInternalInformation($"ThreadId: {threadId}, TerminalResult: {terminalResult?.Command}");
        Messages.Add(terminalResult?.Command ?? string.Empty);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task StreamTaskToolExecutionUpdateAsync(Guid threadId, string executionData, StreamMessageType messageType, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _logger?.LogInternalInformation($"Mock: Task tool execution update for thread {threadId} with type {messageType}: {executionData}");
        Messages.Add(executionData);
        return Task.CompletedTask;
    }
}

