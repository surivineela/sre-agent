// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for agent commands.
/// </summary>
public static class AgentCommandOptions
{
    // Global debug option for all agent commands
    public static readonly Option<bool> DebugOption = new("--debug")
    {
        Description = "Enable verbose debug logging for network calls and operations"
    };

    // Agent options for create (only name is required)
    public static readonly Option<string> NameOptionCreate = new("--name") { Required = true };
    public static readonly Option<string> InstructionsOptionCreate = new("--instructions");
    public static readonly Option<string[]> ToolsOptionCreate = new("--tools")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };

    // Agent options for create (optional)
    public static readonly Option<string> HandoffDescriptionOption = new("--handoff-description");
    public static readonly Option<string[]> HandoffsOption = new("--handoffs")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };
    public static readonly Option<bool> AllowParallelToolCallsOption = new("--allow-parallel-tool-calls");
    public static readonly Option<int> MaxReflectionCountOption = new("--max-reflection-count");
    public static readonly Option<string> CriticPromptPathOption = new("--critic-prompt-path");
    public static readonly Option<bool> CriticOnHandoffOption = new("--critic-on-handoff");
    public static readonly Option<string> CustomReflectionNoteOption = new("--custom-reflection-note");
    public static readonly Option<string[]> CommonPromptsOption = new("--common-prompts")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true
    };
    public static readonly Option<float?> TemperatureOption = new("--temperature");
    public static readonly Option<string> OutputTypeOption = new("--output-type");
    public static readonly Option<bool> SmartOption = new("--smart")
    {
        Description = "Use AI to automatically generate instructions and recommend tools"
    };

    // Agent options for validate (not required)
    public static readonly Option<string> FileOptionValidate = new("--file");
    public static readonly Option<bool> AllOption = new("--all");
    public static readonly Option<bool> CheckToolsOption = new("--check-tools")
    {
        Description = "Validate that all referenced tools exist locally or on the remote server"
    };

    // Agent options for apply
    public static readonly Option<string> ApplyNameOption = new("--name") { Required = true };
    public static readonly Option<bool> ApplyDryRunOption = new("--dry-run")
    {
        Description = "Show what would be applied without making changes"
    };

    // Agent options for delete
    public static readonly Option<string> DeleteNameOption = new("--name") { Required = true };

    // Agent options for test
    public static readonly Option<string> TestNameOption = new("--name")
    {
        Required = true,
        Description = "Name of the agent to test"
    };
    public static readonly Option<string> TestMessageOption = new("--message")
    {
        Required = true,
        Description = "Test message to send to the agent"
    };
    public static readonly Option<string> TestUserIdOption = new("--user-id")
    {
        Description = "User ID for the test message (defaults to current user)"
    };
    public static readonly Option<string> TestDisplayNameOption = new("--display-name")
    {
        Description = "Display name for the test message (defaults to current user)"
    };
    public static readonly Option<bool> TestNoWaitOption = new("--no-wait")
    {
        Description = "Don't wait for the agent's response, just send the test message"
    };

    // Option for apply-yaml command
    public static readonly Option<string> ApplyYamlFileOption = new("--file")
    {
        Description = "Path to the YAML file to apply"
    };

    // Options for thread commands
    public static readonly Option<string> ThreadMessageOption = new("--message")
    {
        Required = true,
        Description = "The message to send to the SRE Agent"
    };
    public static readonly Option<string> ThreadMessageOptionalOption = new("--message")
    {
        Required = false,
        Description = "The message to send to the SRE Agent (optional)"
    };
    public static readonly Option<string> ThreadUserIdOption = new("--user-id")
    {
        Description = "User ID for the message (defaults to current user)"
    };
    public static readonly Option<string> ThreadDisplayNameOption = new("--display-name")
    {
        Description = "Display name for the message (defaults to current user)"
    };
    public static readonly Option<bool> ThreadWaitOption = new("--wait")
    {
        Description = "Wait for the agent's response (default: true)",
        Arity = ArgumentArity.ZeroOrOne
    };
    public static readonly Option<bool> ThreadNoWaitOption = new("--no-wait")
    {
        Description = "Don't wait for the agent's response (overrides default wait behavior)"
    };
    public static readonly Option<string> ThreadIdOption = new("--thread-id")
    {
        Description = "Thread ID to continue (if not provided, uses the last used thread)"
    };
    public static readonly Option<string> ThreadIdRequiredOption = new("--thread-id")
    {
        Required = true,
        Description = "Thread ID to delete"
    };

    // Chat command options
    public static readonly Option<string> ChatAgentNameOption = new("--agent")
    {
        Description = "Name of the specific agent to start chatting with"
    };

    // Agent diff command options
    public static readonly Option<string> DiffNameOption = new("--name") { Required = true };
    public static readonly Option<string> DiffToolOption = new("--tool")
    {
        Description = "Diff tool to use: git, vim, code (default: git)"
    };
    public static readonly Option<bool> DiffRawOption = new("--raw")
    {
        Description = "Show inline diff instead of launching external tool"
    };

    // Completion services disabled - System.CommandLine version doesn't support AddCompletions method
    // These services are implemented but not compatible with current CLI framework version
}
