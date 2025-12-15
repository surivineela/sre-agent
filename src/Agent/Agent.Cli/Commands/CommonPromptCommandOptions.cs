// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for common-prompt commands.
/// </summary>
public static class CommonPromptCommandOptions
{
    // ============================================================
    // Common-Prompt Create Command Options
    // ============================================================

    public static class Create
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the common prompt",
            Required = true
        };

        public static readonly Option<string> PathOption = new("--path")
        {
            Description = "Custom path under CommonPrompts directory (e.g., 'Troubleshooting')"
        };

        public static readonly Option<string> PromptOption = new("--prompt")
        {
            Description = "Prompt content"
        };

        public static readonly Option<string> OwnerOption = new("--owner")
        {
            Description = "Owner of the common prompt"
        };

        public static readonly Option<string[]> TagOption = new("--tag")
        {
            Description = "Tags for the common prompt (can be specified multiple times)",
            Arity = ArgumentArity.ZeroOrMore,
            AllowMultipleArgumentsPerToken = true
        };
    }

    // ============================================================
    // Common-Prompt Get Command Options
    // ============================================================

    public static class Get
    {
        public static readonly Option<string?> SearchOption = new("--search")
        {
            Description = "Search filter for prompt names or content"
        };

        public static readonly Option<string?> NameOption = new("--name")
        {
            Description = "Get a specific common prompt by name and output the full YAML"
        };

        public static readonly Option<bool> DetailOption = new("--detail")
        {
            Description = "Output the full YAML for all prompts in the list"
        };
    }

    // ============================================================
    // Common-Prompt Apply Command Options
    // ============================================================

    public static class Apply
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the common prompt to apply",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Show what would be applied without making changes"
        };
    }

    // ============================================================
    // Common-Prompt Delete Command Options
    // ============================================================

    public static class Delete
    {
        public static readonly Option<string> NameOption = new("--name")
        {
            Description = "Name of the common prompt to delete",
            Required = true
        };

        public static readonly Option<bool> DryRunOption = new("--dry-run")
        {
            Description = "Show what would be deleted without making changes"
        };
    }
}
