// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Agent.Core;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
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
    private readonly CustomerLogger _customerLogger;
    private readonly IPostToTeamsPlugin _postToTeamsService;
    private readonly SinkService _sinkService;
    private readonly IStreamingService _streamingService;
    private readonly IAgentTasksRepository _agentTaskRepository;
    private readonly IThreadRepository _threadRepository;
    private readonly AgentTaskToolResultHelper _agentTaskHelper;
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
        CustomerLogger customerLogger,
        IPostToTeamsPlugin postToTeamsService,
        SinkService sinkService,
        IStreamingService streamingService,
        IAgentTasksRepository agentTaskRepository,
        IThreadRepository threadRepository,
        AgentTaskToolResultHelper agentTaskHelper)
    {
        _mappingManager = mappingManager;
        _logger = logger;
        _customerLogger = customerLogger;
        _postToTeamsService = postToTeamsService;
        _sinkService = sinkService;
        _streamingService = streamingService;
        _agentTaskRepository = agentTaskRepository;
        _threadRepository = threadRepository;
        _agentTaskHelper = agentTaskHelper;
        _azCliKubectlSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    // main method to update thread with agent message
    public async Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, Guid? messageId = null, StreamMessageType? type = null)
    {
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId?.ToString() ?? Guid.Empty.ToString(), orchestrationInstanceId);
        }
        _logger.LogExternalInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);

        _customerLogger.LogMessage($"[ChatThreadId {threadId}] Agent responding: {message.Text}");
        _customerLogger.LogCustomEvent("AgentResponse", new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
            { "Message", message.Text ?? string.Empty }
        });
        _customerLogger.LogAgentResponseEvents("AgentResponse", message.Text ?? string.Empty, properties: new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
        });

        Guid resolvedMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;

        await AppendAgentStreamMessage(threadId ?? Guid.Empty, message.Text ?? string.Empty, type, resolvedMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(threadId ?? Guid.Empty, message.Text ?? string.Empty, agentResponseMessageId: resolvedMessageId, recordedDateTime: recordedDateTime);
    }

    // overload method that takes AgentTaskInfo directly
    public async Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, AgentTaskInfo? agentTaskInfo, Guid? messageId = null, StreamMessageType? type = null)
    {
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId?.ToString() ?? Guid.Empty.ToString(), orchestrationInstanceId);
        }
        _logger.LogExternalInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);

        _customerLogger.LogMessage($"[ChatThreadId {threadId}] Agent responding: {message.Text}");
        _customerLogger.LogCustomEvent("AgentResponse", new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
            { "Message", message.Text ?? string.Empty }
        });
        _customerLogger.LogAgentResponseEvents("AgentResponse", message.Text ?? string.Empty, properties: new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
        });

        Guid resolvedMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;

        await AppendAgentStreamMessage(threadId ?? Guid.Empty, message.Text ?? string.Empty, type, resolvedMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(threadId ?? Guid.Empty, message.Text ?? string.Empty, agentResponseMessageId: resolvedMessageId, recordedDateTime: recordedDateTime, agentTaskInfo: agentTaskInfo);
    }

    // overload method that takes both AgentTaskInfo and TodoInfo
    public async Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, AgentTaskInfo? agentTaskInfo, TodoInfo? todoInfo, Guid? messageId = null, StreamMessageType? type = null)
    {
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId?.ToString() ?? Guid.Empty.ToString(), orchestrationInstanceId);
        }
        _logger.LogExternalInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);

        _customerLogger.LogMessage($"[ChatThreadId {threadId}] Agent responding: {message.Text}");
        _customerLogger.LogCustomEvent("AgentResponse", new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
            { "Message", message.Text ?? string.Empty }
        });
        _customerLogger.LogAgentResponseEvents("AgentResponse", message.Text ?? string.Empty, properties: new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
        });

        Guid resolvedMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;

        await AppendAgentStreamMessage(threadId ?? Guid.Empty, message.Text ?? string.Empty, type, resolvedMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(threadId ?? Guid.Empty, message.Text ?? string.Empty, agentResponseMessageId: resolvedMessageId, recordedDateTime: recordedDateTime, agentTaskInfo: agentTaskInfo, todoInfo: todoInfo);
    }

    // overload method that takes both AgentTaskInfo and Approval
    public async Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message, AgentTaskInfo? agentTaskInfo, Approval? approval, Guid? messageId = null, StreamMessageType? type = null)
    {
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId?.ToString() ?? Guid.Empty.ToString(), orchestrationInstanceId);
        }
        _logger.LogExternalInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}, with approval: {HasApproval}",
            orchestrationInstanceId, threadId, message.Text, approval != null);

        _customerLogger.LogMessage($"[ChatThreadId {threadId}] Agent responding: {message.Text}");
        _customerLogger.LogCustomEvent("AgentResponse", new Dictionary<string, string>
        {
            { "ChatThreadId", threadId?.ToString() ?? string.Empty },
            { "Message", message.Text ?? string.Empty },
            { "HasApproval", (approval != null).ToString() }
        });

        Guid resolvedMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;

        await AppendAgentStreamMessage(threadId ?? Guid.Empty, message.Text ?? string.Empty, type, resolvedMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(threadId ?? Guid.Empty, message.Text ?? string.Empty, isImageContent: false, agentResponseMessageId: resolvedMessageId, recordedDateTime: recordedDateTime, agentTaskInfo: agentTaskInfo, approval: approval);
    }

    // over load method that also takes agent context
    public async Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message, Guid? messageId = null, bool isComplete = true)
    {
        _logger.LogExternalInformation("Agent context {AgentContextId} of type {AgentType} message to thread {ThreadId}: {message}",
            context.Id, context.AgentType.ToString(), context.ThreadId, message.Text);

        _customerLogger.LogMessage($"[ChatThreadId {context.ThreadId}] Agent responding: {message.Text}");
        _customerLogger.LogCustomEvent("AgentResponse", new Dictionary<string, string>
        {
            { "ChatThreadId", context.ThreadId.ToString() ?? string.Empty },
            { "Message", message.Text ?? string.Empty }
        });
        _customerLogger.LogAgentResponseEvents("AgentResponse", message.Text ?? string.Empty, properties: new Dictionary<string, string>
        {
            { "ChatThreadId", context.ThreadId.ToString() ?? string.Empty },
        });

        Guid agentMessageId = messageId ?? Guid.NewGuid();
        DateTime recordedDateTime = DateTime.UtcNow;
        await AppendAgentStreamMessage(context.ThreadId, message.Text ?? string.Empty, null, agentMessageId, recordedDateTime);
        await _sinkService.SinkAgentMessageAsync(context.ThreadId, message.Text ?? string.Empty, agentResponseMessageId: agentMessageId, recordedDateTime: recordedDateTime, isComplete: isComplete);
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

    public async Task<Guid> AppendAgentMemorySearchMessage(Guid threadId, MemorySearchResult memorySearchResult, Guid messageId = default)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        Guid resolvedMessageId = messageId == default ? Guid.NewGuid() : messageId;
        try
        {
            string jsonString = JsonSerializer.Serialize(memorySearchResult, _serializerOptions);
            // Stream to frontend using StreamMessageType.MemorySearch
            await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.MemorySearch, resolvedMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream memory search message for thread {ThreadId}", threadId);
        }

        // Persist to database - use a descriptive text message
        string messageText = $"Memory Search: Found {memorySearchResult.TotalResults} relevant results";
        return await _sinkService.SinkAgentMessageAsync(threadId, messageText, false, null, resolvedMessageId, null, null, memorySearchResult, null);
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

    public async Task NotifyPsqlUpdate(Guid threadId, PsqlExecution execution, Guid messageId = default)
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
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Psql, messageId);
            }
            else
            {
                await AppendAgentStreamMessage(threadId, jsonString, StreamMessageType.Psql);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream PostgreSQL update for thread {ThreadId}", threadId);
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

    public async Task NotifyIncidentStatusMetrics(Guid threadId, IncidentStatusMetrics metrics, Guid? messageId = null)
    {
        try
        {
            string jsonString = JsonSerializer.Serialize(metrics, _serializerOptions);
            if (messageId != default)
            {
                await _streamingService.StreamIncidentUpdateAsync(threadId, jsonString, messageId, messageType: StreamMessageType.IncidentStatus);
            }
            else
            {
                await _streamingService.StreamIncidentUpdateAsync(threadId, jsonString, messageType: StreamMessageType.IncidentStatus);
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

            // Emit structured agent action telemetry for plain agent messages (type == null)
            try
            {
                if (type == null)
                {
                    Guid resolvedMessageIdForLog = messageId ?? Guid.Empty;
                    string action = AgentActionEvents.CreateAgentMessage;
                    string parameter = resolvedMessageIdForLog.ToString();
                    string status = AgentActionStatus.Success;
                    long durationMs = 0;
                    _logger.LogAgentAction(action, parameter, status, durationMs, threadId.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalInformation(ex, "Failed to emit LogAgentAction in AppendAgentStreamMessage for thread {ThreadId}", threadId);
            }

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

    public async Task AppendTodoPlanUpdate(Guid threadId, string todoPlanData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
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
                _logger.LogInternalInformation("Using AsyncLocal cancellation token for streaming todo plan update to thread {ThreadId}", threadId);
            }

            cancellationToken.ThrowIfCancellationRequested();

            await _streamingService.StreamTodoPlanUpdateAsync(threadId, todoPlanData, messageId, recordedDateTime: recordedDateTime, cancellationToken: cancellationToken);

            _logger.LogExternalInformation("Successfully sent todo plan update for thread {ThreadId}", threadId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalInformation("Todo plan update streaming cancelled for thread {ThreadId}", threadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream todo plan update for thread {ThreadId}", threadId);
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
            foreach (var toolCall in manualToolCalls)
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

            // Emit structured agent action telemetry for user messages
            try
            {
                Guid resolvedMessageIdForLog = messageId != Guid.Empty ? messageId : Guid.Empty;
                string action = AgentActionEvents.CreateUserMessage;
                string parameter = resolvedMessageIdForLog.ToString();
                string status = AgentActionStatus.Success;
                long durationMs = 0;
                _logger.LogAgentAction(action, parameter, status, durationMs, threadId.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogInternalInformation(ex, "Failed to emit LogAgentAction in AppendUserStreamMessage for thread {ThreadId}", threadId);
            }

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
            CustomerTraceManager.GetCurrentActivity()?.SetSuccess("Message processed successfully.");
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

    /// <summary>
    /// Handles AzCli tool results specifically for agent tasks/deep investigation.
    /// Saves results to agent task storage and sends task updates instead of streaming to chat.
    /// </summary>
    public async Task HandleAgentTaskAzCliResult(Guid threadId, AzCliExecution execution)
    {
        var agentTaskId = ToolStatic.AsyncLocalAgentTaskId.Value;
        var stepContext = ToolStatic.AsyncLocalInvestigationStepContext.Value;

        _logger.LogInternalInformation("HandleAgentTaskAzCliResult called - Command: {Command}, Status: {Status}, AgentTaskId: {AgentTaskId}",
            execution.Command, execution.Status, agentTaskId);

        if (!agentTaskId.HasValue)
        {
            _logger.LogInternalWarning("HandleAgentTaskAzCliResult called without agent task context - this should not happen");
            return;
        }

        try
        {
            // Handle based on execution status
            switch (execution.Status)
            {
                case AzCliExecutionStatus.Pending:
                case AzCliExecutionStatus.PendingAuthorization:
                    await _agentTaskHelper.HandlePendingAzCliAsync(threadId, execution, agentTaskId.Value, stepContext);
                    break;

                case AzCliExecutionStatus.Running:
                    await _agentTaskHelper.HandleRunningAzCliAsync(threadId, execution, agentTaskId.Value, stepContext);
                    break;

                case AzCliExecutionStatus.Completed:
                case AzCliExecutionStatus.Failed:
                case AzCliExecutionStatus.Cancelled:
                    await _agentTaskHelper.HandleCompletedAzCliAsync(threadId, execution, agentTaskId.Value, stepContext);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to handle agent task AzCli result for task {AgentTaskId}", agentTaskId.Value);
        }
    }

    /// <summary>
    /// Handles Kusto tool results specifically for agent tasks/deep investigation.
    /// Saves results to agent task storage instead of streaming to chat.
    /// </summary>
    public async Task HandleAgentTaskKustoResult(Guid threadId, string chatMessageContent)
    {
        var agentTaskId = ToolStatic.AsyncLocalAgentTaskId.Value;
        var stepContext = ToolStatic.AsyncLocalInvestigationStepContext.Value;

        _logger.LogInternalInformation("HandleAgentTaskKustoResult called - AgentTaskId: {AgentTaskId}", agentTaskId);

        if (!agentTaskId.HasValue)
        {
            _logger.LogInternalWarning("HandleAgentTaskKustoResult called without agent task context - this should not happen");
            return;
        }

        try
        {
            // Save to agent task with exact same content as chat message
            var toolResult = new ToolExecutionResult
            {
                Type = ToolExecutionType.Kusto,
                KustoQueryResults = chatMessageContent,
                ExecutedTimestamp = DateTime.UtcNow
            };

            await _agentTaskHelper.SaveToolResultAsync(agentTaskId.Value, stepContext, toolResult, threadId);

            // Add reasoning message for agent context using the same content
            var kustoMessage = new ChatMessage(ChatRole.Tool, chatMessageContent);
            await _agentTaskHelper.AddToolResultToAgentChatHistoryAsync(threadId, kustoMessage);

            _logger.LogInternalInformation("Kusto tool result saved to agent task {AgentTaskId}", agentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to handle agent task Kusto result for task {AgentTaskId}", agentTaskId.Value);
        }
    }

    /// <summary>
    /// Handles Memory tool results specifically for agent tasks/deep investigation.
    /// Saves results to agent task storage instead of streaming to chat.
    /// </summary>
    public async Task HandleAgentTaskMemoryResult(Guid threadId, string chatMessageContent)
    {
        var agentTaskId = ToolStatic.AsyncLocalAgentTaskId.Value;
        var stepContext = ToolStatic.AsyncLocalInvestigationStepContext.Value;

        _logger.LogInternalInformation("HandleAgentTaskMemoryResult called - AgentTaskId: {AgentTaskId}", agentTaskId);

        if (!agentTaskId.HasValue)
        {
            _logger.LogInternalWarning("HandleAgentTaskMemoryResult called without agent task context - this should not happen");
            return;
        }

        try
        {
            // Save to agent task with exact same content as chat message
            var toolResult = new ToolExecutionResult
            {
                Type = ToolExecutionType.Memory,
                MemorySearchResults = chatMessageContent,
                ExecutedTimestamp = DateTime.UtcNow
            };

            await _agentTaskHelper.SaveToolResultAsync(agentTaskId.Value, stepContext, toolResult, threadId);

            // Add reasoning message for agent context using the same content
            var memoryMessage = new ChatMessage(ChatRole.Tool, chatMessageContent);
            await _agentTaskHelper.AddToolResultToAgentChatHistoryAsync(threadId, memoryMessage);

            _logger.LogInternalInformation("Memory tool result saved to agent task {AgentTaskId}", agentTaskId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to handle agent task Memory result for task {AgentTaskId}", agentTaskId.Value);
        }
    }

}
