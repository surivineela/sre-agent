// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DataModels;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Runtime.Services
{
    public class GenerateInstructionsResult
    {
        public required string ExecutionPlan { get; set; }
        public List<string> ToolsUsed { get; set; } = new List<string>();
    }

    public interface IInstructionGenerationService
    {
        Task<InstructionGenerationResponse> GenerateInstructionsFromIncidents(InstructionGenerationRequest request);

        Task<List<ToolInfo>> FilterTools(string? searchString);

        Task<List<ToolInfo>> GetIncidentHandlerTools(string platform);

    }

    public class InstructionGenerationService : IInstructionGenerationService
    {
        private readonly IChatClientProvider _chatClientProvider;
        private readonly IToolFactory<AgentContext> _toolFactory;
        private readonly IIncidentManagementServiceFactory _incidentManagementServiceFactory;
        private readonly ILogger<InstructionGenerationService> _logger;
        private readonly IncidentManagementSettings _incidentManagementSettings;
        private readonly IPublishedToolsService _publishedToolsService;

        public InstructionGenerationService(
            IToolFactory<AgentContext> toolFactory,
            IChatClientProvider chatClientProvider,
            IIncidentManagementServiceFactory incidentManagementServiceFactory,
            ILogger<InstructionGenerationService> logger,
            IncidentManagementSettings incidentManagementSettings,
            IPublishedToolsService publishedToolsService
            )
        {
            _toolFactory = toolFactory;
            _chatClientProvider = chatClientProvider;
            _incidentManagementServiceFactory = incidentManagementServiceFactory;
            _logger = logger;
            _incidentManagementSettings = incidentManagementSettings;
            _publishedToolsService = publishedToolsService;
        }

        public async Task<List<ToolInfo>> FilterTools(string? searchString)
        {
            _logger.LogInternalInformation("FilterTools: Invoked with searchString: {SearchString}", searchString);

            var availableTools = _toolFactory.FetchAvailableToolInfo();
            _logger.LogInternalInformation("FilterTools: Fetched {ToolCount} available tools.", availableTools.Count);

            availableTools = await ApplyToolFiltering(availableTools);

            if (string.IsNullOrWhiteSpace(searchString))
            {
                _logger.LogInternalInformation("FilterTools: No search string provided. Returning filtered tools.");
                return availableTools;
            }

            var filteredTools = availableTools
                .Where(tool => tool.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                               (tool.Description != null && tool.Description.Contains(searchString, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            _logger.LogInternalInformation("FilterTools: Filtered tools count: {FilteredCount} for searchString: {SearchString}", filteredTools.Count, searchString);

            return filteredTools;
        }

        public Task<List<ToolInfo>> GetIncidentHandlerTools(string platform)
        {
            _logger.LogInternalInformation("GetIncidentHandlerTools: Invoked for platform: {Platform}", platform);

            var availableTools = _toolFactory.FetchAvailableToolInfo();
            var incidentHandlerTools = availableTools
                .Where(tool => tool.IsIncidentHandlerTool &&
                              tool.IncidentHandlerPlatform?.Equals(platform, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            _logger.LogInternalInformation("GetIncidentHandlerTools: Found {ToolCount} incident handler tools for platform: {Platform}", incidentHandlerTools.Count, platform);

            return Task.FromResult(incidentHandlerTools);
        }

        private async Task<List<ToolInfo>> ApplyToolFiltering(List<ToolInfo> availableTools)
        {
            var configuredPlatform = _incidentManagementSettings?.Type.ToString() ?? string.Empty;

            // Filter by platform: keep incident handler tools only for the configured platform
            availableTools = FilterToolsByPlatform(availableTools, configuredPlatform);
            _logger.LogInternalInformation("ApplyToolFiltering: Filtered tools for platform {Platform}. Remaining: {FilteredCount}",
                configuredPlatform, availableTools.Count);

            // Filter by published tools list (since LLM cannot handle all tools)
            availableTools = await FilterToolsByPublishedList(availableTools, configuredPlatform);

            return availableTools;
        }

        private List<ToolInfo> FilterToolsByPlatform(List<ToolInfo> tools, string configuredPlatform)
        {
            // Filter out incident handler tools from general tool selection, except for the configured platform
            return tools.Where(tool =>
                !tool.IsIncidentHandlerTool ||
                (tool.IsIncidentHandlerTool &&
                 tool.IncidentHandlerPlatform?.Equals(configuredPlatform, StringComparison.OrdinalIgnoreCase) == true))
                .ToList();
        }

        private async Task<List<ToolInfo>> FilterToolsByPublishedList(List<ToolInfo> availableTools, string configuredPlatform)
        {
            var publishedToolNames = await _publishedToolsService.GetPublishedToolNamesAsync();

            if (publishedToolNames.Count > 0)
            {
                // Create a combined list: published tools + incident handler tools for the configured platform
                var allowedTools = availableTools.Where(tool =>
                    publishedToolNames.Contains(tool.Name) ||
                    (tool.IsIncidentHandlerTool &&
                     tool.IncidentHandlerPlatform?.Equals(configuredPlatform, StringComparison.OrdinalIgnoreCase) == true))
                    .ToList();

                var incidentHandlerToolsCount = allowedTools.Count(t => t.IsIncidentHandlerTool);
                var publishedToolsCount = allowedTools.Count - incidentHandlerToolsCount;

                _logger.LogInternalInformation("FilterToolsByPublishedList: Filtered by published tools + incident handler tools. " +
                    "Published: {PublishedCount}, Incident Handler: {IncidentHandlerCount}, Total: {TotalCount}",
                    publishedToolsCount, incidentHandlerToolsCount, allowedTools.Count);

                return allowedTools;
            }
            else
            {
                _logger.LogInternalWarning("FilterToolsByPublishedList: No published tools configured. Returning all available tools.");
                return availableTools;
            }
        }

        public async Task<InstructionGenerationResponse> GenerateInstructionsFromIncidents(InstructionGenerationRequest request)
        {
            _logger.LogInternalInformation($"GenerateInstructionsFromIncidents: Invoked for AgentName: {request.AgentName}, Incidents: {JsonConvert.SerializeObject(request.Incidents)}, Tools: {JsonConvert.SerializeObject(request.Tools)}, CustomInstructions: {request.CustomInstructions}, ExistingInstructions: {request.ExistingInstructions}",
                request.AgentName,
                request.Incidents?.Count ?? 0,
                request.Tools?.Count ?? 0,
                !string.IsNullOrEmpty(request.CustomInstructions),
                !string.IsNullOrEmpty(request.ExistingInstructions));

            var incidentSummariesList = new List<string>();
            if (request.Incidents != null && request.Incidents.Count > 0)
            {
                try
                {
                    foreach (var incident in request.Incidents)
                    {
                        _logger.LogInternalInformation("GenerateInstructionsFromIncidents: Extracting knowledge from incident: {IncidentId}", incident);
                        var summary = await ExtractKnowledgeFromIncident(incident);
                        incidentSummariesList.Add(summary);
                        _logger.LogInternalInformation("GenerateInstructionsFromIncidents: Extracted summary for incident {IncidentId}: {Summary}", incident, summary);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "GenerateInstructionsFromIncidents: Error extracting knowledge from incidents. AgentName: {AgentName}, Incidents: {Incidents}", request.AgentName, JsonConvert.SerializeObject(request.Incidents));
                    throw;
                }
            }
            else
            {
                _logger.LogInternalWarning("GenerateInstructionsFromIncidents: No incidents provided for instruction generation. AgentName: {AgentName}", request.AgentName);
            }

            var customInstructions = string.IsNullOrEmpty(request.CustomInstructions)
                ? "No custom instructions provided."
                : $"Here are the CUSTOM_INSTRUCTIONS provided by **human engineers** which should be given preference and importance in coming up with the final set of instructions: {request.CustomInstructions}";

            var incidentSummaries = incidentSummariesList.Count == 0
                ? "No incidents summaries available."
                : string.Join("\n\n", incidentSummariesList);

            var availableToolsPrompt = GetAvailableToolsPrompt(request);

            try
            {
                var systemMessage = GetInstructionGenerationPrompt(incidentSummaries, customInstructions, availableToolsPrompt, request.ExistingInstructions);

                _logger.LogInternalInformation("GenerateInstructionsFromIncidents: Sending system prompt to chat client for instruction generation. AgentName: {AgentName}", request.AgentName);

                var instructionGenerationResponse = await _chatClientProvider.DefaultModel.GetResponseAsync(
                    new ChatMessage(ChatRole.System, systemMessage),
                    new ChatOptions
                    {
                        Temperature = 0.1f
                    }
                );

                var generatedInstructions = instructionGenerationResponse.Messages.LastOrDefault()?.Text ?? string.Empty;
                _logger.LogInternalInformation("GenerateInstructionsFromIncidents: Received generated instructions from chat client. AgentName: {AgentName}, Instructions: {Instructions}", request.AgentName, generatedInstructions);

                GenerateInstructionsResult? generateInstructionsResult = null;
                try
                {
                    generateInstructionsResult = JsonConvert.DeserializeObject<GenerateInstructionsResult>(generatedInstructions);
                    _logger.LogInternalInformation("GenerateInstructionsFromIncidents: Deserialized generated instructions successfully. AgentName: {AgentName}", request.AgentName);
                }
                catch (JsonException jsonEx)
                {
                    _logger.LogInternalError(jsonEx, "GenerateInstructionsFromIncidents: Failed to deserialize generated instructions response. AgentName: {AgentName}, Response: {Response}", request.AgentName, generatedInstructions);
                    throw new InvalidOperationException("Failed to deserialize generated instructions.", jsonEx);
                }

                if (generateInstructionsResult != null)
                {
                    var response = new InstructionGenerationResponse
                    {
                        AgentName = request.AgentName,
                        GeneratedInstructions = generateInstructionsResult.ExecutionPlan,
                        Incidents = request.Incidents ?? [],
                        Tools = generateInstructionsResult?.ToolsUsed != null && generateInstructionsResult.ToolsUsed.Count > 0 ? generateInstructionsResult.ToolsUsed : request.Tools
                    };

                    _logger.LogInternalInformation("GenerateInstructionsFromIncidents: Instruction generation completed successfully. AgentName: {AgentName}, ToolsUsed: {ToolsUsed}", request.AgentName, JsonConvert.SerializeObject(response.Tools));

                    return response;
                }
                throw new InvalidOperationException("Generated instructions are null or empty.");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error generating instructions from incidents. AgentName: {AgentName}, Incidents: {Incidents}, Tools: {Tools}", request.AgentName, JsonConvert.SerializeObject(request.Incidents), JsonConvert.SerializeObject(request.Tools));
                throw;
            }
        }

        private async Task<string> ExtractKnowledgeFromIncident(string incident)
        {
            _logger.LogInternalInformation("ExtractKnowledgeFromIncident: Invoked for IncidentId: {IncidentId}", incident);

            PagerDutyIncidentDocument? incidentDetails = null;
            try
            {
                var pagerDutyService = _incidentManagementServiceFactory.GetService<PagerDutyIncidentDocument, PagerDutyIncidentFilterDocumentPayload>();
                if (pagerDutyService is not null)
                {
                    incidentDetails = await pagerDutyService.GetIncidentDetails(incident);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ExtractKnowledgeFromIncident: Error fetching incident details for IncidentId: {IncidentId}", incident);
                throw;
            }

            if (incidentDetails == null)
            {
                _logger.LogInternalWarning("ExtractKnowledgeFromIncident: Incident with ID {IncidentId} not found.", incident);
                return $"Incident with ID {incident} not found.";
            }

            if (!string.IsNullOrWhiteSpace(incidentDetails.ExtractedKnowledge))
            {
                _logger.LogInternalInformation("ExtractKnowledgeFromIncident: Incident {IncidentId} already has extracted knowledge.", incident);
                return incidentDetails.ExtractedKnowledge;
            }

            var processedNotes = incidentDetails.Notes.Select(note => $"[{note.CreatedAt}][{note.CreatedBy}] {note.Content}").ToList();
            var incidentDetailsPrompt = $@"
                Incident Details:
                **Incident Title:** {incidentDetails.Title ?? "N/A"}
                **Incident Description:** {incidentDetails.Description ?? "N/A"}
                **Incident Notes:**

                {(processedNotes != null && processedNotes.Count > 0 ? string.Join("\n\n", processedNotes) : "N/A")}
            ";

            var systemMessage = $"You are an AI assistant that summarizes incidents based on the provided details. Your task is to create a concise summary as well as a workflow of how the incident was handled. You will also include what are the learnings that will apply to any similar incidents in the future. Ensure you include all details of any HTTP requests made, queries executed, documents referenced, commands that were used. Your output should be a JSON format with 7 attributes 'Title', 'Summary', 'IncidentHandlingWorkflow', 'Commands', 'QueriesExecuted', 'DocumentsReferenced', 'AdditionalDetails'. Here are the details of the incident in question:\n\n{incidentDetailsPrompt}";

            try
            {
                _logger.LogInternalInformation("ExtractKnowledgeFromIncident: Sending incident details to chat client for summarization. IncidentId: {IncidentId}", incident);

                var response = await _chatClientProvider.DefaultModel.GetResponseAsync(
                    new ChatMessage(ChatRole.System, systemMessage),
                    new ChatOptions
                    {
                        Temperature = 0.7f
                    }
                );

                var summary = response.Messages.LastOrDefault()?.Text;

                if (!string.IsNullOrWhiteSpace(summary))
                {
                    incidentDetails.ExtractedKnowledge = summary;
                    try
                    {
                        await _incidentManagementServiceFactory.SaveDocument(System.Text.Json.JsonSerializer.SerializeToNode(incidentDetails));
                        _logger.LogInternalInformation("ExtractKnowledgeFromIncident: Saved extracted knowledge for IncidentId: {IncidentId}", incident);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "ExtractKnowledgeFromIncident: Failed to save extracted knowledge for IncidentId: {IncidentId}", incident);
                    }
                }
                else
                {
                    _logger.LogInternalWarning("ExtractKnowledgeFromIncident: No summary generated for IncidentId: {IncidentId}", incident);
                }

                return summary ?? "No summary generated.";
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "ExtractKnowledgeFromIncident: Error summarizing incident details for IncidentId: {IncidentId}", incident);
                throw;
            }
        }

        private string GetAvailableToolsPrompt(InstructionGenerationRequest request)
        {
            _logger.LogInternalInformation("GetAvailableToolsPrompt: Invoked for AgentName: {AgentName}", request.AgentName);

            var availableTools = _toolFactory.FetchAvailableToolInfo();
            _logger.LogInternalInformation("GetAvailableToolsPrompt: Fetched {ToolCount} available tools.", availableTools.Count);

            // Default filtering for incident handlers
            // Get the configured platform name
            var configuredPlatform = _incidentManagementSettings?.Type.ToString() ?? string.Empty;

            // Filter out incident handler tools from general tool selection, except for the configured platform
            availableTools = [.. availableTools.Where(tool =>
                    !tool.IsIncidentHandlerTool ||
                    (tool.IsIncidentHandlerTool &&
                     tool.IncidentHandlerPlatform?.Equals(configuredPlatform, StringComparison.OrdinalIgnoreCase) == true))];

            _logger.LogInternalInformation("GetAvailableToolsPrompt: Filtered tools for platform {Platform}. Remaining: {FilteredCount}",
                configuredPlatform, availableTools.Count);

            if (request.Tools != null && request.Tools.Count != 0)
            {
                availableTools = [.. availableTools.Where(tool => request.Tools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))];
                _logger.LogInternalInformation("GetAvailableToolsPrompt: Filtered available tools to {Count} based on request.Tools for AgentName: {AgentName}", availableTools.Count, request.AgentName);
            }

            string availableToolsPrompt = availableTools != null && availableTools.Count > 0 ? JsonConvert.SerializeObject(availableTools, Formatting.Indented) : "No Tools Available.";
            return availableToolsPrompt;
        }

        private string GetInstructionGenerationPrompt(string incidentSummariesPrompt, string customInstructionsPrompt, string availableToolsPrompt, string? existingInstructions = null)
        {
            _logger.LogInternalInformation($"GetInstructionGenerationPrompt: Invoked. incidentSummariesPrompt: {incidentSummariesPrompt}, customInstructionsPrompt: {customInstructionsPrompt}, availableToolsPrompt: {availableToolsPrompt}, existingInstructions: {existingInstructions}");

            string exampleOutput = @"{""executionPlan"": ""<the complete execution plan>"", ""toolsUsed"": [""GetContainerLogs"", ""RunAzCliReadCommand""]}";
            string systemPrompt = $@"You are an AI assistant that generates an EXECUTION_PLAN for an LLM-based Agent in the form of list of instructions based on provided template and requirements. You will use knowledge from INCIDENT_SUMMARIES of past incidents that have been handled, and CUSTOM_INSTRUCTIONS from human engineers.

    Here is the template of an EXECUTION_PLAN prompt for a scenario that requires mitigation:

    ### EXECUTION_PLAN ###

    ___guidelines ____,
    - MITIGATION_CONFIRMATION instructions:,
    ____instructions____,

    ### END OF EXECUTION_PLAN ###

    Here is the template of an EXECUTION_PLAN prompt for a scenario that does not require mitigation:

    ### EXECUTION_PLAN ###

    ___guidelines ____,

    ### END OF EXECUTION_PLAN ###

    ------------------------------

    # Here is an example of an EXECUTION_PLAN:

    ### EXECUTION_PLAN ###

        - Identify the 'Error' category from the 'Secondary Queries and results' section of the azure alerting discussion entry.
        - If the Error is not 'Keyset does not exist', then STOP and DO NOT PROCEED:
        - If the Error is 'Keyset does not exist' then proceed with following guidelines:
          - Post discussion Entry for with a table with three columns:
            - Error
            - Requests
            - Preliminary Mitigation
          - Then go ahead and fetch web app details for the web app 'diagacisdataprovider'
          - Then restart the web app. Proceed to ALERT_SIGNAL_MONITORING steps.
        - ALERT_SIGNAL_MONITORING instructions:
          - Wait for 10 minutes.
          - Set MONITORING_ITERATION = 0
          - Repeat running run_alert_kusto_query 10 times with a gap of 10 minutes or until NUM_ROWS_RETURNED == 0. In each iteration:
            - Ensure that you've waited for 10 minutes after the last iteration.
            - run_alert_kusto_query and check if it returns any rows.
            - Post discussion Entry into ICM Incident with the following list details regarding the ALERT_SIGNAL: MONITORING_ITERATION, NUM_ROWS_RETURNED
            - If NUM_ROWS_RETURNED > 0, increase MONITORING_ITERATION by 1 and add wait_timer for 10 minutes.
            - If NUM_ROWS_RETURNED == 0, this confirms that the ALERT_SIGNAL has been cleared. EXIT THE LOOP.
        - Once mitigation is confirmed and monitoring is finished, mitigate the incident with a summary of the incident and actions.

        - Post a discussion Entry/incident note with a summary of stage-by-stage details:
            - Incident Details
            - Impacted Endpoint Details
            - Transient/Non-Transient check
            - Mitigation Actions
            - Confirmation of Recovery
            - Iteration-by-Iteration results of the ALERT_SIGNAL_MONITORING in a table format.

    ### END OF EXECUTION_PLAN ###

    You will be coming up with the required EXECUTION_PLAN based on the following information:

    Here is the INCIDENT_SUMMARIES that you will use to learn from to generate the EXECUTION_PLAN:
    {incidentSummariesPrompt}

    Here are the CUSTOM_INSTRUCTIONS provided by human engineers, which should be given preference and importance in coming up with the final set of instructions:
    {customInstructionsPrompt}


    # Here is a LIST_OF_AVAILABLE_TOOLS (capabilities) that the LLM-based Agent could use to fetch information or carry out actions. The EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools.

    LIST_OF_AVAILABLE_TOOLS that the Agent can use: {availableToolsPrompt}

    # You must follow the below GENERATION_GUIDELINES while generating the EXECUTION_PLAN:
    - Use the detailed steps and incident summaries to create a structured EXECUTION_PLAN
    - When including data/log query execution, include full semantically correct log queries in the instructions together with database/cluster names.
    - **Ensure that the EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools**
    - Ensure that the EXECUTION_PLAN has specific instructions to post discussion entries/incident notes after each major step.
    - MITIGATION_CONFIRMATION instructions are needed only for issues where mitigation is required and applied.
    - The EXECUTION_PLAN must have a conclusion in the form of mitigating or resolving the incident
    - DON'T include the 'Step X:' prefix in the instruction you generate
    - **If an EXISTING_EXECUTION_PLAN is provided, then extract new information from provided INCIDENT_SUMMARIES and CUSTOM_INSTRUCTIONS to improve the EXECUTION_PLAN.**

    EXISTING_EXECUTION_PLAN
    {existingInstructions ?? "No existing execution plan provided."}

    **Your output will be a JSON object with two attributes ""executionPlan"" (the generated execution plan for the LLM) and ""toolsUsed"" (a definitive list of the tools mentioned in the execution plan and any other helper tools to interact with the incident e.g. acknowledge, resolve, post discussion entry/notes)**

    Example output:
    {exampleOutput}
    ";
            return systemPrompt;
        }
    }
}

public class InstructionGenerationRequest
{
    public required string AgentName { get; set; }
    /// <summary>
    /// Custom Instructions provided by the user
    /// </summary>
    public string? CustomInstructions { get; set; }

    /// <summary>
    /// List of incidents to generate learnings from
    /// </summary>
    public List<string>? Incidents { get; set; }

    /// <summary>
    /// List of tools that the agent should be limited to use
    /// </summary>
    public List<string>? Tools { get; set; }

    /// <summary>
    /// Existing instructions to improve upon, if any
    /// </summary>
    public string? ExistingInstructions { get; set; }
}


public class InstructionGenerationResponse
{
    public required string AgentName { get; set; }
    /// <summary>
    /// Generated instructions based on the incidents
    /// </summary>
    public required string GeneratedInstructions { get; set; }
    /// <summary>
    /// List of incidents used for generating the instructions
    /// </summary>
    public required List<string> Incidents { get; set; }

    public List<string>? Tools { get; set; }
}

