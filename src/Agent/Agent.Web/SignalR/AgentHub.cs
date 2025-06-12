// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Web.Models.WebSocket;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Agent.Runtime.Services;

namespace Agent.Web.SignalR
{
    public class AgentHub : Hub<IAgentClient>
    {
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly ThreadManagementService _threadManagementService;
        private readonly IThreadRepository _repository;
        private readonly ILogger<AgentHub> _logger;

        public AgentHub(
            IAgentInboundCommunicationService agentInboundCommunicationService,
            ThreadManagementService threadManagementService,
            IThreadRepository repository,
            ILogger<AgentHub> logger)
        {
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _threadManagementService = threadManagementService;
            _repository = repository;
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

        public async Task CreateThread(WebSocketCreateThreadRequest request, string streamId = "", bool textOnly = false)
        {
            try
            {
                _logger.LogInternalInformation($"SignalR CreateThread request from {Context.ConnectionId}");

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
                        { "connectionId", Context.ConnectionId },
                        { "actionName", nameof(CreateThread) },
                        { "streamId", streamId }
                    }
                };

                await Clients.Caller.ThreadUpdate(initialMessage);

                // Process the request
                var createMessageRequest = new CreateMessageRequest(
                    request.StartMessage.Text,
                    request.StartMessage.UserId,
                    request.StartMessage.DisplayName
                );
                var createThreadRequest = new CreateThreadRequest(createMessageRequest, request.Source);

                var results = _threadManagementService.CreateUserInitiatedThreadStream(createThreadRequest);
                
                await foreach (var result in results)
                {
                    result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    result.AdditionalProperties["connectionId"] = Context.ConnectionId;
                    result.AdditionalProperties["actionName"] = nameof(CreateThread);
                    result.AdditionalProperties["streamId"] = streamId;

                    if (textOnly)
                    {
                        if (!string.IsNullOrEmpty(result.Text))
                        {
                            await Clients.Caller.TextUpdate(result.Text);
                        }
                        if (result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                        {
                            var toolCallContent = (FunctionCallContent)result.Contents[0];
                            await Clients.Caller.TextUpdate("Calling tool... " + toolCallContent.Name);
                            await Clients.Caller.TextUpdate("Tool call params: " + JsonSerializer.Serialize(toolCallContent.Arguments));
                        }
                        if (result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent))
                        {
                            await Clients.Caller.TextUpdate("Tool call completed.");
                        }
                    }
                    else
                    {
                        if (result.CreatedAt == null || result.CreatedAt < new DateTime(2025, 1, 1))
                        {
                            result.CreatedAt = DateTime.UtcNow;
                        }
                        await Clients.Caller.ThreadUpdate(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error processing CreateThread in SignalR");
                
                var errorMessage = new ChatResponseUpdate
                {
                    AuthorName = "System",
                    Role = ChatRole.System,
                    CreatedAt = DateTime.UtcNow,
                    Contents = [new TextContent("Error processing message: " + ex.Message)],
                    FinishReason = ChatFinishReason.Stop,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "connectionId", Context.ConnectionId },
                        { "actionName", nameof(CreateThread) },
                        { "streamId", streamId }
                    }
                };
                
                await Clients.Caller.Error(errorMessage);
            }
        }

        public async Task CreateMessage(Guid threadId, WebSocketCreateMessageRequest request, string streamId = "", bool textOnly = false)
        {
            try
            {
                _logger.LogInternalInformation($"SignalR CreateMessage request from {Context.ConnectionId} for thread {threadId}");

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
                        { "connectionId", Context.ConnectionId },
                        { "actionName", nameof(CreateMessage) },
                        { "streamId", streamId }
                    }
                };

                await Clients.Caller.MessageUpdate(initialMessage);

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
                            { "connectionId", Context.ConnectionId },
                            { "actionName", nameof(CreateMessage) },
                            { "streamId", streamId }
                        }
                    };
                    await Clients.Caller.Error(errorMessage);
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
                            { "connectionId", Context.ConnectionId },
                            { "actionName", nameof(CreateMessage) },
                            { "streamId", streamId }
                        }
                    };
                    await Clients.Caller.Error(errorMessage);
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
                        { "actionName", nameof(CreateMessage) },
                        { "streamId", streamId }
                    }
                };
                
                await Clients.Caller.MessageUpdate(userMessage);

                // Process the message
                var results = _agentInboundCommunicationService.ProcessUserMessageStreamAsync(new ThreadMessage(
                    ThreadId: threadId,
                    AgentContextId: agentContext.Id,
                    MessageId: messageId,
                    Message: request.Text,
                    UserId: request.UserId ?? string.Empty,
                    DisplayName: request.DisplayName ?? string.Empty,
                    Timestamp: DateTime.UtcNow
                ));

                await foreach (var result in results)
                {
                    result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    result.AdditionalProperties["connectionId"] = Context.ConnectionId;
                    result.AdditionalProperties["actionName"] = nameof(CreateMessage);
                    result.AdditionalProperties["streamId"] = streamId;

                    if (textOnly)
                    {
                        if (!string.IsNullOrEmpty(result.Text))
                        {
                            await Clients.Caller.TextUpdate(result.Text);
                        }
                        if (result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                        {
                            var toolCallContent = (FunctionCallContent)result.Contents[0];
                            await Clients.Caller.TextUpdate("Calling tool... " + toolCallContent.Name);
                            await Clients.Caller.TextUpdate("Tool call params: " + JsonSerializer.Serialize(toolCallContent.Arguments));
                        }
                        if (result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent))
                        {
                            await Clients.Caller.TextUpdate("Tool call completed.");
                        }
                    }
                    else
                    {
                        await Clients.Caller.MessageUpdate(result);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error processing CreateMessage in SignalR");
                
                var errorMessage = new ChatResponseUpdate
                {
                    AuthorName = "System",
                    Role = ChatRole.System,
                    CreatedAt = DateTime.UtcNow,
                    Contents = [new TextContent("Error processing message: " + ex.Message)],
                    FinishReason = ChatFinishReason.Stop,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "connectionId", Context.ConnectionId },
                        { "actionName", nameof(CreateMessage) },
                        { "streamId", streamId }
                    }
                };
                
                await Clients.Caller.Error(errorMessage);
            }
        }

        // Test method for console testing
        public async Task Ping()
        {
            await Clients.Caller.Pong(DateTime.UtcNow);
        }
    }
} 