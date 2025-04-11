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
    private readonly ILogger<InmemoryThreadRepository> _repoLogger;
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
        _repoLogger = new NullLogger<InmemoryThreadRepository>();
        _mappingLogger = new NullLogger<InMemoryThreadOrchestrationManager>();
        _serviceLogger = new NullLogger<ThreadService>();
        _sinkLogger = new NullLogger<SinkService>();

        // Setup in-memory repositories
        _threadRepository = new InmemoryThreadRepository(_repoLogger);
        _mappingManager = new InMemoryThreadOrchestrationManager(_mappingLogger);

        // Use real sink service with the existing repository
        _sinkService = new SinkService(_threadRepository, _sinkLogger);

        // Setup thread service
        _threadService = new ThreadService(_serviceLogger, _threadRepository, _mappingManager, _sinkService);
    }

    [Fact]
    public async Task ToChatHistory_WithVariousMessageTypes_FiltersCorrectly()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        await CreateTestThreadAsync(threadId);

        // Add various message types
        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Hello, I need help!",
            TimeStamp: DateTime.UtcNow.AddMinutes(-5),
            Author: new Author(Role.User, "user1", "User One"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "I'll help you with that.",
            TimeStamp: DateTime.UtcNow.AddMinutes(-4),
            Author: new Author(Role.SREAgent, "agent1", "SRE Agent"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Plugin log information",
            TimeStamp: DateTime.UtcNow.AddMinutes(-3),
            Author: new Author(Role.PluginLog, "plugin1", "Plugin"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Image content should be skipped",
            TimeStamp: DateTime.UtcNow.AddMinutes(-2),
            Author: new Author(Role.User, "user1", "User One"),
            IsImageContent: true,
            Posted: new Posted(true)
        ));

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Thank you for your help!",
            TimeStamp: DateTime.UtcNow.AddMinutes(-1),
            Author: new Author(Role.User, "user1", "User One"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        // Act
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var chatHistory = await _threadService.ToChatHistory(threadContext);

        // Assert
        Assert.Equal(3, chatHistory.Count); // Should only include 3 messages (2 user + 1 assistant)

        // Check that only User and Assistant messages are included, and images are excluded
        Assert.Equal(ChatRole.User, chatHistory[0].Role);
        Assert.Equal("Hello, I need help!", chatHistory[0].Text);

        Assert.Equal(ChatRole.Assistant, chatHistory[1].Role);
        Assert.Equal("I'll help you with that.", chatHistory[1].Text);

        Assert.Equal(ChatRole.User, chatHistory[2].Role);
        Assert.Equal("Thank you for your help!", chatHistory[2].Text);
    }

    [Fact]
    public async Task ToLLMChatHistory_AddSystemPrompt_PrependedToHistory()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        await CreateTestThreadAsync(threadId);

        await _threadRepository.AddMessageAsync(threadId, new Message(
            Id: Guid.NewGuid(),
            Text: "Hello, I need help!",
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.User, "user1", "User One"),
            IsImageContent: false,
            Posted: new Posted(true)
        ));

        string systemPrompt = "You are a helpful assistant.";

        // Act
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var chatHistory = await _threadService.ToLLMChatHistory(threadContext, systemPrompt);

        // Assert
        Assert.Equal(2, chatHistory.Count); // System prompt + user message
        Assert.Equal(ChatRole.System, chatHistory[0].Role);
        Assert.Equal(systemPrompt, chatHistory[0].Text);
        Assert.Equal(ChatRole.User, chatHistory[1].Role);
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
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var lastUserMessage = await _threadService.GetLastUserMessage(threadContext);

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
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var lastUserMessage = await _threadService.GetLastUserMessage(threadContext);

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
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var result = await _threadService.GetOrchestrationInstanceId(threadContext);

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
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var result = await _threadService.GetOrchestrationInstanceId(threadContext);

        // Assert
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task CleanOrchestration_WithFailedOrchestration_ReturnsTrue()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        var orchestrationInstanceId = "failed-orchestration-id";
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
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
        var result = await _threadService.CleanOrchestration(threadContext, orchestrationInstanceId, failedOrchestration);

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
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
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
        var result = await _threadService.CleanOrchestration(threadContext, orchestrationInstanceId, completedOrchestration);

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
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
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
        var result = await _threadService.CleanOrchestration(threadContext, orchestrationInstanceId, terminatedOrchestration);

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
    public async Task ToChatHistory_EmptyThread_ReturnsEmptyList()
    {
        // Arrange
        var threadId = Guid.NewGuid();
        await CreateTestThreadAsync(threadId);

        // Act
        var threadContext = new ThreadContext(threadId, AgentTypeEnum.MetaAgent);
        var chatHistory = await _threadService.ToChatHistory(threadContext);

        // Assert
        Assert.Empty(chatHistory);
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

