// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Framework.Hooks;

/// <summary>
/// Defines how a command hook handles execution errors.
/// </summary>
public enum HookFailMode
{
    /// <summary>
    /// Allow the action to proceed on hook errors (default, graceful degradation).
    /// </summary>
    Allow,

    /// <summary>
    /// Block the action if the hook fails (strict mode).
    /// </summary>
    Block
}

/// <summary>
/// Defines a single hook configuration.
/// </summary>
public class HookDefinition
{
    /// <summary>
    /// The type of hook execution (Prompt or Command).
    /// </summary>
    [YamlMember(Alias = "type")]
    public HookType Type { get; set; } = HookType.Prompt;

    /// <summary>
    /// For prompt hooks: the prompt text to send to the LLM.
    /// Use $ARGUMENTS as a placeholder for the hook input context.
    /// If $ARGUMENTS is not present, input is appended to the prompt.
    /// </summary>
    [YamlMember(Alias = "prompt")]
    public string? Prompt { get; set; }

    /// <summary>
    /// For command hooks: the shell command to execute.
    /// The command receives hook context as JSON via stdin.
    /// Mutually exclusive with Script.
    /// </summary>
    [YamlMember(Alias = "command")]
    public string? Command { get; set; }

    /// <summary>
    /// For command hooks: a multi-line bash script to execute.
    /// The script receives hook context as JSON via stdin.
    /// Mutually exclusive with Command. The script is uploaded to the session
    /// and executed via bash.
    /// </summary>
    [YamlMember(Alias = "script")]
    public string? Script { get; set; }

    /// <summary>
    /// Pattern to match tool names for PostToolUse hooks.
    /// Supports regex patterns (e.g., "Edit|Write", "Bash.*").
    /// Use "*" to match all tools. Empty or null will NOT match any tools.
    /// Required for PostToolUse hooks.
    /// </summary>
    [YamlMember(Alias = "matcher")]
    public string? Matcher { get; set; }

    /// <summary>
    /// Timeout in seconds for hook execution. Default is 30 seconds.
    /// </summary>
    [YamlMember(Alias = "timeout")]
    public int Timeout { get; set; } = 30;

    /// <summary>
    /// Model scenario or deployment name for prompt hooks.
    /// Scenario names (environment-agnostic):
    ///   - ReasoningHeavy: Complex, multi-step reasoning
    ///   - ReasoningFast: Good reasoning with low latency (default for hooks)
    ///   - GeneralPurpose: Mixed tasks with balanced accuracy
    ///   - SmallFast: Lowest cost/latency with acceptable accuracy
    ///   - LongContext: Handling long documents
    ///   - Eval: Grading, review, rubric-based assessment
    /// Or use a deployment name (e.g., "gpt-4.1") for direct model access.
    /// If not specified, defaults to ReasoningFast.
    /// </summary>
    [YamlMember(Alias = "model")]
    public string? Model { get; set; }

    /// <summary>
    /// How to handle hook execution failures (command hooks only).
    /// - Allow (default): On failure, proceed as if no hook was configured.
    /// - Block: On failure, block the action and report the error.
    /// Note: This only affects infrastructure failures (timeout, crash), not hook decisions.
    /// </summary>
    [YamlMember(Alias = "failMode")]
    public HookFailMode FailMode { get; set; } = HookFailMode.Allow;

    /// <summary>
    /// Maximum number of times this stop hook can reject stopping before forcing stop.
    /// Only applicable for prompt-type Stop hooks. Command-type Stop hooks have no implicit limit.
    /// Overrides the global default (3). Valid range: 1-25.
    /// When multiple prompt-type Stop hooks have different values, the maximum is used.
    /// If not specified, the global default from RunConfig is used.
    /// </summary>
    [YamlMember(Alias = "maxRejections")]
    public int? MaxRejections { get; set; }
}
