// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;

namespace Agent.Cli.Commands;

/// <summary>
/// Builds command-line options for repo commands.
/// </summary>
public static class RepoCommandOptions
{
    /// <summary>
    /// Connector name option - required for all repo commands.
    /// </summary>
    public static readonly Option<string> NameOption = new("--name", "-n")
    {
        Required = true,
        Description = "Name of the repository connector"
    };

    /// <summary>
    /// Azure DevOps repository URL option - required for add command.
    /// </summary>
    public static readonly Option<string> UrlOption = new("--url", "-u")
    {
        Required = true,
        Description = "Azure DevOps repository URL (e.g., https://dev.azure.com/{org}/{project}/_git/{repo})"
    };

    /// <summary>
    /// Personal Access Token option - optional, will prompt to generate if not provided.
    /// </summary>
    public static readonly Option<string> PatOption = new("--pat", "-p")
    {
        Required = false,
        Description = "Personal Access Token for authentication. If not provided, will offer to generate one using Azure CLI"
    };

    /// <summary>
    /// Regenerate PAT option for update command.
    /// </summary>
    public static readonly Option<bool> RegenerateOption = new("--regenerate", "-r")
    {
        Description = "Generate a new PAT using Azure CLI (mutually exclusive with --pat)"
    };

    /// <summary>
    /// Force option for remove command - skip confirmation prompt.
    /// </summary>
    public static readonly Option<bool> ForceOption = new("--force", "-f")
    {
        Description = "Skip confirmation prompt when removing"
    };
}
