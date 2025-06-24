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

                    // Send initial analyzing message
                    var initialToolCallContent = new FunctionCallContent(Guid.NewGuid().ToString(), "", new AdditionalPropertiesDictionary());
                    initialToolCallContent.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    initialToolCallContent.AdditionalProperties.Add("userDescription", "Analyzing...");

                    var initialMessage = new ChatResponseUpdate
                    {
                        AuthorName = "System",
                        Role = ChatRole.System,
                        CreatedAt = DateTime.UtcNow,
                        Contents = [initialToolCallContent],
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "connectionId", connectionId },
                            { "actionName", nameof(CreateThread) }
                        }
                    };

                    await caller.ThreadUpdate(initialMessage);

                // Process the request without cancellation initially
                var createThreadRequest = request;
                var results = _threadManagementService.CreateUserInitiatedThreadStream(createThreadRequest, CancellationToken.None, userDefinedThreadId);

                await foreach (var result in results)
                {
                    // Get the actual thread ID from the first result and set up cancellation
                    if (actualThreadId == null && result.AdditionalProperties?.TryGetValue("threadId", out var threadIdObj) == true)
                    {
                        if (Guid.TryParse(threadIdObj?.ToString(), out var parsedThreadId))
                        {
                            actualThreadId = parsedThreadId;
                            _logger.LogInternalInformation($"Got thread ID {actualThreadId} for cancellation tracking");
                        }
                    }
                    
                    // Check for cancellation using the real thread token (if available and set up)
                    if (actualThreadId.HasValue && _threadCancellationTokens.TryGetValue(actualThreadId.Value, out var tokenSource) && tokenSource.Token.IsCancellationRequested)
                    {
                        _logger.LogInternalInformation($"CreateThread operation cancelled for thread {actualThreadId}");
                        break;
                    }
                    
                    result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    result.AdditionalProperties["connectionId"] = connectionId;
                    result.AdditionalProperties["actionName"] = nameof(CreateThread);

                    if (textOnly)
                    {
                        if (!string.IsNullOrEmpty(result.Text))
                        {
                            await caller.TextUpdate(result.Text);
                        }
                        if (result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                        {
                            var toolCallContent = (FunctionCallContent)result.Contents[0];
                            var safeDescription = toolCallContent.AdditionalProperties?.TryGetValue("userDescription", out var desc) == true 
                                ? desc?.ToString() ?? ToolDescriptionHelper.DefaultSafeDescription
                                : ToolDescriptionHelper.GetUserDescriptionForFunctionCallName(toolCallContent.Name);
                            await caller.TextUpdate(safeDescription);
                        }
                        if (result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent))
                        {
                            await caller.TextUpdate("Tool call completed.");
                        }
                    }
                    else
                    {
                        if (result.CreatedAt == null || result.CreatedAt < new DateTime(2025, 1, 1))
                        {
                            result.CreatedAt = DateTime.UtcNow;
                        }
                        await caller.ThreadUpdate(result);
                    }
                }
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

                    await caller.ThreadUpdate(cancelMessage);
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

                // Send initial analyzing message
                var initialToolCallContent = new FunctionCallContent(Guid.NewGuid().ToString(), "", new AdditionalPropertiesDictionary());
                initialToolCallContent.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                initialToolCallContent.AdditionalProperties.Add("userDescription", "Analyzing...");

                var initialMessage = new ChatResponseUpdate
                {
                    AuthorName = "System",
                    Role = ChatRole.System,
                    CreatedAt = DateTime.UtcNow,
                    Contents = [initialToolCallContent],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "connectionId", connectionId },
                        { "actionName", nameof(CreateMessage) }
                    }
                };

                await caller.MessageUpdate(initialMessage);

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
                var agentContext = agentContexts.FirstOrDefault(c => c.AgentType == AgentTypeEnum.Meta && c.HandoffFromAgentContextId == null);

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

                // Process the message
                var results = _agentInboundCommunicationService.ProcessUserMessageStreamAsync(new ThreadMessage(
                    ThreadId: threadId,
                    AgentContextId: agentContext.Id,
                    MessageId: messageId,
                    Message: request.Text,
                    UserId: request.UserId ?? string.Empty,
                    DisplayName: request.DisplayName ?? string.Empty,
                    Timestamp: DateTime.UtcNow
                ), cancellationToken);

                await foreach (var result in results.WithCancellation(cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    result.AdditionalProperties["connectionId"] = connectionId;
                    result.AdditionalProperties["actionName"] = nameof(CreateMessage);

                    if (textOnly)
                    {
                        if (!string.IsNullOrEmpty(result.Text))
                        {
                            await caller.TextUpdate(result.Text);
                        }
                        if (result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                        {
                            var toolCallContent = (FunctionCallContent)result.Contents[0];
                            var safeDescription = toolCallContent.AdditionalProperties?.TryGetValue("userDescription", out var desc) == true 
                                ? desc?.ToString() ?? ToolDescriptionHelper.DefaultSafeDescription
                                : ToolDescriptionHelper.GetUserDescriptionForFunctionCallName(toolCallContent.Name);
                            await caller.TextUpdate(safeDescription);
                        }
                        if (result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent))
                        {
                            await caller.TextUpdate("Tool call completed.");
                        }
                    }
                    else
                    {
                        await caller.MessageUpdate(result);
                    }
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
