// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using Agent.Cli.Helpers;
using Agent.Cli.Models;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

/// <summary>
/// Handles incident-filter-related command operations.
/// </summary>
public static class IncidentFilterCommandHandlers
{
    private const string IncidentFiltersDirectory = "IncidentFilters";

    /// <summary>
    /// Handles the incident-filter create command.
    /// </summary>
    public static async Task<int> HandleCreateCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting incident-filter create command");

        // Required options
        var name = parseResult.GetValue(IncidentFilterCommandOptions.Create.NameOption);
        var platform = parseResult.GetValue(IncidentFilterCommandOptions.Create.PlatformOption);

        // Common filter options
        var handlingAgent = parseResult.GetValue(IncidentFilterCommandOptions.Create.HandlingAgentOption);
        var impactedService = parseResult.GetValue(IncidentFilterCommandOptions.Create.ImpactedServiceOption);
        var priority = parseResult.GetValue(IncidentFilterCommandOptions.Create.PriorityOption);
        var incidentType = parseResult.GetValue(IncidentFilterCommandOptions.Create.IncidentTypeOption);
        var alertId = parseResult.GetValue(IncidentFilterCommandOptions.Create.AlertIdOption);
        var titleContains = parseResult.GetValue(IncidentFilterCommandOptions.Create.TitleContainsOption);
        var agentMode = parseResult.GetValue(IncidentFilterCommandOptions.Create.AgentModeOption);
        var owningTeamId = parseResult.GetValue(IncidentFilterCommandOptions.Create.OwningTeamIdOption);
        var maxInvestigationAttempts = parseResult.GetValue(IncidentFilterCommandOptions.Create.MaxAutomatedInvestigationAttemptsOption);
        var deepInvestigation = parseResult.GetValue(IncidentFilterCommandOptions.Create.DeepInvestigationEnabledOption);
        var disabled = parseResult.GetValue(IncidentFilterCommandOptions.Create.DisabledOption);

        // IcM-specific options
        var monitorId = parseResult.GetValue(IncidentFilterCommandOptions.Create.MonitorIdOption);
        var createdBy = parseResult.GetValue(IncidentFilterCommandOptions.Create.CreatedByOption);

        // AzMonitor-specific options
        var targetResourceType = parseResult.GetValue(IncidentFilterCommandOptions.Create.TargetResourceTypeOption);
        var targetResource = parseResult.GetValue(IncidentFilterCommandOptions.Create.TargetResourceOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, Platform: {platform}");

        // Normalize platform name to canonical spelling
        var normalizedPlatform = IncidentFilterHelper.NormalizePlatform(platform);

        // Create IncidentFilterV2 instance
        var incidentFilter = new IncidentFilterV2
        {
            Metadata = new ResourceMetadataModel
            {
                Name = name
            },
            Spec = new IncidentFilterSpecV2
            {
                IncidentPlatform = normalizedPlatform,
                HandlingAgent = handlingAgent,
                ImpactedService = impactedService,
                Priority = priority,
                IncidentType = incidentType,
                AlertId = alertId,
                TitleContains = titleContains,
                AgentMode = agentMode,
                OwningTeamId = owningTeamId,
                MaxAutomatedInvestigationAttempts = maxInvestigationAttempts,
                DeepInvestigationEnabled = deepInvestigation ? true : null,
                IsEnabled = !disabled
            }
        };

        // Add platform-specific settings
        if (string.Equals(normalizedPlatform, IncidentFilterHelper.Platform.IcM, StringComparison.OrdinalIgnoreCase))
        {
            incidentFilter.Spec.IcmFilterSettings = new IcmFilterSettingsV2
            {
                MonitorId = monitorId,
                CreatedBy = createdBy
            };
        }
        else if (string.Equals(normalizedPlatform, IncidentFilterHelper.Platform.AzMonitor, StringComparison.OrdinalIgnoreCase))
        {
            incidentFilter.Spec.AzMonitorFilterSettings = new AzMonitorFilterSettingsV2
            {
                TargetResourceType = targetResourceType,
                TargetResource = targetResource
            };
        }

        // Serialize to YAML
        var filterYaml = incidentFilter.ToYaml();

        // Write filter to file
        Directory.CreateDirectory(IncidentFiltersDirectory);
        var yamlPath = Path.Combine(IncidentFiltersDirectory, $"{name}.yaml");

        DebugLogger.LogFile("WRITE", yamlPath, $"Incident filter YAML content size: {filterYaml.Length} characters");

        await File.WriteAllTextAsync(yamlPath, filterYaml);
        ConsoleUI.WriteStatus(true, $"Successfully created incident filter '{name}' at {IncidentFiltersDirectory}");
        Console.WriteLine();
        ConsoleUI.WriteSection("Created Files:");
        ConsoleUI.WriteBullet(yamlPath, ConsoleColor.Green, 3);

        Console.WriteLine();
        ConsoleUI.WriteSection("Next Steps");
        ConsoleUI.WriteBullet("Review and customize", "Edit the generated YAML file", ConsoleColor.Cyan);
        ConsoleUI.WriteBullet("Apply filter", $"srectl incident-filter apply --name {name}", ConsoleColor.Cyan);

        return 0;
    }

    /// <summary>
    /// Handles the incident-filter get command to display available incident filters.
    /// </summary>
    public static async Task<int> HandleGetCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting incident-filter get command");

        var name = parseResult.GetValue(IncidentFilterCommandOptions.Get.NameOption);
        var detail = parseResult.GetValue(IncidentFilterCommandOptions.Get.DetailOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, Detail: {detail}");

        using var apiService = new ApiService();

        var (filtersList, error) = await apiService.ListIncidentFiltersAsync();

        if (error != null)
        {
            ConsoleUI.WriteStatus(false, error);
            return 1;
        }

        // If a specific filter name is requested, look for it first
        if (!string.IsNullOrWhiteSpace(name))
        {
            var filter = filtersList.FirstOrDefault(f =>
                string.Equals(f.Metadata?.Name, name, StringComparison.OrdinalIgnoreCase));

            if (filter == null)
            {
                ConsoleUI.WriteStatus(false, $"Incident filter '{name}' not found.");
                return 1;
            }

            ConsoleUI.WriteSection("Remote Incident Filter");
            Console.WriteLine(filter.ToYaml());
            return 0;
        }

        if (filtersList.Count == 0)
        {
            ConsoleUI.WriteInfo("No incident filters found on the server.", ConsoleColor.Yellow);
            ConsoleUI.WriteInfo("Use 'srectl incident-filter apply --name <filter-name>' to add filters to the server.", ConsoleColor.Gray);
            return 0;
        }

        ConsoleUI.WriteSection("Remote Incident Filters");

        for (int i = 0; i < filtersList.Count; i++)
        {
            if (detail)
            {
                var yamlOutput = filtersList[i].ToYaml();
                Console.WriteLine(yamlOutput);
                if (i < filtersList.Count - 1)
                {
                    ConsoleUI.DrawLine();
                }
            }
            else
            {
                var filterName = filtersList[i].Metadata?.Name ?? "Unknown";
                var platform = IncidentFilterHelper.NormalizePlatform(filtersList[i].Spec?.IncidentPlatform) ?? "Unknown";
                var isEnabled = filtersList[i].Spec?.IsEnabled ?? false;
                var status = isEnabled ? "enabled" : "disabled";
                ConsoleUI.WriteBullet($"{filterName} ({platform}, {status})");
            }
        }

        Console.WriteLine();
        ConsoleUI.WriteKeyValue("Total", $"{filtersList.Count} incident filter(s)", 0);
        return 0;
    }

    /// <summary>
    /// Handles the incident-filter apply command.
    /// </summary>
    public static async Task<int> HandleApplyCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting incident-filter apply command");

        var name = parseResult.GetValue(IncidentFilterCommandOptions.Apply.NameOption);
        var dryRun = parseResult.GetValue(IncidentFilterCommandOptions.Apply.DryRunOption);

        DebugLogger.Debug("Parameters", $"Name: {name}, DryRun: {dryRun}");

        // Find incident filter YAML file
        var filterFilePath = FindIncidentFilter(name!);
        if (filterFilePath == null)
        {
            ConsoleUI.WriteStatus(false, $"Incident filter file not found for '{name}'. Searched in {IncidentFiltersDirectory} directory for '{name}.yaml'");
            return 1;
        }

        // Read and parse the YAML file as IncidentFilterV2
        var filter = await IncidentFilterV2.LoadYamlAsync(filterFilePath);
        if (filter == null)
        {
            ConsoleUI.WriteStatus(false, $"Failed to parse incident filter YAML file: {filterFilePath}");
            return 1;
        }

        using var apiService = new ApiService();
        var (success, response) = await apiService.ApplyIncidentFilterAsync(filter, dryRun);

        ConsoleUI.WriteStatus(success, response);
        return success ? 0 : 1;
    }

    /// <summary>
    /// Handles the incident-filter delete command.
    /// </summary>
    public static async Task<int> HandleDeleteCommand(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        DebugLogger.Debug("Command", "Starting incident-filter delete command");

        var filterName = parseResult.GetValue(IncidentFilterCommandOptions.Delete.NameOption);
        var dryRun = parseResult.GetValue(IncidentFilterCommandOptions.Delete.DryRunOption);
        var deleteLocalFiles = parseResult.GetValue(IncidentFilterCommandOptions.Delete.DeleteLocalFilesOption);

        DebugLogger.Debug("Parameters", $"FilterName: {filterName}, DryRun: {dryRun}, DeleteLocalFiles: {deleteLocalFiles}");

        if (string.IsNullOrWhiteSpace(filterName))
        {
            ConsoleUI.WriteStatus(false, "Incident filter name is required.");
            return 1;
        }

        using var apiService = new ApiService();

        if (dryRun)
        {
            ConsoleUI.WriteInfo($"Validating incident filter deletion for '{filterName}' (dry run)...", ConsoleColor.Yellow);
        }
        else
        {
            ConsoleUI.WriteInfo($"Deleting incident filter '{filterName}'...", ConsoleColor.Yellow);
        }

        var (success, message) = await apiService.DeleteIncidentFilterAsync(filterName, dryRun);

        if (success)
        {
            ConsoleUI.WriteStatus(true, message);

            // After successful server deletion (not dry-run), offer to clean up local files
            if (!dryRun)
            {
                OfferLocalFilterCleanup(filterName, deleteLocalFiles);
            }

            return 0;
        }
        else
        {
            ConsoleUI.WriteStatus(false, message);
            return 1;
        }
    }

    /// <summary>
    /// Finds an incident filter YAML file by name.
    /// </summary>
    private static string? FindIncidentFilter(string name)
    {
        // Check flat structure: IncidentFilters/{name}.yaml
        var flatPath = Path.Combine(IncidentFiltersDirectory, $"{name}.yaml");
        if (File.Exists(flatPath))
        {
            return flatPath;
        }

        // Check directory structure: IncidentFilters/{name}/{name}.yaml
        var dirPath = Path.Combine(IncidentFiltersDirectory, name, $"{name}.yaml");
        if (File.Exists(dirPath))
        {
            return dirPath;
        }

        return null;
    }

    /// <summary>
    /// Offers to clean up local incident filter files after successful server deletion.
    /// </summary>
    /// <param name="filterName">The name of the incident filter to clean up.</param>
    /// <param name="deleteLocalFiles">If true, delete without prompting. If false, skip without prompting. If null, prompt for confirmation.</param>
    private static void OfferLocalFilterCleanup(string filterName, bool? deleteLocalFiles = null)
    {
        var filterFile = FindIncidentFilter(filterName);

        if (filterFile == null)
        {
            return; // No local files to clean up
        }

        var filterDir = Path.GetDirectoryName(filterFile);

        Console.WriteLine();
        ConsoleUI.WriteSection("Local File Cleanup");
        ConsoleUI.WriteInfo("Local configuration files still exist:", ConsoleColor.Yellow);
        ConsoleUI.WriteBullet(filterFile, ConsoleColor.Gray);
        Console.WriteLine();

        // Determine whether to delete: explicit true, explicit false, or prompt
        var shouldDelete = deleteLocalFiles ?? ConsoleUI.Confirm("Also delete local configuration files?", false);

        if (shouldDelete)
        {
            // If filter is in its own directory, delete the directory
            if (filterDir != null && Path.GetFileName(filterDir) == filterName)
            {
                Directory.Delete(filterDir, true);
                ConsoleUI.WriteStatus(true, $"Deleted directory: {filterDir}");
            }
            else
            {
                // Otherwise just delete the YAML file
                File.Delete(filterFile);
                ConsoleUI.WriteStatus(true, $"Deleted file: {filterFile}");
            }

            Console.WriteLine();
            ConsoleUI.WriteSection("Summary");
            ConsoleUI.WriteBullet($"Incident filter '{filterName}' deleted from server", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Local configuration files deleted", ConsoleColor.Green);
        }
        else
        {
            Console.WriteLine();
            ConsoleUI.WriteSection("Summary");
            ConsoleUI.WriteBullet($"Incident filter '{filterName}' deleted from server", ConsoleColor.Green);
            ConsoleUI.WriteBullet($"Local configuration files preserved: {filterFile}", ConsoleColor.Yellow);

            Console.WriteLine();
            ConsoleUI.WriteInfo($"To redeploy: srectl incident-filter apply --name {filterName}", ConsoleColor.Cyan);
            ConsoleUI.WriteInfo($"To delete locally: rm {filterFile.Replace('\\', '/')}", ConsoleColor.Gray);
        }
    }
}
