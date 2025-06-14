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
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services.AzMonitorAlertInvestigationService;

public class AzMonitorAlertInvestigationService : IAzMonitorAlertInvestigationService
{
    private readonly ILogger<AzMonitorAlertInvestigationService> _logger;
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly ILogQueryService _logQueryService;
    private readonly IChatClient _chatClient;
    private readonly IGraphDBPlugin _graphDBPlugin;
    private readonly IAzureMonitorMetricsPlugin _azureMonitorMetricsPlugin;

    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1) };

    public AzMonitorAlertInvestigationService(
        IThreadRepository repository,
        ILogQueryService logQueryService,
        [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
        IAgentInboundCommunicationService inboundCommunicationService,
        IGraphDBPlugin graphDBPlugin,
        IAzureMonitorMetricsPlugin azureMonitorMetricsPlugin,
        ILogger<AzMonitorAlertInvestigationService> logger)
    {
        _repository = repository;
        _inboundCommunicationService = inboundCommunicationService;
        _logQueryService = logQueryService;
        _chatClient = chatClient;
        _graphDBPlugin = graphDBPlugin;
        _azureMonitorMetricsPlugin = azureMonitorMetricsPlugin;
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

            var alertDetails = GetAlertInfoAsPrompt(alert);

            // Custom prompt for analyzing activity logs
            var activityLogInstructions = @"Review these activity logs and identify:
                                            - Configuration changes directly preceding the alert
                                            - Administrative actions with timestamps that correlate with the issue
                                            - Deployments or updates that could have introduced issues and happened closely preceding the alert
                                            - Evaluate the correlation of each activity based on timeline. The closer to the alert trigger time, the more likely it's correlated
                                            - ONLY mention activities that likely caused the alert
                                            - CRITICAL: Focus on WRITE actions, e.g., Create, Update. READ Actions such as ListSecrets etc. are most likely not relevant. 
                                            - Ignore routine operations unrelated to the issue";

            var promptWithPlaceholders = ChainPrompt
                .Replace("{{AlertDetails}}", alertDetails)
                .Replace("{{ContentAnalysisInstructions}}", activityLogInstructions)
                .Replace("{{ContentToAnalyze}}", activityLogSummary);

            var llmSummary = await SummarizeWithLLM(promptWithPlaceholders);

            var agentChatHistory = await _repository.GetAgentChatHistoryAsync(agentContext.Id);
            var reasoningMessage = new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        JsonSerializer.Serialize(new
                        {
                            description = "Summary of analysis of resource's activity logs for recent configuration changes and operations",
                            llmSummary,
                        }
                    ));
            await PersistReasoningMessageAsync(agentChatHistory, reasoningMessage);

            var resultWithStepIdentifier = $"ACTIVITY LOGS ANALYSIS\n{llmSummary}";
            return resultWithStepIdentifier;
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

            var healthAnalysisInstructions = @"Analyze health data for this Azure resource and its connected components:

1. Identify specific metric deviations in the primary resource that match the alert condition
2. Flag any connected components showing errors/degradation (CPU, memory, availability)
3. Note any correlation between component health and alert timing
4. Specify numeric values for important metrics where available

Important:
- Missing health data for some components is normal
- Focus ONLY on significant deviations from normal metrics
- Quantify the deviation where possible (e.g., '95% CPU vs normal 60%')";


            var alertDetails = GetAlertInfoAsPrompt(alert);


            var healthPromptWithPlaceholders = ChainPrompt
                .Replace("{{AlertDetails}}", alertDetails)
                .Replace("{{ContentAnalysisInstructions}}", healthAnalysisInstructions)
                .Replace("{{ContentToAnalyze}}", healthSummary.ToString());

            var llmHealthSummary = await SummarizeWithLLM(healthPromptWithPlaceholders);

            var agentContext = agentContexts.First();

            var agentChatHistory = await _repository.GetAgentChatHistoryAsync(agentContext.Id);
            var reasoningMessage = new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        JsonSerializer.Serialize(new
                        {
                            description = "Summary of analysis of connected components in the knowledge graph that might be impacting this resource.",
                            llmHealthSummary,
                        }
                    ));
            await PersistReasoningMessageAsync(agentChatHistory, reasoningMessage);

            var resultWithStepIdentifier = $"CONNECTED COMPONENTS ANALYSIS\n{llmHealthSummary}";
            return resultWithStepIdentifier;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error setting up connected components analysis: {ex.Message}");
            return "Encountered an error while setting up connected components analysis. Continuing with the investigation using other data sources";
        }
    }

    public async Task<string> AnalyzeApplicationHealth(AlertItem alert, Thread alertThread)
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

                var alertDetailsForAppHealth = GetAlertInfoAsPrompt(alert);

                var appHealthAnalysisInstructions = @"Analyze this health information focusing ONLY on:
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

                var appHealthPromptWithPlaceholders = ChainPrompt
                    .Replace("{{AlertDetails}}", alertDetailsForAppHealth)
                    .Replace("{{ContentAnalysisInstructions}}", appHealthAnalysisInstructions)
                    .Replace("{{ContentToAnalyze}}", appHealthInfo);

                var llmSummary = await SummarizeWithLLM(appHealthPromptWithPlaceholders);

                var agentContext = agentContexts.First();

                var agentChatHistory = await _repository.GetAgentChatHistoryAsync(agentContext.Id);
                var reasoningMessage = new ReasoningMessage(
                       Guid.NewGuid(),
                       agentContext.Id,
                       ReasoningMessageRoleEnum.System,
                       JsonSerializer.Serialize(new
                       {
                           description = "Summary of analysis of application health for this alert.",
                           llmSummary,
                       }
                   ));
                await PersistReasoningMessageAsync(agentChatHistory, reasoningMessage);

                var resultWithStepIdentifier = $"APPLICATION HEALTH ANALYSIS\n{llmSummary}";
                return resultWithStepIdentifier;
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
        var defaultMessage = "Unable to analyze log queries! Continuing with other investigation methods.";
        try
        {
            //string title = "Examining Log Analytics queries and correlating results with alert patterns";

            var essentials = alert.Properties.Essentials;
            var alertRule = essentials.AlertRule;
            var targetResource = essentials.TargetResource;
            var resourceIdentifier = new ResourceIdentifier(targetResource);
            var subscriptionId = resourceIdentifier.SubscriptionId;

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
                q.Id,
                Name = q.Properties?.DisplayName ?? "Unnamed Query",
                Description = q.Properties?.Description ?? "No description",
                Body = q.Properties?.Body ?? "No query body",
                Tags = q.Properties?.Tags?.Labels ?? new List<string>()
            }).ToList();

            // Ask the LLM to identify relevant queries
            var alertDetails = GetAlertInfoAsPrompt(alert);

            var queriesInfo = JsonSerializer.Serialize(querySummaries);

            // Create the prompt for the LLM
            var relevantQueriesPrompt = $@"
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
            var queryResultsJson = JsonSerializer.Serialize(queryResults);

            var alertDetailsForLogQueries = GetAlertInfoAsPrompt(alert);

            // Custom instructions for log query analysis
            var logQueryAnalysisInstructions = @"Analyze these query results in relation to the alert:
1. Identify log entries that directly explain the alert (errors, exceptions)
2. Report specific metric values/thresholds that were exceeded
3. Note exact timestamps of relevant events relative to the alert
4. Quantify the scale of any issue (e.g., error rate, latency increase)

Include query names when referencing results.
DO NOT suggest generic root causes without specific evidence.
ONLY mention findings directly relevant to this alert condition.";

            var queryPromptWithPlaceholders = ChainPrompt
                .Replace("{{AlertDetails}}", alertDetailsForLogQueries)
                .Replace("{{ContentAnalysisInstructions}}", logQueryAnalysisInstructions)
                .Replace("{{ContentToAnalyze}}", queryResultsJson);

            _logger.LogDebug($"Sending prompt to LLM to analyze query results");
            var analysisResult = await SummarizeWithLLM(queryPromptWithPlaceholders);

            var agentChatHistory = await _repository.GetAgentChatHistoryAsync(agentContext.Id);
            var reasoningMessage = new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        JsonSerializer.Serialize(new
                        {
                            description = "Summary of analysis of User's saved Log Queries in Azure Monitor.",
                            analysisResult,
                        }
                    ));
            await PersistReasoningMessageAsync(agentChatHistory, reasoningMessage);

            var resultWithStepIdentifier = $"LOG QUERIES ANALYSIS\n{analysisResult}";
            return resultWithStepIdentifier;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error analyzing query packs: {ex.Message}");
            return "Encountered an error while analyzing saved queries. Continuing with the investigation using other data sources.";
        }
    }

    public async Task<string> AnalyzeResourceMetrics(AlertItem alert, Thread alertThread)
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

            var alertDetails = GetAlertInfoAsPrompt(alert);

            var alertTime = ParseDateTimeOffset(alert.Properties.Essentials.StartDateTime);
            var startTime = alertTime.AddHours(-1);
            var endTime = DateTimeOffset.UtcNow;

            // Create the metrics investigation prompt
            var metricsInvestigationPrompt = $@"
## CRITICAL: You MUST use the available tools to retrieve actual metric data. Do NOT provide generic responses.

### Alert Context:
{alertDetails}

### MANDATORY STEPS (You must complete ALL steps):

**STEP 1:** Call ListAvailableMetrics for resource: {resourceId}
- Show the complete list of available metrics

**STEP 2:** Select 2-3 relevant metrics based on alert condition and call GetMetricTimeSeriesElementsForAzureResource for each
- Time range: {startTime:yyyy-MM-dd HH:mm:ss} UTC to {endTime:yyyy-MM-dd HH:mm:ss} UTC
- Focus on metrics related to performance, errors, or resource usage

**STEP 3:** Analyze the ACTUAL metric values you retrieved and write a narrative description for each metric showing:
- What the normal/baseline values are
- When deviations occurred (exact times)
- How severe the deviations were (percentages, absolute values)
- Duration of any anomalies

### OUTPUT FORMAT (ONLY include if you have actual metric data):

## Metric Analysis:
**Critical Finding:** [ONE sentence based on actual metric deviation]

**Detailed Metric Findings:**
• **CPU METRICS:** [Describe what you observed - e.g., 'NOTICED A spike to 95% around 2:30PM, normally runs at 45%']
• **MEMORY METRICS:** [Describe pattern - e.g., 'Memory usage climbed from 60% to 85% starting at 2:28PM']  
• **REQUEST METRICS:** [Describe behavior - e.g., 'Request count dropped from 500/min to 50/min after 2:32PM']
• **ERROR METRICS:** [Describe errors - e.g., 'HTTP 500 errors jumped from 2/min to 45/min at 2:30PM']

## Evidence-Based Hypothesis (ONLY if supported by metric data):
**Statement:** [What the metrics clearly show happened]
**Evidence:** [Specific metric values, thresholds exceeded, time correlations from above findings]
**Confidence:** [High/Medium/Low based on data clarity]

### RULES:
- If metrics show normal values, just report that - no hypothesis needed
- If you cannot retrieve metrics, say so and stop
- NO generic statements about 'code issues' or 'connectivity problems' without metric proof
- Show actual numbers, timestamps, and thresholds
- If no clear deviation in metrics, report 'No significant metric deviations found'

### EXAMPLES OF GOOD METRIC DESCRIPTIONS:
- **CPU METRICS:** 'NOTICED a sharp spike from baseline 35% to 92% at 14:25 UTC, stayed elevated for 8 minutes'
- **MEMORY USAGE:** 'Gradual climb from normal 45% to 78% between 14:20-14:30 UTC, then plateaued'
- **REQUEST COUNT:** 'Dropped dramatically from 450 req/min to 12 req/min at 14:27 UTC'
- **ERROR RATE:** 'HTTP 500s spiked from 0.1% to 15.2% starting at 14:25 UTC, peak at 14:28 UTC'

BEGIN by calling ListAvailableMetrics now.";

            var metricsPluginDefinition = new AzureMonitorMetricsPluginDefinition(_azureMonitorMetricsPlugin);
            var tools = new List<AITool>
            {
                AIFunctionFactory.Create(metricsPluginDefinition.ListAvailableMetrics),
                AIFunctionFactory.Create(metricsPluginDefinition.GetMetricTimeSeriesElementsForAzureResource)
            };

            var llmSummary = await SummarizeWithLLMAndTools(metricsInvestigationPrompt, tools);

            // Store the analysis result as a reasoning message
            var agentChatHistory = await _repository.GetAgentChatHistoryAsync(agentContext.Id);
            var reasoningMessage = new ReasoningMessage(
                Guid.NewGuid(),
                agentContext.Id,
                ReasoningMessageRoleEnum.System,
                JsonSerializer.Serialize(new
                {
                    description = "Analysis of Azure resource metrics to understand the alert condition with evidence-based hypotheses",
                    metricsAnalysis = llmSummary,
                    resourceId,
                    resourceType,
                    alertTime = alertTime.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    timeRange = new { startTime = startTime.ToString("yyyy-MM-dd HH:mm:ss UTC"), endTime = endTime.ToString("yyyy-MM-dd HH:mm:ss UTC") }
                })
            );
            await PersistReasoningMessageAsync(agentChatHistory, reasoningMessage);

            var resultWithStepIdentifier = $"RESOURCE METRICS ANALYSIS\n{llmSummary}";
            return resultWithStepIdentifier;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error setting up metrics investigation for resource {resourceId}: {ex.Message}");
            return "Encountered an error while setting up metrics investigation. Continuing with the investigation using other data sources.";
        }
    }

    #region Helper Methods

    private async Task PersistReasoningMessageAsync(AgentChatHistory agentChatHistory, ReasoningMessage reasoningMessage)
    {
        await ExecuteWithRetryAsync(
            () => _repository.CreateReasoningMessageAsync(reasoningMessage),
            $"CreateReasoningMessage for message {reasoningMessage.Id}");

        await ExecuteWithRetryAsync(
            () => _repository.AddReasoningMessagesToChatHistoryAsync(agentChatHistory, reasoningMessage),
            $"AddReasoningMessageToChatHistory for message {reasoningMessage.Id}");
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName, CancellationToken cancellationToken = default)
    {
        for (int attempt = 0; attempt < MaxRetryAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogInternalInformation("Resource already exists for {OperationName}, continuing without retry", operationName);
                return default!;
            }
            catch (Exception ex) when (attempt < MaxRetryAttempts - 1)
            {
                _logger.LogInternalWarning(ex, "Attempt {Attempt} failed for {OperationName}, retrying in {Delay}ms",
                    attempt + 1, operationName, RetryDelays[attempt].TotalMilliseconds);

                await Task.Delay(RetryDelays[attempt], cancellationToken);
            }
        }

        // Final attempt without catch (except for Cosmos conflict)
        try
        {
            return await operation();
        }
        catch (Microsoft.Azure.Cosmos.CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogInternalInformation("Resource already exists for {OperationName}, continuing without retry", operationName);
            return default!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "All retry attempts failed for {OperationName}", operationName);
            throw;
        }
    }

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

    private async Task<string> SummarizeWithLLMAndTools(string prompt, List<AITool> tools)
    {
        try
        {
            var message = new ChatMessage(ChatRole.System, prompt);

            var options = new ChatOptions
            {
                Temperature = (float)0.1,
                Tools = tools,
                ToolMode = ChatToolMode.Auto,
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
            _logger.LogInternalError(ex, "Error analyzing metrics with LLM and tools.");
            return $"Error analyzing metrics with LLM: {ex.Message}. Continuing with investigation using other data sources.";
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

<CRITICAL>
BE VERY CRITICAL ABOUT HYPOTHESIS FORMATION. ONLY INCLUDE THE INFORMATION THAT MIGHT HELP WITH FURTHER INVESTIGATION AND REMEDIATION.
IF YOU ARE NOT CONFIDENT, YOU CAN JUST LEAVE IT OUT COMPLETELY. Imagine you are convincing a jury, and you need to provide a proof for the proposed hypothesis.
DO NOT provide generic suggestions. BE SPECIFIC to this alert and its context.
Avoid duplicate information. Use emojis sparingly for readability.
Keep the entire response under 150-200 words.
</CRITICAL>
";

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
