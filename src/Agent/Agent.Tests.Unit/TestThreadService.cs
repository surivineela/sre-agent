// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime.Communication;
using Agent.Runtime.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Tests.Unit;

public class TestThreadService
{
    private readonly ILogger<InMemoryThreadRepository> _repoLogger;
    private readonly ILogger<ThreadService> _serviceLogger;
    private readonly ILogger<SinkService> _sinkLogger;
    private readonly InMemoryThreadRepository _threadRepository;
    private readonly SinkService _sinkService;
    private readonly ThreadService _threadService;

    public TestThreadService()
    {
        // Setup loggers with null loggers instead of Moq
        _repoLogger = new NullLogger<InMemoryThreadRepository>();
        _serviceLogger = new NullLogger<ThreadService>();
        _sinkLogger = new NullLogger<SinkService>();

        // Setup in-memory repositories
        _threadRepository = new InMemoryThreadRepository(_repoLogger);

        // Use real sink service with the existing repository
        _sinkService = new SinkService(_threadRepository, _sinkLogger);

        // Setup thread service
        _threadService = new ThreadService(_threadRepository);
    }

    [Fact]
    public async Task GetLastUserMessage_WithMultipleMessages_ReturnsLatestUserMessage()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        await CreateTestThreadAsync(threadId);

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "First message",
            TimeStamp: DateTime.UtcNow.AddMinutes(-3),
            Author: new Author(Role.User, "user1", "User One"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Agent response",
            TimeStamp: DateTime.UtcNow.AddMinutes(-2),
            Author: new Author(Role.SREAgent, "agent1", "SRE Agent"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Last user message",
            TimeStamp: DateTime.UtcNow.AddMinutes(-1),
            Author: new Author(Role.User, "user1", "User One"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        // Act
        var lastUserMessage = await _threadService.GetLastUserMessage(threadId);

        // Assert
        Assert.Equal("Last user message", lastUserMessage);
    }

    [Fact]
    public async Task GetLastUserMessage_WithNoUserMessages_ReturnsEmptyString()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        await CreateTestThreadAsync(threadId);

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Agent message",
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent1", "SRE Agent"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        // Act

        var lastUserMessage = await _threadService.GetLastUserMessage(threadId);

        // Assert
        Assert.Equal(string.Empty, lastUserMessage);
    }

    // Helper method to create a test thread
    private async Task<Thread> CreateTestThreadAsync(Guid threadId)
    {
        var message = new Message(
            Id: Guid.NewGuid(),
            Text: "Start message",
            TimeStamp: DateTime.UtcNow.AddMinutes(-10),
            Author: new Author(Role.System, "system", "System"),
            IsImageContent: false,
            Posted: new Posted(true)
        );

        var thread = new Thread(
            Id: threadId,
            Title: "Test Thread",
            StartMessage: message,
            LastMessage: message, // when the thread is first created the start message is the last message
            CreatedTimestamp: DateTime.UtcNow.AddMinutes(-10),
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-10),
            FeatureConfig: null
        );

        return await _threadRepository.CreateThreadAsync(thread);
    }
}

