// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Agent.Tests.Unit.Framework.Hooks;

/// <summary>
/// Tests for PostToolUseHookHelper. Uses real HookManager with mocked IHookExecutor
/// to verify the helper orchestrates hook execution correctly.
/// </summary>
public class PostToolUseHookHelperTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Creates a HookManager with a mock executor that returns the specified result.
    /// </summary>
    private static HookManager CreateHookManager(HookResult resultToReturn, Action<HookContext>? captureContext = null)
    {
        var mockExecutor = new Mock<IHookExecutor>();
        mockExecutor.Setup(e => e.SupportedType).Returns(HookType.Prompt);
        mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .Callback<HookDefinition, HookContext, CancellationToken>((_, ctx, _) => captureContext?.Invoke(ctx))
            .ReturnsAsync(resultToReturn);

        return new HookManager(
            new[] { mockExecutor.Object },
            NullLogger<HookManager>.Instance,
            enabled: true);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsAllow_WhenHookManagerIsNull()
    {
        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: null,
            agentHooks: CreateAgentHooksWithPostToolUseHook(),
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "Success",
            toolSucceeded: true,
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.AllowToolResult);
        Assert.Null(result.AdditionalContextMessage);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsAllow_WhenAgentHooksIsNull()
    {
        var hookManager = CreateHookManager(HookResult.Success());

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: null,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "Success",
            toolSucceeded: true,
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.AllowToolResult);
        Assert.Null(result.AdditionalContextMessage);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsAllow_WhenNoPostToolUseHooksConfigured()
    {
        var hookManager = CreateHookManager(HookResult.Success());
        var agentHooks = new AgentHookConfiguration(); // Empty hooks

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "Success",
            toolSucceeded: true,
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.AllowToolResult);
        Assert.Null(result.AdditionalContextMessage);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsAllow_WhenHookApproves()
    {
        var hookManager = CreateHookManager(HookResult.Success());
        var agentHooks = CreateAgentHooksWithPostToolUseHook();

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "File edited",
            toolSucceeded: true,
            executionSummary: "Executed tools",
            logger: _logger);

        Assert.True(result.AllowToolResult);
        Assert.Null(result.BlockMessage);
        Assert.Null(result.AdditionalContextMessage);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsBlock_WhenHookRejects()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Invalid tool output"));
        var agentHooks = CreateAgentHooksWithPostToolUseHook();

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "Bad output",
            toolSucceeded: true,
            executionSummary: "Executed tools",
            logger: _logger);

        Assert.False(result.AllowToolResult);
        Assert.NotNull(result.BlockMessage);
        Assert.Contains("Invalid tool output", result.BlockMessage.Text);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsAllowWithContext_WhenHookApprovesWithAdditionalContext()
    {
        var hookResult = HookResult.SuccessWithContext("File was auto-formatted");
        var hookManager = CreateHookManager(hookResult);
        var agentHooks = CreateAgentHooksWithPostToolUseHook();

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "File edited",
            toolSucceeded: true,
            executionSummary: "Executed tools",
            logger: _logger);

        Assert.True(result.AllowToolResult);
        Assert.NotNull(result.AdditionalContextMessage);
        Assert.Contains("File was auto-formatted", result.AdditionalContextMessage.Text);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_ReturnsBlockWithContext_WhenHookRejectsWithAdditionalContext()
    {
        var hookResult = HookResult.RejectWithContext("Invalid output", "Consider using different parameters");
        var hookManager = CreateHookManager(hookResult);
        var agentHooks = CreateAgentHooksWithPostToolUseHook();

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "Bad output",
            toolSucceeded: true,
            executionSummary: "Executed tools",
            logger: _logger);

        Assert.False(result.AllowToolResult);
        Assert.NotNull(result.BlockMessage);
        Assert.Contains("Invalid output", result.BlockMessage.Text);
        Assert.NotNull(result.AdditionalContextMessage);
        Assert.Contains("Consider using different parameters", result.AdditionalContextMessage.Text);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_PassesCorrectContext()
    {
        PostToolUseHookContext? capturedContext = null;
        var hookManager = CreateHookManager(HookResult.Success(), ctx => capturedContext = ctx as PostToolUseHookContext);
        var agentHooks = CreateAgentHooksWithPostToolUseHook();
        var threadId = Guid.NewGuid();
        var toolInput = new Dictionary<string, object> { ["file"] = "test.py", ["content"] = "code" };

        await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "my-agent",
            currentTurn: 7,
            maxTurns: 15,
            threadId: threadId,
            toolName: "Edit",
            toolInput: toolInput,
            toolResult: "File edited successfully",
            toolSucceeded: true,
            executionSummary: "Execution summary text",
            logger: _logger);

        Assert.NotNull(capturedContext);
        Assert.Equal("my-agent", capturedContext.AgentName);
        Assert.Equal(7, capturedContext.CurrentTurn);
        Assert.Equal(15, capturedContext.MaxTurns);
        Assert.Equal(threadId, capturedContext.ThreadId);
        Assert.Equal("Edit", capturedContext.ToolName);
        Assert.Equal(toolInput, capturedContext.ToolInput);
        Assert.Equal("File edited successfully", capturedContext.ToolResult);
        Assert.True(capturedContext.ToolSucceeded);
        Assert.Equal("Execution summary text", capturedContext.ExecutionSummary);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_PassesToolSucceededFalse_WhenToolFailed()
    {
        PostToolUseHookContext? capturedContext = null;
        var hookManager = CreateHookManager(HookResult.Success(), ctx => capturedContext = ctx as PostToolUseHookContext);
        var agentHooks = CreateAgentHooksWithPostToolUseHook();

        await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "Edit",
            toolInput: new { file = "test.py" },
            toolResult: "Error: File not found",
            toolSucceeded: false, // Tool failed
            executionSummary: "Summary",
            logger: _logger);

        Assert.NotNull(capturedContext);
        Assert.False(capturedContext.ToolSucceeded);
        Assert.Equal("Error: File not found", capturedContext.ToolResult);
    }

    [Fact]
    public async Task ExecutePostToolUseHooksAsync_UsesMatcherToFilterHooks()
    {
        var mockExecutor = new Mock<IHookExecutor>();
        mockExecutor.Setup(e => e.SupportedType).Returns(HookType.Prompt);
        mockExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HookResult.Reject("Should not run"));

        var hookManager = new HookManager(
            new[] { mockExecutor.Object },
            NullLogger<HookManager>.Instance,
            enabled: true);

        // Create hooks with matcher that doesn't match "View" tool
        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check if valid",
                        Matcher = "Edit|Write" // Only matches Edit or Write, not View
                    }
                ]
            }
        };

        var result = await PostToolUseHookHelper.ExecutePostToolUseHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            toolName: "View", // Tool name doesn't match the matcher
            toolInput: new { file = "test.py" },
            toolResult: "File contents",
            toolSucceeded: true,
            executionSummary: "Summary",
            logger: _logger);

        // Should allow because no hooks matched this tool
        Assert.True(result.AllowToolResult);

        // Verify executor was never called
        mockExecutor.Verify(
            e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static AgentHookConfiguration CreateAgentHooksWithPostToolUseHook()
    {
        return new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["PostToolUse"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Is the tool output valid?",
                        Matcher = "*" // Wildcard to match all tools
                    }
                ]
            }
        };
    }
}
