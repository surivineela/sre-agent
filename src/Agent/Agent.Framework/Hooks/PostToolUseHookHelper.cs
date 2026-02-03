// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework.Hooks;

/// <summary>
/// Result from executing PostToolUse hooks.
/// </summary>
public record PostToolUseHookExecutionResult
{
    /// <summary>
    /// Whether the tool result should be added to the conversation.
    /// When false, the tool result is blocked.
    /// </summary>
    [MemberNotNullWhen(false, nameof(BlockMessage))]
    public bool AllowToolResult { get; init; }

    /// <summary>
    /// Message to inject when tool result is blocked (when AllowToolResult is false).
    /// </summary>
    public ChatMessage? BlockMessage { get; init; }

    /// <summary>
    /// Optional additional context to inject as a user message.
    /// Present when hooks provide additionalContext (even on success).
    /// </summary>
    public ChatMessage? AdditionalContextMessage { get; init; }

    /// <summary>
    /// Creates a result indicating the tool result should be allowed.
    /// </summary>
    public static PostToolUseHookExecutionResult Allow() => new()
    {
        AllowToolResult = true
    };

    /// <summary>
    /// Creates a result indicating the tool result should be allowed, with additional context.
    /// </summary>
    public static PostToolUseHookExecutionResult AllowWithContext(string additionalContext) => new()
    {
        AllowToolResult = true,
        AdditionalContextMessage = new ChatMessage(ChatRole.User, additionalContext)
    };

    /// <summary>
    /// Creates a result indicating the tool result should be blocked.
    /// </summary>
    public static PostToolUseHookExecutionResult Block(string reason, string? additionalContext = null)
    {
        var result = new PostToolUseHookExecutionResult
        {
            AllowToolResult = false,
            BlockMessage = new ChatMessage(ChatRole.User,
                $"[Hook Blocked Tool Result] {reason}")
        };

        if (!string.IsNullOrWhiteSpace(additionalContext))
        {
            return result with
            {
                AdditionalContextMessage = new ChatMessage(ChatRole.User, additionalContext)
            };
        }

        return result;
    }
}

/// <summary>
/// Helper class for executing PostToolUse hooks in the Runner.
/// Encapsulates the logic for checking hooks, building context, and handling results.
/// </summary>
public static class PostToolUseHookHelper
{
    /// <summary>
    /// Executes PostToolUse hooks and determines whether the tool result should be allowed.
    /// </summary>
    /// <param name="hookManager">The hook manager.</param>
    /// <param name="agentHooks">Agent-specific hook configuration.</param>
    /// <param name="agentName">Name of the current agent.</param>
    /// <param name="currentTurn">Current turn number.</param>
    /// <param name="maxTurns">Maximum turns allowed.</param>
    /// <param name="threadId">Thread ID for command hook session building.</param>
    /// <param name="toolName">Name of the tool that was executed.</param>
    /// <param name="toolInput">Arguments passed to the tool.</param>
    /// <param name="toolResult">Result from the tool execution.</param>
    /// <param name="toolSucceeded">Whether the tool execution succeeded.</param>
    /// <param name="executionSummary">Summary of execution so far.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating whether to allow the tool result.</returns>
    public static async Task<PostToolUseHookExecutionResult> ExecutePostToolUseHooksAsync(
        HookManager? hookManager,
        AgentHookConfiguration? agentHooks,
        string agentName,
        int currentTurn,
        int maxTurns,
        Guid threadId,
        string toolName,
        object? toolInput,
        object? toolResult,
        bool toolSucceeded,
        string? executionSummary,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        // No hook manager means hooks are not configured
        if (hookManager == null)
        {
            return PostToolUseHookExecutionResult.Allow();
        }

        // No PostToolUse hooks configured
        if (agentHooks == null || !agentHooks.HasHooksForEvent(HookEventType.PostToolUse))
        {
            return PostToolUseHookExecutionResult.Allow();
        }

        // Build context for the hook
        var postToolUseContext = new PostToolUseHookContext
        {
            AgentName = agentName,
            CurrentTurn = currentTurn,
            MaxTurns = maxTurns,
            ThreadId = threadId,
            ToolName = toolName,
            ToolInput = toolInput,
            ToolResult = toolResult,
            ToolSucceeded = toolSucceeded,
            ExecutionSummary = executionSummary
        };

        // Execute hooks with tool name matcher
        var hookResult = await hookManager.ExecuteHooksForToolAsync(
            agentHooks,
            HookEventType.PostToolUse,
            toolName,
            postToolUseContext,
            cancellationToken);

        var additionalContext = hookResult.GetAdditionalContext();

        // Hook approved the tool result
        if (hookResult.Ok)
        {
            if (!string.IsNullOrWhiteSpace(additionalContext))
            {
                logger.LogDebug("PostToolUse hook allowed tool result with additional context");
                return PostToolUseHookExecutionResult.AllowWithContext(additionalContext);
            }

            return PostToolUseHookExecutionResult.Allow();
        }

        // Hook rejected the tool result
        logger.LogInformation(
            "PostToolUse hook blocked tool result for {ToolName}: {Reason}",
            toolName,
            hookResult.Reason);

        return PostToolUseHookExecutionResult.Block(
            hookResult.Reason ?? "Tool result blocked by hook",
            additionalContext);
    }
}
