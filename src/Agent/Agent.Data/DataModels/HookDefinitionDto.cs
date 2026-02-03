// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Agent.Data.DataModels;

/// <summary>
/// Data transfer object for hook definitions stored in agent documents.
/// </summary>
public class HookDefinitionDto
{
    /// <summary>
    /// The type of hook execution. Required.
    /// Valid values: "prompt" (LLM-based evaluation) or "command" (shell command).
    /// </summary>
    [YamlMember(Alias = "type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// For prompt hooks: the prompt text to send to the LLM.
    /// Use $ARGUMENTS as a placeholder for the hook input context.
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
    /// Mutually exclusive with Command.
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
    /// - "allow" (default): On failure, proceed as if no hook was configured.
    /// - "block": On failure, block the action and report the error.
    /// Note: This only affects infrastructure failures (timeout, crash), not hook decisions.
    /// </summary>
    [YamlMember(Alias = "failMode")]
    public string? FailMode { get; set; }

    /// <summary>
    /// Maximum number of times this stop hook can reject stopping before forcing stop.
    /// Only applicable for Stop hooks. Valid range: 1-25.
    /// </summary>
    [YamlMember(Alias = "maxRejections")]
    public int? MaxRejections { get; set; }
}
