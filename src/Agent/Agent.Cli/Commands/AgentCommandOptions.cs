using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for agent commands.
/// </summary>
public static class AgentCommandOptions
{
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

    // Agent options for apply
    public static readonly Option<string> ApplyNameOption = new("--name") { Required = true };

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
    public static readonly Option<string> ThreadIdOption = new("--thread-id") 
    { 
        Description = "Thread ID to continue (if not provided, uses the last used thread)"
    };
    public static readonly Option<string> ThreadIdRequiredOption = new("--thread-id") 
    { 
        Required = true,
        Description = "Thread ID to delete"
    };
}
