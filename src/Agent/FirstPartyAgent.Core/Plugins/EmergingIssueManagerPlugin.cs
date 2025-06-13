using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Newtonsoft.Json;
using ReverseMarkdown;

namespace FirstPartyAgent.Core.Plugins;

/// <summary>
/// Plugin for managing emerging issues in the system
/// </summary>
public class EmergingIssueManagerPlugin
{
    private readonly Kernel _kernel;
    private readonly ILogger<EmergingIssueManagerPlugin> _logger;
    private readonly IEmergingIssueConfigService _emergingIssueConfigService;
    private readonly IICMAPIClient _icmApiClient;
    private readonly IICMWorkflowClient _icmWorkflowClient;

    private const string EmergingIssueSystemPrompt = @"You are an expert ICM analyst.
Analyze the provided ICM data, including its conversations, to extract information about the described emergency issue.
Focus on identifying actionable details. If specific information is not present in the text, use the string ""<unknown>"".

Format your response as a single JSON object with the following exact keys:
{
  ""condition"": ""Describe the specific conditions or symptoms that indicate an ICM is hitting this particular emergency issue. This should be a clear, concise statement that helps someone quickly identify if a new ICM matches this emergency pattern."",
  ""kusto_query"": ""If a Kusto query is mentioned that helps diagnose or confirm this emergency issue, provide the complete and exact query. If no query is mentioned or relevant, use '<unknown>'. Preserve formatting including line breaks by using \\n in the JSON string."",
  ""eta"": ""What is the Estimated Time for Arrival/Action/Resolution for this issue, or the next update cadence? (e.g., '2 hours', 'End of day', 'Next business day', '<unknown>') "",
  ""mitigation_internal"": ""Describe the mitigation steps recommended for internal engineering or support teams. If none, use '<unknown>'."",
  ""mitigation_customer"": ""Describe the mitigation steps or workarounds recommended for affected customers. If none, use '<unknown>'."",
  ""root_cause_analysis"": ""Summarize the root cause analysis (RCA) of the emergency issue if it's discussed. If not available or not yet determined, use '<unknown>'."",
  ""related_icms"": [""List any other ICM numbers explicitly mentioned as being related to or duplicates of this specific emergency issue. If none, use [""<unknown>""].""]
}

IMPORTANT INSTRUCTIONS:
- Adhere strictly to the JSON format and the specified keys.
- For ""kusto_query"", provide the full query text if available. Do not make up queries.
- For ""related_icms"", only include ICM numbers that are confirmed in the text to be the same underlying emergency issue.
- If any piece of information for a field cannot be found, you MUST use the string ""<unknown>"" for that field (or [""<unknown>""] for the list). Do not omit keys.
- Do not add any explanations or text outside of the JSON object itself. The entire response should be parseable as JSON.";

    /// <summary>
    /// Initializes a new instance of the EmergingIssueManagerPlugin
    /// </summary>
    public EmergingIssueManagerPlugin(
        Kernel kernel,
        ILogger<EmergingIssueManagerPlugin> logger,
        IEmergingIssueConfigService emergingIssueConfigService,
        IICMAPIClient icmApiClient,
        IICMWorkflowClient icmWorkflowClient)
    {
        _kernel = kernel;
        _logger = logger;
        _emergingIssueConfigService = emergingIssueConfigService;
        _icmApiClient = icmApiClient;
        _icmWorkflowClient = icmWorkflowClient;
    }    /// <summary>
    /// Registers a new emerging issue based on an ICM incident
    /// </summary>
    /// <param name="incidentId">The ICM incident ID to register</param>
    /// <param name="isValidated">Whether the command has been validated already</param>
    /// <returns>The ID of the registered emerging issue</returns>
    public async Task<string> RegisterEmergingIssue(
        [Description("The ICM incident ID to register as an emerging issue")] string incidentId,
        bool isValidated = false)
    {
        if (!isValidated)
        {
            return await ProcessCommand($"/register {incidentId}");
        }
        
        try
        {
            var logMessage = $"[register_emerging_issue][{DateTime.UtcNow}] Registering emerging issue for incident {incidentId}";
            _logger.LogInformation(logMessage);

            // Check if the emerging issue already exists
            try
            {
                var existingIssue = await _emergingIssueConfigService.GetEmergingIssue(incidentId);
                if (existingIssue != null)
                {
                    return $"Emerging issue for incident {incidentId} already exists with ID {existingIssue.Id}";
                }
            }
            catch (KeyNotFoundException)
            {
                // This is expected if the issue doesn't exist yet
            }

            // Get the incident details
            var incident = _icmApiClient.IsEnabled() 
                ? await _icmApiClient.GetIncidentAsync(incidentId) 
                : await _icmWorkflowClient.GetIncidentAsync(incidentId);
            
            if (incident == null)
            {
                throw new InvalidOperationException($"Could not find incident with ID {incidentId}");
            }

            // Get discussion entries
            var discussionEntries = _icmApiClient.IsEnabled()
                ? await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId)
                : await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId);

            // Combine incident information and conversation history for analysis
            string combinedContent = await CombineIncidentContent(incident, discussionEntries);
            
            // Process the combined content with LLM to extract emergency issue information
            string analysisResult = await AnalyzeEmergingIssue(combinedContent);

            // Create a new emerging issue config
            var emergingIssue = new Models.EmergingIssueConfig
            {
                IncidentId = incidentId,
                Title = incident.Title,
                OwningTeam = incident.OwningTeam ?? "Unknown",
                Content = analysisResult,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            // Register the emerging issue
            string emergingIssueId = await _emergingIssueConfigService.RegisterEmergingIssue(emergingIssue);
            
            // Save markdown file to disk (optional)
            try
            {
                SaveMarkdownFile(incidentId, analysisResult);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save markdown file for incident {IncidentId}: {Message}", incidentId, ex.Message);
            }
            
            return $"Successfully registered emerging issue with ID {emergingIssueId} for incident {incidentId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering emerging issue for incident {IncidentId}: {Message}", incidentId, ex.Message);
            throw;
        }
    }    /// <summary>
    /// Updates an existing emerging issue based on an ICM incident
    /// </summary>
    /// <param name="incidentId">The ICM incident ID to update</param>
    /// <param name="isValidated">Whether the command has been validated already</param>
    /// <returns>A message indicating successful update</returns>
    public async Task<string> UpdateEmergingIssue(
        [Description("The ICM incident ID of the emerging issue to update")] string incidentId,
        bool isValidated = false)
    {
        if (!isValidated)
        {
            return await ProcessCommand($"/update {incidentId}");
        }
        
        try
        {
            var logMessage = $"[update_emerging_issue][{DateTime.UtcNow}] Updating emerging issue for incident {incidentId}";
            _logger.LogInformation(logMessage);

            // Verify the emerging issue exists
            Models.EmergingIssueConfig existingIssue;
            try
            {
                existingIssue = await _emergingIssueConfigService.GetEmergingIssue(incidentId);
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException($"No emerging issue found for incident {incidentId}");
            }

            // Get the incident details
            var incident = _icmApiClient.IsEnabled() 
                ? await _icmApiClient.GetIncidentAsync(incidentId) 
                : await _icmWorkflowClient.GetIncidentAsync(incidentId);
            
            if (incident == null)
            {
                throw new InvalidOperationException($"Could not find incident with ID {incidentId}");
            }

            // Get discussion entries
            var discussionEntries = _icmApiClient.IsEnabled()
                ? await _icmApiClient.GetIncidentDiscussionEntriesAsync(incidentId)
                : await _icmWorkflowClient.GetIncidentDiscussionEntriesAsync(incidentId);

            // Combine incident information and conversation history for analysis
            string combinedContent = await CombineIncidentContent(incident, discussionEntries);
            
            // Process the combined content with LLM to extract emergency issue information
            string analysisResult = await AnalyzeEmergingIssue(combinedContent);

            // Update the emerging issue config
            existingIssue.Title = incident.Title;
            existingIssue.OwningTeam = incident.OwningTeam ?? existingIssue.OwningTeam;
            existingIssue.Content = analysisResult;
            existingIssue.LastModifiedDate = DateTime.UtcNow;

            // Update the emerging issue
            await _emergingIssueConfigService.UpdateEmergingIssue(existingIssue);
            
            // Save updated markdown file to disk (optional)
            try
            {
                SaveMarkdownFile(incidentId, analysisResult);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save markdown file for incident {IncidentId}: {Message}", incidentId, ex.Message);
            }
            
            return $"Successfully updated emerging issue with ID {existingIssue.Id} for incident {incidentId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating emerging issue for incident {IncidentId}: {Message}", incidentId, ex.Message);
            throw;
        }
    }/// <summary>
    /// Deregisters (removes) an emerging issue
    /// </summary>
    /// <param name="incidentId">The ICM incident ID to deregister</param>
    /// <param name="isValidated">Whether the command has been validated already</param>
    /// <returns>A message indicating successful deregistration</returns>
    public async Task<string> DeregisterEmergingIssue(
        [Description("The ICM incident ID of the emerging issue to deregister")] string incidentId,
        bool isValidated = false)
    {
        if (!isValidated)
        {
            return await ProcessCommand($"/deregister {incidentId}");
        }
        
        try
        {
            var logMessage = $"[deregister_emerging_issue][{DateTime.UtcNow}] Deregistering emerging issue for incident {incidentId}";
            _logger.LogInformation(logMessage);

            // Verify the emerging issue exists
            try
            {
                await _emergingIssueConfigService.GetEmergingIssue(incidentId);
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException($"No emerging issue found for incident {incidentId}");
            }

            // Deregister the emerging issue
            await _emergingIssueConfigService.DeregisterEmergingIssue(incidentId);
            
            return $"Successfully deregistered emerging issue for incident {incidentId}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deregistering emerging issue for incident {IncidentId}: {Message}", incidentId, ex.Message);
            throw;
        }
    }    /// <summary>
    /// Lists all emerging issues for a specific team
    /// </summary>
    /// <param name="owningTeam">The team to list emerging issues for</param>
    /// <param name="isValidated">Whether the command has been validated already</param>
    /// <returns>A list of emerging issues for the specified team</returns>
    public async Task<string> ListEmergingIssuesByTeam(
        [Description("The team to list emerging issues for")] string owningTeam,
        bool isValidated = false)
    {
        if (!isValidated)
        {
            return await ProcessCommand($"/list_by_team {owningTeam}");
        }
        
        try
        {
            var logMessage = $"[list_emerging_issues_by_team][{DateTime.UtcNow}] Listing emerging issues for team {owningTeam}";
            _logger.LogInformation(logMessage);

            if (string.IsNullOrWhiteSpace(owningTeam))
            {
                throw new ArgumentException("Owning team cannot be empty", nameof(owningTeam));
            }

            var issues = await _emergingIssueConfigService.ListEmergingIssuesByTeam(owningTeam);
            
            if (issues == null || !issues.Any())
            {
                return $"No emerging issues found for team {owningTeam}";
            }

            // Build a summarized response with just ID, incident ID, and title
            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine($"Found {issues.Count} emerging issues for team {owningTeam}:");
            summaryBuilder.AppendLine();

            foreach (var issue in issues)
            {
                summaryBuilder.AppendLine($"- Incident: {issue.IncidentId}, Title: {issue.Title}");
            }

            return summaryBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing emerging issues for team {OwningTeam}: {Message}", owningTeam, ex.Message);
            throw;
        }
    }    /// <summary>
    /// Lists all emerging issues in the system
    /// </summary>
    /// <param name="isValidated">Whether the command has been validated already</param>
    /// <returns>A list of all emerging issues</returns>
    public async Task<string> ListAllEmergingIssues(bool isValidated = false)
    {
        if (!isValidated)
        {
            return await ProcessCommand("/list_all");
        }
        
        try
        {
            var logMessage = $"[list_all_emerging_issues][{DateTime.UtcNow}] Listing all emerging issues";
            _logger.LogInformation(logMessage);

            var issues = await _emergingIssueConfigService.ListEmergingIssues();
            
            if (issues == null || !issues.Any())
            {
                return "No emerging issues found in the system";
            }

            // Group issues by team
            var issuesByTeam = issues.GroupBy(i => i.OwningTeam);
            
            // Build a summarized response with issues grouped by team
            var summaryBuilder = new StringBuilder();
            summaryBuilder.AppendLine($"Found {issues.Count} emerging issues across {issuesByTeam.Count()} teams:");
            summaryBuilder.AppendLine();

            foreach (var teamGroup in issuesByTeam)
            {
                summaryBuilder.AppendLine($"Team: {teamGroup.Key}");
                foreach (var issue in teamGroup)
                {
                    summaryBuilder.AppendLine($"  - Incident: {issue.IncidentId}, Title: {issue.Title}");
                }
                summaryBuilder.AppendLine();
            }

            return summaryBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all emerging issues: {Message}", ex.Message);
            throw;
        }
    }    /// <summary>
    /// Gets details of a specific emerging issue
    /// </summary>
    /// <param name="incidentId">The ICM incident ID to get details for</param>
    /// <param name="isValidated">Whether the command has been validated already</param>
    /// <returns>Details of the emerging issue</returns>
    public async Task<string> GetEmergingIssueDetails(
        [Description("The ICM incident ID of the emerging issue to get details for")] string incidentId,
        bool isValidated = false)
    {
        if (!isValidated)
        {
            return await ProcessCommand($"/details {incidentId}");
        }
        
        try
        {
            var logMessage = $"[get_emerging_issue_details][{DateTime.UtcNow}] Getting details for emerging issue with incident {incidentId}";
            _logger.LogInformation(logMessage);

            // Get the emerging issue
            Models.EmergingIssueConfig issue;
            try
            {
                issue = await _emergingIssueConfigService.GetEmergingIssue(incidentId);
            }
            catch (KeyNotFoundException)
            {
                throw new InvalidOperationException($"No emerging issue found for incident {incidentId}");
            }
            
            // Check if the content is already in Markdown format
            if (issue.Content != null && issue.Content.StartsWith("# Emerging Issue Analysis"))
            {
                // Content is already in Markdown format, return it directly
                return issue.Content;
            }
            
            // Check if content is JSON and convert to Markdown
            try
            {
                if (issue.Content != null && (issue.Content.StartsWith("{") || issue.Content.Contains("\"condition\"")))
                {
                    // Try to convert JSON to Markdown format
                    return ConvertJsonToMarkdown(issue.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error converting content to Markdown for incident {IncidentId}: {Message}", incidentId, ex.Message);
            }

            // Fallback to original behavior if conversion fails
            // Format the content data
            dynamic contentObj = null;
            try
            {
                contentObj = JsonConvert.DeserializeObject<dynamic>(issue.Content);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deserializing emerging issue content: {Message}", ex.Message);
            }

            // Build a detailed response
            var detailsBuilder = new StringBuilder();
            detailsBuilder.AppendLine($"Emerging Issue Details for Incident {issue.IncidentId}:");
            detailsBuilder.AppendLine($"Title: {issue.Title}");
            detailsBuilder.AppendLine($"Owning Team: {issue.OwningTeam}");
            detailsBuilder.AppendLine($"Created: {issue.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")} UTC");
            detailsBuilder.AppendLine($"Last Modified: {issue.LastModifiedDate.ToString("yyyy-MM-dd HH:mm:ss")} UTC");
            detailsBuilder.AppendLine();
            
            if (contentObj != null)
            {
                detailsBuilder.AppendLine("Analysis:");
                detailsBuilder.AppendLine($"Condition: {contentObj.condition}");
                detailsBuilder.AppendLine();
                
                if (contentObj.kusto_query != null && contentObj.kusto_query.ToString() != "<unknown>")
                {
                    detailsBuilder.AppendLine("Kusto Query:");
                    detailsBuilder.AppendLine($"{contentObj.kusto_query}");
                    detailsBuilder.AppendLine();
                }
                
                detailsBuilder.AppendLine($"ETA: {contentObj.eta}");
                detailsBuilder.AppendLine();
                
                if (contentObj.mitigation_internal != null && contentObj.mitigation_internal.ToString() != "<unknown>")
                {
                    detailsBuilder.AppendLine("Internal Mitigation:");
                    detailsBuilder.AppendLine($"{contentObj.mitigation_internal}");
                    detailsBuilder.AppendLine();
                }
                
                if (contentObj.mitigation_customer != null && contentObj.mitigation_customer.ToString() != "<unknown>")
                {
                    detailsBuilder.AppendLine("Customer Mitigation:");
                    detailsBuilder.AppendLine($"{contentObj.mitigation_customer}");
                    detailsBuilder.AppendLine();
                }
                
                if (contentObj.root_cause_analysis != null && contentObj.root_cause_analysis.ToString() != "<unknown>")
                {
                    detailsBuilder.AppendLine("Root Cause Analysis:");
                    detailsBuilder.AppendLine($"{contentObj.root_cause_analysis}");
                    detailsBuilder.AppendLine();
                }
                
                if (contentObj.related_icms != null && contentObj.related_icms.Count > 0 && contentObj.related_icms[0].ToString() != "<unknown>")
                {
                    detailsBuilder.AppendLine("Related ICMs:");
                    foreach (var relatedIcm in contentObj.related_icms)
                    {
                        detailsBuilder.AppendLine($"- {relatedIcm}");
                    }
                }
            }
            else
            {
                detailsBuilder.AppendLine("Analysis Content:");
                detailsBuilder.AppendLine(issue.Content);
            }

            return detailsBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting details for emerging issue with incident {IncidentId}: {Message}", incidentId, ex.Message);
            throw;
        }
    }    /// <summary>
    /// Processes a command and validates it has the required format (starts with /)
    /// </summary>
    /// <param name="command">The command text to process</param>
    /// <returns>A response based on the command execution or validation failure</returns>
    [KernelFunction("process_command"), Description("Process the user's exact message as a command. IMPORTANT: Always pass the user's exact input without modifying it. This function will handle the validation of whether it starts with '/' internally.")]
    public async Task<string> ProcessCommand(
        [Description("The EXACT user input, unmodified. DO NOT add the '/' prefix or reformat the input in any way. This function will check if it starts with '/' internally.")] string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "Please provide a valid command. Commands must start with '/' (e.g., /register 12345678).";
        }

        // Trim any leading/trailing whitespace
        command = command.Trim();

        // Check if the command starts with /
        if (!command.StartsWith("/"))
        {
            return "Commands must start with '/'. For example: /register 12345678, /update 12345678, /deregister 12345678, /list_all, /list_by_team TeamName, /details 12345678";
        }

        try
        {
            // Remove the / prefix and split by whitespace
            string[] parts = command.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length == 0)
            {
                return "Please provide a valid command operation after the '/' character.";
            }

            string operation = parts[0].ToLowerInvariant();
            
            switch (operation)
            {
                case "register":
                    if (parts.Length < 2)
                    {
                        return "Please provide an incident ID after /register.";
                    }
                    return await RegisterEmergingIssue(parts[1], isValidated: true);
                    
                case "update":
                    if (parts.Length < 2)
                    {
                        return "Please provide an incident ID after /update.";
                    }
                    return await UpdateEmergingIssue(parts[1], isValidated: true);
                    
                case "deregister":
                    if (parts.Length < 2)
                    {
                        return "Please provide an incident ID after /deregister.";
                    }
                    return await DeregisterEmergingIssue(parts[1], isValidated: true);
                    
                case "list_all":
                    return await ListAllEmergingIssues(isValidated: true);
                    
                case "list_by_team":
                    if (parts.Length < 2)
                    {
                        return "Please provide a team name after /list_by_team.";
                    }
                    // Join remaining parts to allow team names with spaces
                    string teamName = string.Join(" ", parts.Skip(1));
                    return await ListEmergingIssuesByTeam(teamName, isValidated: true);
                    
                case "details":
                    if (parts.Length < 2)
                    {
                        return "Please provide an incident ID after /details.";
                    }
                    return await GetEmergingIssueDetails(parts[1], isValidated: true);
                    
                default:
                    return $"Unknown command: {operation}. Valid commands are: /register, /update, /deregister, /list_all, /list_by_team, /details.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing command: {Command}, Error: {Message}", command, ex.Message);
            return $"Error processing command: {ex.Message}";
        }
    }

    /// <summary>
    /// Combines the incident information and conversation history for analysis
    /// </summary>
    private async Task<string> CombineIncidentContent(Incident incident, List<DiscussionEntry> discussionEntries)
    {
        var combinedBuilder = new StringBuilder();
        
        // Add incident info
        combinedBuilder.AppendLine($"ICM Number: {incident.IncidentId}");
        combinedBuilder.AppendLine($"ICM Title: {incident.Title}");
        combinedBuilder.AppendLine();
          // Add incident summary and description
        // Convert HTML to markdown if the summary contains HTML tags
        string summary = incident.Summary;
        if (summary != null && summary.Contains("<"))
        {
            try
            {
                summary = ConvertHtmlToMarkdown(summary, "summary");
            }
            catch (Exception ex)
            {
                // Log the error and continue with original summary
                _logger.LogError(ex, "Error converting HTML to markdown for summary");
            }
        }
        
        combinedBuilder.AppendLine(summary);
        combinedBuilder.AppendLine();
        
        // Add conversation history
        combinedBuilder.AppendLine("---CONVERSATION HISTORY---");
        if (discussionEntries != null && discussionEntries.Any())
        {
            foreach (var entry in discussionEntries.OrderBy(e => e.Date))
            {
                combinedBuilder.AppendLine($"[{entry.Date.ToString("yyyy-MM-dd HH:mm:ss")}] :");
                  // Convert discussion entry from HTML to markdown if it contains HTML tags
                string entryText = entry.Text;
                if (entryText != null && entryText.Contains("<"))
                {                    try
                    {
                        entryText = ConvertHtmlToMarkdown(entryText, "discussion entry");
                    }
                    catch (Exception ex)
                    {
                        // Log the error and continue with original text
                        _logger.LogError(ex, "Error converting HTML to markdown for discussion entry");
                    }
                }
                
                combinedBuilder.AppendLine(entryText);
                combinedBuilder.AppendLine();
            }
        }
        else
        {
            combinedBuilder.AppendLine("No conversation history available.");
        }
        
        return combinedBuilder.ToString();
    }    /// <summary>
    /// Analyzes the combined content to extract emergency issue information and formats it as Markdown
    /// </summary>
    private async Task<string> AnalyzeEmergingIssue(string combinedContent)
    {
        try
        {
            _logger.LogInformation("Analyzing emerging issue content: {Length} chars", combinedContent?.Length ?? 0);
            
            // Create chat history with system prompt and user message
            var history = new ChatHistory();
            history.AddSystemMessage(EmergingIssueSystemPrompt);
            history.AddUserMessage(combinedContent);
            
            // Get chat completion service
            var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
            
            // Get response from chat completion service
            var result = await chatCompletionService.GetChatMessageContentAsync(
                history,
                executionSettings: new AzureOpenAIPromptExecutionSettings
                {
                    Temperature = 0
                },
                kernel: _kernel);
            
            // Convert JSON result to Markdown format
            return ConvertJsonToMarkdown(result.Content);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing emerging issue: {Message}", ex.Message);
            throw new Exception("Error analyzing emerging issue. Please try again later.", ex);
        }}
      /// <summary>
    /// Converts HTML content to Markdown format
    /// </summary>
    /// <param name="htmlContent">The HTML content to convert</param>
    /// <param name="contentType">A description of the content type for logging purposes</param>
    /// <returns>Markdown formatted content</returns>
    private string ConvertHtmlToMarkdown(string htmlContent, string contentType)
    {
        if (htmlContent == null || !htmlContent.Contains("<"))
        {
            return htmlContent;
        }
        
        // Process complex content using TextProcessing helpers
        List<(string, string)> base64Images = new List<(string, string)>();
        
        // Remove and replace binary image data with placeholders
        htmlContent = TextProcessingHelpers.StripBase64Images(htmlContent, base64Images);
        
        // Convert HTML to markdown
        var config = new ReverseMarkdown.Config
        {
            // Keep original HTML for unsupported tags
            UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.PassThrough,
            // Drop attributes that aren't needed for markdown
            RemoveComments = true,
            SmartHrefHandling = true
        };
        
        var converter = new ReverseMarkdown.Converter(config);
        htmlContent = converter.Convert(htmlContent);
        
        // Replace image placeholders with descriptive text
        for (int i = 0; i < base64Images.Count; i++)
        {
            htmlContent = htmlContent.Replace($"####{i+1}####", $"[Image {i + 1}: Binary data removed]");
        }
        
        // Clean up any remaining HTML tags that didn't get converted properly
        htmlContent = System.Text.RegularExpressions.Regex.Replace(htmlContent, "<[^>]*>", string.Empty);
        
        return htmlContent;
    }
      /// <summary>
    /// Converts JSON analysis result to Markdown format
    /// </summary>
    /// <param name="jsonContent">The JSON content to convert</param>
    /// <returns>Markdown formatted content</returns>
    private string ConvertJsonToMarkdown(string jsonContent)
    {
        try
        {
            // Parse JSON content
            dynamic analysisData = JsonConvert.DeserializeObject<dynamic>(jsonContent);
            
            if (analysisData == null)
            {
                _logger.LogWarning("Failed to parse JSON analysis result");
                return jsonContent; // Return original content if parsing fails
            }
            
            // Create markdown builder
            var markdownBuilder = new StringBuilder();
            
            // Add title and metadata section
            markdownBuilder.AppendLine("# Emerging Issue Analysis");
            markdownBuilder.AppendLine();
            markdownBuilder.AppendLine($"*Generated on: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");
            markdownBuilder.AppendLine();
            
            // Add condition section
            markdownBuilder.AppendLine("## Condition");
            markdownBuilder.AppendLine();
            markdownBuilder.AppendLine(analysisData.condition?.ToString() ?? "<unknown>");
            markdownBuilder.AppendLine();
            
            // Add Kusto query section if available
            if (analysisData.kusto_query != null && analysisData.kusto_query.ToString() != "<unknown>")
            {
                markdownBuilder.AppendLine("## Diagnostic Query");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine("```kusto");
                markdownBuilder.AppendLine(analysisData.kusto_query.ToString());
                markdownBuilder.AppendLine("```");
                markdownBuilder.AppendLine();
            }
            
            // Add ETA section
            markdownBuilder.AppendLine("## Estimated Time for Action/Resolution");
            markdownBuilder.AppendLine();
            markdownBuilder.AppendLine(analysisData.eta?.ToString() ?? "<unknown>");
            markdownBuilder.AppendLine();
            
            // Add mitigation sections if available
            if (analysisData.mitigation_internal != null && analysisData.mitigation_internal.ToString() != "<unknown>")
            {
                markdownBuilder.AppendLine("## Internal Mitigation Steps");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine(analysisData.mitigation_internal.ToString());
                markdownBuilder.AppendLine();
            }
            
            if (analysisData.mitigation_customer != null && analysisData.mitigation_customer.ToString() != "<unknown>")
            {
                markdownBuilder.AppendLine("## Customer Mitigation Steps");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine(analysisData.mitigation_customer.ToString());
                markdownBuilder.AppendLine();
            }
            
            // Add root cause analysis section if available
            if (analysisData.root_cause_analysis != null && analysisData.root_cause_analysis.ToString() != "<unknown>")
            {
                markdownBuilder.AppendLine("## Root Cause Analysis");
                markdownBuilder.AppendLine();
                markdownBuilder.AppendLine(analysisData.root_cause_analysis.ToString());
                markdownBuilder.AppendLine();
            }
            
            // Add related ICMs section if available
            if (analysisData.related_icms != null && analysisData.related_icms.Count > 0 && analysisData.related_icms[0].ToString() != "<unknown>")
            {
                markdownBuilder.AppendLine("## Related ICMs");
                markdownBuilder.AppendLine();
                foreach (var relatedIcm in analysisData.related_icms)
                {
                    markdownBuilder.AppendLine($"- {relatedIcm}");
                }
                markdownBuilder.AppendLine();
            }
            
            return markdownBuilder.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting JSON to Markdown: {Message}", ex.Message);
            return jsonContent; // Return original content if conversion fails
        }
    }
    
    /// <summary>
    /// Saves the markdown content to a file
    /// </summary>
    /// <param name="incidentId">The incident ID to use in the filename</param>
    /// <param name="markdownContent">The markdown content to save</param>
    private void SaveMarkdownFile(string incidentId, string markdownContent)
    {
        try
        {
            // Create output directory if it doesn't exist
            string outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emerging_issues");
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            
            // Construct filename
            string fileName = $"emerging_summary_icm_{incidentId}.md";
            string filePath = Path.Combine(outputDir, fileName);
            
            // Write markdown content to file
            File.WriteAllText(filePath, markdownContent);
            
            _logger.LogInformation("Saved markdown file for incident {IncidentId} to {FilePath}", incidentId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving markdown file for incident {IncidentId}: {Message}", incidentId, ex.Message);
            throw;
        }
    }
}
