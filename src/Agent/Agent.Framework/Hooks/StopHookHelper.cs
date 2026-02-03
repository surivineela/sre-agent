// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework.Hooks;

/// <summary>
/// Result from executing stop hooks.
/// </summary>
public class StopHookExecutionResult
{
    /// <summary>
    /// Whether the agent should stop execution.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ContinueMessage))]
    public bool ShouldStop { get; init; }

    /// <summary>
    /// Message to inject as user input when continuing (when ShouldStop is false).
    /// </summary>
    public ChatMessage? ContinueMessage { get; init; }

    /// <summary>
    /// The updated rejection count after this execution.
    /// </summary>
    public int UpdatedRejectionCount { get; init; }

    /// <summary>
    /// Creates a result indicating the agent should stop.
    /// </summary>
    public static StopHookExecutionResult Stop(int rejectionCount) => new()
    {
        ShouldStop = true,
        UpdatedRejectionCount = rejectionCount
    };

    /// <summary>
    /// Creates a result indicating the agent should continue with the given message.
    /// </summary>
    public static StopHookExecutionResult Continue(int rejectionCount, string reason) => new()
    {
        ShouldStop = false,
        ContinueMessage = new ChatMessage(ChatRole.User, reason),
        UpdatedRejectionCount = rejectionCount
    };
}

/// <summary>
/// Helper class for executing Stop hooks in the Runner.
/// Encapsulates the logic for checking hooks, counting rejections, and generating continue messages.
/// </summary>
public static class StopHookHelper
{
    /// <summary>
    /// Executes stop hooks and determines whether the agent should stop or continue.
    /// Prompt and command hooks are executed separately. Only prompt hook rejections
    /// count towards the maxRejections limit; command hooks have no implicit limit.
    /// Important: Hooks must provide a reason when rejecting - a rejection without
    /// a reason is treated as approval and the agent will be allowed to stop.
    /// </summary>
    /// <param name="hookManager">The hook manager.</param>
    /// <param name="agentHooks">Agent-specific hook configuration.</param>
    /// <param name="agentName">Name of the current agent.</param>
    /// <param name="currentTurn">Current turn number.</param>
    /// <param name="maxTurns">Maximum turns allowed.</param>
    /// <param name="threadId">Thread ID for command hook session building.</param>
    /// <param name="currentRejectionCount">Current number of stop hook rejections (prompt hooks only).</param>
    /// <param name="defaultMaxRejections">Default maximum rejections before forcing stop (used if no hook-level override).</param>
    /// <param name="finalOutput">The output the agent is about to return.</param>
    /// <param name="executionSummary">Summary of execution so far.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether to stop or continue.</returns>
    public static async Task<StopHookExecutionResult> ExecuteStopHooksAsync(
        HookManager? hookManager,
        AgentHookConfiguration? agentHooks,
        string agentName,
        int currentTurn,
        int maxTurns,
        Guid threadId,
        int currentRejectionCount,
        int defaultMaxRejections,
        string? finalOutput,
        string? executionSummary,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // No hook manager means hooks are not configured
        if (hookManager == null)
        {
            return StopHookExecutionResult.Stop(currentRejectionCount);
        }

        // No stop hooks configured
        if (agentHooks == null || !agentHooks.HasHooksForEvent(HookEventType.Stop))
        {
            return StopHookExecutionResult.Stop(currentRejectionCount);
        }

        // Build context for the hooks
        var stopContext = new StopHookContext
        {
            AgentName = agentName,
            CurrentTurn = currentTurn,
            MaxTurns = maxTurns,
            ThreadId = threadId,
            StopHookActive = currentRejectionCount > 0,
            StopRejectionCount = currentRejectionCount,
            FinalOutput = finalOutput,
            ExecutionSummary = executionSummary
        };

        // Execute prompt and command hooks separately
        var promptHooks = agentHooks.GetPromptStopHooks();
        var commandHooks = agentHooks.GetCommandStopHooks();

        HookResult? promptResult = null;
        HookResult? commandResult = null;

        if (promptHooks.Count > 0)
        {
            promptResult = await hookManager.ExecuteHookListAsync(promptHooks, stopContext, cancellationToken);
        }

        if (commandHooks.Count > 0)
        {
            commandResult = await hookManager.ExecuteHookListAsync(commandHooks, stopContext, cancellationToken);
        }

        // Determine if hooks effectively rejected with a reason.
        // A rejection without a reason is treated as approval - hooks must provide
        // a reason to block stopping.
        var promptRejectedWithReason = promptResult is { Ok: false } && !string.IsNullOrWhiteSpace(promptResult.Reason);
        var commandRejectedWithReason = commandResult is { Ok: false } && !string.IsNullOrWhiteSpace(commandResult.Reason);

        // If no hook rejected with a reason, agent stops
        if (!promptRejectedWithReason && !commandRejectedWithReason)
        {
            return StopHookExecutionResult.Stop(currentRejectionCount);
        }

        // At least one hook rejected with a reason - agent should continue
        // Only count towards limit if PROMPT hooks rejected with a reason
        var newRejectionCount = currentRejectionCount;
        if (promptRejectedWithReason)
        {
            newRejectionCount++;

            // Apply limit only for prompt hook rejections
            var effectiveMaxRejections = agentHooks.GetMaxStopHookRejections() ?? defaultMaxRejections;
            if (newRejectionCount >= effectiveMaxRejections)
            {
                logger.LogWarning(
                    "Stop hook rejection limit reached ({Count}/{Max}), forcing stop",
                    newRejectionCount,
                    effectiveMaxRejections);
                return StopHookExecutionResult.Stop(newRejectionCount);
            }
        }

        // Combine rejection reasons from both hook types
        var reasons = new List<string>();
        if (promptRejectedWithReason)
        {
            reasons.Add(promptResult!.Reason!);
        }
        if (commandRejectedWithReason)
        {
            reasons.Add(commandResult!.Reason!);
        }

        var combinedReason = string.Join("\n", reasons);

        logger.LogInformation("Stop hook rejected stopping: {Reason}", combinedReason);

        return StopHookExecutionResult.Continue(newRejectionCount, combinedReason);
    }
}
