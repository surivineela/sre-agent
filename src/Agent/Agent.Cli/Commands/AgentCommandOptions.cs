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
    // ============================================================
    // Agent Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the agent",
            Required = true
        };

        public static readonly Option<string> InstructionsOption = new("--instructions")
        {
            Description = "Instructions for the agent"
        };

        public static readonly Option<string[]> ToolsOption = new("--tools")
        {
            Description = "Tools the agent can use",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        public static readonly Option<string> HandoffDescriptionOption = new("--handoff-description")
        {
            Description = "Description for handoff capabilities"
        };

        public static readonly Option<string[]> HandoffsOption = new("--handoffs")
        {
            Description = "Agents this agent can hand off to",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        public static readonly Option<bool> AllowParallelToolCallsOption = new("--allow-parallel-tool-calls")
        {
            Description = "Allow parallel tool execution",
            DefaultValueFactory = _ => true
        };

        public static readonly Option<int> MaxReflectionCountOption = new("--max-reflection-count")
        {
            Description = "Maximum number of reflection iterations"
        };

        public static readonly Option<string> CriticPromptPathOption = new("--critic-prompt-path")
        {
            Description = "Path to critic prompt file"
        };

        public static readonly Option<bool> CriticOnHandoffOption = new("--critic-on-handoff")
        {
            Description = "Enable critic on handoff"
        };

        public static readonly Option<string> CustomReflectionNoteOption = new("--custom-reflection-note")
        {
            Description = "Custom note for reflection"
        };

        public static readonly Option<string[]> CommonPromptsOption = new("--common-prompts")
        {
            Description = "Common prompts to include",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };

        public static readonly Option<float?> TemperatureOption = new("--temperature")
        {
            Description = "Model temperature setting"
        };

        public static readonly Option<string> OutputTypeOption = new("--output-type")
        {
            Description = "Expected output format"
        };

        public static readonly Option<bool> VanillaModeOption = new("--vanilla-mode")
        {
            Description = "Use vanilla mode without enhancements"
        };

        public static readonly Option<bool> SmartOption = new("--smart")
        {
            Description = "Use AI to generate instructions and recommend tools"
        };

        public static readonly Option<bool> EnableSkillsOption = new("--enable-skills")
        {
            Description = "Enable skills for the agent",
            DefaultValueFactory = _ => false
        };

        public static readonly Option<bool> AddSystemSkillsOption = new("--add-system-skills")
        {
            Description = "Add system skills (not recommended for custom meta-agents)",
            DefaultValueFactory = _ => false
        };
    }

    // ============================================================
    // Agent Validate Command Options
    // ============================================================

    public static class Validate
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Agent name to validate"
        };

        public static readonly Option<string> FileOption = new("--file")
        {
            Description = "YAML file to validate"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Validate all agents"
        };

        public static readonly Option<bool> CheckToolsOption = new("--check-tools")
        {
            Description = "Validate that referenced tools exist"
        };
    }

    // ============================================================
    // Agent List Command Options
    // ============================================================

    public static class List
    {
        public static readonly Option<string?> SearchOption = new("--search")
        {
            Description = "Filter agents by name or instructions"
        };

        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Get specific agent and output full YAML"
        };

        public static readonly Option<bool> DetailOption = new("--detail")
        {
            Description = "Output full YAML for all agents"
        };
    }

    // ============================================================
    // Agent Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the agent to apply",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Preview changes without applying"
        };
    }

    // ============================================================
    // Agent Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the agent to delete",
            Required = true
        };

        public static readonly Option<bool?> DeleteLocalFilesOption = new("--delete-local-files")
        {
            Description = "Also delete local configuration files without prompting (true=delete, false=skip, omit=prompt)",
            Arity = ArgumentArity.ZeroOrOne
        };
    }

    // ============================================================
    // Agent Test Command Options
    // ============================================================

    public static class Test
    {
        // Agent test uses thread new options with --agent mapped to --name
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the agent to test",
            Required = true
        };

        public static readonly Option<string> MessageOption = new("--message")
        {
            Description = "Test message to send",
            Required = true
        };

        public static readonly Option<string> UserIdOption = new("--user-id")
        {
            Description = "[DEPRECATED] User ID (obtained from token)"
        };

        public static readonly Option<string> DisplayNameOption = new("--display-name")
        {
            Description = "[DEPRECATED] Display name (obtained from token)"
        };

        public static readonly Option<bool> WaitOption = new("--wait")
        {
            Description = "[DEPRECATED] Always starts interactive session",
            Arity = ArgumentArity.ZeroOrOne
        };

        public static readonly Option<bool> NoWaitOption = new("--no-wait")
        {
            Description = "Send message without waiting for response (requires --message)"
        };
    }

    // ============================================================
    // Agent Diff Command Options
    // ============================================================

    public static class Diff
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the agent to diff",
            Required = true
        };

        public static readonly Option<string> ToolOption = new("--tool")
        {
            Description = "Diff tool: git, vim, code (default: git)"
        };

        public static readonly Option<bool> RawOption = new("--raw")
        {
            Description = "Show inline diff"
        };
    }

    // ============================================================
    // Agent Migrate Command Options
    // ============================================================

    public static class Migrate
    {
        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Agent to migrate from V1 to V2"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Migrate all V1 agents to V2"
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Preview changes without modifying files"
        };
    }
}
