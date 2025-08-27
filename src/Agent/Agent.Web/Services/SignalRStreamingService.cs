// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Web.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;

namespace Agent.Web.Services
{
    /// <summary>
    /// SignalR implementation of the streaming service
    /// </summary>
    public class SignalRStreamingService : IStreamingService
    {
        private readonly IHubContext<AgentHub, IAgentClient> _hubContext;
        private readonly ILogger<SignalRStreamingService> _logger;

        public SignalRStreamingService(
            IHubContext<AgentHub, IAgentClient> hubContext,
            ILogger<SignalRStreamingService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task StreamThreadUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for cancellation before processing
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInternalInformation("Streaming message for thread {ThreadId} with type {Type}", threadId, type);

                // Create a ChatResponseUpdate with the message and type metadata
                var streamMessage = new ChatResponseUpdate
                {
                    AuthorName = "Azure SRE Agent",
                    Role = ChatRole.Assistant,
                    CreatedAt = recordedDateTime ?? DateTime.UtcNow,
                    Contents = [new TextContent(message)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "streamMessageType", type?.ToString() },
                        { "threadId", threadId.ToString() },
                        { "messageId", messageId?.ToString() ?? Guid.NewGuid().ToString() },
                    }
                };

                await _hubContext.Clients.All.ThreadUpdate(streamMessage);

                _logger.LogInternalInformation("Successfully streamed message for thread {ThreadId} with type {Type}",
                    threadId, type);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
                // Don't rethrow cancellation - it's expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to stream message for thread {ThreadId} with type {Type}",
                    threadId, type);
                // Don't rethrow - streaming failures should not break the tool call
            }
        }

        public async Task StreamActionUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for cancellation before processing
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInternalInformation("Streaming message for thread {ThreadId} with type {Type}", threadId, type);

                // Create a ChatResponseUpdate with the message and type metadata
                var streamMessage = new ChatResponseUpdate
                {
                    AuthorName = "Azure SRE Agent",
                    Role = ChatRole.Assistant,
                    CreatedAt = recordedDateTime ?? DateTime.UtcNow,
                    Contents = [new TextContent(message)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "streamMessageType", type?.ToString() },
                        { "threadId", threadId.ToString() },
                        { "messageId", messageId?.ToString() ?? Guid.NewGuid().ToString() },
                    }
                };

                await _hubContext.Clients.All.ActionUpdate(streamMessage);

                _logger.LogInternalInformation("Successfully streamed message for thread {ThreadId} with type {Type}",
                    threadId, type);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
                // Don't rethrow cancellation - it's expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to stream message for thread {ThreadId} with type {Type}",
                    threadId, type);
                // Don't rethrow - streaming failures should not break the tool call
            }
        }

        public async Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, Guid? agentTaskId = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for cancellation before processing
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInternalInformation("Streaming message for thread {ThreadId} with type {Type}", threadId, type);

                // Create a ChatResponseUpdate with the message and type metadata
                var streamMessage = new ChatResponseUpdate
                {
                    AuthorName = "Azure SRE Agent",
                    Role = ChatRole.Assistant,
                    CreatedAt = recordedDateTime ?? DateTime.UtcNow,
                    Contents = [new TextContent(message)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "streamMessageType", type?.ToString() },
                        { "threadId", threadId.ToString() },
                        { "messageId", messageId?.ToString() ?? Guid.NewGuid().ToString() },
                        { "agentTaskId", agentTaskId?.ToString() ?? string.Empty }
                    }
                };

                await _hubContext.Clients.All.MessageUpdate(streamMessage);

                _logger.LogInternalInformation("Successfully streamed message for thread {ThreadId} with type {Type}",
                    threadId, type);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
                // Don't rethrow cancellation - it's expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to stream message for thread {ThreadId} with type {Type}",
                    threadId, type);
                // Don't rethrow - streaming failures should not break the tool call
            }
        }

        public async Task StreamTaskUpdateAsync(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for cancellation before processing
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInternalInformation("Streaming task update for thread {ThreadId}", threadId);

                // Create a ChatResponseUpdate with the task data and TaskUpdate type
                var streamMessage = new ChatResponseUpdate
                {
                    AuthorName = "Azure SRE Agent",
                    Role = ChatRole.Assistant,
                    CreatedAt = recordedDateTime ?? DateTime.UtcNow,
                    Contents = [new TextContent(taskData)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "streamMessageType", StreamMessageType.TaskUpdate.ToString() },
                        { "threadId", threadId.ToString() },
                        { "messageId", messageId?.ToString() ?? Guid.NewGuid().ToString() },
                    }
                };

                await _hubContext.Clients.All.TaskUpdate(streamMessage);

                _logger.LogInternalInformation("Successfully streamed task update for thread {ThreadId}", threadId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("Task update streaming cancelled for thread {ThreadId}", threadId);
                // Don't rethrow cancellation - it's expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to stream task update for thread {ThreadId}", threadId);
                // Don't rethrow - streaming failures should not break the tool call
            }
        }

        public async Task StreamIncidentUpdateAsync(Guid threadId, string incidentData, Guid? messageId = null, DateTime? recordedDateTime = null, StreamMessageType? messageType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for cancellation before processing
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInternalInformation("Streaming incident update for thread {ThreadId}", threadId);

                // Create a ChatResponseUpdate with the incident data and IncidentStatus type
                var streamMessage = new ChatResponseUpdate
                {
                    AuthorName = "Azure SRE Agent",
                    Role = ChatRole.Assistant,
                    CreatedAt = recordedDateTime ?? DateTime.UtcNow,
                    Contents = [new TextContent(incidentData)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "streamMessageType", messageType?.ToString() ?? StreamMessageType.IncidentStatus.ToString() },
                        { "threadId", threadId.ToString() },
                        { "messageId", messageId?.ToString() ?? Guid.NewGuid().ToString() },
                    }
                };

                await _hubContext.Clients.All.IncidentUpdate(streamMessage);

                _logger.LogInternalInformation("Successfully streamed incident update for thread {ThreadId}", threadId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("Task update streaming cancelled for thread {ThreadId}", threadId);
                // Don't rethrow cancellation - it's expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to stream task update for thread {ThreadId}", threadId);
                // Don't rethrow - streaming failures should not break the tool call
            }
        }

        public async Task StreamChatResponseUpdateAsync(Guid threadId, ChatResponseUpdate update, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check for cancellation before processing
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogInternalInformation("Streaming message for thread {ThreadId}", threadId);

                // Update a ChatResponseUpdate with threadId and messageId if not provided
                update.AdditionalProperties ??= new AdditionalPropertiesDictionary();
                if (!update.AdditionalProperties.TryGetValue("threadId", out var existingThreadId))
                {
                    update.AdditionalProperties["threadId"] = threadId.ToString();
                }

                if (!update.AdditionalProperties.TryGetValue("messageId", out var existingMessageId))
                {
                    update.AdditionalProperties["messageId"] = Guid.NewGuid().ToString();
                }


                await _hubContext.Clients.All.MessageUpdate(update);

                _logger.LogInternalInformation("Successfully streamed message for thread {ThreadId}",
                    threadId);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInternalInformation("Streaming cancelled for thread {ThreadId}", threadId);
                // Don't rethrow cancellation - it's expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to stream message for thread {ThreadId}",
                    threadId);
                // Don't rethrow - streaming failures should not break the tool call
            }
        }
    }
}
