using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Agent.Runtime.Services
{
    public interface IInstructionGenerationService
    {
        Task<InstructionGenerationResponse> GenerateInstructionsFromIncidents(InstructionGenerationRequest request);

        Task<List<ToolInfo>> FilterTools(string searchString);

    }

    public class InstructionGenerationService : IInstructionGenerationService
    {
        private readonly IChatClient _chatClient;
        private readonly IToolFactory<AgentContext> _toolFactory;
        private readonly IncidentManagementService<PagerDutyIncidentDocument> _incidentManagementService;
        private readonly ILogger<InstructionGenerationService> _logger;

        public InstructionGenerationService(
            IToolFactory<AgentContext> toolFactory,
            IChatClient chatClient,
            IncidentManagementService<PagerDutyIncidentDocument> incidentManagementService,
            ILogger<InstructionGenerationService> logger
            )
        {
            _toolFactory = toolFactory;
            _chatClient = chatClient;
            _incidentManagementService = incidentManagementService;
            _logger = logger;
        }

        public async Task<List<ToolInfo>> FilterTools(string searchString)
        {
            var availableTools = _toolFactory.FetchAvailableToolInfo();
            if (string.IsNullOrWhiteSpace(searchString))
            {
                return availableTools;
            }
            return availableTools
                .Where(tool => tool.Name.Contains(searchString, StringComparison.OrdinalIgnoreCase) ||
                               (tool.Description != null && tool.Description.Contains(searchString, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        public async Task<InstructionGenerationResponse> GenerateInstructionsFromIncidents(InstructionGenerationRequest request)
        {
            if (request.Incidents == null || !request.Incidents.Any())
            {
                //_logger.LogInformation($"No incidents provided for instruction generation. Will leverage custom instructions.");
            }

            // Core instruction generation logic
            var incidentSummariesList = new List<string>();
            foreach (var incident in request.Incidents)
            {
                var summary = await ExtractKnowledgeFromIncident(incident);
                incidentSummariesList.Add(summary);
            }

            // Combine summaries and custom instructions to generate final instructions
            var customInstructions = string.IsNullOrEmpty(request.CustomInstructions) ? "No custom instructions provided." : $"Here are the CUSTOM_INSTRUCTIONS provided by **human engineers** which should be given preference and importance in coming up with the final set of instructions: {request.CustomInstructions}";
            var incidentSummaries = incidentSummariesList.Count == 0 ? "No incidents summaries available." : string.Join("\n\n", incidentSummariesList);
            var availableToolsPrompt = GetAvailableToolsPrompt(request);
            try
            {
                // Generate final instructions
                var systemMessage = GetInstructionGenerationPrompt(incidentSummaries, customInstructions, availableToolsPrompt, request.ExistingInstructions);

                var instructionGenerationResponse = await _chatClient.GetResponseAsync(
                    new ChatMessage(ChatRole.System, systemMessage),
                    new ChatOptions
                    {
                        Temperature = 0.1f
                    }
                );

                var generatedInstructions = instructionGenerationResponse.Messages.LastOrDefault()?.Text;

                return new InstructionGenerationResponse
                {
                    AgentName = request.AgentName,
                    GeneratedInstructions = generatedInstructions,
                    Incidents = request.Incidents,
                    Tools = request.Tools
                };
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private async Task<string> ExtractKnowledgeFromIncident(string incident)
        {
            var incidentDetails = await _incidentManagementService.GetIncidentDetails(incident);
            if (incidentDetails == null)
            {
                //TODO: Log event
                return $"Incident with ID {incident} not found.";
            }

            if (!string.IsNullOrWhiteSpace(incidentDetails.ExtractedKnowledge))
            {
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

            var response = await _chatClient.GetResponseAsync(
                new ChatMessage(ChatRole.System, systemMessage),
                new ChatOptions
                {
                    Temperature = 0.7f
                }
            );

            //TODO: Log event and check for errors

            var summary = response.Messages.LastOrDefault()?.Text;

            if (!string.IsNullOrWhiteSpace(summary))
            {
                incidentDetails.ExtractedKnowledge = summary;
                // Save document back to cosmos using the incidentManagementService
                await _incidentManagementService.SaveDocument(incidentDetails);
            }
            return summary;
        }

        private string GetAvailableToolsPrompt(InstructionGenerationRequest request)
        {
            var availableTools = _toolFactory.FetchAvailableToolInfo();
            if (request.Tools != null && request.Tools.Any())
            {
                availableTools = availableTools.Where(tool => request.Tools.Contains(tool.Name, StringComparer.OrdinalIgnoreCase)).ToList();
            }

            string availableToolsPrompt = availableTools != null && availableTools.Count > 0 ? JsonConvert.SerializeObject(availableTools, Formatting.Indented) : "No Tools Available.";
            return availableToolsPrompt;
        }

        private string GetInstructionGenerationPrompt(string incidentSummariesPrompt, string customInstructionsPrompt, string availableToolsPrompt, string existingInstructions = null)
        {
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

    - Identify the 'Error' category from the 'Secondary Queries and results' section of the azure alerting discussion entry.,
    - If the Error is not 'Keyset does not exist', then STOP and DO NOT PROCEED:,
    - If the Error is 'Keyset does not exist' then proceed with following guidelines:,
      - Post discussion Entry for with a table with three columns:,
        - Error,
        - Requests,
        - Preliminary Mitigation,
      - Then go ahead and fetch web app details for the web app 'diagacisdataprovider',
      - Then restart the web app. Proceed to ALERT_SIGNAL_MONITORING steps.,
    - ALERT_SIGNAL_MONITORING instructions:,
      - Wait for 10 minutes.,
      - Set MONITORING_ITERATION = 0,
      - Repeat running run_alert_kusto_query 10 times with a gap of 10 minutes or until NUM_ROWS_RETURNED == 0. In each iteration:,
        - Ensure that you've waited for 10 minutes after the last iteration.,
        - run_alert_kusto_query and check if it returns any rows.,
        - Post discussion Entry into ICM Incident with the following list details regarding the ALERT_SIGNAL: MONITORING_ITERATION, NUM_ROWS_RETURNED,
        - If NUM_ROWS_RETURNED > 0, increase MONITORING_ITERATION by 1 and add wait_timer for 10 minutes.,
        - If NUM_ROWS_RETURNED == 0, this confirms that the ALERT_SIGNAL has been cleared. EXIT THE LOOP.,

    - Post a discussion Entry with a summary of stage-by-stage details:,
        - Incident Details,
        - Impacted Endpoint Details,
        - Transient/Non-Transient check,
        - Mitigation Actions,
        - Confirmation of Recovery,
        - Iteration-by-Iteration results of the ALERT_SIGNAL_MONITORING in a table format.

### END OF EXECUTION_PLAN ###

You will be coming up with the required EXECUTION_PLAN based on the following information:

Here is the INCIDENT_SUMMARIES that you will use to learn from to generate the EXECUTION_PLAN:
{incidentSummariesPrompt}

Here are the CUSTOM_INSTRUCTIONS provided by human engineers, which should be given preference and importance in coming up with the final set of instructions:
{customInstructionsPrompt}


# Here is a LIST_OF_AVAILABLE_TOOLS (capabilities) that the LLM-based Agent could use to fetch information or carry out actions. The EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools.

Available Tools that the Agent can use: {availableToolsPrompt}

# You must follow the below GENERATION_GUIDELINES while generating the EXECUTION_PLAN:
- Use the detailed steps and incident summaries to create a structured EXECUTION_PLAN
- When including data/log query execution, include full semantically correct log queries in the instructions together with database/cluster names.
- **Ensure that the EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools**
- MITIGATION_CONFIRMATION instructions are needed only for issues where mitigation is required and applied.
- DON'T include the 'Step X:' prefix in the instruction you generate
- **If an EXISTING_EXECUTION_PLAN is provided, then extract new information from provided INCIDENT_SUMMARIES and CUSTOM_INSTRUCTIONS to improve the EXECUTION_PLAN.**

EXISTING_EXECUTION_PLAN
{existingInstructions ?? "No existing execution plan provided."}
";
            return systemPrompt;
        }
    }
}

public class InstructionGenerationRequest
{
    public string AgentName { get; set; }
    /// <summary>
    /// Custom Instructions provided by the user
    /// </summary>
    public string CustomInstructions { get; set; }

    /// <summary>
    /// List of incidents to generate learnings from
    /// </summary>
    public List<string> Incidents { get; set; }

    /// <summary>
    /// List of tools that the agent should be limited to use
    /// </summary>
    public List<string> Tools { get; set; }

    /// <summary>
    /// Existing instructions to improve upon, if any
    /// </summary>
    public string ExistingInstructions { get; set; } = string.Empty;
}


public class InstructionGenerationResponse
{
    public string AgentName { get; set; }
    /// <summary>
    /// Generated instructions based on the incidents
    /// </summary>
    public string GeneratedInstructions { get; set; }
    /// <summary>
    /// List of incidents used for generating the instructions
    /// </summary>
    public List<string> Incidents { get; set; }

    public List<string> Tools { get; set; }
}

