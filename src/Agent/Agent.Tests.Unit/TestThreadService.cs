// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime.Communication;
using Agent.Runtime.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Microsoft.DurableTask.Client;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;

namespace Agent.Tests.Unit;

public class TestThreadService
{
    private readonly ILogger<InMemoryThreadRepository> _repoLogger;
    private readonly ILogger<InMemoryThreadOrchestrationManager> _mappingLogger;
    private readonly ILogger<ThreadService> _serviceLogger;
    private readonly ILogger<SinkService> _sinkLogger;
    private readonly IThreadRepository _threadRepository;
    private readonly InMemoryThreadOrchestrationManager _mappingManager;
    private readonly SinkService _sinkService;
    private readonly ThreadService _threadService;

    public TestThreadService()
    {
        // Setup loggers with null loggers instead of Moq
        _repoLogger = new NullLogger<InMemoryThreadRepository>();
        _mappingLogger = new NullLogger<InMemoryThreadOrchestrationManager>();
        _serviceLogger = new NullLogger<ThreadService>();
        _sinkLogger = new NullLogger<SinkService>();

        // Setup in-memory repositories
        _threadRepository = new InMemoryThreadRepository(_repoLogger);
        _mappingManager = new InMemoryThreadOrchestrationManager(_mappingLogger);

        // Use real sink service with the existing repository
        _sinkService = new SinkService(_threadRepository, _sinkLogger);

        // Setup thread service
        _threadService = new ThreadService(_serviceLogger, _threadRepository, _mappingManager, _sinkService);
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

    [Fact]
    public async Task GetOrchestrationInstanceId_WithExistingMapping_ReturnsInstanceId()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var orchestrationInstanceId = "test-orchestration-id";
        await CreateTestThreadAsync(threadId);

        // Add a mapping for the thread
        await _mappingManager.AddMappingAsync(threadId.ToString(), orchestrationInstanceId);

        // Act

        var result = await _threadService.GetOrchestrationInstanceId(threadId);

        // Assert
        Assert.Equal(orchestrationInstanceId, result);
    }

    [Fact]
    public async Task GetOrchestrationInstanceId_WithNoMapping_ReturnsEmptyString()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        await CreateTestThreadAsync(threadId);

        // Act

        var result = await _threadService.GetOrchestrationInstanceId(threadId);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task CleanOrchestration_WithFailedOrchestration_ReturnsTrue()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var orchestrationInstanceId = "failed-orchestration-id";

        await CreateTestThreadAsync(threadId);

        // Add a mapping for the thread
        await _mappingManager.AddMappingAsync(threadId.ToString(), orchestrationInstanceId);

        // Create a failed orchestration metadata
        var failedOrchestration = new OrchestrationMetadata(
            "TestOrchestration",
            orchestrationInstanceId)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Failed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _threadService.CleanOrchestration(threadId, orchestrationInstanceId, failedOrchestration);

        // Assert
        Assert.True(result);

        // Check that a message was added to the thread
        var messages = await _threadRepository.GetMessagesAsync(threadId);
        var agentMessage = messages.LastOrDefault(m => m.Author.Role == Role.SREAgent);
        Assert.NotNull(agentMessage);
        Assert.Contains("has failed", agentMessage.Text);

        // Verify mapping was removed
        var mappings = await _mappingManager.GetMappingsByThreadIdAsync(threadId.ToString());
        Assert.Empty(mappings);
    }

    [Fact]
    public async Task CleanOrchestration_WithCompletedOrchestration_ReturnsFalse()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var orchestrationInstanceId = "completed-orchestration-id";

        await CreateTestThreadAsync(threadId);

        // Initial message count
        var initialMessages = (await _threadRepository.GetMessagesAsync(threadId)).Count();

        // Create a completed orchestration metadata
        var completedOrchestration = new OrchestrationMetadata(
            "TestOrchestration",
            orchestrationInstanceId)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Completed,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _threadService.CleanOrchestration(threadId, orchestrationInstanceId, completedOrchestration);

        // Assert
        Assert.False(result);

        // Check no message was added
        var messages = await _threadRepository.GetMessagesAsync(threadId);
        Assert.Equal(initialMessages, messages.Count());
    }

    [Fact]
    public async Task CleanOrchestration_WithTerminatedOrchestration_ReturnsTrue()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var orchestrationInstanceId = "terminated-orchestration-id";

        await CreateTestThreadAsync(threadId);

        // Add a mapping for the thread
        await _mappingManager.AddMappingAsync(threadId.ToString(), orchestrationInstanceId);

        // Create a terminated orchestration metadata (which is also considered completed)
        var terminatedOrchestration = new OrchestrationMetadata(
            "TestOrchestration",
            orchestrationInstanceId)
        {
            RuntimeStatus = OrchestrationRuntimeStatus.Terminated,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        // Act
        var result = await _threadService.CleanOrchestration(threadId, orchestrationInstanceId, terminatedOrchestration);

        // Assert
        Assert.True(result);

        // Check that a message was added to the thread
        var messages = await _threadRepository.GetMessagesAsync(threadId);
        var agentMessage = messages.LastOrDefault(m => m.Author.Role == Role.SREAgent);
        Assert.NotNull(agentMessage);
        Assert.Contains("has failed", agentMessage.Text);

        // Verify mapping was removed
        var mappings = await _mappingManager.GetMappingsByThreadIdAsync(threadId.ToString());
        Assert.Empty(mappings);
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
            ModifiedTimestamp: DateTime.UtcNow.AddMinutes(-10)
        );

        return await _threadRepository.CreateThreadAsync(thread);
    }
}

