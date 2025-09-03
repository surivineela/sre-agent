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

        public Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
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

        public Task StreamTaskUpdateAsync(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
        {
            var streamedMessage = new StreamedMessage
            {
                ThreadId = threadId,
                Message = taskData,
                Type = StreamMessageType.TaskUpdate,
                Timestamp = DateTime.UtcNow,
                MessageId = messageId ?? Guid.NewGuid()
            };

            StreamedMessages.Add(streamedMessage);

            _logger.LogInternalInformation("Mock: Streamed task update for thread {ThreadId}: {TaskData}",
                threadId, taskData);

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

        public Task StreamThreadUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
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

            _logger.LogInternalInformation("Mock: Streamed thread update for thread {ThreadId} with type {Type}: {Message}",
                threadId, type, message);

            return Task.CompletedTask;
        }

        public Task StreamActionUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
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

        public Task StreamIncidentUpdateAsync(Guid threadId, string incidentData, Guid? messageId = null, DateTime? recordedDateTime = null, StreamMessageType? messageType = null, CancellationToken cancellationToken = default)
        {
            var streamedMessage = new StreamedMessage
            {
                ThreadId = threadId,
                Message = incidentData,
                Type = messageType,
                Timestamp = DateTime.UtcNow,
                MessageId = messageId ?? Guid.NewGuid()
            };

            StreamedMessages.Add(streamedMessage);

            _logger.LogInternalInformation("Mock: Streamed incident update for thread {ThreadId} with type {Type}: {Message}",
                threadId, messageType, incidentData);

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
