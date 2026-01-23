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
    // ============================================================
    // Skill Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Required = true,
            Description = "Name of the skill to create"
        };

        public static readonly Option<string> DescriptionOption = new("--description")
        {
            Description = "Description of what the skill does and when to use it"
        };
    }

    // ============================================================
    // Skill Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Required = true,
            Description = "Name of the skill to apply to the server"
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Preview changes without applying them"
        };
    }

    // ============================================================
    // Skill Convert Command Options
    // ============================================================

    public static class Convert
    {
        public static readonly Option<string> AgentNameOption = new("--agent-name")
        {
            Required = true,
            Description = "Name of the agent to convert to a skill"
        };

        public static readonly Option<string[]> TopLevelAgentsOption = new("--top-level-agents")
        {
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true,
            Description = "List of top-level agent names for handoff context"
        };

        public static readonly Option<string> OutputPathOption = new("--output-path")
        {
            Description = "Output path for the generated skill (default: skills/{agent-name})"
        };
    }

    // ============================================================
    // Skill List Command Options
    // ============================================================

    public static class List
    {
        public static readonly Option<string?> SearchOption = new("--search")
        {
            Description = "Search filter for skill names or descriptions"
        };

        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Get a specific skill by name and output the full YAML"
        };

        public static readonly Option<bool> DetailOption = new("--detail")
        {
            Description = "Output the full YAML for all skills in the list"
        };
    }

    // ============================================================
    // Skill Sync Command Options
    // ============================================================

    public static class Sync
    {
        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Name of the skill to sync"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Sync all skills from the server"
        };

        public static readonly Option<string?> PathOption = new("--path", "--output-path")
        {
            Description = "Output path for the synced skill(s) (default: skills/)"
        };
    }

    // ============================================================
    // Skill Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Required = true,
            Description = "Name of the skill to delete"
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Preview deletion without actually removing the skill"
        };

        public static readonly Option<bool?> DeleteLocalFilesOption = new("--delete-local-files")
        {
            Description = "Also delete local configuration files without prompting (true=delete, false=skip, omit=prompt)",
            Arity = ArgumentArity.ZeroOrOne
        };
    }

    // ============================================================
    // Skill Migrate Command Options
    // ============================================================

    public static class Migrate
    {
        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Name of the skill to migrate to frontmatter format"
        };

        public static readonly Option<bool> AllOption = new("--all")
        {
            Description = "Migrate all skills to frontmatter format"
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Preview migration without modifying files"
        };
    }
}
