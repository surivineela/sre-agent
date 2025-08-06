// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Runtime.Helpers;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ToolStatic = Agent.Core.ToolStatic;

namespace Agent.Runtime.Communication;

public class OutboundCommunicationService : IAgentOutboundCommunicationService
{
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly ILogger<OutboundCommunicationService> _logger;
    private readonly IPostToTeamsPlugin _postToTeamsService;
    private readonly SinkService _sinkService;
    private readonly IStreamingService _streamingService;
    private readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = new LowerCaseNamingPolicy(),
        WriteIndented = true,
    };

    // TODO: make this default for all serializer options
    private readonly JsonSerializerOptions _azCliKubectlSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DictionaryKeyPolicy = new LowerCaseNamingPolicy(),
        WriteIndented = true,
    };

    public OutboundCommunicationService(
        IThreadOrchestrationManager mappingManager,
        ILogger<OutboundCommunicationService> logger,
        IPostToTeamsPlugin postToTeamsService,
        SinkService sinkService,
        IStreamingService streamingService)
    {
        _mappingManager = mappingManager;
        _logger = logger;
        _postToTeamsService = postToTeamsService;
        _sinkService = sinkService;
        _streamingService = streamingService;
        _azCliKubectlSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public async Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, Guid? messageId = null)
    {
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId?.ToString() ?? Guid.Empty.ToString(), orchestrationInstanceId);
        }
        _logger.LogExternalInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);
        Guid agentMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;
        await AppendAgentStreamMessage(threadId ?? Guid.Empty, message.Text ?? string.Empty, null, agentMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(threadId ?? Guid.Empty, message.Text ?? string.Empty, agentResponseMessageId: agentMessageId, recordedDateTime: recordedDateTime);
    }

    public async Task<Guid> AppendAgentImageMessage(Guid threadId, string message, Guid messageId = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        // Use SinkService to add the image message
        return await _sinkService.SinkAgentMessageAsync(threadId, message, true, agentResponseMessageId: messageId);
    }

    public async Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        Guid messageId = Guid.NewGuid();
        try
        {
            Approval modifiedApproval = approval with { OboTokenScope = string.IsNullOrEmpty(approval.OboTokenScope) ? Constants.DefaultOboTokenScope : approval.OboTokenScope };
            string jsonString = JsonSerializer.Serialize(modifiedApproval, _serializerOptions);
            // Use the streaming service abstraction to send the message
            await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Approval, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }

        // Use SinkService to add the image message
        return await _sinkService.SinkAgentMessageAsync(threadId, "Approval Request for Processing Azure SRE Agent Request", true, approval, messageId);
    }

    public async Task NotifyThreadEvent(Guid threadId, Core.Models.Api.v1.Thread thread)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            string jsonString = JsonSerializer.Serialize(thread, _serializerOptions);
            await _streamingService.StreamThreadUpdateAsync(threadId, jsonString, StreamMessageType.ThreadEvent);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task NotifyGenericAgentMessage(Guid threadId, Message message, StreamMessageType? type)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            if (type != null)
            {
                string jsonString = JsonSerializer.Serialize(message, _serializerOptions);
                await AppendAgentStreamMessage(threadId, jsonString, type, messageId: message.Id);
            }
            await AppendAgentStreamMessage(threadId, message.Text ?? string.Empty, null, messageId: message.Id);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task NotifyAzCliUpdate(Guid threadId, AzCliExecution execution, Guid messageId = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            string jsonString = JsonSerializer.Serialize(execution, _azCliKubectlSerializerOptions);
            if (messageId != default)
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.AzCli, messageId);
            }
            else
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.AzCli);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream AzCli update for thread {ThreadId}", threadId);
        }
    }

    public async Task NotifyKubectlUpdate(Guid threadId, KubectlExecution execution, Guid messageId = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            string jsonString = JsonSerializer.Serialize(execution, _azCliKubectlSerializerOptions);
            if (messageId != default)
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Kubectl, messageId);
            }
            else
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Kubectl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream Kubectl update for thread {ThreadId}", threadId);
        }
    }

    public async Task NotifyApprovalUpdate(Guid threadId, Approval approval, Guid messageId = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            Approval modifiedApproval = approval with { OboTokenScope = string.IsNullOrEmpty(approval.OboTokenScope) ? Constants.DefaultOboTokenScope : approval.OboTokenScope };
            string jsonString = JsonSerializer.Serialize(modifiedApproval, _serializerOptions);
            if (messageId != default)
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Approval, messageId);
            }
            else
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Approval);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream Approval update for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();

            // Use the streaming service abstraction to send the message
            await _streamingService.StreamMessageAsync(threadId, message, type, messageId, recordedDateTime: recordedDateTime, cancellationToken: cancellationToken);

            _logger.LogExternalInformation("Successfully sent direct stream message for thread {ThreadId} with type {Type}",
                threadId, type);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendAgentTaskUpdate(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming task update to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();

            // Use the streaming service abstraction to send the task update
            await _streamingService.StreamTaskUpdateAsync(threadId, taskData, messageId, recordedDateTime: recordedDateTime, cancellationToken: cancellationToken);

            _logger.LogExternalInformation("Successfully sent task update for thread {ThreadId}", threadId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Task update streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream task update for thread {ThreadId}", threadId);
        }
    }

    public async Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null)
    {
        _logger.LogInternalInformation("orchestrationInstanceId {orchestrationInstanceId} completed with status: {Status}", orchestrationInstanceId, status);

        var mapping = await _mappingManager.GetMappingsByThreadIdAsync(threadId);
        if (mapping.Any())
        {
            // todo - once meta agent context is separate from thread history, consider appending a message to the meta agent context so it knows that control has transferred back

            // Remove the mapping as the orchestration is completed
            await _mappingManager.RemoveMappingAsync(threadId, orchestrationInstanceId);
        }
    }

    public async Task PostActivity(string threadId, Activity activity, string messageId = "")
    {
        await _postToTeamsService.PostTeamsMessage(threadId, activity, messageId);
    }

    public async Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message, Guid? messageId = null)
    {
        _logger.LogExternalInformation("Agent context {AgentContextId} of type {AgentType} message to thread {ThreadId}: {message}",
            context.Id, context.AgentType.ToString(), context.ThreadId, message.Text);

        Guid agentMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;
        await AppendAgentStreamMessage(context.ThreadId, message.Text ?? string.Empty, null, agentMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(context.ThreadId, message.Text ?? string.Empty, agentResponseMessageId: agentMessageId, recordedDateTime: recordedDateTime);
    }

    public Task NotifyCompletionAsync(AgentContext context, string subAgentIdentifier, string status, string? summary = null)
    {
        var message = $"{subAgentIdentifier} completed with status: {status}";

        if (!string.IsNullOrEmpty(summary))
        {
            message += $" summary: {summary}";
        }

        return UpdateThreadWithAgentMessageAsync(context, new(ChatRole.Assistant, message));
    }

    public async Task NotifyActionAsync(Guid threadId, Core.Models.Api.v1.Action action)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }
        try
        {
            string jsonString = JsonSerializer.Serialize(action, _serializerOptions);
            await _streamingService.StreamActionUpdateAsync(threadId, jsonString, StreamMessageType.Action);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream action update for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendAgentToolCallMessage(Guid threadId, AIFunction aiTool, Guid? messageId = null, string? callId = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();
            Guid agentMessageId = messageId ?? Guid.NewGuid();

            // Send stop reason for tool calls in response
            var stopMessageToolCalls = new ChatResponseUpdate
            {
                AuthorName = "Azure SRE Agent",
                Role = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "threadId", threadId.ToString() },
                    { "messageId", agentMessageId.ToString() },
                },
                FinishReason = ChatFinishReason.ToolCalls,
            };
            await _streamingService.StreamChatResponseUpdateAsync(threadId, stopMessageToolCalls, cancellationToken);

            // Use the streaming service abstraction to send the ChatUpdateResponse
            string userDisplayedToolDescription = ToolDescriptionHelper.GetUserDescriptionForFunctionCallName(aiTool.Name);
            var functionCallContent = new FunctionCallContent(callId ?? threadId.ToString(), "operation");
            functionCallContent.AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "userDescription", userDisplayedToolDescription }
            };

            var message = new ChatResponseUpdate
            {
                AuthorName = "Azure SRE Agent",
                Role = ChatRole.Tool,
                CreatedAt = DateTime.UtcNow,
                Contents = [functionCallContent],
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "threadId", threadId.ToString() },
                    { "messageId", agentMessageId.ToString() },
                    { "actionName", nameof(AppendAgentToolCallMessage) },
                },
            };

            await _streamingService.StreamChatResponseUpdateAsync(threadId, message, cancellationToken);

            _logger.LogExternalInformation("Successfully sent tool call message for thread {ThreadId} with type",
                threadId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendAgentManualToolCallMessage(Guid threadId, List<ManualToolCall>? manualToolCalls, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        if (manualToolCalls == null || !manualToolCalls.Any())
        {
            _logger.LogInternalWarning("No manual tool calls provided for thread {ThreadId}", threadId);
            return;
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();
            Guid agentMessageId = messageId ?? Guid.NewGuid();

            // Send stop reason for tool calls in response
            var stopMessageToolCalls = new ChatResponseUpdate
            {
                AuthorName = "Azure SRE Agent",
                Role = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "threadId", threadId.ToString() },
                    { "messageId", agentMessageId.ToString() },
                },
                FinishReason = ChatFinishReason.ToolCalls,
            };
            await _streamingService.StreamChatResponseUpdateAsync(threadId, stopMessageToolCalls, cancellationToken);

            List<AIContent> toolCalls = new List<AIContent>();
            foreach(var toolCall in manualToolCalls)
            {
                var functionCall = toolCall.FunctionCall;
                functionCall.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                functionCall.AdditionalProperties.Add("userDescription", ToolDescriptionHelper.GetUserDescriptionForFunctionCallName(functionCall.Name));

                // Create a safe version that doesn't expose the real function name
                var safeFunctionCall = new FunctionCallContent(
                    functionCall.CallId,
                    "operation", // Never expose real function names
                    null // Don't expose arguments for security
                );
                safeFunctionCall.AdditionalProperties = functionCall.AdditionalProperties;
                toolCalls.Add(safeFunctionCall);
            }

            ChatResponseUpdate toolCallUpdate = new ChatResponseUpdate(ChatRole.Tool, toolCalls);
            toolCallUpdate.AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "threadId", threadId.ToString() },
                { "messageId", agentMessageId.ToString() },
                { "actionName", nameof(AppendAgentManualToolCallMessage) },
            };

            await _streamingService.StreamChatResponseUpdateAsync(threadId, toolCallUpdate, cancellationToken);

            _logger.LogExternalInformation("Successfully sent tool call message for thread {ThreadId} with type",
                threadId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendAgentToolCallResult(Guid threadId, FunctionResultContent result, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();
            Guid agentMessageId = messageId ?? Guid.NewGuid();

            // Use the streaming service abstraction to send the ChatUpdateResponse
            var message = new ChatResponseUpdate
            {
                AuthorName = "Azure SRE Agent",
                Role = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                Contents = [result],
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "threadId", threadId.ToString() },
                    { "messageId", agentMessageId.ToString() },
                    { "actionName", nameof(AppendAgentToolCallResult) },
                }
            };

            await _streamingService.StreamChatResponseUpdateAsync(threadId, message, cancellationToken);

            _logger.LogExternalInformation("Successfully sent tool call message for thread {ThreadId} with type",
                threadId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendAgentManualToolCallResult(Guid threadId, List<ManualToolCallResult>? manualToolCallResults, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            if (manualToolCallResults == null || !manualToolCallResults.Any())
            {
                _logger.LogInternalWarning("No manual tool call results provided for thread {ThreadId}", threadId);
                return;
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();
            List<AIContent> safeFunctionResults = new List<AIContent>();

            foreach (var manualToolCallResult in manualToolCallResults)
            {
                var safeFunctionResult = new FunctionResultContent(
                    manualToolCallResult.FunctionCall.CallId,
                    "operation result"
                );
                safeFunctionResult.AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "userDescription", ToolDescriptionHelper.GetUserDescriptionForFunctionCallName(manualToolCallResult.FunctionCall.Name) }
                };
                safeFunctionResults.Add(safeFunctionResult);
            }
            Guid agentMessageId = messageId ?? Guid.NewGuid();

            // Use the streaming service abstraction to send the ChatUpdateResponse
            var message = new ChatResponseUpdate
            {
                AuthorName = "Azure SRE Agent",
                Role = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                Contents = safeFunctionResults,
                AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "threadId", threadId.ToString() },
                        { "messageId", agentMessageId.ToString() },
                        { "actionName", nameof(AppendAgentManualToolCallResult) },
                    }
            };

            await _streamingService.StreamChatResponseUpdateAsync(threadId, message, cancellationToken);

            _logger.LogExternalInformation("Successfully sent tool call message for thread {ThreadId} with type",
                threadId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task AppendUserStreamMessage(Guid threadId, string displayName, string message, Guid messageId, string? userId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();

            var userMessage = new ChatResponseUpdate
            {
                AuthorName = displayName,
                Role = ChatRole.User,
                CreatedAt = recordedDateTime ?? DateTime.UtcNow,
                Contents = [new TextContent(message)],
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { "messageId", messageId.ToString() },
                    { "threadId", threadId.ToString() },
                    { "userId", userId?.ToString() },
                    { "actionName", nameof(AppendUserStreamMessage) }
                }
            };

            // Use the streaming service abstraction to send the message
            await _streamingService.StreamChatResponseUpdateAsync(threadId, userMessage, cancellationToken);

            _logger.LogExternalInformation("Successfully sent direct stream message for thread {ThreadId}");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task SignalProcessingComplete(Guid threadId, Guid? messageId = null, CancellationToken cancellationToken = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // If no cancellation token provided, try to get it from AsyncLocal (set during tool execution)
            if (cancellationToken == default && ToolStatic.AsyncLocalCancellationToken.Value != default)
            {
                cancellationToken = ToolStatic.AsyncLocalCancellationToken.Value;
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming message to thread {ThreadId}", threadId);
            }

            // Check for cancellation before streaming
            cancellationToken.ThrowIfCancellationRequested();
            Guid agentMessageId = messageId ?? Guid.NewGuid();

            // Use the streaming service abstraction to send the ChatFinishReason.Stop command back to the user
            var stopMessage = new ChatResponseUpdate
            {
                AuthorName = "Azure SRE Agent",
                Role = ChatRole.Assistant,
                CreatedAt = DateTime.UtcNow,
                FinishReason = ChatFinishReason.Stop
            };
            stopMessage.AdditionalProperties = new AdditionalPropertiesDictionary
            {
                { "threadId", threadId.ToString() },
                { "messageId", agentMessageId.ToString() },
                { "actionName", nameof(SignalProcessingComplete) }
            };

            await _streamingService.StreamChatResponseUpdateAsync(threadId, stopMessage, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
            // Don't rethrow - cancellation is expected
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to signal processing complete for thread {ThreadId}", threadId);
        }
    }
}
