// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Framework;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Xunit;

namespace Agent.Tests.Unit.Reasoning;

/// <summary>
/// Tests to verify proper handling of parallel tool calls using RunHooks.
/// These tests verify that tool spans are properly isolated when tools execute in parallel.
/// </summary>
public class ReasoningLoopParallelToolCallTests
{
    [Fact]
    public async Task RunHooks_ShouldHandleParallelToolCalls_WithDifferentCallIds()
    {
        // Arrange
        var hooks = new RunHooks<AgentContext>();
        var spanTracker = new ConcurrentDictionary<string, bool>();
        var spanEndTracker = new ConcurrentDictionary<string, bool>();

        // Track when tools start
        hooks.ToolStart += (context, agent, functionCall, tool, input) =>
        {
            spanTracker[functionCall.CallId] = true;
            return Task.CompletedTask;
        };

        // Track when tools end
        hooks.ToolEnd += (context, agent, functionCallContent, tool, output) =>
        {
            spanEndTracker[functionCallContent.CallId] = true;
            return Task.CompletedTask;
        };

        var agent = new Agent<AgentContext>("test_agent");
        var testContext = CreateTestContext();
        var context = new RunContextWrapper<AgentContext>(testContext);

        var tool1 = CreateMockTool("GetMetrics");
        var tool2 = CreateMockTool("GetLogs");
        var tool3 = CreateMockTool("GetEvents");

        var functionCall1 = new FunctionCallContent("call-1", "GetMetrics");
        var functionCall2 = new FunctionCallContent("call-2", "GetLogs");
        var functionCall3 = new FunctionCallContent("call-3", "GetEvents");

        var input = new List<KeyValuePair<string, object?>>();

        // Act - Start three tools in parallel
        await hooks.OnToolStart(context, agent, functionCall1, tool1, input);
        await hooks.OnToolStart(context, agent, functionCall2, tool2, input);
        await hooks.OnToolStart(context, agent, functionCall3, tool3, input);

        // Assert - All three should be tracked separately
        Assert.True(spanTracker.ContainsKey("call-1"));
        Assert.True(spanTracker.ContainsKey("call-2"));
        Assert.True(spanTracker.ContainsKey("call-3"));
        Assert.Equal(3, spanTracker.Count);

        // Act - End the tools in different order
        await hooks.OnToolEnd(context, agent, functionCall2, tool2, "logs result");
        await hooks.OnToolEnd(context, agent, functionCall1, tool1, "metrics result");
        await hooks.OnToolEnd(context, agent, functionCall3, tool3, "events result");

        // Assert - All spans should have been ended
        Assert.True(spanEndTracker.ContainsKey("call-1"));
        Assert.True(spanEndTracker.ContainsKey("call-2"));
        Assert.True(spanEndTracker.ContainsKey("call-3"));
        Assert.Equal(3, spanEndTracker.Count);
    }

    [Fact]
    public async Task RunHooks_ShouldIsolateToolSpans_WhenToolsExecuteSimultaneously()
    {
        // Arrange
        var hooks = new RunHooks<AgentContext>();
        var concurrentStarts = new ConcurrentBag<string>();
        var concurrentEnds = new ConcurrentBag<string>();

        hooks.ToolStart += (context, agent, functionCall, tool, input) =>
        {
            concurrentStarts.Add(functionCall.CallId);
            return Task.CompletedTask;
        };

        hooks.ToolEnd += (context, agent, functionCallContent, tool, output) =>
        {
            concurrentEnds.Add(functionCallContent.CallId);
            return Task.CompletedTask;
        };

        var agent = new Agent<AgentContext>("test_agent");
        var testContext = CreateTestContext();
        var context = new RunContextWrapper<AgentContext>(testContext);
        var tool = CreateMockTool("ParallelTool");
        var input = new List<KeyValuePair<string, object?>>();

        // Act - Simulate 10 parallel tool calls
        var tasks = new List<Task>();
        for (int i = 0; i < 10; i++)
        {
            var callId = $"parallel-call-{i}";
            var functionCall = new FunctionCallContent(callId, "ParallelTool");

            tasks.Add(Task.Run(async () =>
            {
                await hooks.OnToolStart(context, agent, functionCall, tool, input);
                await Task.Delay(10); // Simulate some work
                await hooks.OnToolEnd(context, agent, functionCall, tool, $"result-{callId}");
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - All 10 calls should be tracked
        Assert.Equal(10, concurrentStarts.Count);
        Assert.Equal(10, concurrentEnds.Count);

        // Verify all CallIds are unique
        Assert.Equal(10, concurrentStarts.Distinct().Count());
        Assert.Equal(10, concurrentEnds.Distinct().Count());
    }

    [Fact]
    public async Task RunHooks_ShouldHandleToolEnd_WithoutCorrespondingToolStart()
    {
        // Arrange
        var hooks = new RunHooks<AgentContext>();
        var endCalled = false;

        hooks.ToolEnd += (context, agent, functionCallContent, tool, output) =>
        {
            endCalled = true;
            return Task.CompletedTask;
        };

        var agent = new Agent<AgentContext>("test_agent");
        var testContext = CreateTestContext();
        var context = new RunContextWrapper<AgentContext>(testContext);
        var tool = CreateMockTool("TestTool");
        var functionCall = new FunctionCallContent("orphan-call", "TestTool");

        // Act - Call ToolEnd without calling ToolStart first
        var exception = await Record.ExceptionAsync(async () =>
        {
            await hooks.OnToolEnd(context, agent, functionCall, tool, "result");
        });

        // Assert - Should not throw an exception
        Assert.Null(exception);
        Assert.True(endCalled);
    }

    [Fact]
    public async Task RunHooks_ShouldHandleMultipleToolEnd_ForSameCallId()
    {
        // Arrange
        var hooks = new RunHooks<AgentContext>();
        var endCallCount = 0;

        hooks.ToolEnd += (context, agent, functionCallContent, tool, output) =>
        {
            endCallCount++;
            return Task.CompletedTask;
        };

        var agent = new Agent<AgentContext>("test_agent");
        var testContext = CreateTestContext();
        var context = new RunContextWrapper<AgentContext>(testContext);
        var tool = CreateMockTool("TestTool");
        var functionCall = new FunctionCallContent("duplicate-call", "TestTool");
        var input = new List<KeyValuePair<string, object?>>();

        // Act
        await hooks.OnToolStart(context, agent, functionCall, tool, input);
        await hooks.OnToolEnd(context, agent, functionCall, tool, "result1");

        // Call ToolEnd again with the same CallId
        await hooks.OnToolEnd(context, agent, functionCall, tool, "result2");

        // Assert - Both calls should execute (no deduplication in hook itself)
        Assert.Equal(2, endCallCount);
    }

    [Fact]
    public async Task RunHooks_ShouldPreserveCallIdThroughToolLifecycle()
    {
        // Arrange
        var hooks = new RunHooks<AgentContext>();
        var capturedStartCallIds = new ConcurrentBag<string>();
        var capturedEndCallIds = new ConcurrentBag<string>();

        hooks.ToolStart += (context, agent, functionCall, tool, input) =>
        {
            capturedStartCallIds.Add(functionCall.CallId);
            return Task.CompletedTask;
        };

        hooks.ToolEnd += (context, agent, functionCallContent, tool, output) =>
        {
            capturedEndCallIds.Add(functionCallContent.CallId);
            return Task.CompletedTask;
        };

        var agent = new Agent<AgentContext>("test_agent");
        var testContext = CreateTestContext();
        var context = new RunContextWrapper<AgentContext>(testContext);
        var tool = CreateMockTool("TestTool");
        var callId = "test-call-123";
        var functionCall = new FunctionCallContent(callId, "TestTool", new Dictionary<string, object?> { ["param1"] = "value1" });
        var input = new List<KeyValuePair<string, object?>> { new("param1", "value1") };

        // Act
        await hooks.OnToolStart(context, agent, functionCall, tool, input);
        await hooks.OnToolEnd(context, agent, functionCall, tool, "result");

        // Assert - CallId should be preserved throughout the lifecycle
        Assert.Single(capturedStartCallIds);
        Assert.Single(capturedEndCallIds);
        Assert.Equal(callId, capturedStartCallIds.First());
        Assert.Equal(callId, capturedEndCallIds.First());
    }

    private static AgentContext CreateTestContext()
    {
        return new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: Guid.NewGuid(),
            AgentType: AgentTypeEnum.Meta,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null,
            CurrentAgent: "test_agent",
            AgentHandoffChain: new List<string> { "test_agent" },
            AgentMode: "Review");
    }

    private static AIFunction CreateMockTool(string name)
    {
        Func<string, string> toolMethod = (input) => $"Result for {input}";
        return AIFunctionFactory.Create(toolMethod, name: name);
    }
}
