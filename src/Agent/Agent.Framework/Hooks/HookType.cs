// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Framework.Hooks;

/// <summary>
/// Defines how a hook is executed.
/// </summary>
public enum HookType
{
    /// <summary>
    /// Hook uses an LLM to evaluate a prompt and return a decision.
    /// The LLM responds with JSON: {"ok": true/false, "reason": "..."}
    /// </summary>
    Prompt,

    /// <summary>
    /// Hook executes a shell command in the code execution sandbox.
    /// The command receives hook context via stdin and outputs JSON:
    /// {"ok": true/false, "reason": "...", "hookSpecificOutput": {"additionalContext": "..."}}
    /// </summary>
    Command,
}
