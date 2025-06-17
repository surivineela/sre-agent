// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
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

        public Task StreamMessageAsync(Guid threadId, string message, StreamMessageType type)
        {
            var streamedMessage = new StreamedMessage
            {
                ThreadId = threadId,
                Message = message,
                Type = type,
                Timestamp = DateTime.UtcNow
            };

            StreamedMessages.Add(streamedMessage);
            
            _logger.LogInternalInformation("Mock: Streamed message for thread {ThreadId} with type {Type}: {Message}", 
                threadId, type, message);

            return Task.CompletedTask;
        }
    }

    public class StreamedMessage
    {
        public Guid ThreadId { get; set; }
        public string Message { get; set; } = string.Empty;
        public StreamMessageType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }
} 
