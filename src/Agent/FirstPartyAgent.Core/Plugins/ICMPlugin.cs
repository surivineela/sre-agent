// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Constants;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class ICMPlugin
    {
        private readonly IICMAPIClient _icmApiClient;
        private readonly IICMWorkflowClient _icmWorkflowClient;
        private readonly ILogger<ICMPlugin> _logger;
        private readonly ITeamsClient _teamsClient;
        private readonly ISessionMessageService _sessionMessageService;
        private readonly AlertHandlerClient _alertHandlerClient;
        private const string HumanInterventionTag = "SREAgent_HumanIntervention";
        private const string AgentProcessedTag = "SREAgent_Processed";
        private const string AgentProcessingTag = "SREAgent_Processing";
        private const string AgentMitigatedTag = "SREAgent_Mitigated";

        public ICMPlugin(IICMAPIClient icmAPIClient, IICMWorkflowClient icmWorkflowClient, ILogger<ICMPlugin> logger, ITeamsClient teamsClient, ISessionMessageService sessionMessageService, AlertHandlerClient alertHandlerClient)
        {
            _logger = logger;
            _icmApiClient = icmAPIClient;
            _icmWorkflowClient = icmWorkflowClient;
            _teamsClient = teamsClient;
            _sessionMessageService = sessionMessageService;
            _alertHandlerClient = alertHandlerClient;
        }

        /// <summary>
        /// Extracts text from an image using the chat completion service with SK (does not carry around the conversation history/context etc.; no tool calling)
        /// </summary>
        /// <param name="kernel"></param>
        /// <param name="mimeType"></param>
        /// <param name="base64Image"></param>
        /// <param name="chatCompletionService"></param>
        /// <returns></returns>
        private static async Task<ChatMessageContent> ExtractTextFromImage(Kernel kernel, string mimeType, string base64Image, IChatCompletionService chatCompletionService, ILogger<ICMPlugin> logger)
        {
            logger.LogInformation($"Extracting text from image ({mimeType}, {base64Image.Length} characters)");

            var history = new ChatHistory();
            var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the text from the image"),
                            new ImageContent($"{mimeType};base64,{base64Image}")
                        };

            history.AddUserMessage(message);

            var result = await chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            },
            kernel: kernel);

            return result;
        }

        private async Task<string> ProcessComplexICMContent(string complexContent, Kernel kernel, bool skipImages = false)
        {
            List<(string, string)> base64Images = new List<(string, string)>();
            // remove base64 images from the complexContent (they would blow the response which goes back to the model and the model wouldn't make sense out of it) and store them in a list
            complexContent = TextProcessingHelpers.StripBase64Images(complexContent, base64Images);
            
            // remove html attributes as they don't provide much value and make the response longer (todo: it might be useful to strip html tags completely and convert the text rather to markdown or something like that)
            complexContent = TextProcessingHelpers.RemoveHtmlAttributes(complexContent);
            
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            if (chatCompletionService == null)
            {
                _logger.LogInternalError("ChatCompletionService is not available in the kernel. Cannot process complex ICM content.");
                return complexContent;
            }
            
            for (int i = 0; i < base64Images.Count; i++)
            {
                string imageText = "No Image Description.";
                var deploymentName = chatCompletionService.Attributes["DeploymentName"]?.ToString() ?? string.Empty;
                if (deploymentName.StartsWith("o") && !skipImages)
                {
                    // extract text from the image and replace the placeholder in the summary with the extracted text
                    try
                    {
                        ChatMessageContent result = await ExtractTextFromImage(kernel, base64Images[i].Item1, base64Images[i].Item2, chatCompletionService, _logger);
                        imageText = result.Content ?? imageText;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error extracting text from image {base64Images[i].Item1} - {base64Images[i].Item2}");
                    }
                }
                
                complexContent = complexContent.Replace($"####{i}####", "[The following text was in an image in the incident]" + imageText + "\r\n[End of the image]");
            }

            return complexContent;
        }

        [KernelFunction("get_icm_incident_details")]
        [Description("Get ICM incident details")]
        public async Task<Incident> GetIncidentInfo(
           [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var logMessage = $"[get_icm_incident_details][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            if (kernel.Data.ContainsKey("incidentDetails"))
            {
                var incidentDetails = kernel.Data["incidentDetails"] as Incident;
                if (incidentDetails != null && incidentDetails.IncidentId == incidentId)
                {
                    return incidentDetails;
                }
            }

            var incident = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.GetIncidentAsync(incidentId)
                : await _icmApiClient.GetIncidentAsync(incidentId);
            incident.Summary = await ProcessComplexICMContent(incident.Summary, kernel, !_icmWorkflowClient.ProcessImages);
            incident.DiscussionEntry = await ProcessComplexICMContent(incident.DiscussionEntry, kernel, !_icmWorkflowClient.ProcessImages);
            kernel.Data["incidentDetails"] = incident;
            await SetupIncidentProcessing(incidentId, incident, kernel);
            return incident;
        }

        [KernelFunction("get_icm_custom_fields")]
        [Description("Get ICM incident custom fields")]
        public async Task<List<CustomField>> GetCustomFields(
            [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var logMessage = $"[get_icm_custom_fields][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);

            var customFields = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.GetCustomFieldsAsync(incidentId)
                : await _icmApiClient.GetCustomFieldsAsync(incidentId);

            return customFields;
        }

        [KernelFunction("search_incidents")]
        [Description("Search for incidents and returns matching incidents with details like CreatedDateTime, Id, Title etc.")]
        public async Task<string> SearchIncidents(
            [Description("Search String")] string searchString,
            [Description("Lookback Period in Days")] int lookbackPeriodInDays,
            [Description("Limit on result count")] int resultCountLimit,
            Kernel kernel)
        {
            var logMessage = $"[search_incidents][{DateTime.UtcNow}] Invoked with searchString {searchString}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);

            var incidents = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.SearchIncidentsAsync(searchString, lookbackPeriodInDays, resultCountLimit)
                : await _icmApiClient.SearchIncidentsAsync(searchString);

            return JsonConvert.SerializeObject(incidents);
        }

        [KernelFunction("get_queryable_columns_for_incidents")]
        [Description("Get queryable columns for advanced incident search")]
        public async Task<Dictionary<string, List<string>>> GetQueryableColumnsForIncidentLookup(Kernel kernel)
        {
            var logMessage = $"[get_queryable_columns_for_incidents][{DateTime.UtcNow}]";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var searchableColumns = IncidentAdvancedSearchFilter.GetQueryableIncidentProperties();
            return searchableColumns;
        }

        [KernelFunction("advanced_search_for_incidents")]
        [Description("Advanced search for incidents. Use get_queryable_columns_for_incidents to get list of queryable properties for help with constructing filters")]
        public async Task<List<IncidentAdvancedSearchResultItem>> AdvancedSearchIncidents(
            [Description("Lookback Period in Days")] int lookbackPeriodInDays,
            [Description("Limit on result count. Maximum 10 search results returned.")] int resultLimit,
            [Description("List of filters as tuples (ColumnName, Operator, Value) e.g.. (CreateDate, >=, 2025-02-01 04:15:00), (CreateDate, <=,  2025-02-01 06:15:00), (IncidentId, ==,  123456)")]
            List<Tuple<string, string, string>> filter3Tuple,
            Kernel kernel)
        {
            var logMessage = $"[advanced_search_for_incidents][{DateTime.UtcNow}] Invoked with lookbackPeriodInDays {lookbackPeriodInDays}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var incidents = await _icmWorkflowClient.SearchIncidentsWithParametersAsync(lookbackPeriodInDays, resultLimit, filter3Tuple);
            return incidents;
        }

        [KernelFunction("get_current_utc_datetime")]
        [Description("Get current UTC date and time")]
        public async Task<string> GetCurrentUtcDateTime(Kernel kernel)
        {
            string returnValue = $"Current timestamp: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";
            var logMessage = $"[get_current_utc_datetime][{DateTime.UtcNow}] Invoked. Returned {returnValue}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return returnValue;
        }

        [KernelFunction("get_icm_correlation_and_linking_rules")]
        [Description("This tool identifies potential relationships between incidents. Invoke this tool whenever the user requests assistance with finding related, parent, or child incidents; especially when conditions such as time windows, title matching, or shared patterns are specified. The rules are applied internally to guide the agent's actions without being returned to the user.")]
        public async Task<string> GetIcmCorrelationAndLinkingRules(Kernel kernel)
        {
            await kernel.LogInformation($"[get_icm_correlation_and_linking_guidelines][{DateTime.UtcNow}] invoked.", _logger, _teamsClient, _sessionMessageService);
            const string guidelines = "Follow the below workflow carefully, this guide is to be used for identifying potential matches only and does not apply to incidents already linked as related/parent/child, as those are considered high-confidence correlations.\n" +
                "1. Initial Setup (Mandatory)\n" +
                "     - Always use advanced search (advanced_search_for_incidents) for all incident search or lookup operations for lookups as part of this guide.\n" +
                "     - If you are not given an IncidentId, ask the user to specify an incident Id that needs to be worked upon. If you have the incident Id, quietly continue with the flow.\n" +
                "     - Start by calling get_queryable_columns_for_incidents to identify all columns on which filters can be applied.\n" +
                "     - Next, call get_current_utc_datetime to get the latest UTC dateTime. This will help you adjust the various date-time values and apply correct filter values.\n" +
                "     - CRITICALLY: Before proceeding with any correlation operations, you MUST call advanced_search_for_incidents for the current incident and apply the IncidentId filter.This ensures you retrieve accurate values to apply as filters before proceeding further. This step is non-negotiable and must never be skipped.\n" +
                "2. Perform Advanced Search On Current Incident For Filter Values (Non-Negotiable)\n" +
                "     - After identifying queryable columns, **MANDATORILY invoke advanced_search_for_incidents for the incident you are correlating, applying the IncidentId filter.**\n" +
                "     - This forced advanced search ensures you have accurate values to apply as filters before proceeding.\n" +
                "3. Prepare Filters Based on User Instruction\n" +
                "     - Carefully parse the instructions to extract filter criteria (e.g., column names and conditions).\n" +
                "     - Use advanced search filters to apply the specified conditions. If the user provides a time-based condition:\n" +
                "         - Use the appropriate date column (based on the instructions) with the '>=' operator for the start time and the '<=' operator for the end time.\n" +
                "     - For other column conditions (e.g., title, severity, status, slice etc.), apply filters as specified in the instructions.\n" +
                "     - Adjust the lookbackPeriod by calculating the difference between the current UTC date and the date you are querying for, ensuring it is applied correctly.\n" +
                "     - Ensure strict adherence to user provided criteria.\n" +
                "4. Validate Filters Before Execution\n" +
                "     - Once the filters are prepared, evaluate and validate them to ensure they match the criteria given in the instructions and do not contain errors. \n" +
                "     - If necessary, refine the filters before calling the advanced search operation.\n" +
                "     - Everytime you refine, change, fix filters; evaluate and validate them to ensure strict adherence to user provided criteria.\n" +
                "     - Cross check the values applied in filters with the values you have from **Perform Advanced Search On Current Incident For Filter Values**.\n" +
                "5. Perform Advanced Search For Potential Correlations\n" +
                "     - Execute the advanced search with the validated filters to look up potential correlated incidents.\n" +
                "     - If the instructions require multiple conditions that cannot be combined in a single query (due to AND logic limitations), run multiple queries as needed and consolidate the results.\n" +
                "6. Important Notes\n" +
                "     - Advanced search applies all conditions within a single query using AND logic; it does not support OR logic.\n" +
                "     - **DO NOT SKIP the step of Perform Advanced Search On Current Incident For Filter Values as specified in step 2** before correlating, it is critical for ensuring accuracy.\n\n";
            return guidelines;
        }


        [KernelFunction("get_alerting_discussion_entry")]
        [Description("Get Azure Alerting discussion entry")]
        public async Task<DiscussionEntry?> GetAlertingDiscussionEntry(
            [Description("Incident ID")] string incidentId,
            Kernel kernel)
        {
            var logMessage = $"[get_alerting_discussion_entry][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var discussionEntries = _icmApiClient.IsEnabled()
                ? await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId)
                : await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId);

            if (discussionEntries != null)
            {
                foreach (var entry in discussionEntries)
                {
                    if (entry.IsHtml)
                    {
                        entry.Text = await ProcessComplexICMContent(entry.Text, kernel, skipImages: true);
                    }
                    if (entry.Text.Contains("Open in AzureAlerting"))
                    {
                        return entry;
                    }
                }
            }
            return null;
        }

        [KernelFunction("get_icm_discussion_entries")]
        [Description("Get ICM discussion entries")]
        public async Task<List<DiscussionEntry>> GetDiscussionEntries(
            [Description("Incident ID")] string incidentId, Kernel kernel)
        {
            var logMessage = $"[get_icm_discussion_entries][{DateTime.UtcNow}] Fetching ICM Discussion entries for Incident {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var discussionEntries = _icmApiClient.IsEnabled()
                ? await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId)
                : await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId);
            foreach (var entry in discussionEntries)
            {
                if (entry.IsHtml)
                {
                    entry.Text = await ProcessComplexICMContent(entry.Text, kernel, !_icmWorkflowClient.ProcessImages);
                }
            }
            return discussionEntries;
        }

        [KernelFunction("transfer_icm_incident")]
        [Description("Transfer ICM incident")]
        public async Task<string> TransferIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry - reason for transferring the incident")] string discussionEntry,
               [Description("Tenant ID of the team to transfer the incident to")] string tenantName,
               [Description("Team ID of the team to transfer the incident to")] string owningTeam,
               Kernel kernel)
        {
            var logMessage = $"[transfer_icm_incident][{DateTime.UtcNow}] Transferring Incident {incidentId} to the team {tenantName}/{owningTeam}.\n<b>Reason</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            var result = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.TransferIncidentAsync(incidentId, discussionEntry, tenantName, owningTeam)
                : await _icmApiClient.TransferIncidentAsync(incidentId, discussionEntry, tenantName, owningTeam);
            await UpdateAgentStatus(incidentId, AgentStatus.Transferred, kernel);
            return result;
        }

        [KernelFunction("mitigate_icm_incident")]
        [Description("Mitigate ICM incident")]
        public async Task<string> MitigateIncident(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry (HTML) - reason for mitigating the incident")] string discussionEntry,
           Kernel kernel)
        {
            var logMessage = $"[mitigate_icm_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);

            var mitigationResult = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.MitigateIncidentAsync(incidentId, discussionEntry)
                : await _icmApiClient.MitigateIncidentAsync(incidentId, discussionEntry);
            var addMitigationTagResult = await AddTagToIncident(incidentId, AgentMitigatedTag, kernel);
            await UpdateAgentStatus(incidentId, AgentStatus.Mitigated, kernel);
            return mitigationResult;
        }

        [KernelFunction("escalate_for_human_intervention")]
        [Description("Escalate incident for human intervention")]
        public async Task<string> EscalateToHumanQueue(
            [Description("Incident ID")] string incidentId,
            [Description("Reason for escalating the incident")] string escalationReason,
            Kernel kernel)
        {
            var logMessage = $"[escalate_for_human_intervention][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>escalationReason</b>:\n {escalationReason}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var result = await EscalateToHumanQueueInternal(incidentId, escalationReason, kernel);
            return result;
        }

        [KernelFunction("downgrade_sev2_incident_to_sev3")]
        [Description("Downgrade severity of ICM incident 2 to 3")]
        public async Task<string> DowngradeSeverity(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion Entry (HTML) - reason for downgrading the incident")] string discussionEntry,
            Kernel kernel)
        {
            var logMessage = $"[downgrade_sev2_incident_to_sev3][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            var result = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.DowngradeSeverityAsync(incidentId, discussionEntry)
                : await _icmApiClient.ChangeSeverityAsync(incidentId, 3, discussionEntry);
            await AddTagToIncident(incidentId, AgentProcessedTag, kernel);
            return result;
        }

        [KernelFunction("resolve_icm_incident")]
        [Description("Resolve ICM incident")]
        public async Task<string> ResolveIncident(
               [Description("Incident ID")] string incidentId,
               [Description("Discussion Entry (HTML) - reason for resolving the incident")] string discussionEntry,
               Kernel kernel)
        {
            var logMessage = $"[resolve_icm_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            var result = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.ResolveIncidentAsync(incidentId, discussionEntry)
                : await _icmApiClient.ResolveIncidentAsync(incidentId, discussionEntry);
            await UpdateAgentStatus(incidentId, AgentStatus.Resolved, kernel);
            return result;
        }

        [KernelFunction("post_icm_discussion_entry")]
        [Description("Post ICM discussion entry")]
        public async Task<string> PostDiscussionEntry(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry (HTML)")] string discussionEntry, Kernel kernel)
        {
            var logMessage = $"[post_icm_discussion_entry][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
            var result = _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.PostDiscussionEntryAsync(incidentId, discussionEntry)
                : await _icmApiClient.PostDiscussionEntryAsync(incidentId, discussionEntry);
            await AddTagToIncident(incidentId, AgentProcessedTag, kernel);
            return result;
        }

        [KernelFunction("icm_add_tag")]
        [Description("Add a tag to an ICM incident")]
        public async Task<string> AddTagToIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("Tag to add")] string tag,
            Kernel kernel)
        {
            var logMessage = $"[icm_add_tag][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, tag: {tag}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            // Note: We don't call AddTagToIncident with AgentProcessedTag here to avoid potential infinite loops
            // if this method is called with AgentProcessedTag itself. The caller should handle adding the processed tag if needed.
            return _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.AddTagToIncident(incidentId, tag)
                : await _icmApiClient.AddTagToIncident(incidentId, tag);
        }

        [KernelFunction("icm_add_keyword")]
        [Description("Add a keyword to an ICM incident")]
        public async Task<string> AddKeywordToIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("Keyword to add")] string keyword,
            Kernel kernel)
        {
            var logMessage = $"[icm_add_keyword][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, keyword: {keyword}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            return _icmWorkflowClient.IsEnabled()
                ? await _icmWorkflowClient.AddKeywordToIncident(incidentId, keyword)
                : await _icmApiClient.AddKeywordToIncident(incidentId, keyword);
        }

        // acknowledge_icm_incident using ICM API
        [KernelFunction("acknowledge_icm_incident")]
        [Description("Acknowledges an ICM incident")]
        public async Task<string> AcknowledgeIncident(
            [Description("Incident ID")] string incidentId,
            Kernel kernel)
        {
            var logMessage = $"[acknowledge_icm_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            if (!_icmApiClient.IsEnabled())
            {
                return "Unable to acknowledge the incident as the ICM API client is not enabled.";
            }
            var result = await _icmApiClient.AcknowledgeIncidentAsync(incidentId);
            await UpdateAgentStatus(incidentId, AgentStatus.Acknowledged, kernel);
            return result;
        }

        [KernelFunction("get_incident_repair_items")]
        [Description("Get repair items associated with an ICM incident")]
        public async Task<List<IncidentRepairItem>> GetIncidentRepairItems(
            [Description("Incident ID")] long incidentId,
            Kernel kernel)
        {
            var logMessage = $"[get_incident_repair_items][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            if (!_icmApiClient.IsEnabled())
            {
                return new List<IncidentRepairItem>()
                {
                    new IncidentRepairItem
                    {
                       Id = -1,
                       Title = "ICM API not enabled. No repair items can be fetched.",
                    }
                };
            }

            var repairItems = await _icmApiClient.GetIncidentRepairItemsAsync(incidentId);
            return repairItems;
        }

        /// <summary>
        /// Enum representing the possible statuses of the agent.
        /// </summary>
        private enum AgentStatus
        {
            Acknowledged,
            Transferred,
            Mitigated,
            Resolved
        }

        /// <summary>
        /// Updates the status of the agent by removing existing status tags and adding a new one.
        /// </summary>
        /// <param name="incidentId">The ID of the incident.</param>
        /// <param name="status">The new status to update.</param>
        /// <param name="kernel">The kernel instance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task UpdateAgentStatus(string incidentId, AgentStatus status, Kernel kernel)
        {
            var logMessage = $"[update_agent_status][{DateTime.UtcNow}] Updating status to '{status}' for incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);

            // Add the new status tag
            var newStatusTag = $"SREAgent_{status}";
            switch (status)
            {
                case AgentStatus.Transferred:
                case AgentStatus.Mitigated:
                case AgentStatus.Resolved:
                    await AddTagToIncident(incidentId, AgentProcessedTag, kernel);
                    break;
                default:
                    break;
            }
            await AddTagToIncident(incidentId, newStatusTag, kernel);
        }

        /// <summary>
        /// Sets up the necessary steps when the agent starts working on an incident.
        /// </summary>
        /// <param name="incidentId">The ID of the incident.</param>
        /// <param name="incidentDetails">The details of the incident.</param>
        /// <param name="kernel">The kernel instance.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task SetupIncidentProcessing(string incidentId, Incident incidentDetails, Kernel kernel)
        {
            try
            {
                await AcknowledgeIncident(incidentId, kernel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, incidentId, "Error acknowledging incident during setup processing.");
            }

            var logMessage = $"[setup_incident_processing][{DateTime.UtcNow}] Setting up processing for incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);

            // Add a tag to indicate the agent has started processing
            await AddTagToIncident(incidentId, AgentProcessingTag, kernel);

            ICMAlertConfig? alertConfig = await _alertHandlerClient.GetConfigAsync(incidentDetails, kernel);
            
            string? agentName = alertConfig?.AgentName;
            if (!string.IsNullOrEmpty(agentName))
            {
                await AddTagToIncident(incidentId, $"SREAgent_{agentName}", kernel);
            }
            else
            {
                var warningMessage = $"[setup_incident_processing][{DateTime.UtcNow}] AgentName could not be determined for incidentId {incidentId}. No matching AlertConfig found based on Title, TitleContains, or OwningTeam.";
                await kernel.LogInformation(warningMessage, _logger, _teamsClient, _sessionMessageService);
                await AddTagToIncident(incidentId, $"SREAgent_UnnamedAgent", kernel);
            }

            // Escalate to human intervention if CloudInstance is not Public
            if (incidentDetails.CloudInstance != "Public")
            {
                await EscalateToHumanQueueInternal(incidentId, "CloudInstance is not Public.", kernel);
            }
        }

        private async Task<string> EscalateToHumanQueueInternal(string incidentId, string reason, Kernel kernel)
        {
            var logMessage = $"[escalate_to_human_queue][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);

            await AddTagToIncident(incidentId, HumanInterventionTag, kernel);

            var incidentDetails = await GetIncidentInfo(incidentId, kernel);

            ICMAlertConfig? alertConfig = await _alertHandlerClient.GetConfigAsync(incidentDetails, kernel);

            string discussionEntryMessage = $"The SRE Agent would not be able to proceed further with its current set of capabilities.<br><b>Reason</b>: {reason}<br>Request Human Intervention from the current on-calls to take this further.";
            if (alertConfig != null && !string.IsNullOrWhiteSpace(alertConfig.DefaultHumanInterventionLoop))
            {
                string? teamTenant = null; string? teamName = null;
                var loopParts = alertConfig.DefaultHumanInterventionLoop.Split('/');
                if (loopParts.Length > 1)
                {
                    teamTenant = loopParts[0];
                    teamName = loopParts[1];
                    var discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntryMessage);
                    var transferResult = await TransferIncident(incidentId, discussionEntry, teamTenant, teamName, kernel);
                    return transferResult;
                }
            }
            var errorMessage = $"[escalate_to_human_queue][{DateTime.UtcNow}] No valid default human intervention loop found for incidentId {incidentId}. Provided value is {alertConfig?.DefaultHumanInterventionLoop}. Tagged the Incident and requested the on-calls to take a look.";
            await kernel.LogInformation(errorMessage, _logger, _teamsClient, _sessionMessageService);
            var errorDiscussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntryMessage);
            var postResult = await PostDiscussionEntry(incidentId, errorDiscussionEntry, kernel);
            return errorMessage;
        }

        #region RelatedIncidents operation methods
        [KernelFunction("get_linked_related_incidents_info")]
        [Description("​Gets basic info for all the linked incidents maked as related and associated with the given incident id")]
        public async Task<List<string>> GetLinkedRelatedIncidentInfo(
            [Description("Incident ID used to fetch and return basic information about the related incidents associated with it.")] long incidentId, Kernel kernel)
        {
            var logMessage = $"[get_related_incidents][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var relatedIncidents = await _icmApiClient.GetLinkedRelatedIncidentInfoAsync(incidentId);
            return relatedIncidents;
        }

        [KernelFunction("add_related_incidents_link")]
        [Description("Adds a related incident link to the given incident id")]
        public async Task<string> AddRelatedIncidentLink(
            [Description("Incident ID to assign a related incident to")] long incidentId,
            [Description("Incident ID to assign as a related incident")] long relatedIncidentId,
            Kernel kernel)
        {
            var logMessage = $"[add_related_incidents_link][{DateTime.UtcNow}] Invoked with incidentId {incidentId} and relatedIncidentId {relatedIncidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var result = await _icmApiClient.AddRelatedIncidentLinkAsync(incidentId, relatedIncidentId);
            return result;
        }

        [KernelFunction("remove_related_incidents_link")]
        [Description("Removes a related incident link from the given incident id")]
        public async Task<string> RemoveRelatedIncidentLink(
            [Description("Incident ID to remove the related incident from")] long incidentId,
            [Description("Incident ID to remove as a related incident")] long relatedIncidentId,
            Kernel kernel)
        {
            var logMessage = $"[remove_related_incidents_link][{DateTime.UtcNow}] Invoked with incidentId {incidentId} and relatedIncidentId {relatedIncidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var result = await _icmApiClient.RemoveRelatedIncidentLinkAsync(incidentId, relatedIncidentId);
            return result;
        }


        #endregion

        #region ParentIncident operation methods
        [KernelFunction("get_parent_incident_info")]
        [Description("​Gets basic info of the parent incident associated with the given incident id")]
        public async Task<string> GetParentIncidentInfo(
            [Description("Incident ID used to fetch and return basic information about the parent incident ID associated with it.")] long incidentId, Kernel kernel)
        {
            var logMessage = $"[get_parent_incident][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var parentIncidentInfo = await _icmApiClient.GetParentIncidentInfoAsync(incidentId);
            return parentIncidentInfo;
        }

        [KernelFunction("add_parent_incident_link")]
        [Description("Adds a parent incident link to the given incident id")]
        public async Task<string> AddParentIncidentLink(
            [Description("Incident ID to assign a parent to")] long incidentId,
            [Description("Incident ID to assign as a parent")] long parentIncidentId,
            Kernel kernel)
        {
            var logMessage = $"[add_parent_incident_link][{DateTime.UtcNow}] Invoked with incidentId {incidentId} and parentIncidentId {parentIncidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var result = await _icmApiClient.AddParentIncidentLinkAsync(incidentId, parentIncidentId);
            return result;
        }

        [KernelFunction("remove_parent_incident_link")]
        [Description("Removes a parent incident link from the given incident id")]
        public async Task<string> RemoveParentIncidentLink(
            [Description("Incident ID to remove the parent from")] long incidentId,
            Kernel kernel)
        {
            var logMessage = $"[remove_parent_incident_link][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var result = await _icmApiClient.RemoveParentIncidentLinkAsync(incidentId);
            return result;
        }
        #endregion

        [KernelFunction("get_child_incidents_info")]
        [Description("​Gets basic info for all the child incidents associated with the given incident id")]
        public async Task<List<string>> GetChildIncidentsInfo(
            [Description("Incident ID used to fetch and return basic information about the child incidents associated with it.")] long incidentId, Kernel kernel)
        {
            var logMessage = $"[get_child_incidents][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var childIncidents = await _icmApiClient.GetChildIncidentsInfoAsync(incidentId);
            return childIncidents;
        }
    }
}

