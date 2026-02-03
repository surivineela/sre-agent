// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Hooks;

/// <summary>
/// Defines the types of events that can trigger hooks.
/// </summary>
public enum HookEventType
{
    /// <summary>
    /// Triggered when the agent is about to stop/complete its execution.
    /// Hook can return ok=false to prevent stopping and continue working.
    /// </summary>
    Stop,

    /// <summary>
    /// Triggered after a tool completes execution successfully.
    /// Hook can return ok=false to block the tool result and provide feedback.
    /// Supports matcher patterns to filter by tool name.
    /// </summary>
    PostToolUse,

    // Future event types (not yet implemented):
    // PreToolUse,       // Before a tool is executed
    // PostToolUseFailure, // After a tool fails
    // UserPromptSubmit, // When user submits a prompt
    // Handoff,         // When agent hands off to another agent
    // SessionStart,    // When a session begins
    // SessionEnd,      // When a session ends
}
