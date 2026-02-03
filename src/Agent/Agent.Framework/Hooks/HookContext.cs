// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Framework.Hooks;

/// <summary>
/// Base context passed to hook executors.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "context_type")]
[JsonDerivedType(typeof(HookContext), "base")]
[JsonDerivedType(typeof(StopHookContext), "stop")]
[JsonDerivedType(typeof(PostToolUseHookContext), "post_tool_use")]
public class HookContext
{
    /// <summary>
    /// The type of event that triggered this hook.
    /// Serialized as string (e.g., "Stop") for LLM readability.
    /// </summary>
    [JsonPropertyName("hook_event_name")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public HookEventType HookEventName { get; set; }

    /// <summary>
    /// Name of the agent executing the hook.
    /// </summary>
    [JsonPropertyName("agent_name")]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Current turn number in the agent execution loop.
    /// </summary>
    [JsonPropertyName("current_turn")]
    public int CurrentTurn { get; set; }

    /// <summary>
    /// Maximum turns allowed for this execution.
    /// </summary>
    [JsonPropertyName("max_turns")]
    public int MaxTurns { get; set; }

    /// <summary>
    /// Summary of the conversation/execution so far.
    /// </summary>
    [JsonPropertyName("execution_summary")]
    public string? ExecutionSummary { get; set; }

    /// <summary>
    /// Thread ID for the current execution context.
    /// Used by command hooks to build a session identifier.
    /// Not serialized to hook command stdin.
    /// </summary>
    [JsonIgnore]
    public Guid ThreadId { get; set; }
}

/// <summary>
/// Context specific to Stop hooks.
/// </summary>
public class StopHookContext : HookContext
{
    /// <summary>
    /// Indicates if a stop hook has already rejected stopping in this execution.
    /// Used to prevent infinite loops - check this to avoid repeatedly rejecting.
    /// </summary>
    [JsonPropertyName("stop_hook_active")]
    public bool StopHookActive { get; set; }

    /// <summary>
    /// Number of times stop hooks have rejected stopping in this execution.
    /// </summary>
    [JsonPropertyName("stop_rejection_count")]
    public int StopRejectionCount { get; set; }

    /// <summary>
    /// The final output/response that the agent is about to return.
    /// </summary>
    [JsonPropertyName("final_output")]
    public string? FinalOutput { get; set; }

    public StopHookContext()
    {
        HookEventName = HookEventType.Stop;
    }
}

/// <summary>
/// Context specific to PostToolUse hooks.
/// </summary>
public class PostToolUseHookContext : HookContext
{
    /// <summary>
    /// Name of the tool that was executed.
    /// </summary>
    [JsonPropertyName("tool_name")]
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// The input/arguments passed to the tool (serialized JSON).
    /// </summary>
    [JsonPropertyName("tool_input")]
    public object? ToolInput { get; set; }

    /// <summary>
    /// The result/output from the tool execution (serialized).
    /// </summary>
    [JsonPropertyName("tool_result")]
    public object? ToolResult { get; set; }

    /// <summary>
    /// Whether the tool execution succeeded (no exception thrown).
    /// </summary>
    [JsonPropertyName("tool_succeeded")]
    public bool ToolSucceeded { get; set; } = true;

    public PostToolUseHookContext()
    {
        HookEventName = HookEventType.PostToolUse;
    }
}
