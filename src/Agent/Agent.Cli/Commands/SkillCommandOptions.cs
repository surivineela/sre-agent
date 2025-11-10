// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for skill commands.
/// </summary>
public static class SkillCommandOptions
{
    // Global debug option for all skill commands
    public static readonly Option<bool> DebugOption = new("--debug")
    {
        Description = "Enable verbose debug logging for network calls and operations"
    };

    // Upload command options
    public static readonly Option<string> UploadPathOption = new("--path")
    {
        Description = "Path to a single skill directory to upload (e.g., skills/my-skill)"
    };

    public static readonly Option<string> UploadFolderOption = new("--folder")
    {
        Description = "Path to a folder containing multiple skill directories to upload"
    };

    // Convert command options
    public static readonly Option<string> ConvertAgentNameOption = new("--agent-name")
    {
        Required = true,
        Description = "Name of the agent to convert to a skill"
    };

    public static readonly Option<string[]> ConvertTopLevelAgentsOption = new("--top-level-agents")
    {
        Arity = ArgumentArity.ZeroOrMore,
        AllowMultipleArgumentsPerToken = true,
        Description = "List of top-level agent names for handoff context"
    };

    public static readonly Option<string> ConvertOutputPathOption = new("--output-path")
    {
        Description = "Output path for the generated skill (default: skills/{agent-name})"
    };

    // List command options
    public static readonly Option<int> ListLimitOption = new("--limit")
    {
        Description = "Number of skills per page (1-200, default: 50)",
        DefaultValueFactory = _ => 50
    };

    public static readonly Option<int> ListPageOption = new("--page")
    {
        Description = "Page number (1-based, default: 1)",
        DefaultValueFactory = _ => 1
    };

    public static readonly Option<string> ListSearchOption = new("--search")
    {
        Description = "Search skills by name or description"
    };

    // Download command options
    public static readonly Option<string> DownloadNameOption = new("--name")
    {
        Required = true,
        Description = "Name of the skill to download"
    };

    public static readonly Option<string> DownloadOutputPathOption = new("--output-path")
    {
        Description = "Output path for the downloaded skill (default: skills/{skill-name})"
    };

    // Delete command options
    public static readonly Option<string> DeleteNameOption = new("--name")
    {
        Required = true,
        Description = "Name of the skill to delete"
    };

    // Create command options
    public static readonly Option<string> CreateNameOption = new("--name")
    {
        Required = true,
        Description = "Name of the skill to create"
    };

    public static readonly Option<string> CreateOutputPathOption = new("--output-path")
    {
        Description = "Output path for the created skill (default: skills/{skill-name})"
    };
}
