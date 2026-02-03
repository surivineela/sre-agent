// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Framework.Hooks;
using Agent.Runtime.Hooks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Agent.Tests.Unit.Framework.Hooks;

public class PromptHookExecutorTests
{
    private readonly Mock<IChatClientProvider> _mockChatClientProvider;
    private readonly Mock<IChatClient> _mockChatClient;
    private readonly Mock<IHookFileTools> _mockHookFileTools;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<PromptHookExecutor> _logger;

    public PromptHookExecutorTests()
    {
        _mockChatClientProvider = new Mock<IChatClientProvider>();
        _mockChatClient = new Mock<IChatClient>();
        _mockHookFileTools = new Mock<IHookFileTools>();
        _loggerFactory = NullLoggerFactory.Instance;
        _logger = NullLogger<PromptHookExecutor>.Instance;

        // Default setup: use ReasoningFastModel
        _mockChatClientProvider.Setup(p => p.ReasoningFastModel).Returns(_mockChatClient.Object);

        // Default setup: hook file tools does nothing (returns empty paths)
        _mockHookFileTools.Setup(h => h.SaveTranscriptAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((Guid threadId, string content) => $"/tmp/hooks/transcript_{threadId}.txt");
        _mockHookFileTools.Setup(h => h.DeleteTranscriptAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenPromptIsEmpty()
    {
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenPromptIsWhitespace()
    {
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "   " };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsSuccess_WhenLlmReturnsOkTrue()
    {
        SetupMockResponse("{\"ok\": true}");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        Assert.Null(result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsRejection_WhenLlmReturnsOkFalse()
    {
        SetupMockResponse("{\"ok\": false, \"reason\": \"Tasks incomplete\"}");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Tasks incomplete", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesMarkdownCodeBlock()
    {
        SetupMockResponse("```json\n{\"ok\": false, \"reason\": \"Need more work\"}\n```");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.False(result.Ok);
        Assert.Equal("Need more work", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesPlainCodeBlock()
    {
        SetupMockResponse("```\n{\"ok\": true}\n```");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenJsonParsingFails()
    {
        SetupMockResponse("This is not valid JSON");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        // Should default to allowing action (fail-open)
        Assert.True(result.Ok);
        Assert.Contains("Hook error", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenLlmThrows()
    {
        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        // Should default to allowing action (fail-open)
        Assert.True(result.Ok);
        Assert.Contains("Hook error", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsError_WhenTimeout()
    {
        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (IEnumerable<ChatMessage> _, ChatOptions _, CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return CreateChatResponse("{\"ok\": true}");
            });

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete", Timeout = 1 };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        // Should default to allowing action (fail-open)
        Assert.True(result.Ok);
        Assert.Contains("timed out", result.Reason);
    }

    [Fact]
    public async Task ExecuteAsync_UsesSpecifiedModel_WhenProvided()
    {
        var customChatClient = new Mock<IChatClient>();
        SetupMockResponse(customChatClient, "{\"ok\": true}");

        _mockChatClientProvider
            .Setup(p => p.GetModelByKey<IChatClient>("custom-model"))
            .Returns(customChatClient.Object);

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete", Model = "custom-model" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        customChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToReasoningFastModel_WhenModelNotFound()
    {
        _mockChatClientProvider
            .Setup(p => p.GetModelByKey<IChatClient>("nonexistent-model"))
            .Throws(new KeyNotFoundException("Model not found"));

        SetupMockResponse("{\"ok\": true}");

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete", Model = "nonexistent-model" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UsesReasoningFastModel_WhenNoModelSpecified()
    {
        SetupMockResponse("{\"ok\": true}");

        var executor = CreateExecutor();

        // Hook without model specified should use ReasoningFastModel
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        _mockChatClientProvider.Verify(p => p.ReasoningFastModel, Times.Once);
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_HookModelOverridesDefault()
    {
        var hookChatClient = new Mock<IChatClient>();
        SetupMockResponse(hookChatClient, "{\"ok\": true}");

        _mockChatClientProvider
            .Setup(p => p.GetModelByKey<IChatClient>("hook-model"))
            .Returns(hookChatClient.Object);

        var executor = CreateExecutor();

        // Hook with explicit model should use that model, not default
        var hook = new HookDefinition { Prompt = "Check if complete", Model = "hook-model" };
        var context = new StopHookContext();

        var result = await executor.ExecuteAsync(hook, context);

        Assert.True(result.Ok);
        hookChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockChatClient.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.IsAny<ChatOptions>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_SubstitutesArgumentsPlaceholder()
    {
        string? capturedPrompt = null;
        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((messages, _, _) =>
            {
                var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
                capturedPrompt = userMessage?.Text;
            })
            .ReturnsAsync(CreateChatResponse("{\"ok\": true}"));

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Context: $ARGUMENTS\nIs this complete?" };
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            CurrentTurn = 5,
            MaxTurns = 10
        };

        await executor.ExecuteAsync(hook, context);

        Assert.NotNull(capturedPrompt);
        Assert.Contains("test-agent", capturedPrompt);
        Assert.Contains("\"current_turn\"", capturedPrompt);
        Assert.DoesNotContain("$ARGUMENTS", capturedPrompt);
    }

    [Fact]
    public async Task ExecuteAsync_AppendsContext_WhenNoPlaceholder()
    {
        string? capturedPrompt = null;
        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions, CancellationToken>((messages, _, _) =>
            {
                var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User);
                capturedPrompt = userMessage?.Text;
            })
            .ReturnsAsync(CreateChatResponse("{\"ok\": true}"));

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Is the task complete?" };
        var context = new StopHookContext { AgentName = "test-agent" };

        await executor.ExecuteAsync(hook, context);

        Assert.NotNull(capturedPrompt);
        Assert.StartsWith("Is the task complete?", capturedPrompt);
        Assert.Contains("Context:", capturedPrompt);
        Assert.Contains("test-agent", capturedPrompt);
    }

    [Fact]
    public async Task ExecuteAsync_SavesTranscript_WhenExecutionSummaryProvided()
    {
        SetupMockResponse("{\"ok\": true}");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var threadId = Guid.NewGuid();
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            ThreadId = threadId,
            ExecutionSummary = "This is the execution summary content"
        };

        await executor.ExecuteAsync(hook, context);

        // Verify transcript was saved
        _mockHookFileTools.Verify(h => h.SaveTranscriptAsync(threadId, "This is the execution summary content"), Times.Once);

        // Verify transcript was cleaned up
        _mockHookFileTools.Verify(h => h.DeleteTranscriptAsync(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSaveTranscript_WhenExecutionSummaryEmpty()
    {
        SetupMockResponse("{\"ok\": true}");
        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            ExecutionSummary = null
        };

        await executor.ExecuteAsync(hook, context);

        // Verify transcript was NOT saved
        _mockHookFileTools.Verify(h => h.SaveTranscriptAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);

        // Verify cleanup was NOT called
        _mockHookFileTools.Verify(h => h.DeleteTranscriptAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CleansUpTranscript_EvenOnError()
    {
        _mockChatClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        var executor = CreateExecutor();
        var hook = new HookDefinition { Prompt = "Check if complete" };
        var threadId = Guid.NewGuid();
        var context = new StopHookContext
        {
            AgentName = "test-agent",
            ThreadId = threadId,
            ExecutionSummary = "Summary content"
        };

        await executor.ExecuteAsync(hook, context);

        // Verify transcript was cleaned up even though LLM threw
        _mockHookFileTools.Verify(h => h.DeleteTranscriptAsync(It.IsAny<string>()), Times.Once);
    }

    private PromptHookExecutor CreateExecutor()
    {
        return new PromptHookExecutor(_mockChatClientProvider.Object, _mockHookFileTools.Object, _loggerFactory, _logger);
    }

    private void SetupMockResponse(string jsonResponse)
    {
        SetupMockResponse(_mockChatClient, jsonResponse);
    }

    private static void SetupMockResponse(Mock<IChatClient> mockClient, string jsonResponse)
    {
        mockClient
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateChatResponse(jsonResponse));
    }

    private static ChatResponse CreateChatResponse(string text)
    {
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }
}
