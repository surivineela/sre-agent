// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Framework.Hooks;

/// <summary>
/// Result returned by a hook execution.
/// Supports both command hook format (decision/reason) and
/// prompt hook format (ok/reason).
/// </summary>
public class HookResult
{
    private bool? _ok;

    /// <summary>
    /// Whether the hook allows the action to proceed.
    /// true = allow, false = reject/prevent.
    ///
    /// When deserializing, this property handles both formats:
    /// - If "decision" is "block", returns false
    /// - If "ok" is explicitly set, returns that value
    /// - Otherwise returns true (default to allow)
    /// </summary>
    [JsonPropertyName("ok")]
    public bool Ok
    {
        get
        {
            // If Decision is explicitly set, derive Ok from it (command hook format)
            if (!string.IsNullOrEmpty(Decision))
            {
                return !Decision.Equals("block", StringComparison.OrdinalIgnoreCase);
            }
            // Otherwise use the explicit ok value, defaulting to true
            return _ok ?? true;
        }
        set => _ok = value;
    }

    /// <summary>
    /// Decision field for command hooks.
    /// "block" = reject the action, anything else or null = allow.
    /// Takes precedence over Ok when set.
    /// </summary>
    [JsonPropertyName("decision")]
    public string? Decision { get; set; }

    /// <summary>
    /// Explanation for the decision. Required when blocking.
    /// This reason is injected as a user message when an action is prevented.
    /// </summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    /// <summary>
    /// Hook-specific output data containing additional context and hook-type-specific fields.
    /// </summary>
    [JsonPropertyName("hookSpecificOutput")]
    public HookSpecificOutput? HookSpecificOutput { get; set; }

    #region unused fields for future use

    /// <summary>
    /// Whether the agent should continue after hook execution.
    /// Defaults to true. When false, the agent stops processing.
    /// </summary>
    [JsonPropertyName("continue")]
    public bool? Continue { get; set; }

    /// <summary>
    /// Message shown when Continue is false.
    /// Shown to user but not to the agent.
    /// </summary>
    [JsonPropertyName("stopReason")]
    public string? StopReason { get; set; }

    /// <summary>
    /// Hide stdout from transcript mode.
    /// </summary>
    [JsonPropertyName("suppressOutput")]
    public bool? SuppressOutput { get; set; }

    /// <summary>
    /// Optional warning message shown to the user.
    /// </summary>
    [JsonPropertyName("systemMessage")]
    public string? SystemMessage { get; set; }

    #endregion

    /// <summary>
    /// Gets the additionalContext from HookSpecificOutput if present.
    /// </summary>
    public string? GetAdditionalContext()
    {
        return HookSpecificOutput?.AdditionalContext;
    }

    /// <summary>
    /// Creates a successful result (action allowed).
    /// </summary>
    public static HookResult Success() => new() { _ok = true };

    /// <summary>
    /// Creates a successful result with additional context to inject.
    /// </summary>
    /// <param name="additionalContext">Context to inject as user message.</param>
    public static HookResult SuccessWithContext(string additionalContext) => new()
    {
        _ok = true,
        HookSpecificOutput = new HookSpecificOutput { AdditionalContext = additionalContext }
    };

    /// <summary>
    /// Creates a rejection result (action prevented).
    /// </summary>
    /// <param name="reason">Explanation for why the action was rejected.</param>
    public static HookResult Reject(string reason) => new() { _ok = false, Reason = reason };

    /// <summary>
    /// Creates a rejection result with additional context.
    /// </summary>
    /// <param name="reason">Explanation for why the action was rejected.</param>
    /// <param name="additionalContext">Context to inject as user message.</param>
    public static HookResult RejectWithContext(string reason, string additionalContext) => new()
    {
        _ok = false,
        Reason = reason,
        HookSpecificOutput = new HookSpecificOutput { AdditionalContext = additionalContext }
    };

    /// <summary>
    /// Creates an error result when hook execution fails.
    /// Defaults to allowing the action to prevent hooks from blocking execution.
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    public static HookResult Error(string error) => new() { _ok = true, Reason = $"Hook error: {error}" };

    /// <summary>
    /// Creates an error result that blocks the action (for strict fail mode).
    /// </summary>
    /// <param name="error">The error that occurred.</param>
    public static HookResult ErrorBlocking(string error) => new() { _ok = false, Reason = $"Hook error: {error}" };
}

/// <summary>
/// Hook-specific output data. Contains additional context and hook-type-specific fields.
/// </summary>
public class HookSpecificOutput
{
    /// <summary>
    /// The hook event name this output is associated with.
    /// </summary>
    [JsonPropertyName("hookEventName")]
    public string? HookEventName { get; set; }

    /// <summary>
    /// Additional context to inject into the agent's context.
    /// </summary>
    [JsonPropertyName("additionalContext")]
    public string? AdditionalContext { get; set; }

    /// <summary>
    /// For PreToolUse hooks: permission decision (allow, deny, ask).
    /// </summary>
    [JsonPropertyName("permissionDecision")]
    public string? PermissionDecision { get; set; }

    /// <summary>
    /// For PreToolUse hooks: reason for the permission decision.
    /// </summary>
    [JsonPropertyName("permissionDecisionReason")]
    public string? PermissionDecisionReason { get; set; }

    /// <summary>
    /// For PreToolUse/PermissionRequest hooks: modified tool input parameters.
    /// </summary>
    [JsonPropertyName("updatedInput")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? UpdatedInput { get; set; }
}
