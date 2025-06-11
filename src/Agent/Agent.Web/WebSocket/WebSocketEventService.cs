// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using WebSocketSharp.Server;
using WebSocketSharp;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Web.Models.WebSocket;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Agent.Runtime.Services;


namespace Agent.Web.WebSocket
{
    public class WebSocketEventService : WebSocketBehavior, IAsyncEventService
    {
        private Guid _webSocketId = Guid.Empty;
        private readonly IAgentInboundCommunicationService _agentInboundCommunicationService;
        private readonly ThreadManagementService _threadManagementService;
        private readonly IThreadRepository _repository;
        private readonly ILogger<WebSocketEventService> _logger;
        public WebSocketEventService(
            IAgentInboundCommunicationService agentInboundCommunicationService,
            ThreadManagementService threadManagementService,
            IThreadRepository repository,
            ILogger<WebSocketEventService> logger
        )
        {
            _agentInboundCommunicationService = agentInboundCommunicationService;
            _threadManagementService = threadManagementService;
            _repository = repository;
            _logger = logger;
            _webSocketId = Guid.NewGuid();
        }

        public FunctionCallContent FunctionCall { get; set; }

        protected override void OnOpen()
        {
            _logger.LogInternalInformation("Client connected to websocketId: " + _webSocketId);
        }

        protected override void OnClose(CloseEventArgs e)
        {
            _logger.LogInternalInformation("Client disconnected. WebsocketId: " + _webSocketId);
        }

        protected override async void OnMessage(MessageEventArgs e)
        {
            _logger.LogInternalInformation("Received from client: " + e.Data);
            try
            {
                var message = ParseMessage(e.Data);
                if (message == null)
                {
                    var errorMessage = new ChatResponseUpdate
                    {
                        AuthorName = "System",
                        Role = ChatRole.System,
                        CreatedAt = DateTime.UtcNow,
                        Contents = [new TextContent("Invalid message format")],
                        FinishReason = ChatFinishReason.Stop,
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            { "websocketId", _webSocketId.ToString() },
                            { "actionName", nameof(OnMessage) },
                        }
                    };
                    Send(JsonSerializer.Serialize(errorMessage));
                    return;
                }

                var streamId = message.StreamId ?? string.Empty;

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
                                { "websocketId", _webSocketId.ToString() },
                                { "actionName", nameof(OnMessage) },
                                { "streamId", streamId }
                            }
                };
                Send(JsonSerializer.Serialize(initialMessage));

                switch (message.MessageType)
                {
                    case "CreateMessage":
                        var threadId = Guid.Parse(message.ThreadId);
                        await ProcessCreateMessage(threadId, message.Content, message.TextOnly ?? false, streamId);
                        break;
                    case "CreateThread":
                        await ProcessCreateThread(message.Content, message.TextOnly ?? false, streamId);
                        break;
                    default:
                        var errorMessage = new ChatResponseUpdate
                        {
                            AuthorName = "System",
                            Role = ChatRole.System,
                            CreatedAt = DateTime.UtcNow,
                            Contents = [new TextContent("Unknown message type: " + message.MessageType)],
                            FinishReason = ChatFinishReason.Stop,
                            AdditionalProperties = new AdditionalPropertiesDictionary
                            {
                                { "websocketId", _webSocketId.ToString() },
                                { "actionName", nameof(OnMessage) },
                                { "streamId", message.StreamId ?? string.Empty }
                            }
                        };
                        _logger.LogInternalError("Unknown message type: " + message.MessageType);
                        Send(JsonSerializer.Serialize(errorMessage));
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError("Error processing message: " + ex.Message);
                var message = ParseMessage(e.Data);
                var streamId = message?.StreamId ?? string.Empty;
                var errorMessage = new ChatResponseUpdate
                {
                    AuthorName = "System",
                    Role = ChatRole.System,
                    CreatedAt = DateTime.UtcNow,
                    Contents = [new TextContent("Error processing message: " + ex.Message)],
                    FinishReason = ChatFinishReason.Stop,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "websocketId", _webSocketId.ToString() },
                        { "actionName", nameof(OnMessage) },
                        { "streamId", streamId }
                    }
                };
                Send(JsonSerializer.Serialize(errorMessage));
            }
        }

        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            _logger.LogInternalError("WebSocket error: " + e.Message);
        }

        private WebSocketRequestMessage ParseMessage(string message)
        {
            try
            {
                return JsonSerializer.Deserialize<WebSocketRequestMessage>(message);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError("Error parsing WebSocket message: " + ex.Message);
                throw new Exception("Invalid message format", ex);
            }
        }

        private async Task ProcessCreateThread(string content, bool textOnly = false, string streamId = "")
        {
            IAsyncEnumerable<ChatResponseUpdate> results = AsyncEnumerable.Empty<ChatResponseUpdate>();
            try
            {
                var createThreadRequestWs = JsonSerializer.Deserialize<WebSocketCreateThreadRequest>(content);
                if (createThreadRequestWs == null)
                {
                    throw new Exception("Invalid message format");
                }
                var createMessageRequest = new CreateMessageRequest(
                    createThreadRequestWs.StartMessage.Text,
                    createThreadRequestWs.StartMessage.UserId,
                    createThreadRequestWs.StartMessage.DisplayName
                );
                var createThreadRequest = new CreateThreadRequest(createMessageRequest, createThreadRequestWs.Source);

                results = _threadManagementService.CreateUserInitiatedThreadStream(createThreadRequest);
                await foreach (var result in results)
                {
                    result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    result.AdditionalProperties["websocketId"] = _webSocketId.ToString();
                    result.AdditionalProperties["actionName"] = nameof(ProcessCreateThread);
                    result.AdditionalProperties["streamId"] = streamId;

                    if (textOnly)
                    {
                        if (!String.IsNullOrEmpty(result.Text))
                        {
                            Send(result.Text);
                        }
                        if (result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                        {
                            var toolCallContent = (FunctionCallContent)result.Contents[0];

                            Send("Calling tool... " + toolCallContent.Name);
                            Send("Tool call params: " + JsonSerializer.Serialize(toolCallContent.Arguments));
                        }
                        if (textOnly && result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent))
                        {
                            Send("Tool call completed.");
                        }
                    }
                    else
                    {
                        if (result.CreatedAt == null || result.CreatedAt < new DateTime(2025, 1, 1))
                        {
                            result.CreatedAt = DateTime.UtcNow; // Ensure CreatedAt is set to now for invalid dates
                        }
                        Send(JsonSerializer.Serialize(result));
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogInternalError("Error processing user message stream: " + ex.Message);
                throw ex;
            }
        }

        private async Task ProcessCreateMessage(Guid threadId, string content, bool textOnly = false, string streamId = "")
        {
            IAsyncEnumerable<ChatResponseUpdate> results = AsyncEnumerable.Empty<ChatResponseUpdate>();
            try
            {
                var createMessageRequest = JsonSerializer.Deserialize<WebSocketCreateMessageRequest>(content);
                if (createMessageRequest == null)
                {
                    throw new Exception("Invalid message format");
                }
                var thread = await _repository.GetThreadAsync(threadId);

                if (thread == null)
                {
                    throw new Exception("Thread not found");
                }

                var agentContexts = await _repository.GetAgentContextsForThreadAsync(threadId);

                // pick out the meta agent context from all the agent contexts
                var agentContext = agentContexts.FirstOrDefault(c => c.AgentType == AgentTypeEnum.Meta && c.HandoffFromAgentContextId == null);
                if (agentContext == null)
                {
                    throw new Exception("Meta agent context not found");
                }
                var messageId = Guid.NewGuid();
                var userMessage = new ChatResponseUpdate
                {
                    AuthorName = createMessageRequest.DisplayName,
                    Role = ChatRole.User,
                    CreatedAt = DateTime.UtcNow,
                    Contents = [new TextContent(createMessageRequest.Text)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "messageId", messageId.ToString() },
                        { "threadId", threadId.ToString() },
                        { "userId", createMessageRequest.UserId },
                        { "actionName", nameof(ProcessCreateMessage) },
                        { "streamId", streamId }
                    }
                };
                Send(JsonSerializer.Serialize(userMessage));

                results = _agentInboundCommunicationService.ProcessUserMessageStreamAsync(new ThreadMessage
                (
                    ThreadId: threadId,
                    AgentContextId: agentContext.Id,
                    MessageId: messageId,
                    Message: createMessageRequest.Text,
                    UserId: createMessageRequest.UserId ?? string.Empty,
                    DisplayName: createMessageRequest.DisplayName ?? string.Empty,
                    Timestamp: DateTime.UtcNow
                ));
                await foreach (var result in results)
                {
                    result.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                    result.AdditionalProperties["websocketId"] = _webSocketId.ToString();
                    result.AdditionalProperties["actionName"] = nameof(ProcessCreateMessage);
                    result.AdditionalProperties["streamId"] = streamId;

                    if (textOnly)
                    {
                        if (!String.IsNullOrEmpty(result.Text))
                        {
                            Send(result.Text);
                        }
                        if (result.FinishReason == ChatFinishReason.ToolCalls && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionCallContent))
                        {
                            var toolCallContent = (FunctionCallContent)result.Contents[0];

                            Send("Calling tool... " + toolCallContent.Name);
                            Send("Tool call params: " + JsonSerializer.Serialize(toolCallContent.Arguments));
                        }
                        if (textOnly && result.Role == ChatRole.Tool && result.Contents.Count > 0 && result.Contents[0].GetType() == typeof(FunctionResultContent))
                        {
                            Send("Tool call completed.");
                        }
                    }
                    else
                    {
                        Send(JsonSerializer.Serialize(result));
                    }
                }

            }
            catch (Exception ex)
            {
                _logger.LogInternalError("Error processing user message stream: " + ex.Message);
                throw ex;
            }
        }

        public void SendMessageAsync(string message)
        {
            try
            {
                Send(message);
            }
            catch (Exception ex)
            {
                // log error but don't halt event service
                _logger.LogInternalError("Error sending message: " + ex.Message);
            }
        }

        public void SendMessageAsync(ChatResponseUpdate message)
        {
            try
            {
                Send(JsonSerializer.Serialize(message));
            }
            catch (Exception ex)
            {
                // log error but don't halt event service
                _logger.LogInternalError("Error sending message: " + ex.Message);
            }
        }
    }
}
