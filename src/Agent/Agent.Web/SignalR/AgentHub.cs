// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Web.Models.Streaming;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Agent.Runtime.Services;
using Agent.Runtime.Helpers;
using Agent.Runtime.Reasoning;
using Agent.Web.Services;

namespace Agent.Web.SignalR
{
    public class AgentHub : Hub<IAgentClient>
    {
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly ThreadManagementService _threadManagementService;
        private readonly IThreadRepository _repository;
        private readonly IReasoningLoopManager _reasoningLoopManager;
        private readonly ILogger<AgentHub> _logger;

        // Thread-based cancellation tokens
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _threadCancellationTokens = new();

        public AgentHub(
            IAgentInboundCommunicationService agentInboundCommunicationService,
            ThreadManagementService threadManagementService,
            IThreadRepository repository,
            IReasoningLoopManager reasoningLoopManager,
            ILogger<AgentHub> logger)
        {
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _threadManagementService = threadManagementService;
            _repository = repository;
            _reasoningLoopManager = reasoningLoopManager;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            _logger.LogInternalInformation($"Client connected to SignalR: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInternalInformation($"Client disconnected from SignalR: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        // Thread token management
        private static CancellationToken GetOrCreateThreadToken(Guid threadId)
        {
            var tokenSource = _threadCancellationTokens.GetOrAdd(threadId, _ => new CancellationTokenSource());
            return tokenSource.Token;
        }

        private static void CancelThreadToken(Guid threadId)
        {
            if (_threadCancellationTokens.TryRemove(threadId, out var tokenSource))
            {
                try
                {
                    tokenSource.Cancel();
                }
                finally
                {
                    tokenSource.Dispose();
                }
            }
        }

        public Task CreateThread(Guid userDefinedThreadId, CreateThreadRequest request, bool textOnly = false)
        {
            // Capture context before async operation
            var connectionId = Context.ConnectionId;
            var caller = Clients.Caller;

            // Fire and forget - don't block the SignalR method
            _ = Task.Run(async () =>
            {
                Guid? actualThreadId = null;

                try
                {
                    _logger.LogInternalInformation($"SignalR CreateThread request from {connectionId}");

                    var createThreadRequest = request;
                    // Thread service will also push the user message to stream if thread is sucessfully created
                    var result = await _threadManagementService.CreateUserInitiatedThread(createThreadRequest, userDefinedThreadId);

                    _logger.LogInternalInformation($"Created thread {result.Id} for connection {connectionId}");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInternalInformation($"CreateThread operation cancelled for connection {connectionId} (thread {actualThreadId})");

                    try
                    {
                        var cancelMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "connectionId", connectionId },
                            { "actionName", nameof(CreateThread) },
                            { "isCancelled", true }
                        }
                        };

                        await caller.MessageUpdate(cancelMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to send cancellation message to client {ConnectionId}", connectionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error processing CreateThread in SignalR");

                    try
                    {
                        var errorMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            Contents = [new TextContent("Error processing message: " + ex.Message)],
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                                                    { "connectionId", connectionId },
                        { "actionName", nameof(CreateThread) }
                    }
                        };

                        await caller.Error(errorMessage);
                    }
                    catch (Exception sendEx)
                    {
                        _logger.LogInternalWarning(sendEx, "Failed to send error message to client {ConnectionId}", connectionId);
                    }
                }
            });

            return Task.CompletedTask;
        }

        public Task CreateMessage(Guid threadId, CreateMessageRequest request, bool textOnly = false)
        {
            // Capture context before async operation
            var connectionId = Context.ConnectionId;
            var caller = Clients.Caller;

            // Fire and forget - don't block the SignalR method
            _ = Task.Run(async () =>
            {
                // Get thread-specific cancellation token
                var cancellationToken = GetOrCreateThreadToken(threadId);

                try
                {
                    _logger.LogInternalInformation($"SignalR CreateMessage request from {connectionId} for thread {threadId}");

                    // Get thread and agent context
                    var thread = await _repository.GetThreadAsync(threadId);
                    if (thread == null)
                    {
                        var errorMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            Contents = [new TextContent("Thread not found")],
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "connectionId", connectionId },
                            { "actionName", nameof(CreateMessage) }
                        }
                        };
                        await caller.Error(errorMessage);
                        return;
                    }

                    var agentContexts = await _repository.GetAgentContextsForThreadAsync(threadId);
                    var agentContext = agentContexts.FirstOrDefault(c => (c.AgentType == AgentTypeEnum.Meta || c.AgentType == AgentTypeEnum.Incident) && c.HandoffFromAgentContextId == null);

                    if (agentContext == null)
                    {
                        var errorMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            Contents = [new TextContent("Meta agent context not found")],
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "connectionId", connectionId },
                            { "actionName", nameof(CreateMessage) }
                        }
                        };
                        await caller.Error(errorMessage);
                        return;
                    }

                    // Send user message first
                    var messageId = Guid.NewGuid();
                    var userMessage = new ChatResponseUpdate
                    {
                        AuthorName = request.DisplayName,
                        Role = ChatRole.User,
                        CreatedAt = DateTime.UtcNow,
                        Contents = [new TextContent(request.Text)],
                        AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "messageId", messageId.ToString() },
                        { "threadId", threadId.ToString() },
                        { "userId", request.UserId },
                        { "actionName", nameof(CreateMessage) }
                    }
                    };



                    await caller.MessageUpdate(userMessage);

                    switch (agentContext.AgentType)
                    {
                        case AgentTypeEnum.Incident:
                            _logger.LogInternalInformation($"Processing user message for Incident agent in thread {threadId}");
                            await _agentInboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                                ThreadId: threadId,
                                AgentContextId: agentContext.Id,
                                MessageId: messageId,
                                Message: request.Text,
                                UserId: request.UserId ?? string.Empty,
                                DisplayName: request.DisplayName ?? string.Empty,
                                Timestamp: DateTime.UtcNow,
                                ConversationModifier: request.ConversationModifier
                            ), defaultHandler: false);
                            _logger.LogInternalInformation($"Processed alert message for thread {threadId}");
                            // Record agent action for accepted user message (incident)
                            _logger.LogAgentAction(
                                action: AgentActionEvents.CreateUserMessage,
                                parameter: string.Empty,
                                status: AgentActionStatus.Success,
                                duration: 0,
                                threadId: threadId.ToString(),
                                subAgentName: string.Empty,
                                inputToken: 0,
                                outputToken: 0,
                                threadSource: thread?.Source.ToString() ?? string.Empty,
                                featureConfig: string.Empty
                            );
                            break;
                        default:
                            _logger.LogInternalWarning($"Agent type {agentContext.AgentType} for thread {threadId}");
                            var result = await _agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage(
                                ThreadId: threadId,
                                AgentContextId: agentContext.Id,
                                MessageId: messageId,
                                Message: request.Text,
                                UserId: request.UserId ?? string.Empty,
                                DisplayName: request.DisplayName ?? string.Empty,
                                Timestamp: DateTime.UtcNow,
                                ConversationModifier: request.ConversationModifier
                            ));
                            _logger.LogInternalInformation($"Processed user message for thread {threadId} with result: {result}");
                            if (result.Busy)
                            {
                                var errorMessage = new ChatResponseUpdate
                                {
                                    AuthorName = "System",
                                    Role = ChatRole.System,
                                    CreatedAt = DateTime.UtcNow,
                                    Contents = [new TextContent("The agent is currently busy processing your request. Please try again later.")],
                                    AdditionalProperties = new AdditionalPropertiesDictionary
                                {
                                    { "connectionId", connectionId },
                                    { "actionName", nameof(CreateMessage) },
                                    { "statusCode", 422 }
                                }
                                };

                                await caller.Error(errorMessage);
                            }
                            else
                            {
                                // Record agent action for accepted user message (normal)
                                _logger.LogAgentAction(
                                    action: AgentActionEvents.CreateUserMessage,
                                    parameter: string.Empty,
                                    status: AgentActionStatus.Success,
                                    duration: 0,
                                    threadId: threadId.ToString(),
                                    subAgentName: string.Empty,
                                    inputToken: 0,
                                    outputToken: 0,
                                    threadSource: thread?.Source.ToString() ?? string.Empty,
                                    featureConfig: string.Empty
                                );
                            }
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInternalInformation($"CreateMessage operation cancelled for connection {connectionId}");

                    try
                    {
                        var cancelMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            Contents = [new TextContent("Operation cancelled by user.")],
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "connectionId", connectionId },
                            { "threadId", threadId.ToString() },
                            { "actionName", nameof(CreateMessage) },
                            { "isCancelled", true }
                        }
                        };

                        await caller.MessageUpdate(cancelMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to send cancellation message to client {ConnectionId}", connectionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Error processing CreateMessage in SignalR");

                    try
                    {
                        var errorMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            Contents = [new TextContent("Error processing message: " + ex.Message)],
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "connectionId", connectionId },
                            { "actionName", nameof(CreateMessage) }
                        }
                        };

                        await caller.Error(errorMessage);
                    }
                    catch (Exception sendEx)
                    {
                        _logger.LogInternalWarning(sendEx, "Failed to send error message to client {ConnectionId}", connectionId);
                    }
                }
            });

            return Task.CompletedTask;
        }

        // Cancellation methods
        public async Task CancelThread(Guid threadId)
        {
            try
            {
                _logger.LogInternalInformation($"SignalR CancelThread request from {Context.ConnectionId} for thread {threadId}");

                // 1. Cancel the thread token (immediate cancellation)
                CancelThreadToken(threadId);
                _logger.LogInternalInformation($"Cancelled thread token for {threadId}");

                // 2. Cancel reasoning loop (existing logic)
                var agentContexts = await _repository.GetAgentContextsForThreadAsync(threadId);
                if (agentContexts != null && agentContexts.Any())
                {
                    var agentContext = agentContexts.First();
                    _reasoningLoopManager.CancelCurrentOperation(agentContext);
                    _logger.LogInternalInformation($"Reasoning loop cancellation requested for thread {threadId}");
                }

                // Send cancellation message
                var cancelMessage = new ChatResponseUpdate
                {
                    AuthorName = "System",
                    Role = ChatRole.System,
                    CreatedAt = DateTime.UtcNow,
                    FinishReason = ChatFinishReason.Stop,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "connectionId", Context.ConnectionId },
                        { "threadId", threadId.ToString() },
                        { "actionName", nameof(CancelThread) },
                        { "isCancelled", true }
                    }
                };

                await Clients.Caller.MessageUpdate(cancelMessage);
                _logger.LogInternalInformation($"Sent cancellation message to client for thread {threadId}");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error processing CancelThread in SignalR");
            }
        }

        // Test method for console testing
        public async Task Ping()
        {
            await Clients.Caller.Pong(DateTime.UtcNow);
        }
    }
}
