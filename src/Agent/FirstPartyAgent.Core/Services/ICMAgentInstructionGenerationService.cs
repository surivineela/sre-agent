using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kusto.Data.Common;
using Kusto.Data.Net.Client;
using Kusto.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Core.Extensions;
using Agent.Core.Configuration;
using FirstPartyAgent.Models;
using Newtonsoft.Json;
using FirstPartyAgent.Core.Plugins;
using Microsoft.Extensions.Options;

namespace FirstPartyAgent.Core.Services;
public class ICMAgentInstructionGenerationService
{
    private IConfiguration _configuration;
    private ILogger<ICMAgentInstructionGenerationService> _logger;
    private readonly Kernel _kernel;
    private readonly IICMWorkflowClient _icmWorkflowClient;
    private readonly AzureSettings _azureSettings;
    private readonly IKernelService _kernelService;
    private readonly GenevaActionsPlugin _genevaActionsPlugin;

    public class GenerateInstructionsRequest
    {
        public int[] IncidentIds { get; set; } = Array.Empty<int>();
        public string? CustomInstructions { get; set; }
    }


    public ICMAgentInstructionGenerationService(
        IConfiguration configuration,
        ILogger<ICMAgentInstructionGenerationService> logger,
        Kernel kernel,
        IKernelService kernelService,
        GenevaActionsPlugin genevaActionsPlugin,
        IICMWorkflowClient icmWorkflowClient,
        IOptions<AzureSettings> azureSettings)
    {
        _kernelService = kernelService;
        _genevaActionsPlugin = genevaActionsPlugin;
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration), "Configuration cannot be null");
        _logger = logger;
        _kernel = kernel;
        _icmWorkflowClient = icmWorkflowClient;
        //_kustoMsiClientId = _configuration.GetValue<string>("AppSettings:Core:External") ?? throw new ArgumentNullException("KustoMsiClientId", "KustoMsiClientId cannot be null");
        _azureSettings = azureSettings?.Value ?? throw new ArgumentNullException(nameof(azureSettings), "AzureSettings cannot be null");
    }

    public class InstructionResult
    {
        public string[] Instructions { get; set; } = Array.Empty<string>();
        public string TroubleshootingGuide { get; set; } = string.Empty;
    }

    public async Task<InstructionResult> GenerateInstructions(GenerateInstructionsRequest request)
    {
        var tasks = request.IncidentIds.Select(GetIncidentSummaryAsync);
        var icmSummaryList = (await Task.WhenAll(tasks)).Where(x => x != null).ToList();

        if(request.IncidentIds.Any() && icmSummaryList.Count == 0)
        {
            throw new Exception("Failed to fetch incident summaries for the provided incident IDs.");
        }

        // Generate troubleshooting guide based on incident summaries and custom instructions
        string troubleshootingGuide = await GetTroubleShootingGuide(icmSummaryList, request.CustomInstructions);
        _logger.LogInformation($"Generated troubleshooting guide: {troubleshootingGuide}");

        // Use only troubleshootingGuide for generating instructions
        string prompt = await GetInstructionPrompt(troubleshootingGuide, agentMode: "TestModeAgent");
        try
        {
            var result = await _kernel.RunAsync(prompt);
            var resultLines = result.Split("\n");
            var instructionResult = new InstructionResult
            {
                Instructions = resultLines,
                TroubleshootingGuide = troubleshootingGuide
            };

            return instructionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception occurred in running Instruction generation: {ex.Message}");
            throw;
        }
    }

    private async Task<string> GetTroubleShootingGuide(List<string> icmSummaryList, string? customInstructions = null)
    {
        try
        {
            string summariesContent = string.Join("\n\n", icmSummaryList);
            string customInstructionsSection = string.IsNullOrWhiteSpace(customInstructions) ? 
                string.Empty : 
                $@"
# Existing Troubleshooting Guide or Guidance
{customInstructions}
";

            string prompt = $@"# Instructions
You are a Troubleshooting Guide Generator that creates comprehensive, step-by-step guides based on incident summaries.

Your task is to analyze the provided incident summaries and create a detailed troubleshooting guide that includes:

1. A systematic approach to diagnosing similar incidents
2. Detailed steps for resolving each type of issue identified
3. Complete Kusto queries that can be executed to:
   - Identify the affected resources
   - Monitor the status of resources
   - Verify that mitigation actions are working
   - Confirm recovery of impacted resources
4. Specific mitigation actions with exact commands or operations to execute
5. Common patterns and relationships between symptoms and solutions
6. Expected outcomes for each troubleshooting step
7. Verification procedures to confirm issue resolution

Format the guide with clear sections, including:
- Initial Diagnosis
- Verification Steps
- Mitigation Actions
- Recovery Confirmation
- Post-Resolution Monitoring

IMPORTANT: If you cannot generate a comprehensive guide based on the provided incident summaries for any of the following reasons:
- Insufficient details in the provided incidents
- Incidents used significantly different approaches with no common practice
- Incidents involve unrelated systems or problems with no meaningful patterns
- The data is too limited to establish reliable troubleshooting steps
- Any other reason that prevents creating a meaningful common guide

Then instead of generating a partial or inaccurate guide, provide a clear explanation of:
1. Why a comprehensive troubleshooting guide cannot be generated
2. Which specific aspects are missing or inconsistent across incidents
3. What additional information would be needed to create a proper guide
4. Recommendations for collecting better incident data in the future{customInstructionsSection}

# Incident Summaries
{summariesContent}

Generate a comprehensive troubleshooting guide that engineers can follow to efficiently resolve similar incidents, or explain why such a guide cannot be created based on the provided information. If an existing troubleshooting guide is provided above, use it as a reference and enhance it with any additional insights from the incident summaries.";

            var result = await _kernel.RunAsync(prompt);
            _logger.LogInformation("Successfully generated troubleshooting guide or explanation");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while generating troubleshooting guide");
            return "Failed to generate troubleshooting guide due to error.";
        }
    }

    private async Task<string> GetIncidentSummaryAsync(int incidentId)
    {
        try
        {
            var incidentDetails = await _icmWorkflowClient.GetIncidentAsync(incidentId.ToString());
            var incidentDiscussions = await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId.ToString());
            var incidentCustomFields = await _icmWorkflowClient.GetCustomFieldsAsync(incidentId.ToString());

            incidentDiscussions = incidentDiscussions.OrderBy(d => d.Date).ToList();
            if (!string.IsNullOrWhiteSpace(incidentDetails.Summary))
            {
                incidentDetails.Summary = string.Join("\n", (await TextProcessingHelpers.ProcessComplexICMContent(incidentDetails.Summary, _kernel, _logger, skipImages: true)).Split("\n").Select(s => s.Trim()).Where(s => s.Length > 0));
            }
            incidentDetails.DiscussionEntry = "";

            string incidentMarkdown = $"## Title: {incidentDetails.Title}\r\n\r\n";
            incidentMarkdown += $"### IncidentDetails: {JsonConvert.SerializeObject(incidentDetails)}\r\n\r\n";

            var discussions = new StringBuilder();
            foreach (var discussion in incidentDiscussions)
            {
                discussions.AppendLine($"### {discussion.Date.ToString("MM-dd HH:mm")} {discussion.ChangedBy} {discussion.Cause}");
                string discussionText = await TextProcessingHelpers.ProcessComplexICMContent(discussion.Text, _kernel, _logger, skipImages: true);
                discussions.AppendLine($" {string.Join("\n", discussionText.Split("\n").Select(s => s.Trim()).Where(s => s.Length > 0))}");
            }
            incidentMarkdown += discussions.ToString();

            var customFields = new StringBuilder();
            customFields.AppendLine("Some Additional Information for the Incident:\n\n");
            foreach (var customField in incidentCustomFields)
            {
                if (!string.IsNullOrWhiteSpace(customField.CustomFieldValue))
                {
                    string customFieldText = await TextProcessingHelpers.ProcessComplexICMContent(customField.CustomFieldValue, _kernel, _logger, skipImages: true);
                    customFields.AppendLine($"### {customField.CustomFieldName}: {string.Join("\n", customFieldText.Split("\n").Select(s => s.Trim()).Where(s => s.Length > 0))}");
                }
            }

            incidentMarkdown += customFields.ToString();

            string prompt = GetIncidentSummaryPrompt(incidentMarkdown);

            var result = await _kernel.RunAsync(prompt);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while getting incident details for incident ID: {IncidentId}", incidentId);
            throw new Exception($"Failed to get incident summary for incident ID: {incidentId}. Error: {ex.Message}", ex);
        }
    }

    private string GetIncidentSummaryPrompt(string incident)
    {
        string prompt = $@"# Instructions
You are an **Incident Summarizer** that extracts the various actions taken and information gathered to mitigate and resolve an incident, and creating a full Incident Summary. 

Your tasks will involve extracting the following information: 
- Issue Symptoms 
- Kusto Queries with a one liner describing what the Kusto Query does for each Kusto Query. 
  - Kusto Queries that help identify the resources having the impact. 
  - Kusto Queries that can be executed to get additional details about the impacted resources. 
  - Kusto Queries that are executed to verify that the mitigation actions are working. 
  - Kusto Queries that are executed to verify that the impacted resource has recovered. 
- Mitigation Actions that are carried out in each issue scenario (this includes any resource operations like scaling, reboot, restart app, updating config/app settings and any other commands like ACIS). 
  - Collect full details about each mitigation action and what it achieves. 
  - Structure of Mitigation Summary for each type of scenario.
- Then spend some time to reason about the issue symptoms, issue verification, and think hard about mitigation actions, why they work. 
- Extract details about monitoring after the mitigation action has been carried out and for how long that monitoring should be done. 

Then create the Incident Summary. Read the details, discussions, and any linked incidents (details/discussions) as well and come up with a summary of how it was handled. Extract the full Kusto Queries that were executed. Create a CUSTOM_INSTRUCTIONS list for handling this type of incidents. Clearly provide full Kusto Queries to be executed to verify the issue's occurrence and to monitor the recovery. Clearly provide the detailed mitigation actions with all relevant commands or operations that were carried out. Create a full Incident Report of this incident using all the above details. Provide the detailed Kusto queries. Start your response with 'Regarding the incident ....' 
- The report should be structured and well formatted. 
- The report should include the following sections: 
  - Summary
  - Incident Type i.e. Live Site (LSI) or Customer Reported (CRI)
  - Issue Symptoms 
  - Kusto Queries 
  - Mitigation Actions 
  - Monitoring 
  - Resolution

Finally create an Incident Handling Workflow Diagram (flowchart in markdown) that describes the entire process of the Incident Handling.
  
# Incident
{incident}
";
        return prompt;

    }

    private async Task<string> GetInstructionPrompt(string troubleshootingGuide, string agentMode = "TestModeAgent")
    {
        var pluginToolsInfo = _kernelService.GetAvailablePluginToolInfo(agentMode);
        var availableGenevaActions = await _genevaActionsPlugin.GetAvailableGenevaActions();

        string pluginToolsPrompt = pluginToolsInfo != null ? JsonConvert.SerializeObject(pluginToolsInfo) : "No Plugin Tools Available.";
        string availableGenevaActionsPrompt = availableGenevaActions != null && availableGenevaActions.Count > 0 ? JsonConvert.SerializeObject(availableGenevaActions) : "No Geneva Actions Available.";

        string troubleshootingGuideSection = string.IsNullOrWhiteSpace(troubleshootingGuide) ?
            string.Empty :
            $@"
# Troubleshooting Guide
This guide contains detailed diagnostics steps, Kusto queries, and mitigation actions based on analysis of similar incidents:

{troubleshootingGuide}";

        var prompt = $@"You are an AI assistant that generates an EXECUTION_PLAN for an LLM-based Agent in the form of list of instructions based on provided template and requirements.
Incidents can be of two types, Live Site (LSI) and Customer Reported (CRI).

# Template for Live Site (LSI)
### START OF INSTRUCTIONS ###

    Create an EXECUTION_PLAN using the below guidelines:,
    ___guidelines ____,
    - ALERT_SIGNAL_MONITORING instructions:,
    ____instructions____,
    Execute the EXECUTION_PLAN in a fully autonomous manner leveraging the tools available to you.

### END OF INSTRUCTIONS ###


# Template for Customer Reported (CRI)
### START OF INSTRUCTIONS ###

    Create an EXECUTION_PLAN using the below guidelines:,
    ___guidelines ____,
    Execute the EXECUTION_PLAN in a fully autonomous manner leveraging the tools available to you.

### END OF INSTRUCTIONS ###

# Here is a LIST_OF_AVAILABLE_TOOLS (capabilities) that the LLM-based Agent could use to fetch information or carry out actions. The EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools.

Available Plugin Tools: {pluginToolsPrompt}

Available Geneva Actions: {availableGenevaActionsPrompt}{troubleshootingGuideSection}


# Requirements
- Use the detailed steps and Kusto queries from the Troubleshooting Guide to create a structured EXECUTION_PLAN
- When including kusto query execution, include full semantically correct Kusto queries in the instructions
- **Ensure that the EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools**
- ALERT_SIGNAL_MONITORING instructions are needed only for LiveSite issues.
- DON'T include the ""Step X:"" prefix in the instruction you generate


# Example instructions for a Live Site Incident

### START OF INSTRUCTIONS ###

    Create an EXECUTION_PLAN using the below guidelines:,
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

    Execute the COMPLETE execution plan in a FULLY AUTONOMOUS manner, start the investigation by yourself WITHOUT printing anything like 'next steps' or 'next actions' etc, DO NOT ask for any user confirmation or input.,
    Post a discussion Entry with a summary of stage-by-stage details:,
        - Incident Details,
        - Impacted Endpoint Details,
        - Transient/Non-Transient check,
        - Mitigation Actions,
        - Confirmation of Recovery,
        - Iteration-by-Iteration results of the ALERT_SIGNAL_MONITORING in a table format.

### END OF INSTRUCTIONS ###


Remember these # Requirements
- Use the detailed steps and Kusto queries from the Troubleshooting Guide to create a structured EXECUTION_PLAN
- When including kusto query execution, include full semantically correct Kusto queries in the instructions
- **Ensure that the EXECUTION_PLAN must adhere to instructions that involve actions that can be performed using these tools**
- ALERT_SIGNAL_MONITORING instructions are needed only for LiveSite issues.

";
        return prompt;
    }
}
