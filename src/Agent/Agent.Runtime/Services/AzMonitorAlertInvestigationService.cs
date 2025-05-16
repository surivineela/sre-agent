// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Logging;
using Agent.Plugins;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class AzMonitorAlertInvestigationService : IAzMonitorAlertInvestigationService
{
    private readonly ILogger<AzMonitorAlertInvestigationService> _logger;
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly ILogQueryService _logQueryService;
    private readonly IChatClient _chatClient;
    private readonly IGraphDBPlugin _graphDBPlugin;

    public AzMonitorAlertInvestigationService(
        IThreadRepository repository,
        ILogQueryService logQueryService,
        IChatClient chatClient,
        IAgentInboundCommunicationService inboundCommunicationService,
        IGraphDBPlugin graphDBPlugin,
        ILogger<AzMonitorAlertInvestigationService> logger)
    {
        _repository = repository;
        _inboundCommunicationService = inboundCommunicationService;
        _logQueryService = logQueryService;
        _chatClient = chatClient;
        _graphDBPlugin = graphDBPlugin;
        _logger = logger;
    }

    public async Task<string> AnalyzeActivityLogsForResource(AlertItem alert, Thread alertThread)
    {
        try
        {
            var resourceId = alert.Properties.Essentials.TargetResource;

            // Fetch and summarize activity logs using the existing GraphDBPlugin method
            var activityLogSummary = await _graphDBPlugin.FetchAndSummarizeActivityLogs(
                resourceId,
                hoursBack: 1,
                threadId: alertThread.Id
            );

            _logger.LogInternalInformation($"The summarized activity log for the alert is: {activityLogSummary}");

            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogInternalWarning("No agent context found for thread");
                return "Failed to get activity log summary for the resource!";
            }

            var agentContext = agentContexts.First();

            string alertDetails = GetAlertInfoAsPrompt(alert);

            // Custom prompt for analyzing activity logs
            string activityLogInstructions = @"Review these activity logs and identify:
                                            - Configuration changes closely preceding the alert
                                            - Administrative actions with timestamps that correlate with the issue
                                            - Deployments or updates that could have introduced issues and happened closely preceding the alert
                                            - Evaluate the correlation of each activity based on timeline. The closer to the alert trigger time, the more likely it's correlated
                                            - ONLY mention activities that likely caused the alert
                                            - Focus on WRITE actions, e.g., Create, Update.
                                            - Ignore routine operations unrelated to the issue";

            string promptWithPlaceholders = ChainPrompt
                .Replace("{{AlertDetails}}", alertDetails)
                .Replace("{{ContentAnalysisInstructions}}", activityLogInstructions)
                .Replace("{{ContentToAnalyze}}", activityLogSummary);

            string llmSummary = await SummarizeWithLLM(promptWithPlaceholders);

            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        JsonSerializer.Serialize(new
                        {
                            description = "Summary of analysis of resource's activity logs for recent configuration changes and operations",
                            llmSummary,
                        }
                    )));

            return llmSummary;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error analyzing activity logs: {ex.Message}");
            return "Encountered an error while analyzing activity logs. Continuing with the investigation using other data sources.";
        }
    }

    public async Task<string> AnalyzeConnectedComponents(AlertItem alert, Thread alertThread)
    {
        try
        {
            var resourceId = alert.Properties.Essentials.TargetResource;

            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogInternalWarning("No agent context found for thread");
                return "Error: No agent context found for thread. Continuing investigation using other data sources.";
            }

            var connectedComponents = await _graphDBPlugin.GetApplicationComponentsSummary(resourceId);

            var healthSummary = new StringBuilder();

            if (connectedComponents != null && connectedComponents.Any())
            {
                var primaryResourceHealth = await _graphDBPlugin.GetApplicationHealthInfoAsync(resourceId);

                healthSummary.AppendLine("## Overall Health Assessment");

                if (primaryResourceHealth != null)
                {
                    healthSummary.AppendLine($"### Primary Resource Health ({resourceId})");
                    healthSummary.AppendLine($"- Health summary: {primaryResourceHealth}");
                    healthSummary.AppendLine();
                }

                // Process each connected component
                healthSummary.AppendLine("### Connected Components Health Details");
                foreach (var component in connectedComponents)
                {
                    // Get health information for this component
                    var componentHealth = await _graphDBPlugin.GetApplicationHealthInfoAsync(component.Id);

                    if (componentHealth != null)
                    {
                        healthSummary.AppendLine($"#### {component.Name} ({component.Id})");
                        healthSummary.AppendLine($"- Health summary: {componentHealth}");
                        healthSummary.AppendLine();
                    }
                }
            }

            string healthAnalysisInstructions = @"Analyze health data for this Azure resource and its connected components:

1. Identify specific metric deviations in the primary resource that match the alert condition
2. Flag any connected components showing errors/degradation (CPU, memory, availability)
3. Note any correlation between component health and alert timing
4. Specify numeric values for important metrics where available

Important:
- Missing health data for some components is normal
- Focus ONLY on significant deviations from normal metrics
- Quantify the deviation where possible (e.g., '95% CPU vs normal 60%')";


            string alertDetails = GetAlertInfoAsPrompt(alert);


            string healthPromptWithPlaceholders = ChainPrompt
                .Replace("{{AlertDetails}}", alertDetails)
                .Replace("{{ContentAnalysisInstructions}}", healthAnalysisInstructions)
                .Replace("{{ContentToAnalyze}}", healthSummary.ToString());

            var llmHealthSummary = await SummarizeWithLLM(healthPromptWithPlaceholders);

            var agentContext = agentContexts.First();

            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        JsonSerializer.Serialize(new
                        {
                            description = "Summary of analysis of connected components in the knowledge graph that might be impacting this resource.",
                            llmHealthSummary,
                        }
                    )));

            return llmHealthSummary;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error setting up connected components analysis: {ex.Message}");
            return "Encountered an error while setting up connected components analysis. Continuing with the investigation using other data sources";
        }
    }

    public async Task<string> GetApplicationHealthAsync(AlertItem alert, Thread alertThread)
    {
        var essentials = alert.Properties.Essentials;
        var alertRule = essentials.AlertRule;
        var targetResource = essentials.TargetResource;
        var resourceId = new ResourceIdentifier(targetResource);

        try
        {
            var appHealthInfo = await _graphDBPlugin.GetApplicationHealthInfoAsync(targetResource);

            // If health info is available, add it to the thread
            if (appHealthInfo != null)
            {
                // add to the agent context for reference in future reasoning
                var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
                if (agentContexts == null || !agentContexts.Any())
                {
                    _logger.LogInternalWarning("No agent context found for thread");
                    return "Error: No agent context found for thread. Continuing investigation using other data sources.";
                }

                string alertDetailsForAppHealth = GetAlertInfoAsPrompt(alert);

                string appHealthAnalysisInstructions = @"Analyze this health information focusing ONLY on:
                    - Specific metrics showing deviation from baseline with exact values
                    - Critical performance bottlenecks with quantifiable impact
                    - Direct correlation between health patterns and the alert condition
                    - Resource constraints with numerical thresholds exceeded

                    Refer to following examples when you do the analysis, you do not need to follow the examples exactly but they should help you understand how to think:
                    Example analysis - 1:
                    - App health info: The request metrics have a large value while with CPU percentage is over 60%, memory percentage is normal
                    - Your analysis: The app is experiencing high request volume, causing the CPU percentage to increase, indicating a potential resource constraint.

                    Example analysis - 2:
                    - App health info: The CPU percentage is extremely high (over 90%) while the request metrics are normal, memory percentage is normal
                    - Your analysis: The app is not having high traffic but is still experiencing high CPU usage, indicating a potential performance issue, like deadlocks, infinite loops or inefficient queries.

                    Example analysis - 3:
                    - App health info: The availability is low (less than 50%) while the memory percentage is high (over 70%), the request metrics are normal 
                    - Your analysis: The high memory consumption is likely causing the app to be unavailable, indicating potential memory leaks or resource constraint.
                     
                    Avoid general observations. Include specific times, durations, and metric values.";

                string appHealthPromptWithPlaceholders = ChainPrompt
                    .Replace("{{AlertDetails}}", alertDetailsForAppHealth)
                    .Replace("{{ContentAnalysisInstructions}}", appHealthAnalysisInstructions)
                    .Replace("{{ContentToAnalyze}}", appHealthInfo);

                var llmSummary = await SummarizeWithLLM(appHealthPromptWithPlaceholders);

                var agentContext = agentContexts.First();

                await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                       Guid.NewGuid(),
                       agentContext.Id,
                       ReasoningMessageRoleEnum.System,
                       JsonSerializer.Serialize(new
                       {
                           description = "Summary of analysis of application health for this alert.",
                           llmSummary,
                       }
                   )));

                return llmSummary;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error during alert investigation flow: {ex.Message}");
            return $"Error during alert investigation flow: {ex.Message}";
        }

        return "No health scorecard data is available for this resource. Continuing with investigation using other data sources.";
    }

    public async Task<string> AnalyzeLogQueries(AlertItem alert, Thread alertThread)
    {
        string defaultMessage = "Unable to analyze log queries! Continuing with other investigation methods.";
        try
        {
            //string title = "Examining Log Analytics queries and correlating results with alert patterns";

            var essentials = alert.Properties.Essentials;
            var alertRule = essentials.AlertRule;
            var targetResource = essentials.TargetResource;
            var resourceIdentifier = new ResourceIdentifier(targetResource);
            string subscriptionId = resourceIdentifier.SubscriptionId;

            if (string.IsNullOrEmpty(subscriptionId))
            {
                _logger.LogInternalWarning("Subscription id cannot be null or empty!");
                return defaultMessage;
            }

            // Get agent context
            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogInternalWarning("No agent context found for thread");
                return defaultMessage;
            }
            var agentContext = agentContexts.First();

            // Get all saved queries for this subscription
            var savedQueries = await _logQueryService.GetSavedQueriesForSubscriptionAsync(subscriptionId);
            _logger.LogInternalInformation($"Found {savedQueries.Count()} saved queries from Azure Log Analytics workspace!");

            // Prepare a summary of queries to pass to the LLM
            var querySummaries = savedQueries.Select(q => new
            {
                Id = q.Id,
                Name = q.Properties?.DisplayName ?? "Unnamed Query",
                Description = q.Properties?.Description ?? "No description",
                Body = q.Properties?.Body ?? "No query body",
                Tags = q.Properties?.Tags?.Labels ?? new List<string>()
            }).ToList();

            // Ask the LLM to identify relevant queries
            string alertDetails = GetAlertInfoAsPrompt(alert);

            string queriesInfo = JsonSerializer.Serialize(querySummaries);

            // Create the prompt for the LLM
            string relevantQueriesPrompt = $@"
                You are an Azure SRE Agent investigating an alert. Your task is to identify saved queries that might help understand this alert.

                # Alert Details
                ```
                {alertDetails}
                ```

                # Available Saved Queries
                ```json
                {queriesInfo}
                ```

                Please identify the queries that are most relevant to understanding this alert. Consider:
                1. Queries that target similar resource types. ** CRITICAL ** Pick at most 5 relevant queries.
                2. Queries that look for errors, failures, or performance issues
                3. Queries with tags or descriptions that match the alert's context

                Return ONLY a JSON array of the selected query IDs with a brief explanation for each. Format:
                [
                  {{
                    ""id"": ""query-id-1""
                  }},
                  ...
                ]
                DONOT Add any other text or else my JSON deserialization is going to fail. 
                Limit your selection to the 5 most relevant queries. If none are relevant, return an empty array.";

            _logger.LogDebug($"Sending prompt to LLM to identify relevant queries");

            var options = new ChatOptions
            {
                Temperature = (float)0.1
            };

            var relevantQueriesJson = await _chatClient.GetResponseAsync(
                relevantQueriesPrompt,
                options);

            List<RelevantQuery> relevantQueries;
            try
            {
                relevantQueries = JsonSerializer.Deserialize<List<RelevantQuery>>(relevantQueriesJson.Text) ?? new List<RelevantQuery>();
                _logger.LogInternalInformation($"LLM identified {relevantQueries.Count} relevant queries");
            }
            catch (JsonException ex)
            {
                _logger.LogInternalError(ex, $"Error parsing LLM response for relevant queries: {ex.Message}");
                relevantQueries = new List<RelevantQuery>();
            }

            if (relevantQueries.Count == 0)
            {
                return "No relevant saved queries were found for this alert. Continuing with other investigation methods.";
            }

            // Execute the relevant queries
            var queryResults = new List<QueryExecutionResult>();

            foreach (var relevantQuery in relevantQueries)
            {
                var query = savedQueries.FirstOrDefault(q => q.Id == relevantQuery.Id);
                if (query == null || string.IsNullOrEmpty(query.Properties?.Body))
                {
                    _logger.LogInternalWarning($"Query with ID {relevantQuery.Id} not found or has no body");
                    continue;
                }

                try
                {
                    _logger.LogInternalInformation($"Executing query: {query.Properties.DisplayName} (ID: {query.Id})");

                    // Execute the query - time range is 24 hours before the alert to now
                    var startTime = ParseDateTimeOffset(essentials.StartDateTime).AddHours(-2);
                    var endTime = DateTimeOffset.UtcNow;

                    // Currently there is no way to get workspace associated to a query.
                    // So, we are going to brute force query execution against every workspace in the subscription.
                    var queryResponse = await _logQueryService.ExecuteLogQueryAsync(
                        subscriptionId,
                        query.Properties.Body,
                        startTime,
                        endTime);

                    queryResults.Add(new QueryExecutionResult
                    {
                        QueryId = query.Id,
                        QueryName = query.Properties.DisplayName,
                        QueryBody = query.Properties.Body,
                        Result = queryResponse
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error executing query {query.Properties.DisplayName} (ID: {query.Id}): {ex.Message}");
                }
            }

            if (queryResults.Count == 0)
            {
                return "Found potentially relevant queries but was unable to execute them successfully. Continuing with other investigation methods.";
            }

            // Create the analysis prompt
            string queryResultsJson = JsonSerializer.Serialize(queryResults);

            string alertDetailsForLogQueries = GetAlertInfoAsPrompt(alert);

            // Custom instructions for log query analysis
            string logQueryAnalysisInstructions = @"Analyze these query results in relation to the alert:
1. Identify log entries that directly explain the alert (errors, exceptions)
2. Report specific metric values/thresholds that were exceeded
3. Note exact timestamps of relevant events relative to the alert
4. Quantify the scale of any issue (e.g., error rate, latency increase)

Include query names when referencing results.
DO NOT suggest generic root causes without specific evidence.
ONLY mention findings directly relevant to this alert condition.";

            string queryPromptWithPlaceholders = ChainPrompt
                .Replace("{{AlertDetails}}", alertDetailsForLogQueries)
                .Replace("{{ContentAnalysisInstructions}}", logQueryAnalysisInstructions)
                .Replace("{{ContentToAnalyze}}", queryResultsJson);

            _logger.LogDebug($"Sending prompt to LLM to analyze query results");
            var analysisResult = await SummarizeWithLLM(queryPromptWithPlaceholders);

            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        JsonSerializer.Serialize(new
                        {
                            description = "Summary of analysis of User's saved Log Queries in Azure Monitor.",
                            analysisResult,
                        }
                    )));

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error analyzing query packs: {ex.Message}");
            return "Encountered an error while analyzing saved queries. Continuing with the investigation using other data sources.";
        }
    }

    public async Task<string> GetMetricsForResource(AlertItem alert, Thread alertThread)
    {
        var resourceId = alert.Properties.Essentials.TargetResource;

        var resourceType = alert.Properties.Essentials.TargetResourceType;

        try
        {

            // Get agent context
            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogInternalWarning("No agent context found for thread");
                return "No metric summary available. Use other data points to continue the investigation!";
            }

            var agentContext = agentContexts.First();

            // Create a reasoning message for the agent to decide which metrics to retrieve
            // we already have container app and web app plugins - let the agent figure out which is the best tool to call.
            var alertDetails = JsonSerializer.Serialize(new
            {
                alertRule = alert.Properties.Essentials.AlertRule,
                description = alert.Properties.Essentials.Description,
                severity = alert.Properties.Essentials.Severity,
                resourceId,
                resourceType,
                signalType = alert.Properties.Essentials.SignalType,
                monitorCondition = alert.Properties.Essentials.MonitorCondition
            });

        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error setting up metrics investigation for resource {resourceId}: {ex.Message}");
        }

        return "No metric summary available. Use other data points to continue the investigation!";
    }

    #region Helper Methods

    private DateTimeOffset ParseDateTimeOffset(string? value)
    {
        DateTimeOffset createdAt;
        if (!string.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, out var parsedDate))
        {
            createdAt = parsedDate;
        }
        else
        {
            createdAt = DateTimeOffset.UtcNow;
            _logger.LogInternalWarning($"Could not parse start time {value}, using current time instead");
        }

        return createdAt;
    }

    private string GetAlertInfoAsPrompt(AlertItem alert)
    {
        if (alert == null)
        {
            return "Alert information unavailable";
        }

        var essentials = alert.Properties?.Essentials;

        // Is Unknown the best fallback?
        return $@"Azure Monitor Alert Context:
                ID: {alert.Id ?? "Unknown"}
                Name: {alert.Name ?? "Unknown"}
                Rule: {essentials?.AlertRule ?? "Unknown"}
                Severity: {essentials?.Severity ?? "Unknown"}
                Condition: {essentials?.MonitorCondition ?? "Unknown"}
                Description: {essentials?.Description ?? "Unknown"}
                Resource: {essentials?.TargetResourceName ?? essentials?.TargetResource ?? "Unknown"}
                Type: {essentials?.TargetResourceType ?? "Unknown"}
                Time: {essentials?.StartDateTime ?? "Unknown"}";
    }

    private async Task<string> SummarizeWithLLM(string prompt)
    {
        try
        {
            var message = new ChatMessage(ChatRole.System, prompt);

            var options = new ChatOptions
            {
                Temperature = (float)0.1,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await _chatClient.GetResponseAsync(new List<ChatMessage> { message }, options);
            return response.Text;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error summarizing content with llm.");
            return $"Error summarizing with LLM: {ex.Message}";
        }
    }

    private readonly string ChainPrompt =
        @"You are an Azure SRE Agent investigating an alert. Here are the alert details:
---
{{AlertDetails}}
---

{{ContentAnalysisInstructions}}

---
{{ContentToAnalyze}}
---

Produce a concise investigation report with:

## Observations
- One critical insight (max 1-2 sentences)
- 2-3 key data points that matter most

## Hypotheses  
For each hypothesis (max 2):
- **Hypothesis:** One-sentence statement with **Confidence:** High/Medium/Low

DO NOT use generic suggestions. BE SPECIFIC to this alert and its context.
Avoid duplicate information. Use emojis sparingly for readability.
CRITICAL: Keep the entire response under 200 words.";

    #endregion
}

#region Models

// Helper classes for the query analysis
public class RelevantQuery
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
}

public class QueryExecutionResult
{
    public string QueryId { get; set; }
    public string QueryName { get; set; }
    public string QueryBody { get; set; }
    public string Result { get; set; }
}

#endregion
