// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Tests.Common.Mocks
{
    /// <summary>
    /// Mock implementation of IStreamingService for testing
    /// </summary>
    public class MockStreamingService : IStreamingService
    {
        private readonly ILogger<MockStreamingService> _logger;

        public List<StreamedMessage> StreamedMessages { get; } = new();

        public MockStreamingService(ILogger<MockStreamingService> logger)
        {
            _logger = logger;
        }

        public Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, CancellationToken cancellationToken = default)
        {
            var streamedMessage = new StreamedMessage
            {
                ThreadId = threadId,
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow,
                MessageId = messageId ?? Guid.NewGuid()
            };

            StreamedMessages.Add(streamedMessage);

            _logger.LogInternalInformation("Mock: Streamed message for thread {ThreadId} with type {Type}: {Message}",
                threadId, type, message);

            return Task.CompletedTask;
        }

        public Task StreamChatResponseUpdateAsync(Guid threadId, ChatResponseUpdate update, CancellationToken cancellationToken = default)
        {
            var streamedMessage = new StreamedMessage
            {
                ThreadId = threadId,
                Message = update.Text,
                Type = StreamMessageType.Image,
                Timestamp = DateTime.UtcNow,
                MessageId = update.AdditionalProperties?.GetValueOrDefault("messageId") as Guid? ?? Guid.NewGuid()
            };

            StreamedMessages.Add(streamedMessage);

            _logger.LogInternalInformation("Mock: Streamed message for thread {ThreadId}",
                threadId);

            return Task.CompletedTask;
        }
    }

    public class StreamedMessage
    {
        public Guid ThreadId { get; set; }
        public string Message { get; set; } = string.Empty;
        public StreamMessageType? Type { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid MessageId { get; set; }
    }
}
