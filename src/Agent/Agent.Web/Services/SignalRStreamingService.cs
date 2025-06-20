// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Web.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.AI;
using Agent.Logging;

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


        public async Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, CancellationToken cancellationToken = default)
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
                    CreatedAt = DateTime.UtcNow,
                    Contents = [new TextContent(message)],
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        { "streamMessageType", type?.ToString() },
                        { "threadId", threadId.ToString() },
                        { "messageId", messageId?.ToString() ?? Guid.NewGuid().ToString() },
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
    }
}
