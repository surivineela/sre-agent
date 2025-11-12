// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Nodes;
using Agent.Cli.Services;
using Agent.Cli.Helpers;

namespace Agent.Cli.Commands;

public static class IncidentHandlerCommandHandlers
{
    public static async Task HandleMapAgentCommand(ParseResult parseResult)
    {
        var filterName = parseResult.GetValue(IncidentHandlerCommandOptions.FilterNameOption);
        var handlingAgent = parseResult.GetValue(IncidentHandlerCommandOptions.HandlingAgentOption);

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
            var (agentsSuccess, agentsResponse) = await apiService.ListAgentsAsync();
            if (!agentsSuccess)
            {
                ConsoleUI.WriteStatus(false, $"Failed to list agents: {agentsResponse}");
                Environment.Exit(1);
                return;
            }

            // Parse agents response and check if our agent exists
            var agentExists = false;
            try
            {
                var jsonDoc = JsonDocument.Parse(agentsResponse);
                JsonElement agents = default;
                bool foundAgents = false;

                // Try different response structure patterns (same as in ListAgentsAsync)
                if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    if (dataElement.ValueKind == JsonValueKind.Object &&
                        dataElement.TryGetProperty("agents", out agents) && agents.ValueKind == JsonValueKind.Array)
                    {
                        foundAgents = true;
                    }
                    else if (dataElement.ValueKind == JsonValueKind.Array)
                    {
                        agents = dataElement;
                        foundAgents = true;
                    }
                }
                else if (jsonDoc.RootElement.TryGetProperty("agents", out agents) && agents.ValueKind == JsonValueKind.Array)
                {
                    foundAgents = true;
                }
                else if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    agents = jsonDoc.RootElement;
                    foundAgents = true;
                }

                if (foundAgents)
                {
                    foreach (var agent in agents.EnumerateArray())
                    {
                        if (agent.TryGetProperty("name", out var nameProperty) &&
                            nameProperty.GetString() == handlingAgent)
                        {
                            agentExists = true;
                            break;
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                ConsoleUI.WriteStatus(false, $"Failed to parse agents response: {ex.Message}");
                Environment.Exit(1);
                return;
            }

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
        var id = parseResult.GetValue(IncidentHandlerCommandOptions.CreateIdOption);
        var name = parseResult.GetValue(IncidentHandlerCommandOptions.CreateNameOption);
        var impactedService = parseResult.GetValue(IncidentHandlerCommandOptions.ImpactedServiceOption);
        var priority = parseResult.GetValue(IncidentHandlerCommandOptions.PriorityOption);
        var incidentType = parseResult.GetValue(IncidentHandlerCommandOptions.IncidentTypeOption);
        var alertId = parseResult.GetValue(IncidentHandlerCommandOptions.AlertIdOption);
        var titleContains = parseResult.GetValue(IncidentHandlerCommandOptions.TitleContainsOption);
        var agentMode = parseResult.GetValue(IncidentHandlerCommandOptions.AgentModeOption);
        var handlingAgent = parseResult.GetValue(IncidentHandlerCommandOptions.CreateHandlingAgentOption);
        var owningTeamId = parseResult.GetValue(IncidentHandlerCommandOptions.OwningTeamIdOption);
        var maxAttempts = parseResult.GetValue(IncidentHandlerCommandOptions.MaxAttemptsOption);

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
        var verbose = parseResult.GetValue(IncidentHandlerCommandOptions.VerboseOption);

        try
        {
            using var apiService = new ApiService();

            ConsoleUI.WriteBullet("Fetching incident handlers...", ConsoleColor.Cyan);

            // Fetch incident handlers
            var handlers = await apiService.GetIncidentHandlersAsync();
            if (handlers == null)
            {
                ConsoleUI.WriteStatus(false, "Failed to fetch incident handlers.");
                Environment.Exit(1);
                return;
            }

            if (handlers.Count == 0)
            {
                ConsoleUI.WriteInfo("No incident handlers found.");
                return;
            }

            ConsoleUI.WriteSection($"Found {handlers.Count} incident handler(s):");

            // Optionally fetch filters for verbose mode
            Dictionary<string, JsonNode>? filterMap = null;
            if (verbose)
            {
                ConsoleUI.WriteBullet("Fetching incident filters for detailed view...", ConsoleColor.Cyan);
                var filters = await apiService.GetIncidentFiltersAsync();
                if (filters != null)
                {
                    filterMap = [];
                    foreach (var filter in filters)
                    {
                        var filterId = filter["Id"]?.ToString();
                        if (!string.IsNullOrEmpty(filterId) && !filterMap.ContainsKey(filterId))
                        {
                            filterMap[filterId] = filter;
                        }
                    }
                }
            }

            // Display handlers
            for (int i = 0; i < handlers.Count; i++)
            {
                var handler = handlers[i];
                var handlerId = handler["id"]?.ToString() ?? "N/A";
                var handlerName = handler["name"]?.ToString() ?? "Unknown";
                var filterId = handler["incidentFilterId"]?.ToString() ?? "N/A";
                var createdAt = handler["createdAt"]?.ToString() ?? "N/A";
                var updatedAt = handler["updatedAt"]?.ToString() ?? "N/A";

                Console.WriteLine($"[{i + 1}] {handlerName}");
                ConsoleUI.WriteKeyValue("ID", handlerId, 4);
                ConsoleUI.WriteKeyValue("Filter ID", filterId, 4);

                if (verbose && filterMap != null && filterMap.TryGetValue(filterId, out var filter))
                {
                    var filterName = filter["Name"]?.ToString() ?? "Unknown";
                    var handlingAgent = filter["HandlingAgent"]?.ToString();

                    ConsoleUI.WriteKeyValue("Filter Name", filterName, 4);
                    if (!string.IsNullOrEmpty(handlingAgent))
                    {
                        ConsoleUI.WriteKeyValue("Handling Agent", handlingAgent, 4);
                    }
                }

                ConsoleUI.WriteKeyValue("Created", createdAt, 4);
                ConsoleUI.WriteKeyValue("Updated", updatedAt, 4);

                // Show additional properties if available
                if (verbose)
                {
                    var description = handler["Description"]?.ToString();
                    if (!string.IsNullOrEmpty(description))
                    {
                        ConsoleUI.WriteKeyValue("Description", description, 4);
                    }

                    var enabled = handler["Enabled"]?.ToString();
                    if (!string.IsNullOrEmpty(enabled))
                    {
                        ConsoleUI.WriteKeyValue("Enabled", enabled, 4);
                    }
                }

                if (i < handlers.Count - 1)
                {
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.WriteStatus(false, $"Error listing incident handlers: {ex.Message}");
            Environment.Exit(1);
        }
    }
}
