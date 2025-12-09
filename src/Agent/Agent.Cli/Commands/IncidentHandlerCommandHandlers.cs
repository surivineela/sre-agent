// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text.Json.Nodes;
using Agent.Cli.Helpers;
using Agent.Cli.Services;

namespace Agent.Cli.Commands;

public static class IncidentHandlerCommandHandlers
{
    public static async Task HandleMapAgentCommand(ParseResult parseResult)
    {
        var filterName = parseResult.GetValue(IncidentHandlerCommandOptions.MapAgent.NameOption);
        var handlingAgent = parseResult.GetValue(IncidentHandlerCommandOptions.MapAgent.HandlingAgentOption);

        if (string.IsNullOrWhiteSpace(filterName) || string.IsNullOrWhiteSpace(handlingAgent))
        {
            ConsoleUI.WriteStatus(false, "Both filter name and handling agent are required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteSection($"Mapping agent '{handlingAgent}' to filter '{filterName}'");

            // Step 1: Fetch the incident filter
            ConsoleUI.WriteBullet("Fetching incident filter...", ConsoleColor.Cyan);
            var filters = await apiService.GetIncidentFiltersAsync();

            var filter = filters?.FirstOrDefault(f =>
                f["Name"]?.ToString() == filterName ||
                f["name"]?.ToString() == filterName ||
                f["Id"]?.ToString() == filterName ||
                f["id"]?.ToString() == filterName);

            if (filter == null)
            {
                ConsoleUI.WriteStatus(false, $"Filter '{filterName}' not found.");

                // Show available filters to help user
                if (filters != null && filters.Count > 0)
                {
                    ConsoleUI.WriteSection("Available filters:");
                    for (int i = 0; i < filters.Count; i++)
                    {
                        var f = filters[i];
                        var id = f?["id"]?.GetValue<string>() ?? f?["Id"]?.GetValue<string>() ?? "N/A";
                        var name = f?["name"]?.GetValue<string>() ?? f?["Name"]?.GetValue<string>() ?? "N/A";
                        var titleContains = f?["titleContains"]?.GetValue<string>() ?? "";

                        if (string.IsNullOrEmpty(name) || name == "N/A")
                        {
                            ConsoleUI.WriteBullet($"[{i + 1}] {id} (matches title: \"{titleContains}\")", ConsoleColor.White);
                        }
                        else
                        {
                            ConsoleUI.WriteBullet($"[{i + 1}] {name} (ID: {id})", ConsoleColor.White);
                        }
                    }
                }
                else
                {
                    ConsoleUI.WriteInfo("No incident filters found on the server.");
                }

                Environment.Exit(1);
                return;
            }

            var filterId = filter["id"]?.ToString() ?? filter["Id"]?.ToString();
            if (string.IsNullOrEmpty(filterId))
            {
                ConsoleUI.WriteStatus(false, "Filter ID not found.");
                Environment.Exit(1);
                return;
            }

            // Step 2: Check if agent exists
            ConsoleUI.WriteBullet("Verifying agent exists...", ConsoleColor.Cyan);
            var (agents, error) = await apiService.ListExtendedAgentsAsync();
            if (error != null)
            {
                ConsoleUI.WriteStatus(false, $"Failed to list agents: {error}");
                Environment.Exit(1);
                return;
            }

            // Check if our agent exists
            var agentExists = agents.Any(a => a.Metadata.Name == handlingAgent);

            if (!agentExists)
            {
                ConsoleUI.WriteStatus(false, $"Agent '{handlingAgent}' not found. Please create the agent first.");
                Environment.Exit(1);
                return;
            }

            // Step 3: Update the filter with HandlingAgent
            ConsoleUI.WriteBullet("Updating incident filter with handling agent...", ConsoleColor.Cyan);
            filter["handlingAgent"] = handlingAgent;

            var updateSuccess = await apiService.UpdateIncidentFilterAsync(filterId, filter);
            if (!updateSuccess)
            {
                ConsoleUI.WriteStatus(false, "Failed to update incident filter.");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, "Successfully updated incident filter with handling agent.");

            // Step 4: Check for existing incident handlers
            ConsoleUI.WriteBullet("Checking for existing incident handlers...", ConsoleColor.Cyan);
            var handlers = await apiService.GetIncidentHandlersAsync();
            var matchingHandlers = handlers?.Where(h => h["incidentFilterId"]?.ToString() == filterId).ToList();

            if (matchingHandlers?.Count > 0)
            {
                ConsoleUI.WriteInfo($"Found {matchingHandlers.Count} existing incident handler(s) for this filter.");

                foreach (var handler in matchingHandlers)
                {
                    var handlerId = handler["Id"]?.ToString();
                    var handlerName = handler["Name"]?.ToString() ?? "Unknown";

                    if (!string.IsNullOrEmpty(handlerId))
                    {
                        ConsoleUI.WriteBullet($"Deleting handler '{handlerName}' (ID: {handlerId})...", ConsoleColor.Yellow);
                        var deleteSuccess = await apiService.DeleteIncidentHandlerAsync(handlerId);

                        if (deleteSuccess)
                        {
                            ConsoleUI.WriteStatus(true, $"Deleted handler '{handlerName}'.");
                        }
                        else
                        {
                            ConsoleUI.WriteStatus(false, $"Failed to delete handler '{handlerName}'. You may need to delete it manually.");
                        }
                    }
                }
            }
            else
            {
                ConsoleUI.WriteInfo("No existing incident handlers found for this filter.");
            }

            ConsoleUI.WriteStatus(true, $"Successfully mapped agent '{handlingAgent}' to filter '{filterName}'.");
            ConsoleUI.WriteInfo("The agent will now handle incidents matching this filter.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error mapping agent to filter: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleCreateCommand(ParseResult parseResult)
    {
        var id = parseResult.GetValue(IncidentHandlerCommandOptions.Create.IdOption);
        var name = parseResult.GetValue(IncidentHandlerCommandOptions.Create.NameOption);
        var impactedService = parseResult.GetValue(IncidentHandlerCommandOptions.Create.ImpactedServiceOption);
        var priority = parseResult.GetValue(IncidentHandlerCommandOptions.Create.PriorityOption);
        var incidentType = parseResult.GetValue(IncidentHandlerCommandOptions.Create.IncidentTypeOption);
        var alertId = parseResult.GetValue(IncidentHandlerCommandOptions.Create.AlertIdOption);
        var titleContains = parseResult.GetValue(IncidentHandlerCommandOptions.Create.TitleContainsOption);
        var agentMode = parseResult.GetValue(IncidentHandlerCommandOptions.Create.AgentModeOption);
        var handlingAgent = parseResult.GetValue(IncidentHandlerCommandOptions.Create.HandlingAgentOption);
        var owningTeamId = parseResult.GetValue(IncidentHandlerCommandOptions.Create.OwningTeamIdOption);
        var maxAttempts = parseResult.GetValue(IncidentHandlerCommandOptions.Create.MaxAttemptsOption);

        if (string.IsNullOrWhiteSpace(id))
        {
            ConsoleUI.WriteStatus(false, "Filter ID is required.");
            Environment.Exit(1);
            return;
        }

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteSection($"Creating incident filter '{id}'");

            // Check if filter already exists
            ConsoleUI.WriteBullet("Checking if filter already exists...", ConsoleColor.Cyan);
            var existingFilters = await apiService.GetIncidentFiltersAsync();
            var existingFilter = existingFilters?.FirstOrDefault(f =>
                f["id"]?.ToString() == id || f["Id"]?.ToString() == id);

            if (existingFilter != null)
            {
                ConsoleUI.WriteStatus(false, $"Filter with ID '{id}' already exists.");
                Environment.Exit(1);
                return;
            }

            // If handling agent is specified, verify it exists
            if (!string.IsNullOrWhiteSpace(handlingAgent))
            {
                ConsoleUI.WriteBullet("Skipping agent verification (agents API unavailable)...", ConsoleColor.Yellow);
            }

            // Create the filter JSON object
            var filter = new JsonObject
            {
                ["id"] = id,
                ["name"] = name ?? string.Empty,
                ["impactedService"] = impactedService ?? string.Empty,
                ["priority"] = priority ?? string.Empty,
                ["incidentType"] = incidentType ?? string.Empty,
                ["alertId"] = alertId ?? string.Empty,
                ["titleContains"] = titleContains ?? string.Empty,
                ["agentMode"] = agentMode ?? "autonomous",
                ["handlingAgent"] = handlingAgent ?? string.Empty,
                ["owningTeamId"] = !string.IsNullOrWhiteSpace(owningTeamId) ? owningTeamId : null,
                ["maxAutomatedInvestigationAttempts"] = maxAttempts > 0 ? maxAttempts : 3,
                ["isEnabled"] = true,
                ["isDeleted"] = false,
                ["documentType"] = "IncidentFilterIcm",
                ["partitionKey"] = "IncidentFilterIcm",
                ["monitorId"] = null,
                ["createdBy"] = null
            };

            // Create the filter
            ConsoleUI.WriteBullet("Creating incident filter...", ConsoleColor.Cyan);
            var (createSuccess, createMessage) = await apiService.CreateIncidentFilterAsync(filter);
            if (!createSuccess)
            {
                ConsoleUI.WriteStatus(false, $"Failed to create incident filter: {createMessage}");
                Environment.Exit(1);
                return;
            }

            ConsoleUI.WriteStatus(true, "Successfully created incident filter.");
            ConsoleUI.WriteKeyValue("Filter ID", id);

            if (!string.IsNullOrWhiteSpace(name))
                ConsoleUI.WriteKeyValue("Name", name);
            if (!string.IsNullOrWhiteSpace(handlingAgent))
                ConsoleUI.WriteKeyValue("Handling Agent", handlingAgent);
            if (!string.IsNullOrWhiteSpace(titleContains))
                ConsoleUI.WriteKeyValue("Title Contains", titleContains);
            if (!string.IsNullOrWhiteSpace(incidentType))
                ConsoleUI.WriteKeyValue("Incident Type", incidentType);
            if (!string.IsNullOrWhiteSpace(priority))
                ConsoleUI.WriteKeyValue("Priority", priority);

            ConsoleUI.WriteInfo("The filter is now ready to match incidents based on the specified criteria.");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error creating incident filter: {ex.Message}");
            Environment.Exit(1);
        }
    }

    public static async Task HandleListCommand(ParseResult parseResult)
    {
        var verbose = parseResult.GetValue(IncidentHandlerCommandOptions.List.VerboseOption);

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet("Fetching incident handlers (incident response plans)...", ConsoleColor.Cyan);

            // Fetch both filters (incident response plan) and handlers (custom response plan)
            var filtersTask = apiService.GetIncidentFiltersAsync();
            var handlersTask = apiService.GetIncidentHandlersAsync();

            await Task.WhenAll(filtersTask, handlersTask);

            var filters = await filtersTask;
            var handlers = await handlersTask;

            if (filters == null && handlers == null)
            {
                ConsoleUI.WriteStatus(false, "Failed to fetch incident handlers (incident response plans).");
                Environment.Exit(1);
                return;
            }

            // Build a map of handlers by filterId so we can link them to filters
            Dictionary<string, JsonNode>? handlersByFilterId = null;
            if (handlers != null && handlers.Count > 0)
            {
                handlersByFilterId = new Dictionary<string, JsonNode>();
                foreach (var handler in handlers)
                {
                    var filterId = handler["incidentFilterId"]?.ToString();
                    if (!string.IsNullOrEmpty(filterId) && !handlersByFilterId.ContainsKey(filterId))
                    {
                        handlersByFilterId[filterId] = handler;
                    }
                }
            }

            // Display filters (what users actually create)
            if (filters == null || filters.Count == 0)
            {
                ConsoleUI.WriteInfo("No incident handlers (incident response plans)found.");
                return;
            }

            ConsoleUI.WriteSection($"Found {filters.Count} incident handler(s) (incident response plans):");
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                var filterId = filter["id"]?.ToString() ?? filter["Id"]?.ToString() ?? "N/A";
                var filterName = filter["name"]?.ToString() ?? filter["Name"]?.ToString() ?? "Unnamed Filter";
                var handlingAgent = filter["handlingAgent"]?.ToString() ?? filter["HandlingAgent"]?.ToString();
                var titleContains = filter["titleContains"]?.ToString() ?? filter["TitleContains"]?.ToString();
                var priority = filter["priority"]?.ToString() ?? filter["Priority"]?.ToString();
                var impactedService = filter["impactedService"]?.ToString() ?? filter["ImpactedService"]?.ToString();
                var incidentType = filter["incidentType"]?.ToString() ?? filter["IncidentType"]?.ToString();
                var isEnabled = filter["isEnabled"]?.ToString() ?? filter["IsEnabled"]?.ToString() ?? "true";
                var agentMode = filter["agentMode"]?.ToString() ?? filter["AgentMode"]?.ToString();

                Console.WriteLine($"[{i + 1}] {filterName}");
                ConsoleUI.WriteKeyValue("ID", filterId, 4);

                if (!string.IsNullOrEmpty(handlingAgent))
                {
                    ConsoleUI.WriteKeyValue("Handling Agent", handlingAgent, 4);
                }
                else
                {
                    ConsoleUI.WriteKeyValue("Handling Agent", "Meta Agent (default)", 4);
                }

                ConsoleUI.WriteKeyValue("Enabled", isEnabled, 4);

                // Show filter fields
                if (verbose)
                {
                    if (!string.IsNullOrEmpty(titleContains))
                    {
                        ConsoleUI.WriteKeyValue("Title Contains", titleContains, 4);
                    }
                    if (!string.IsNullOrEmpty(priority))
                    {
                        ConsoleUI.WriteKeyValue("Priority", priority, 4);
                    }
                    if (!string.IsNullOrEmpty(impactedService))
                    {
                        ConsoleUI.WriteKeyValue("Impacted Service", impactedService, 4);
                    }
                    if (!string.IsNullOrEmpty(incidentType))
                    {
                        ConsoleUI.WriteKeyValue("Incident Type", incidentType, 4);
                    }
                    if (!string.IsNullOrEmpty(agentMode))
                    {
                        ConsoleUI.WriteKeyValue("Autonomy Level", agentMode, 4);
                    }
                }

                // Show associated handler (custom response plan) if exists
                if (handlersByFilterId != null && handlersByFilterId.TryGetValue(filterId, out var handler))
                {
                    var handlerId = handler["id"]?.ToString() ?? handler["Id"]?.ToString();
                    var handlerName = handler["name"]?.ToString() ?? handler["Name"]?.ToString();

                    ConsoleUI.WriteKeyValue("Custom Response Plan", "Set up", 4);

                    if (verbose)
                    {
                        ConsoleUI.WriteKeyValue("Custom Response Plan ID", handlerId ?? "N/A", 4);
                        if (!string.IsNullOrEmpty(handlerName))
                        {
                            ConsoleUI.WriteKeyValue("Custom Response Plan Name", handlerName, 4);
                        }
                    }
                }
                else
                {
                    if (verbose)
                    {
                        ConsoleUI.WriteKeyValue("Custom Response Plan", "Not configured", 4);
                    }
                }

                if (i < filters.Count - 1)
                {
                    Console.WriteLine();
                }
            }

            // Show summary
            Console.WriteLine();
            ConsoleUI.WriteInfo($"Total: {filters.Count} filter(s), {handlers?.Count ?? 0} custom response plan(s)");
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error listing incident filters (incident response plans): {ex.Message}");
            Environment.Exit(1);
        }
    }
}
