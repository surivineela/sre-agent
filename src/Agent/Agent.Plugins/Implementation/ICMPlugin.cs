using System.ComponentModel;
using System.Text;
using Agent.Core.Helpers;
using Agent.Core.Services;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SREAgent.Incidents.IcM.Model;
using Newtonsoft.Json;
using Microsoft.AzureAd.Icm.Types;
using ChatMessageContent = Microsoft.SemanticKernel.ChatMessageContent;
using TextContent = Microsoft.SemanticKernel.TextContent;
using Microsoft.AzureAd.Icm.IcmV3OData.Models;
using Attachment = Microsoft.AzureAd.Icm.IcmV3OData.Models.Attachment;
using Agent.Framework;

namespace Agent.Plugins.Implementation;

public class InvestigationTimeRangeResult
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class ICMPlugin : IICMPlugin
{
    private readonly IICMAPIClient _icmApiClient;
    private readonly ILogger<ICMPlugin> _logger;
    private readonly IChatCompletionService _chatCompletionService;
    //private const string HumanInterventionTag = "SREAgent_HumanIntervention";
    private const string AgentProcessedTag = "SREAgent_Processed";
    private const string AgentProcessingTag = "SREAgent_Processing";
    private const string AgentMitigatedTag = "SREAgent_Mitigated";
    private const bool ProcessImages = true;

    public ICMPlugin(IICMAPIClient icmAPIClient, ILogger<ICMPlugin> logger, IChatClientProvider chatClientProvider)
    {
        _logger = logger;
        _icmApiClient = icmAPIClient;


#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        _chatCompletionService = chatClientProvider.DefaultModel.AsChatCompletionService();
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

    }

    public InvestigationTimeRangeResult GetIssueInvestigationTimeRange(DateTime? issueFirstOccurrence, DateTime? issueLastOccurrence, DateTime? reportedIssueObservedOnTime)
    {
        if (issueFirstOccurrence == null && issueLastOccurrence == null && reportedIssueObservedOnTime == null)
        {
            throw new ArgumentException("At least one of the issueFirstOccurrence, issueLastOccurrence or reportedIssueObservedOnTime should be provided.");
        }

        var now = DateTime.UtcNow;

        // If no endDate, set to now
        DateTime endDate = (issueLastOccurrence != null && issueLastOccurrence != DateTime.MinValue) ? issueLastOccurrence.Value
            : (reportedIssueObservedOnTime.HasValue ? reportedIssueObservedOnTime.Value.AddDays(2) : now);

        // If no startDate, set to now-10d
        DateTime startDate = (issueFirstOccurrence != null && issueFirstOccurrence != DateTime.MinValue) ? issueFirstOccurrence.Value
            : (reportedIssueObservedOnTime.HasValue ? reportedIssueObservedOnTime.Value.AddDays(-2) : now.AddDays(-10));

        // Ensure the start date is not after the end date
        if (startDate > endDate)
        {
            startDate = endDate.AddDays(-10);
        }

        // If the range is greater than 1 month, adjust startDate to be 1 month before endDate
        if ((endDate - startDate).TotalDays > 30)
        {
            startDate = endDate.AddMonths(-1);
        }



        // Add a 1-hour buffer before and after the time window to capture events near the start and end of the investigation period.
        startDate = startDate.AddHours(-1);
        endDate = endDate.AddHours(1);

        _logger.LogInternalInformation($"Calculated investigation time range: StartDate={startDate}, EndDate={endDate}");
        return new InvestigationTimeRangeResult()
        {
            StartDate = startDate,
            EndDate = endDate
        };
    }

    /// <summary>
    /// Extracts text from an image using the chat completion service with SK (does not carry around the conversation history/context etc.; no tool calling)
    /// </summary>
    /// <param name="mimeType"></param>
    /// <param name="base64Image"></param>
    /// <param name="logger"></param>
    /// <returns></returns>
    private async Task<ChatMessageContent> ExtractTextFromImage(string mimeType, string base64Image)
    {
        _logger.LogInternalInformation($"Extracting text from image ({mimeType}, {base64Image.Length} characters)");

        var history = new ChatHistory();
        var message = new ChatMessageContentItemCollection
                        {
                            new TextContent("Please extract the text from the image"),
                            new ImageContent($"{mimeType};base64,{base64Image}")
                        };

        history.AddUserMessage(message);
        var result = await _chatCompletionService.GetChatMessageContentAsync(
            history,
            executionSettings: new()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.None()
            });
        return result;
    }

    private async Task<string> ProcessComplexICMContent(string complexContent, bool skipImages = false)
    {
        var base64Images = new List<(string, string)>();
        if (complexContent != null)
        {
            // remove base64 images from the complexContent (they would blow the response which goes back to the model and the model wouldn't make sense out of it) and store them in a list
            complexContent = TextProcessingHelpers.StripBase64Images(complexContent, base64Images);

            // remove html attributes as they don't provide much value and make the response longer (todo: it might be useful to strip html tags completely and convert the text rather to markdown or something like that)
            complexContent = TextProcessingHelpers.RemoveHtmlAttributes(complexContent);

            for (var i = 0; i < base64Images.Count; i++)
            {
                var imageText = "No Image Description.";
                if (_chatCompletionService.Attributes != null &&
                   _chatCompletionService.Attributes.TryGetValue("ModelId", out var modelId) &&
                   modelId?.ToString()?.StartsWith("o", StringComparison.OrdinalIgnoreCase) == false &&
                   !skipImages)
                {

                    // extract text from the image and replace the placeholder in the summary with the extracted text
                    try
                    {
                        var result = await ExtractTextFromImage(base64Images[i].Item1, base64Images[i].Item2);
                        imageText = result.Content;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, $"Error extracting text from image {base64Images[i].Item1} - {base64Images[i].Item2}");
                    }
                }

                complexContent = complexContent.Replace($"####{i}####", "[The following text was in an image in the incident]" + imageText + "\r\n[End of the image]");
            }
        }
        return complexContent ?? string.Empty;
    }

    public async Task<ICMIncident> GetIncidentInfo(string incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetIncidentInfo)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);

        var incident = await _icmApiClient.GetIncidentAsync(incidentId);
        incident.Summary = await ProcessComplexICMContent(incident.Summary, !ProcessImages);
        incident.Description = await ProcessComplexICMContent(incident.Description, !ProcessImages);
        //kernel.Data["incidentDetails"] = incident;
        //await SetupIncidentProcessing(incidentId, incident);
        return incident;
    }

    public async Task<string> GetParametersFromIncident(string incidentId, string instruction)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetParametersFromIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);

        try
        {
            // 1. Get incident information
            var incident = await _icmApiClient.GetIncidentAsync(incidentId);
            if (incident == null)
            {
                return JsonConvert.SerializeObject(new
                {
                    error = "Incident not found",
                    incidentId = incidentId
                }, Formatting.Indented);
            }

            // 2. Get discussion information
            var discussionEntries = await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId);

            // 3. Process summary (including image processing)
            var processedSummary = await ProcessComplexICMContent(incident.Summary, !ProcessImages);

            // 4. Convert summary to Markdown
            var summaryMarkdown = IcmHelper.ConvertToMarkDown(processedSummary);

            // 5. Process discussions and convert to Markdown
            var discussionMarkdown = new StringBuilder();
            if (discussionEntries != null && discussionEntries.Any())
            {
                foreach (var entry in discussionEntries)
                {
                    var entryText = entry.Text;

                    // Process images if content is HTML
                    if (entry.RenderType == DescriptionTextRenderType.Html)
                    {
                        entryText = await ProcessComplexICMContent(entryText, !ProcessImages);
                    }

                    // Convert to Markdown
                    var entryMarkdown = IcmHelper.ConvertToMarkDown(entryText);

                    discussionMarkdown.AppendLine($"## Discussion Entry - {entry.Date:yyyy-MM-dd HH:mm:ss} UTC");
                    discussionMarkdown.AppendLine($"**Author:** {entry.ChangedBy}");
                    discussionMarkdown.AppendLine();
                    discussionMarkdown.AppendLine(entryMarkdown);
                    discussionMarkdown.AppendLine();
                    discussionMarkdown.AppendLine("---");
                    discussionMarkdown.AppendLine();
                }
            }

            // 6. Create prompt template
            var prompt = $@"
You are an expert at extracting parameters from ICM (Incident Communication Manager) incidents for RCA (Root Cause Analysis) investigations.

**Instruction:** {instruction}

**Incident Information:**
- Incident ID: {incident.Id}
- Title: {incident.Title}
- Create Date: {incident.CreatedDate:yyyy-MM-dd HH:mm:ss} UTC
- Status: {incident.State}
- Severity: {incident.Severity}
- Owning Team: {incident.OwningTeamName}
- Cloud Instance: {incident.MonitorLocation.Instance}

**Incident Summary:**
{summaryMarkdown}

**Discussion Entries:**
{discussionMarkdown}

**Task:**
Based on the incident information provided above, extract the parameters requested in the instruction. 
Return the results in a structured JSON format that can be easily parsed by the calling application.

If any requested parameter cannot be found in the incident data, indicate this clearly in your response.
Focus on extracting accurate timestamps, resource names, and other technical details that would be useful for RCA analysis.

**Response Format:**
Return a JSON object with the extracted parameters. Use clear, descriptive property names.

Example structure:
```json
{{
  ""incidentId"": ""{incident.Id}"",
  ""title"": ""{incident.Title}"",
  ""startTime"": ""extracted start time if available"",
  ""endTime"": ""extracted end time if available"",
  ""siteName"": ""extracted site name if available"",
  ""eventPrimaryStampName"": ""extracted stamp name if available"",
  ""functionName"": ""extracted function name if available"",
  ""additionalParameters"": {{
    ""key"": ""value""
  }}
}}
```
";

            // 7. Send request to LLM
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var result = await _chatCompletionService.GetChatMessageContentAsync(
                history,
                executionSettings: new()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.None()
                });

            return result.Content ?? "Error: No response from LLM";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error extracting parameters from incident {IncidentId}", incidentId);
            return JsonConvert.SerializeObject(new
            {
                error = $"Error: {ex.Message}",
                incidentId = incidentId
            }, Formatting.Indented);
        }
    }

    [KernelFunction("get_icm_custom_fields")]
    [Description("Get ICM incident custom fields")]
    public async Task<List<CustomField>> GetCustomFields(
        [Description("Incident ID")] string incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetCustomFields)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);

        var customFields = await _icmApiClient.GetCustomFieldsAsync(incidentId);

        return customFields;
    }

    public async Task<string> SearchIncidents(string searchString, int lookbackPeriodInDays, int resultCountLimit)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(SearchIncidents)}][{DateTime.UtcNow}] Invoked with searchString {searchString}";
        _logger.LogInternalInformation(logMessage);
        _logger.LogInternalInformation(logMessage);

        var incidents = await _icmApiClient.SearchIncidentsAsync(searchString);

        return JsonConvert.SerializeObject(incidents);
    }

    //[KernelFunction("get_queryable_columns_for_incidents")]
    //[Description("Get queryable columns for advanced incident search")]
    //public async Task<Dictionary<string, List<string>>> GetQueryableColumnsForIncidentLookup(Kernel kernel)
    //{
    //    var logMessage = $"[get_queryable_columns_for_incidents][{DateTime.UtcNow}]";
    //    await kernel.LogKernelInfo(logMessage, _logger, _teamsClient, _sessionMessageService);
    //    var searchableColumns = IncidentAdvancedSearchFilter.GetQueryableIncidentProperties();
    //    return searchableColumns;
    //}

    //[KernelFunction("advanced_search_for_incidents")]
    //[Description("Advanced search for incidents. Use get_queryable_columns_for_incidents to get list of queryable properties for help with constructing filters")]
    //public async Task<List<IncidentAdvancedSearchResultItem>> AdvancedSearchIncidents(
    //    [Description("Lookback Period in Days")] int lookbackPeriodInDays,
    //    [Description("Limit on result count. Maximum 10 search results returned.")] int resultLimit,
    //    [Description("List of filters as tuples (ColumnName, Operator, Value) e.g.. (CreateDate, >=, 2025-02-01 04:15:00), (CreateDate, <=,  2025-02-01 06:15:00), (IncidentId, ==,  123456)")]
    //        List<Tuple<string, string, string>> filter3Tuple,
    //    Kernel kernel)
    //{
    //    var logMessage = $"[advanced_search_for_incidents][{DateTime.UtcNow}] Invoked with lookbackPeriodInDays {lookbackPeriodInDays}";
    //    await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
    //    var incidents = await _icmWorkflowClient.SearchIncidentsWithParametersAsync(lookbackPeriodInDays, resultLimit, filter3Tuple);
    //    return incidents;
    //}

    public string GetCurrentUtcDateTime()
    {
        var returnValue = $"Current timestamp: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")} UTC";
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetCurrentUtcDateTime)}][{DateTime.UtcNow}] Invoked. Returned {returnValue}";
        _logger.LogInternalInformation(logMessage);
        return returnValue;
    }

    public string GetIcmCorrelationAndLinkingRules()
    {
        _logger.LogInternalInformation($"[get_icm_correlation_and_linking_guidelines][{DateTime.UtcNow}] invoked.");
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


    public async Task<DescriptionEntry?> GetAlertingDiscussionEntry(string incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetAlertingDiscussionEntry)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        var discussionEntries = await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId);

        if (discussionEntries != null)
        {
            foreach (var entry in discussionEntries)
            {
                if (entry.RenderType == DescriptionTextRenderType.Html)
                {
                    entry.Text = await ProcessComplexICMContent(entry.Text, skipImages: true);
                }
                if (entry.Text.Contains("Open in AzureAlerting"))
                {
                    return entry;
                }
            }
        }
        return null;
    }


    public async Task<List<DescriptionEntry>> GetDiscussionEntries(string incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetDiscussionEntries)}][{DateTime.UtcNow}] Fetching ICM Discussion entries for Incident {incidentId}";
        _logger.LogInternalInformation(logMessage);
        var discussionEntries = await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId);
        if (discussionEntries != null)
        {
            foreach (var entry in discussionEntries)
            {
                if (entry.RenderType == DescriptionTextRenderType.Html)
                {
                    entry.Text = await ProcessComplexICMContent(entry.Text, !ProcessImages);
                }
            }
        }
        return discussionEntries ?? new List<DescriptionEntry>();
    }

    public async Task<string> TransferIncident(string incidentId, string discussionEntry, string tenantName, string owningTeam)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(TransferIncident)}][{DateTime.UtcNow}] Transferring Incident {incidentId} to the team {tenantName}/{owningTeam}.\n<b>Reason</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
        var result = await _icmApiClient.TransferIncidentAsync(incidentId, discussionEntry, tenantName, owningTeam);
        await UpdateAgentStatus(incidentId, AgentStatus.Transferred);
        return result;
    }


    public async Task<string> MitigateIncident(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(MitigateIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);

        var mitigationResult = await _icmApiClient.MitigateIncidentAsync(incidentId, discussionEntry);
        var addMitigationTagResult = await AddTagToIncident(incidentId, AgentMitigatedTag);
        await UpdateAgentStatus(incidentId, AgentStatus.Mitigated);
        return mitigationResult;
    }

    //[KernelFunction("escalate_for_human_intervention")]
    //[Description("Escalate incident for human intervention")]
    //public async Task<string> EscalateToHumanQueue(
    //    [Description("Incident ID")] string incidentId,
    //    [Description("Reason for escalating the incident")] string escalationReason,
    //    Kernel kernel)
    //{
    //    var logMessage = $"[escalate_for_human_intervention][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>escalationReason</b>:\n {escalationReason}";
    //    await kernel.LogKernelInfo(logMessage, _logger, _teamsClient, _sessionMessageService);
    //    var result = await EscalateToHumanQueueInternal(incidentId, escalationReason, kernel);
    //    return result;
    //}

    public async Task<string> DowngradeSeverity(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(DowngradeSeverity)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
        var result = await _icmApiClient.ChangeSeverityAsync(incidentId, 3, discussionEntry);
        await AddTagToIncident(incidentId, AgentProcessedTag);
        return result;
    }

    public async Task<string> UpdateIncidentSeverity(string incidentId, int severity, string discussionEntry)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(UpdateIncidentSeverity)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, severity {severity}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);

        // Validate severity level
        if ((severity < 2 || severity > 4) && severity != 25)
        {
            var errorMessage = $"Invalid severity level: {severity}. Valid severity levels are: 2=High, 3=Medium, 4=Low, 25=Security Incident";
            _logger.LogInternalError(errorMessage);
            throw new ArgumentException(errorMessage, nameof(severity));
        }

        discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
        var result = await _icmApiClient.ChangeSeverityAsync(incidentId, severity, discussionEntry);
        await AddTagToIncident(incidentId, AgentProcessedTag);
        return result;
    }

    public async Task<string> ResolveIncident(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(ResolveIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
        var result = await _icmApiClient.ResolveIncidentAsync(incidentId, discussionEntry);
        await UpdateAgentStatus(incidentId, AgentStatus.Resolved);
        return result;
    }

    public async Task<string> PostDiscussionEntry(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(PostDiscussionEntry)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntry);
        var result = await _icmApiClient.PostDiscussionEntryAsync(incidentId, discussionEntry);
        await AddTagToIncident(incidentId, AgentProcessedTag);
        return result;
    }


    public async Task<string> AddTagToIncident(string incidentId, string tag)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AddTagToIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, tag: {tag}";
        _logger.LogInternalInformation(logMessage);
        // Note: We don't call AddTagToIncident with AgentProcessedTag here to avoid potential infinite loops
        // if this method is called with AgentProcessedTag itself. The caller should handle adding the processed tag if needed.
        return await _icmApiClient.AddTagToIncident(incidentId, tag);
    }

    public async Task<string> AddKeywordToIncident(string incidentId, string keyword)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AddKeywordToIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, keyword: {keyword}";
        _logger.LogInternalInformation(logMessage);
        return await _icmApiClient.AddKeywordToIncident(incidentId, keyword);
    }

    // acknowledge_icm_incident using ICM API
    public async Task<string> AcknowledgeIncident(string incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AcknowledgeIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        //if (!_icmApiClient.IsEnabled())
        //{
        //    return "Unable to acknowledge the incident as the ICM API client is not enabled.";
        //}
        var result = await _icmApiClient.AcknowledgeIncidentAsync(incidentId);
        await UpdateAgentStatus(incidentId, AgentStatus.Acknowledged);
        return result;
    }

    public async Task<List<ExternalLink>> GetIncidentRepairItems(long incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetIncidentRepairItems)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        //if (!_icmApiClient.IsEnabled())
        //{
        //    return new List<IncidentRepairItem>()
        //        {
        //            new IncidentRepairItem
        //            {
        //               Id = -1,
        //               Title = "ICM API not enabled. No repair items can be fetched.",
        //            }
        //        };
        //}

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
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task UpdateAgentStatus(string incidentId, AgentStatus status)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(UpdateAgentStatus)}][{DateTime.UtcNow}] Updating status to '{status}' for incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);

        // Add the new status tag
        var newStatusTag = $"SREAgent_{status}";
        switch (status)
        {
            case AgentStatus.Transferred:
            case AgentStatus.Mitigated:
            case AgentStatus.Resolved:
                await AddTagToIncident(incidentId, AgentProcessedTag);
                break;
            default:
                break;
        }
        await AddTagToIncident(incidentId, newStatusTag);
    }

    /// <summary>
    /// Sets up the necessary steps when the agent starts working on an incident.
    /// </summary>
    /// <param name="incidentId">The ID of the incident.</param>
    /// <param name="incidentDetails">The details of the incident.</param>
    /// <param name="kernel">The kernel instance.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    private async Task SetupIncidentProcessing(string incidentId, Incident incidentDetails)
    {
        try
        {
            await AcknowledgeIncident(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, incidentId, "Error acknowledging incident during setup processing.");
        }

        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(SetupIncidentProcessing)}][{DateTime.UtcNow}] Setting up processing for incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);

        // Add a tag to indicate the agent has started processing
        await AddTagToIncident(incidentId, AgentProcessingTag);

        //ICMAlertConfig alertConfig = await _alertHandlerClient.GetConfigAsync(incidentDetails, kernel);

        //string agentName = alertConfig?.AgentName;

        //if (!string.IsNullOrEmpty(agentName))
        //{
        //    await AddTagToIncident(incidentId, $"SREAgent_{agentName}", kernel);
        //}
        //else
        //{
        //    var warningMessage = $"[setup_incident_processing][{DateTime.UtcNow}] AgentName could not be determined for incidentId {incidentId}. No matching AlertConfig found based on Title, TitleContains, or OwningTeam.";
        //    await kernel.LogKernelInfo(warningMessage, _logger, _teamsClient, _sessionMessageService);
        //    await AddTagToIncident(incidentId, $"SREAgent_UnnamedAgent", kernel);
        //}

        //// Escalate to human intervention if CloudInstance is not Public
        //if (incidentDetails.CloudInstance != "Public")
        //{
        //    await EscalateToHumanQueueInternal(incidentId, "CloudInstance is not Public.", kernel);
        //}
    }

    private async Task<string> EscalateToHumanQueueInternal(string incidentId, string reason)
    {
        //var logMessage = $"[escalate_to_human_queue][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        //await kernel.LogKernelInfo(logMessage, _logger, _teamsClient, _sessionMessageService);

        //await AddTagToIncident(incidentId, HumanInterventionTag, kernel);

        //var incidentDetails = await GetIncidentInfo(incidentId, kernel);

        //ICMAlertConfig alertConfig = await _alertHandlerClient.GetConfigAsync(incidentDetails, kernel);

        //string discussionEntryMessage = $"The SRE Agent would not be able to proceed further with its current set of capabilities.<br><b>Reason</b>: {reason}<br>Request Human Intervention from the current on-calls to take this further.";
        //if (alertConfig != null && !string.IsNullOrWhiteSpace(alertConfig.DefaultHumanInterventionLoop))
        //{
        //    string teamTenant = null; string teamName = null;
        //    var loopParts = alertConfig.DefaultHumanInterventionLoop.Split('/');
        //    if (loopParts.Length > 1)
        //    {
        //        teamTenant = loopParts[0];
        //        teamName = loopParts[1];
        //        var discussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntryMessage);
        //        var transferResult = await TransferIncident(incidentId, discussionEntry, teamTenant, teamName, kernel);
        //        return transferResult;
        //    }
        //}
        //var errorMessage = $"[escalate_to_human_queue][{DateTime.UtcNow}] No valid default human intervention loop found for incidentId {incidentId}. Provided value is {alertConfig.DefaultHumanInterventionLoop}. Tagged the Incident and requested the on-calls to take a look.";
        //await kernel.LogKernelInfo(errorMessage, _logger, _teamsClient, _sessionMessageService);
        //var errorDiscussionEntry = IcmPostTemplates.DiscussionEntryTemplate.Replace("POST_CONTENT_HERE", discussionEntryMessage);
        //var postResult = await PostDiscussionEntry(incidentId, errorDiscussionEntry, kernel);
        //return errorMessage;
        await Task.Yield();
        throw new NotImplementedException();
    }

    #region RelatedIncidents operation methods
    public async Task<List<string>> GetLinkedRelatedIncidentInfo(long incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetLinkedRelatedIncidentInfo)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        var relatedIncidents = await _icmApiClient.GetLinkedRelatedIncidentInfoAsync(incidentId);
        return relatedIncidents;
    }

    public async Task<string> AddRelatedIncidentLink(long incidentId, long relatedIncidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AddRelatedIncidentLink)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId} and relatedIncidentId {relatedIncidentId}";
        _logger.LogInternalInformation(logMessage);
        var result = await _icmApiClient.AddRelatedIncidentLinkAsync(incidentId, relatedIncidentId);
        return result;
    }

    public async Task<string> RemoveRelatedIncidentLink(long incidentId, long relatedIncidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(RemoveRelatedIncidentLink)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId} and relatedIncidentId {relatedIncidentId}";
        _logger.LogInternalInformation(logMessage);
        var result = await _icmApiClient.RemoveRelatedIncidentLinkAsync(incidentId, relatedIncidentId);
        return result;
    }


    #endregion

    #region ParentIncident operation methods
    public async Task<string> GetParentIncidentInfo(long incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetParentIncidentInfo)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        var parentIncidentInfo = await _icmApiClient.GetParentIncidentInfoAsync(incidentId);
        return parentIncidentInfo;
    }

    public async Task<string> AddParentIncidentLink(long incidentId, long parentIncidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AddParentIncidentLink)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId} and parentIncidentId {parentIncidentId}";
        _logger.LogInternalInformation(logMessage);
        var result = await _icmApiClient.AddParentIncidentLinkAsync(incidentId, parentIncidentId);
        return result;
    }

    public async Task<string> RemoveParentIncidentLink(long incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(RemoveParentIncidentLink)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        var result = await _icmApiClient.RemoveParentIncidentLinkAsync(incidentId);
        return result;
    }
    #endregion


    public async Task<List<string>> GetChildIncidentsInfo(long incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(GetChildIncidentsInfo)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        var childIncidents = await _icmApiClient.GetChildIncidentsInfoAsync(incidentId);
        return childIncidents;
    }

    public async Task<string> AddIncidentAttachmentFromFile(
        [Description("Incident ID")] string incidentId,
        [Description("Local file path to attach to the incident")] string filePath)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AddIncidentAttachmentFromFile)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, filePath: {filePath}";
        _logger.LogInternalInformation(logMessage);

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(incidentId))
            {
                throw new ArgumentException("Incident ID cannot be null or empty.", nameof(incidentId));
            }

            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
            }

            // Check if file exists
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            // Get file info
            var fileInfo = new FileInfo(filePath);
            var fileName = fileInfo.Name;

            // Check file type - only allow specific extensions
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".html", ".txt", ".zip", ".csv", ".json", ".xlsx", ".svg", ".jpg", ".docx",
                ".msg", ".pdf", ".log", ".eml", ".mp4", ".xml", ".gif", ".jpeg", ".dat", ".etl",
                ".sts", ".sql", ".mht", ".cab", ".evtx", ".pptx", ".sqlplan", ".alcpuprofile",
                ".7z", ".pbix", ".rar", ".one", ".yaml", ".py", ".jfif", ".wav", ".xls", ".tsv",
                ".tiff", ".tgz", ".sit", ".sgml", ".rtf", ".rdf", ".ram", ".ra", ".qt", ".ppt",
                ".pl", ".ogg", ".mpeg", ".mp3", ".midi", ".jar", ".hqx", ".gz", ".doc", ".dtd",
                ".css", ".bz2", ".bmp", ".avi", ".au"
            };

            var fileExtension = fileInfo.Extension;
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                throw new InvalidOperationException($"File type '{fileExtension}' is not allowed. Allowed file types: {string.Join(", ", allowedExtensions.OrderBy(x => x))}");
            }

            // Check file size (15MB limit as per ICM API documentation)
            const long maxFileSizeBytes = 15 * 1024 * 1024; // 15MB
            if (fileInfo.Length > maxFileSizeBytes)
            {
                throw new InvalidOperationException($"File size ({fileInfo.Length} bytes) exceeds the maximum allowed size of {maxFileSizeBytes} bytes (15MB).");
            }

            _logger.LogInternalInformation($"Reading file: {filePath} (Size: {fileInfo.Length} bytes)");

            // Read file and convert to base64
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);
            string base64Content = Convert.ToBase64String(fileBytes);

            _logger.LogInternalInformation($"Successfully converted file to base64. Base64 length: {base64Content.Length} characters");

            // Call the ICM API client to add the attachment
            var result = await _icmApiClient.AddIncidentAttachment(incidentId, fileName, base64Content);

            _logger.LogInternalInformation($"Successfully attached file '{fileName}' to incident {incidentId}");
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to add attachment to incident {incidentId} from file {filePath}. Error: {ex.Message}";
            _logger.LogInternalError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
    }

    public async Task<string> AddIncidentAttachmentFromContent(
        [Description("Incident ID")] string incidentId,
        [Description("Name of the file to create (with extension)")] string fileName,
        [Description("String content to attach as a file")] string content)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(AddIncidentAttachmentFromContent)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, fileName: {fileName}";
        _logger.LogInternalInformation(logMessage);

        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(incidentId))
            {
                throw new ArgumentException("Incident ID cannot be null or empty.", nameof(incidentId));
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
            }

            if (content == null)
            {
                throw new ArgumentException("Content cannot be null.", nameof(content));
            }

            // Check file type - only allow specific extensions
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".png", ".html", ".txt", ".zip", ".csv", ".json", ".xlsx", ".svg", ".jpg", ".docx",
                ".msg", ".pdf", ".log", ".eml", ".mp4", ".xml", ".gif", ".jpeg", ".dat", ".etl",
                ".sts", ".sql", ".mht", ".cab", ".evtx", ".pptx", ".sqlplan", ".alcpuprofile",
                ".7z", ".pbix", ".rar", ".one", ".yaml", ".py", ".jfif", ".wav", ".xls", ".tsv",
                ".tiff", ".tgz", ".sit", ".sgml", ".rtf", ".rdf", ".ram", ".ra", ".qt", ".ppt",
                ".pl", ".ogg", ".mpeg", ".mp3", ".midi", ".jar", ".hqx", ".gz", ".doc", ".dtd",
                ".css", ".bz2", ".bmp", ".avi", ".au"
            };

            var fileExtension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(fileExtension) || !allowedExtensions.Contains(fileExtension))
            {
                throw new InvalidOperationException($"File type '{fileExtension}' is not allowed. Allowed file types: {string.Join(", ", allowedExtensions.OrderBy(x => x))}");
            }

            // Convert string content to bytes and check size
            byte[] contentBytes = Encoding.UTF8.GetBytes(content);

            // Check content size (15MB limit as per ICM API documentation)
            const long maxFileSizeBytes = 15 * 1024 * 1024; // 15MB
            if (contentBytes.Length > maxFileSizeBytes)
            {
                throw new InvalidOperationException($"Content size ({contentBytes.Length} bytes) exceeds the maximum allowed size of {maxFileSizeBytes} bytes (15MB).");
            }

            _logger.LogInternalInformation($"Converting content to base64 for file: {fileName} (Size: {contentBytes.Length} bytes)");

            // Convert content to base64
            string base64Content = Convert.ToBase64String(contentBytes);

            _logger.LogInternalInformation($"Successfully converted content to base64. Base64 length: {base64Content.Length} characters");

            // Call the ICM API client to add the attachment
            var result = await _icmApiClient.AddIncidentAttachment(incidentId, fileName, base64Content);

            _logger.LogInternalInformation($"Successfully attached content as file '{fileName}' to incident {incidentId}");
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to add attachment to incident {incidentId} from content with fileName {fileName}. Error: {ex.Message}";
            _logger.LogInternalError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
    }

    public async Task<List<Attachment>> ListIncidentAttachments(string incidentId)
    {
        var logMessage = $"[{nameof(ICMPlugin)}_{nameof(ListIncidentAttachments)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);

        try
        {
            if (string.IsNullOrWhiteSpace(incidentId))
            {
                throw new ArgumentException("Incident ID cannot be null or empty.", nameof(incidentId));
            }

            var attachments = await _icmApiClient.ListIncidentAttachments(incidentId);
            _logger.LogInternalInformation($"Successfully retrieved {attachments.Count} attachments for incident {incidentId}");
            return attachments;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Failed to list attachments for incident {incidentId}. Error: {ex.Message}";
            _logger.LogInternalError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
    }

    //public async Task<string> DownloadIncidentAttachment(string incidentId, string attachmentId)
    //{
    //    var logMessage = $"[{nameof(ICMPlugin)}_{nameof(DownloadIncidentAttachment)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}, attachmentId {attachmentId}";
    //    _logger.LogInternalInformation(logMessage);

    //    try
    //    {
    //        if (string.IsNullOrWhiteSpace(incidentId))
    //        {
    //            throw new ArgumentException("Incident ID cannot be null or empty.", nameof(incidentId));
    //        }

    //        if (string.IsNullOrWhiteSpace(attachmentId))
    //        {
    //            throw new ArgumentException("Attachment ID cannot be null or empty.", nameof(attachmentId));
    //        }

    //        var result = await _icmApiClient.DownloadIncidentAttachment(incidentId, attachmentId);
    //        _logger.LogInternalInformation($"Successfully processed download request for attachment {attachmentId} from incident {incidentId}");
    //        return result;
    //    }
    //    catch (Exception ex)
    //    {
    //        var errorMessage = $"Failed to download attachment {attachmentId} from incident {incidentId}. Error: {ex.Message}";
    //        _logger.LogInternalError(ex, errorMessage);
    //        return errorMessage; // Return error message instead of throwing for download operations
    //    }
    //}
}
