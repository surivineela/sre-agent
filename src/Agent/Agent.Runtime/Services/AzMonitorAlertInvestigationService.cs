// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Author = Agent.Core.Models.Api.v1.Author;
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

            await _repository.AddMessageAsync(
                alertThread.Id,
                new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    "📜 Analyzing recent activity logs to understand configuration changes and operations..."
                ));

            // Fetch and summarize activity logs using the existing GraphDBPlugin method
            var activityLogSummary = await _graphDBPlugin.FetchAndSummarizeActivityLogs(
                resourceId,
                daysBack: 1,
                threadId: alertThread.Id
            );

            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogWarning("No agent context found for thread");
                return "Failed to get activity log summary for the resource!";
            }

            var agentContext = agentContexts.First();

            // Store the activity log summary in the agent's reasoning context
            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                Guid.NewGuid(),
                agentContext.Id,
                ReasoningMessageRoleEnum.System,
                JsonSerializer.Serialize(new
                {
                    title = "Activity Log Analysis",
                    resourceId,
                    activityLogSummary,
                    instructions = "Review this activity log summary and correlate it with the metrics and health data analyzed previously. Look for any configuration changes, deployments, or operations that might be related to the alert. Provide a concise summary focusing on relevant findings."
                })
            ));

            // Update the thread with the summary
            await _repository.AddMessageAsync(
                alertThread.Id,
                new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    $"I've analyzed the activity logs for the affected resource and found the following:\n\n{activityLogSummary}\n\nContinuing investigation... correlating more data points to identify patterns that may explain this alert."
                ));

            return activityLogSummary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error analyzing activity logs: {ex.Message}");

            // Notify in the thread that an error occurred
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                $"❌ Encountered an error while analyzing activity logs. Continuing with the investigation using other data sources."
            ));
        }

        return string.Empty;
    }

    public async Task<string> AnalyzeConnectedComponents(AlertItem alert, Thread alertThread)
    {
        try
        {
            var resourceId = alert.Properties.Essentials.TargetResource;

            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                "🔄 Crawling the knowledge graph to identify and analyze connected components that might be impacting this resource..."
            ));

            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogWarning("No agent context found for thread");
                return "";
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

            string prompt = @"
You are analyzing health data for an Azure resource and its connected components in response to an alert. 

Analyze and create a comprehensive summary of your findings, being sure to include:
1. Overall assessment of the primary resource's health state
2. Potential issues identified in any component, focusing on:
   - Unusual or critical values in availability, CPU, memory usage, or latency
   - Components that are inactive but should be active
   - Performance degradation patterns across multiple components
3. Correlation between the observed metrics and the current alert
4. Possible root causes based on the health data patterns

Important notes:
- Some components may not have health data available - this is normal and should not be treated as an anomaly
- Focus on significant deviations from normal metrics
- Prioritize findings based on severity and relevance to the alert
- Consider relationships between components when identifying potential cascading failures

Your assessment should be concise, actionable, and focus on insights that would help resolve the alert condition.";

            var llmHealthSummary = await SummarizeWithLLM(prompt, healthSummary.ToString());

            var agentContext = agentContexts.First();

            // Create a reasoning message to prompt the agent to analyze connected components
            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                Guid.NewGuid(),
                agentContext.Id,
                ReasoningMessageRoleEnum.System,
                JsonSerializer.Serialize(new
                {
                    title = "Connected Components Analysis Instructions",
                    resourceId,
                    instructions = $"Health summary of the affected resource and it's components: {llmHealthSummary}"
                })
            ));

            // Add a message to the thread indicating the agent is analyzing connected components
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                 $"I've analyzed the connected components and their health status. Here is the summary of what I found:\n\n{llmHealthSummary}"
            ));

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error setting up connected components analysis: {ex.Message}");

            // Notify in the thread that an error occurred
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                $"❌ Encountered an error while setting up connected components analysis. Continuing with the investigation using other data sources."
            ));
        }

        return "";
    }

    public async Task<string> GetApplicationHealthAsync(AlertItem alert, Thread alertThread)
    {
        var essentials = alert.Properties.Essentials;
        var alertRule = essentials.AlertRule;
        var targetResource = essentials.TargetResource;
        var resourceId = new ResourceIdentifier(targetResource);

        try
        {
            // Get general app health info
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                "📊 Checking the resource's health scorecard..."
            ));

            var appHealthInfo = await _graphDBPlugin.GetApplicationHealthInfoAsync(targetResource);

            // If health info is available, add it to the thread
            if (appHealthInfo != null)
            {
                // add to the agent context for reference in future reasoning
                var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
                if (agentContexts != null && agentContexts.Any())
                {
                    var agentContext = agentContexts.First();

                    string serializedHealthInfo = JsonSerializer.Serialize(new
                    {
                        title = $"Resource Health Analysis for {targetResource}",
                        resourceId = targetResource,
                        healthInfo = appHealthInfo,
                        timestamp = DateTime.UtcNow
                    });

                    await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                        Guid.NewGuid(),
                        agentContext.Id,
                        ReasoningMessageRoleEnum.System,
                        serializedHealthInfo
                    ));
                }

                string prompt = $"You are an SRE Agent investigating an alert. Here are the details about the alert: {GetAlertInfoAsPrompt(alert)}. " +
                    $"Using this prompt as context, correlate the findings with this alert and summarize your findings for the following health summary of an application.";

                var llmSummary = await SummarizeWithLLM(prompt, appHealthInfo);

                await _repository.AddMessageAsync(alertThread.Id, new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    llmSummary
                ));

                return llmSummary;
            }
            else
            {
                // No health info available
                await _repository.AddMessageAsync(alertThread.Id, new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    "⚠️ No health scorecard data is available for this resource. Continuing with investigation using other data sources."
                ));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error during alert investigation flow: {ex.Message}");
        }

        return "No app health summary available. Use other data points to continue the investigation!";
    }

    public async Task<string> GetMetricsForResource(AlertItem alert, Thread alertThread)
    {
        var resourceId = alert.Properties.Essentials.TargetResource;

        var resourceType = alert.Properties.Essentials.TargetResourceType;

        try
        {
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                "📈 Analyzing relevant metrics for this resource..."
            ));

            // Get agent context
            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogWarning("No agent context found for thread");
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
            _logger.LogError(ex, $"Error setting up metrics investigation for resource {resourceId}: {ex.Message}");

            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                $"❌ Encountered an error while setting up metrics analysis. Continuing with the investigation using other data sources."
            ));
        }

        return "No metric summary available. Use other data points to continue the investigation!";
    }

    public async Task<string> AnalyzeLogQueries(AlertItem alert, Thread alertThread)
    {
        try
        {
            var essentials = alert.Properties.Essentials;
            var alertRule = essentials.AlertRule;
            var targetResource = essentials.TargetResource;
            var resourceIdentifier = new ResourceIdentifier(targetResource);
            string subscriptionId = resourceIdentifier.SubscriptionId;

            if (string.IsNullOrEmpty(subscriptionId))
            {
                _logger.LogWarning("Subscription id cannot be null or empty!");
                return "";
            }

            // Add a message to the thread indicating we're analyzing saved queries
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                "🔍 Analyzing saved queries from Log Analytics to find patterns relevant to this alert..."
            ));

            // Get agent context
            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            if (agentContexts == null || !agentContexts.Any())
            {
                _logger.LogWarning("No agent context found for thread");
                return "";
            }
            var agentContext = agentContexts.First();

            // Get all saved queries for this subscription
            var savedQueries = await _logQueryService.GetSavedQueriesForSubscriptionAsync(subscriptionId);
            _logger.LogInformation($"Found {savedQueries.Count()} saved queries from Azure Log Analytics workspace!");

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
            string alertDetails = JsonSerializer.Serialize(new
            {
                alertId = alert.Id,
                alertRule = essentials.AlertRule,
                description = essentials.Description,
                severity = essentials.Severity,
                monitorService = essentials.MonitorService,
                targetResource = essentials.TargetResource,
                resourceType = essentials.TargetResourceType,
                resourceName = essentials.TargetResourceName,
                signalType = essentials.SignalType,
                monitorCondition = essentials.MonitorCondition
            });

            string queriesInfo = JsonSerializer.Serialize(querySummaries);

            // Create the prompt for the LLM
            string relevantQueriesPrompt = $@"
You are an Azure SRE Agent investigating an alert. Your task is to identify saved queries that might help understand this alert.

# Alert Details
```json
{alertDetails}
```

# Available Saved Queries
```json
{queriesInfo}
```

Please identify the queries that are most relevant to understanding this alert. Consider:
1. Queries that target similar resource types
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
            var relevantQueriesJson = await _chatClient.GetResponseAsync(relevantQueriesPrompt);

            List<RelevantQuery> relevantQueries;
            try
            {
                relevantQueries = JsonSerializer.Deserialize<List<RelevantQuery>>(relevantQueriesJson.Text) ?? new List<RelevantQuery>();
                _logger.LogInformation($"LLM identified {relevantQueries.Count} relevant queries");
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, $"Error parsing LLM response for relevant queries: {ex.Message}");
                relevantQueries = new List<RelevantQuery>();
            }

            if (relevantQueries.Count == 0)
            {
                await _repository.AddMessageAsync(alertThread.Id, new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    "No relevant saved queries were found for this alert. Continuing with other investigation methods."
                ));
                return "";
            }

            // Execute the relevant queries
            var queryResults = new List<QueryExecutionResult>();

            foreach (var relevantQuery in relevantQueries)
            {
                var query = savedQueries.FirstOrDefault(q => q.Id == relevantQuery.Id);
                if (query == null || string.IsNullOrEmpty(query.Properties?.Body))
                {
                    _logger.LogWarning($"Query with ID {relevantQuery.Id} not found or has no body");
                    continue;
                }

                try
                {
                    _logger.LogInformation($"Executing query: {query.Properties.DisplayName} (ID: {query.Id})");

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
                    _logger.LogError(ex, $"Error executing query {query.Properties.DisplayName} (ID: {query.Id}): {ex.Message}");
                }
            }

            if (queryResults.Count == 0)
            {
                await _repository.AddMessageAsync(alertThread.Id, new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    "I found potentially relevant queries but was unable to execute them successfully. Continuing with other investigation methods."
                ));
                return "No relevant log queries found!";
            }

            // Create the analysis prompt
            string queryResultsJson = JsonSerializer.Serialize(queryResults);

            string analysisPrompt = $@"
You are an Azure SRE Agent investigating an alert. You have executed relevant queries and now need to analyze the results.

# Alert Details
```json
{alertDetails}
```

# Query Results
```json
{queryResultsJson}
```

Analyze these query results in relation to the alert. Focus on:
1. Patterns or anomalies that might explain the alert
2. Correlation between the alert timing and any spikes in errors or performance issues
3. Evidence that confirms or contradicts the alert's significance
4. Potential root causes based on the query data

Provide a concise summary of your findings that would help in understanding and resolving the alert.
Use bullet points for key insights. Include the query display name if available when summarizing the results. Include specific metrics or logs when relevant.";

            _logger.LogDebug($"Sending prompt to LLM to analyze query results");
            var analysisResult = await _chatClient.GetResponseAsync(analysisPrompt);

            // Add the analysis to the agent's reasoning context and thread
            await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                Guid.NewGuid(),
                agentContext.Id,
                ReasoningMessageRoleEnum.System,
                JsonSerializer.Serialize(new
                {
                    title = "Log Query Analysis",
                    alertDetails,
                    queryResults = queryResultsJson,
                    analysis = analysisResult,
                    instructions = "Use this analysis of log query results to help inform your investigation of the alert. Consider these findings when formulating recommendations and next steps."
                })
            ));

            // Add a summary message to the thread
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                $"📊 **Log Query Analysis**\n\n{analysisResult}"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error analyzing query packs: {ex.Message}");

            await _repository.AddMessageAsync(alertThread.Id, new Message(
                Guid.NewGuid(),
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                $"❌ Encountered an error while analyzing saved queries. Continuing with the investigation using other data sources."
            ));
        }

        return "";
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
            _logger.LogWarning($"Could not parse start time {value}, using current time instead");
        }

        return createdAt;
    }

    private string GetAlertInfoAsPrompt(AlertItem alert)
    {
        StringBuilder investigationSummary = new();

        investigationSummary.AppendLine("# Alert Information");
        investigationSummary.AppendLine($"- Alert ID: {alert.Id}");
        investigationSummary.AppendLine($"- Alert Name: {alert.Name}");
        investigationSummary.AppendLine($"- Severity: {alert.Properties.Essentials.Severity}");
        investigationSummary.AppendLine($"- Monitor Condition: {alert.Properties.Essentials.MonitorCondition}");
        investigationSummary.AppendLine($"- Alert Rule: {alert.Properties.Essentials.AlertRule}");
        investigationSummary.AppendLine($"- Description: {alert.Properties.Essentials.Description}");
        investigationSummary.AppendLine($"- Target Resource: {alert.Properties.Essentials.TargetResource}");
        investigationSummary.AppendLine($"- Resource Type: {alert.Properties.Essentials.TargetResourceType}");
        investigationSummary.AppendLine($"- Fired At: {alert.Properties.Essentials.StartDateTime}");

        return investigationSummary.ToString();
    }

    private async Task<string> SummarizeWithLLM(string prompt, string content)
    {
        try
        {
            var messages = new List<ChatMessage>();
            messages.Add(new ChatMessage(ChatRole.System, prompt));
            messages.Add(new ChatMessage(ChatRole.System, prompt));

            var options = new ChatOptions
            {
                Temperature = (float)0.2,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    ["response_format"] = "text"
                }
            };

            var response = await _chatClient.GetResponseAsync(messages, options);
            string summary = response.Text;

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error summarizing content with llm.");
            return $"Error summarizing with LLM: {ex.Message}";
        }
    }

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
