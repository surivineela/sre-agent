// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Services;
using Agent.Core.Helpers;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles repository connector command operations.
/// </summary>
public static class RepoCommandHandlers
{
    /// <summary>
    /// Handles the repo add command.
    /// </summary>
    public static async Task HandleAddCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting repo add command");

        try
        {
            var name = parseResult.GetValue(RepoCommandOptions.NameOption);
            var url = parseResult.GetValue(RepoCommandOptions.UrlOption);
            var pat = parseResult.GetValue(RepoCommandOptions.PatOption);

            if (string.IsNullOrWhiteSpace(name))
            {
                ConsoleUI.WriteStatus(false, "Connector name is required.");
                Environment.Exit(1);
                return;
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                ConsoleUI.WriteStatus(false, "Repository URL is required.");
                Environment.Exit(1);
                return;
            }

            // Detect repository type from URL
            var repoType = RepoTypeHelper.DetectRepoType(url);
            DebugLogger.Debug("URL", $"Detected RepoType: {repoType}");

            // If PAT not provided, offer to generate/retrieve one based on repo type
            if (string.IsNullOrWhiteSpace(pat))
            {
                if (repoType == RepoType.GitHub)
                {
                    // GitHub: use gh CLI to get token
                    ConsoleUI.WriteInfo("No PAT provided. Would you like to use GitHub CLI to get one?", ConsoleColor.Cyan);
                    if (ConsoleUI.Confirm("Use gh auth token?", defaultYes: true))
                    {
                        ConsoleUI.WriteInfo("Getting PAT from GitHub CLI...", ConsoleColor.Cyan);
                        var patResult = await GitHubPatHelper.GetPatAsync();
                        if (!patResult.Success)
                        {
                            ConsoleUI.WriteStatus(false, patResult.ErrorMessage ?? "Failed to get PAT");
                            ConsoleUI.WriteInfo("You can manually provide a PAT using the --pat option.", ConsoleColor.Gray);
                            Environment.Exit(1);
                            return;
                        }
                        pat = patResult.Token;
                        ConsoleUI.WriteStatus(true, "PAT retrieved from GitHub CLI");
                    }
                    else
                    {
                        ConsoleUI.WriteStatus(false, "PAT is required to add a repository connector.");
                        ConsoleUI.WriteInfo("Provide a PAT using --pat or allow retrieval via GitHub CLI.", ConsoleColor.Gray);
                        Environment.Exit(1);
                        return;
                    }
                }
                else
                {
                    // Azure DevOps: validate URL and use az CLI to generate PAT
                    var urlInfo = AzureDevOpsPatHelper.ParseAzureDevOpsUrl(url);
                    if (urlInfo == null)
                    {
                        ConsoleUI.WriteStatus(false, "Invalid Azure DevOps URL format.");
                        ConsoleUI.WriteInfo("Expected format: https://dev.azure.com/{org}/{project}/_git/{repo}", ConsoleColor.Gray);
                        ConsoleUI.WriteInfo("          or: https://{org}.visualstudio.com/{project}/_git/{repo}", ConsoleColor.Gray);
                        Environment.Exit(1);
                        return;
                    }

                    DebugLogger.Debug("URL", $"Parsed URL: org={urlInfo.Organization}, project={urlInfo.Project}, repo={urlInfo.Repository}");

                    ConsoleUI.WriteInfo("No PAT provided. Would you like to generate one using Azure CLI?", ConsoleColor.Cyan);
                    if (ConsoleUI.Confirm("Generate PAT?", defaultYes: true))
                    {
                        var scope = AzureDevOpsPatHelper.PromptForScope();
                        var scopeDescription = scope == AzureDevOpsPatHelper.PatScope.ReadOnly ? "read-only" : "read-write";

                        ConsoleUI.WriteInfo($"Generating {scopeDescription} PAT for {urlInfo.Organization}...", ConsoleColor.Cyan);

                        var patResult = await AzureDevOpsPatHelper.GeneratePatAsync(
                            urlInfo.OrganizationUrl,
                            urlInfo.Repository,
                            scope);

                        if (!patResult.Success)
                        {
                            ConsoleUI.WriteStatus(false, $"Failed to generate PAT: {patResult.ErrorMessage}");
                            ConsoleUI.WriteInfo("You can manually provide a PAT using the --pat option.", ConsoleColor.Gray);
                            Environment.Exit(1);
                            return;
                        }

                        pat = patResult.Token;
                        ConsoleUI.WriteStatus(true, "PAT generated successfully");
                    }
                    else
                    {
                        ConsoleUI.WriteStatus(false, "PAT is required to add a repository connector.");
                        ConsoleUI.WriteInfo("Provide a PAT using --pat or allow generation via Azure CLI.", ConsoleColor.Gray);
                        Environment.Exit(1);
                        return;
                    }
                }
            }

            // Create the connector
            ConsoleUI.WriteInfo("Creating repository connector...", ConsoleColor.Cyan);

            using var apiService = new ApiService();
            var request = new ApiService.RepoConnectorRequest
            {
                Name = name,
                DataSource = url,
                PersonalAccessToken = pat!
            };

            var (success, connector, error) = await apiService.CreateOrUpdateRepoConnectorAsync(request);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to create connector: {error}");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Repository connector '{name}' created successfully!");
            Console.WriteLine();

            if (connector != null)
            {
                DisplayConnectorDetails(connector);
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"RepoAdd failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to add repository connector: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the repo update command.
    /// </summary>
    public static async Task HandleUpdateCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting repo update command");

        try
        {
            var name = parseResult.GetValue(RepoCommandOptions.NameOption);
            var pat = parseResult.GetValue(RepoCommandOptions.PatOption);
            var regenerate = parseResult.GetValue(RepoCommandOptions.RegenerateOption);

            if (string.IsNullOrWhiteSpace(name))
            {
                ConsoleUI.WriteStatus(false, "Connector name is required.");
                Environment.Exit(1);
                return;
            }

            using var apiService = new ApiService();

            // Get existing connector to retrieve URL for PAT regeneration
            var (existingConnector, getError) = await apiService.GetRepoConnectorAsync(name);
            if (existingConnector == null)
            {
                ConsoleUI.WriteStatus(false, $"Connector '{name}' not found: {getError}");
                Environment.Exit(1);
                return;
            }

            // If regenerate is requested, generate/retrieve a new PAT based on repo type
            if (regenerate)
            {
                var repoType = RepoTypeHelper.DetectRepoType(existingConnector.DataSource);

                if (repoType == RepoType.GitHub)
                {
                    ConsoleUI.WriteInfo("Getting PAT from GitHub CLI...", ConsoleColor.Cyan);
                    var patResult = await GitHubPatHelper.GetPatAsync();
                    if (!patResult.Success)
                    {
                        ConsoleUI.WriteStatus(false, patResult.ErrorMessage ?? "Failed to get PAT");
                        Environment.Exit(1);
                        return;
                    }
                    pat = patResult.Token;
                    ConsoleUI.WriteStatus(true, "PAT retrieved from GitHub CLI");
                }
                else
                {
                    var urlInfo = AzureDevOpsPatHelper.ParseAzureDevOpsUrl(existingConnector.DataSource);
                    if (urlInfo == null)
                    {
                        ConsoleUI.WriteStatus(false, "Cannot parse existing repository URL for PAT regeneration.");
                        Environment.Exit(1);
                        return;
                    }

                    var scope = AzureDevOpsPatHelper.PromptForScope();
                    var scopeDescription = scope == AzureDevOpsPatHelper.PatScope.ReadOnly ? "read-only" : "read-write";

                    ConsoleUI.WriteInfo($"Generating {scopeDescription} PAT for {urlInfo.Organization}...", ConsoleColor.Cyan);

                    var patResult = await AzureDevOpsPatHelper.GeneratePatAsync(
                        urlInfo.OrganizationUrl,
                        urlInfo.Repository,
                        scope);

                    if (!patResult.Success)
                    {
                        ConsoleUI.WriteStatus(false, $"Failed to generate PAT: {patResult.ErrorMessage}");
                        Environment.Exit(1);
                        return;
                    }

                    pat = patResult.Token;
                    ConsoleUI.WriteStatus(true, "PAT generated successfully");
                }
            }

            if (string.IsNullOrWhiteSpace(pat))
            {
                ConsoleUI.WriteStatus(false, "Either --pat or --regenerate must be specified.");
                Environment.Exit(1);
                return;
            }

            // Update the connector
            ConsoleUI.WriteInfo("Updating repository connector...", ConsoleColor.Cyan);

            var request = new ApiService.RepoConnectorRequest
            {
                Name = name,
                DataSource = existingConnector.DataSource, // Use existing URL
                PersonalAccessToken = pat!
            };

            var (success, connector, error) = await apiService.CreateOrUpdateRepoConnectorAsync(request);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to update connector: {error}");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Repository connector '{name}' updated successfully!");
            Console.WriteLine();

            if (connector != null)
            {
                DisplayConnectorDetails(connector);
            }

            ConsoleUI.WriteInfo("Note: URL cannot be updated. Delete and recreate to change the URL.", ConsoleColor.Gray);

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"RepoUpdate failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to update repository connector: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the repo remove command.
    /// </summary>
    public static async Task HandleRemoveCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting repo remove command");

        try
        {
            var name = parseResult.GetValue(RepoCommandOptions.NameOption);
            var force = parseResult.GetValue(RepoCommandOptions.ForceOption);

            if (string.IsNullOrWhiteSpace(name))
            {
                ConsoleUI.WriteStatus(false, "Connector name is required.");
                Environment.Exit(1);
                return;
            }

            // Confirm deletion unless force is specified
            if (!force)
            {
                if (!ConsoleUI.Confirm($"Are you sure you want to delete connector '{name}'?", defaultYes: false))
                {
                    ConsoleUI.WriteInfo("Operation cancelled.", ConsoleColor.Gray);
                    Environment.Exit(0);
                    return;
                }
            }

            ConsoleUI.WriteInfo("Removing repository connector...", ConsoleColor.Cyan);

            using var apiService = new ApiService();
            var (success, error) = await apiService.DeleteRepoConnectorAsync(name);

            if (!success)
            {
                ConsoleUI.WriteStatus(false, $"Failed to remove connector: {error}");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, $"Repository connector '{name}' removed successfully!");

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"RepoRemove failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to remove repository connector: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Handles the repo list command.
    /// </summary>
    public static async Task HandleListCommand(ParseResult parseResult)
    {
        DebugLogger.Debug("Command", "Starting repo list command");

        try
        {
            using var apiService = new ApiService();
            var (connectors, error) = await apiService.ListRepoConnectorsAsync();

            if (error != null)
            {
                ConsoleUI.WriteStatus(false, $"Failed to list connectors: {error}");
                Environment.Exit(1);
                return;
            }

            if (connectors.Count == 0)
            {
                ConsoleUI.WriteInfo("No repository connectors found.", ConsoleColor.Gray);
                ConsoleUI.WriteInfo("Use 'srectl repo add' to add a new connector.", ConsoleColor.Gray);
                Environment.Exit(0);
                return;
            }

            ConsoleUI.WriteSection("Repository Connectors");
            Console.WriteLine();

            foreach (var connector in connectors)
            {
                DisplayConnectorSummary(connector);
                Console.WriteLine();
            }

            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            DebugLogger.Debug("Exception", $"RepoList failed: {ex.Message}");
            ConsoleUI.WriteStatus(false, $"Failed to list repository connectors: {ex.Message}");
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Displays a connector summary for list view.
    /// </summary>
    private static void DisplayConnectorSummary(ApiService.RepoConnectorResponse connector)
    {
        // Determine status color
        var statusColor = connector.Status switch
        {
            "Healthy" => ConsoleColor.Green,
            "Unhealthy" => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };

        var cloneStatusColor = connector.CloneStatus switch
        {
            "Ready" => ConsoleColor.Green,
            "Failed" => ConsoleColor.Red,
            "Cloning" or "Syncing" => ConsoleColor.Yellow,
            _ => ConsoleColor.Gray
        };

        ConsoleUI.WriteKeyValue("Name", connector.Name, 20, ConsoleColor.White);
        ConsoleUI.WriteKeyValue("Type", connector.RepoType, 20);
        ConsoleUI.WriteKeyValue("URL", connector.DataSource, 20);

        ConsoleUI.WithColor(statusColor, () =>
            ConsoleUI.WriteKeyValue("Status", connector.Status, 20));

        ConsoleUI.WithColor(cloneStatusColor, () =>
            ConsoleUI.WriteKeyValue("Clone Status", connector.CloneStatus, 20));

        if (connector.LastSuccessfulSync.HasValue)
        {
            ConsoleUI.WriteKeyValue("Last Sync", connector.LastSuccessfulSync.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"), 20);
        }

        if (!string.IsNullOrEmpty(connector.ErrorMessage))
        {
            ConsoleUI.WriteKeyValue("Error", connector.ErrorMessage, 20, ConsoleColor.Red);
        }
    }

    /// <summary>
    /// Displays detailed connector information.
    /// </summary>
    private static void DisplayConnectorDetails(ApiService.RepoConnectorResponse connector)
    {
        ConsoleUI.WriteKeyValue("Name", connector.Name, 20);
        ConsoleUI.WriteKeyValue("Type", connector.RepoType, 20);
        ConsoleUI.WriteKeyValue("URL", connector.DataSource, 20);
        ConsoleUI.WriteKeyValue("Status", connector.Status, 20);
        ConsoleUI.WriteKeyValue("Clone Status", connector.CloneStatus, 20);

        if (connector.LastValidated.HasValue)
        {
            ConsoleUI.WriteKeyValue("Last Validated", connector.LastValidated.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"), 20);
        }

        if (connector.LastSuccessfulSync.HasValue)
        {
            ConsoleUI.WriteKeyValue("Last Sync", connector.LastSuccessfulSync.Value.ToString("yyyy-MM-dd HH:mm:ss UTC"), 20);
        }

        if (!string.IsNullOrEmpty(connector.LatestCommit))
        {
            ConsoleUI.WriteKeyValue("Latest Commit", connector.LatestCommit, 20);
        }

        if (!string.IsNullOrEmpty(connector.ErrorMessage))
        {
            ConsoleUI.WriteKeyValue("Error", connector.ErrorMessage, 20, ConsoleColor.Red);
        }
    }
}
