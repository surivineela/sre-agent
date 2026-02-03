// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework.Hooks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Agent.Tests.Unit.Framework.Hooks;

/// <summary>
/// Tests for StopHookHelper. Uses real HookManager with mocked IHookExecutor
/// since HookManager.ExecuteHooksAsync is not virtual.
/// </summary>
public class StopHookHelperTests
{
    private readonly ILogger _logger = NullLogger.Instance;

    /// <summary>
    /// Creates a HookManager with a mock executor that returns the specified result for prompt hooks.
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

    /// <summary>
    /// Creates a HookManager with separate mock executors for prompt and command hooks.
    /// </summary>
    private static HookManager CreateHookManagerWithBothExecutors(
        HookResult promptResult,
        HookResult commandResult,
        Action<HookContext>? capturePromptContext = null,
        Action<HookContext>? captureCommandContext = null)
    {
        var mockPromptExecutor = new Mock<IHookExecutor>();
        mockPromptExecutor.Setup(e => e.SupportedType).Returns(HookType.Prompt);
        mockPromptExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .Callback<HookDefinition, HookContext, CancellationToken>((_, ctx, _) => capturePromptContext?.Invoke(ctx))
            .ReturnsAsync(promptResult);

        var mockCommandExecutor = new Mock<IHookExecutor>();
        mockCommandExecutor.Setup(e => e.SupportedType).Returns(HookType.Command);
        mockCommandExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .Callback<HookDefinition, HookContext, CancellationToken>((_, ctx, _) => captureCommandContext?.Invoke(ctx))
            .ReturnsAsync(commandResult);

        return new HookManager(
            new[] { mockPromptExecutor.Object, mockCommandExecutor.Object },
            NullLogger<HookManager>.Instance,
            enabled: true);
    }

    #region Basic Tests

    [Fact]
    public async Task ExecuteStopHooksAsync_ReturnsStop_WhenHookManagerIsNull()
    {
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: null,
            agentHooks: CreateAgentHooksWithStopHook(),
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop);
        Assert.Null(result.ContinueMessage);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_ReturnsStop_WhenAgentHooksIsNull()
    {
        var hookManager = CreateHookManager(HookResult.Success());

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: null,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop);
        Assert.Null(result.ContinueMessage);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_ReturnsStop_WhenNoStopHooksConfigured()
    {
        var hookManager = CreateHookManager(HookResult.Success());
        var agentHooks = new AgentHookConfiguration(); // Empty hooks

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop);
        Assert.Null(result.ContinueMessage);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_ReturnsStop_WhenHookApproves()
    {
        var hookManager = CreateHookManager(HookResult.Success());
        var agentHooks = CreateAgentHooksWithStopHook();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Task completed",
            executionSummary: "Executed 3 tools",
            logger: _logger);

        Assert.True(result.ShouldStop);
        Assert.Null(result.ContinueMessage);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_ReturnsContinue_WhenHookRejects()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Task not fully complete"));
        var agentHooks = CreateAgentHooksWithStopHook();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Task completed",
            executionSummary: "Executed 3 tools",
            logger: _logger);

        Assert.False(result.ShouldStop);
        Assert.NotNull(result.ContinueMessage);
        Assert.Equal(ChatRole.User, result.ContinueMessage.Role);
        Assert.Equal("Task not fully complete", result.ContinueMessage.Text);
        Assert.Equal(1, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_IncrementsRejectionCount()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Keep working"));
        var agentHooks = CreateAgentHooksWithStopHook();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 1, // Already had one rejection
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop);
        Assert.Equal(2, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_ForcesStop_WhenRejectionLimitReached()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Keep working"));
        var agentHooks = CreateAgentHooksWithStopHook();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 2, // Already at 2, limit is 3
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should force stop even though hook rejected
        Assert.True(result.ShouldStop);
        Assert.Null(result.ContinueMessage);
        Assert.Equal(3, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_PassesCorrectContext()
    {
        StopHookContext? capturedContext = null;
        var hookManager = CreateHookManager(HookResult.Success(), ctx => capturedContext = ctx as StopHookContext);
        var agentHooks = CreateAgentHooksWithStopHook();
        var threadId = Guid.NewGuid();

        await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "my-agent",
            currentTurn: 7,
            maxTurns: 15,
            threadId: threadId,
            currentRejectionCount: 2,
            defaultMaxRejections: 5,
            finalOutput: "Final output text",
            executionSummary: "Execution summary text",
            logger: _logger);

        Assert.NotNull(capturedContext);
        Assert.Equal("my-agent", capturedContext.AgentName);
        Assert.Equal(7, capturedContext.CurrentTurn);
        Assert.Equal(15, capturedContext.MaxTurns);
        Assert.Equal(threadId, capturedContext.ThreadId);
        Assert.True(capturedContext.StopHookActive); // currentRejectionCount > 0
        Assert.Equal(2, capturedContext.StopRejectionCount);
        Assert.Equal("Final output text", capturedContext.FinalOutput);
        Assert.Equal("Execution summary text", capturedContext.ExecutionSummary);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_SetsStopHookActiveToFalse_WhenNoRejections()
    {
        StopHookContext? capturedContext = null;
        var hookManager = CreateHookManager(HookResult.Success(), ctx => capturedContext = ctx as StopHookContext);
        var agentHooks = CreateAgentHooksWithStopHook();

        await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 1,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0, // No rejections yet
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.NotNull(capturedContext);
        Assert.False(capturedContext.StopHookActive);
    }

    #endregion

    #region Rejection Without Reason Tests (Reason Required)

    [Fact]
    public async Task ExecuteStopHooksAsync_Stops_WhenPromptHookRejectsWithoutReason()
    {
        // A rejection without a reason is treated as approval
        var hookManager = CreateHookManager(new HookResult { Ok = false, Reason = null });
        var agentHooks = CreateAgentHooksWithStopHook();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should stop because rejection has no reason
        Assert.True(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount); // Counter not incremented
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_Stops_WhenPromptHookRejectsWithEmptyReason()
    {
        // Empty string reason is also treated as no reason
        var hookManager = CreateHookManager(new HookResult { Ok = false, Reason = "   " });
        var agentHooks = CreateAgentHooksWithStopHook();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should stop because rejection has whitespace-only reason
        Assert.True(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_Stops_WhenCommandHookRejectsWithoutReason()
    {
        var mockCommandExecutor = new Mock<IHookExecutor>();
        mockCommandExecutor.Setup(e => e.SupportedType).Returns(HookType.Command);
        mockCommandExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HookResult { Ok = false, Reason = null });

        var hookManager = new HookManager(
            new[] { mockCommandExecutor.Object },
            NullLogger<HookManager>.Instance,
            enabled: true);

        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Command,
                        Command = "check.sh"
                    }
                ]
            }
        };

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should stop because command rejection has no reason
        Assert.True(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_PromptRejectsWithReason_CommandRejectsWithoutReason_OnlyPromptCounts()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Reject("Prompt has reason"),
            commandResult: new HookResult { Ok = false, Reason = null }); // No reason

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should continue because prompt rejected with reason
        Assert.False(result.ShouldStop);
        Assert.Equal(1, result.UpdatedRejectionCount);
        Assert.Equal("Prompt has reason", result.ContinueMessage!.Text);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_PromptRejectsWithoutReason_CommandRejectsWithReason_OnlyCommandCounts()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: new HookResult { Ok = false, Reason = null }, // No reason
            commandResult: HookResult.Reject("Command has reason"));

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should continue because command rejected with reason
        // But counter not incremented because prompt didn't reject with reason
        Assert.False(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount);
        Assert.Equal("Command has reason", result.ContinueMessage!.Text);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_BothRejectWithoutReason_Stops()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: new HookResult { Ok = false, Reason = null },
            commandResult: new HookResult { Ok = false, Reason = "" });

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should stop because neither has a reason
        Assert.True(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    #endregion

    #region Prompt Hook MaxRejections Tests

    [Fact]
    public async Task ExecuteStopHooksAsync_UsesHookLevelMaxRejections_WhenSpecified()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Keep working"));
        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Is the task complete?",
                        MaxRejections = 10 // Override the default
                    }
                ]
            }
        };

        // With hook-level MaxRejections=10, should continue at rejection count 4
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 4,
            defaultMaxRejections: 3, // Would force stop if used
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop); // Hook override allows continuing
        Assert.Equal(5, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_ForcesStopAtHookLevelLimit()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Keep working"));
        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Is the task complete?",
                        MaxRejections = 5
                    }
                ]
            }
        };

        // At rejection count 4, next rejection will be 5 which equals the hook limit
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 4,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop); // Hook limit reached
        Assert.Equal(5, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_UsesMaxValueFromMultiplePromptHooks()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Keep working"));
        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "First check",
                        MaxRejections = 5
                    },
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Second check",
                        MaxRejections = 15 // Higher value - should be used
                    },
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Third check",
                        MaxRejections = 8
                    }
                ]
            }
        };

        // With max hook MaxRejections=15, should continue at rejection count 10
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 10,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop); // Max of 15 allows continuing
        Assert.Equal(11, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_UsesDefaultWhenNoPromptHookSpecifiesMaxRejections()
    {
        var hookManager = CreateHookManager(HookResult.Reject("Keep working"));
        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check task",
                        MaxRejections = null // Not specified
                    }
                ]
            }
        };

        // With no hook-level MaxRejections, should use default of 3
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 2, // At 2, next rejection is 3 which equals default
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop); // Default limit reached
        Assert.Equal(3, result.UpdatedRejectionCount);
    }

    #endregion

    #region Mixed Hook Scenarios - Prompt and Command

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_BothApprove_Stops()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Success(),
            commandResult: HookResult.Success());

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_PromptRejectsCommandApproves_IncrementsCounter()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Reject("Prompt says keep working"),
            commandResult: HookResult.Success());

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop);
        Assert.Equal(1, result.UpdatedRejectionCount); // Counter incremented because prompt rejected
        Assert.Contains("Prompt says keep working", result.ContinueMessage!.Text);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_PromptApprovesCommandRejects_DoesNotIncrementCounter()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Success(),
            commandResult: HookResult.Reject("Command says keep working"));

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount); // Counter NOT incremented because only command rejected
        Assert.Contains("Command says keep working", result.ContinueMessage!.Text);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_BothReject_OnlyPromptCountsTowardsLimit()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Reject("Prompt reason"),
            commandResult: HookResult.Reject("Command reason"));

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop);
        Assert.Equal(1, result.UpdatedRejectionCount); // Only +1 even though both rejected
        Assert.Contains("Prompt reason", result.ContinueMessage!.Text);
        Assert.Contains("Command reason", result.ContinueMessage!.Text);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_PromptLimitReached_ForcesStopEvenIfCommandWouldContinue()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Reject("Prompt reason"),
            commandResult: HookResult.Reject("Command reason")); // Both reject but shouldn't matter

        var agentHooks = CreateMixedHooksConfiguration();

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 2, // At 2, next prompt rejection is 3 which equals default limit
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        // Should force stop because prompt rejection limit reached
        Assert.True(result.ShouldStop);
        Assert.Equal(3, result.UpdatedRejectionCount);
    }

    #endregion

    #region Command-Only Hook Scenarios (No Limit)

    [Fact]
    public async Task ExecuteStopHooksAsync_CommandOnlyHooks_CanRejectBeyondDefaultLimit()
    {
        // Create a HookManager that only has command executor
        var mockCommandExecutor = new Mock<IHookExecutor>();
        mockCommandExecutor.Setup(e => e.SupportedType).Returns(HookType.Command);
        mockCommandExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HookResult.Reject("Command keeps rejecting"));

        var hookManager = new HookManager(
            new[] { mockCommandExecutor.Object },
            NullLogger<HookManager>.Instance,
            enabled: true);

        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Command,
                        Command = "check_completion.sh"
                    }
                ]
            }
        };

        // Even at rejection count 100 (way beyond default 3), should continue
        // because command hooks have no implicit limit
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 100,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop); // No limit for command-only hooks
        Assert.Equal(100, result.UpdatedRejectionCount); // Counter NOT incremented
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_CommandOnlyHooks_Approves_Stops()
    {
        var mockCommandExecutor = new Mock<IHookExecutor>();
        mockCommandExecutor.Setup(e => e.SupportedType).Returns(HookType.Command);
        mockCommandExecutor
            .Setup(e => e.ExecuteAsync(It.IsAny<HookDefinition>(), It.IsAny<HookContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HookResult.Success());

        var hookManager = new HookManager(
            new[] { mockCommandExecutor.Object },
            NullLogger<HookManager>.Instance,
            enabled: true);

        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Command,
                        Command = "check_completion.sh"
                    }
                ]
            }
        };

        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 0,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop);
        Assert.Equal(0, result.UpdatedRejectionCount);
    }

    #endregion

    #region Mixed Hooks with MaxRejections on Prompt Hooks

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_UsesPromptMaxRejections_IgnoresCommandMaxRejections()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Reject("Prompt reason"),
            commandResult: HookResult.Success());

        var agentHooks = new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Check",
                        MaxRejections = 10 // Should be used
                    },
                    new HookDefinition
                    {
                        Type = HookType.Command,
                        Command = "check.sh",
                        MaxRejections = 25 // Should be ignored
                    }
                ]
            }
        };

        // At rejection count 9, next prompt rejection is 10 which equals prompt limit
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 9,
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.True(result.ShouldStop); // Prompt limit (10) reached
        Assert.Equal(10, result.UpdatedRejectionCount);
    }

    [Fact]
    public async Task ExecuteStopHooksAsync_MixedHooks_CommandRejectsManytimes_NoLimitApplied()
    {
        var hookManager = CreateHookManagerWithBothExecutors(
            promptResult: HookResult.Success(), // Prompt approves
            commandResult: HookResult.Reject("Command reason"));

        var agentHooks = CreateMixedHooksConfiguration();

        // Simulate many command rejections without hitting any limit
        var result = await StopHookHelper.ExecuteStopHooksAsync(
            hookManager: hookManager,
            agentHooks: agentHooks,
            agentName: "test-agent",
            currentTurn: 5,
            maxTurns: 10,
            threadId: Guid.NewGuid(),
            currentRejectionCount: 50, // High count, but no prompt rejections counted
            defaultMaxRejections: 3,
            finalOutput: "Done",
            executionSummary: "Summary",
            logger: _logger);

        Assert.False(result.ShouldStop); // Command can reject indefinitely
        Assert.Equal(50, result.UpdatedRejectionCount); // Counter unchanged because only command rejected
    }

    #endregion

    #region Helper Methods

    private static AgentHookConfiguration CreateAgentHooksWithStopHook()
    {
        return new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "Is the task complete?"
                    }
                ]
            }
        };
    }

    private static AgentHookConfiguration CreateMixedHooksConfiguration()
    {
        return new AgentHookConfiguration
        {
            Hooks = new Dictionary<string, List<HookDefinition>>
            {
                ["Stop"] =
                [
                    new HookDefinition
                    {
                        Type = HookType.Prompt,
                        Prompt = "LLM check"
                    },
                    new HookDefinition
                    {
                        Type = HookType.Command,
                        Command = "check_script.sh"
                    }
                ]
            }
        };
    }

    #endregion
}
