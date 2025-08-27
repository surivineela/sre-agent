// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Data.DataModels;
using Agent.Plugins.Interface;
using Agent.Runtime.Interfaces;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Azure.Core;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Author = Agent.Core.Models.Api.v1.Author;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Message = Agent.Core.Models.Api.v1.Message;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.SubAgents.AzMonitorAlertAgent;

public class AzMonitorAlertScanner
{
    private readonly ILogger<AzMonitorAlertScanner> _logger;
    private readonly IGraphDBPlugin _graphDBPlugin;
    private readonly IAgentInboundCommunicationService _inboundCommunicationService;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IThreadRepository _repository;
    private readonly IChatClient _chatClient;
    private readonly IAzMonitorAlertService _azMonitorAlertService;
    private readonly IInvestigationOrchestrator _investigationOrchestrator;
    private readonly Container _dbContainer;
    private readonly IGraphDatabaseClient _graphDbClient;
    private readonly ILogQueryService _logQueryService;
    private readonly IAzMonitorAlertInvestigationService _azMonitorInvestigationService;
    private readonly IAgentsFactory _agentsFactory;
    private readonly IIncidentStatusMetricsService _incidentsStatusMetricsService;
    private readonly IncidentManagementSettings _incidentManagementSettings;


    public AzMonitorAlertScanner(
        IGraphDBPlugin graphDbPlugin,
        IAzMonitorAlertService azMonitorAlertService,
        IAgentInboundCommunicationService inboundCommunicationService,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository repository,
        CosmosClient cosmosClient,
        CosmosDBSettings cosmosDbSettings,
        IGraphDatabaseClient graphDatabaseClient,
        ILogQueryService logQueryService,
        IAzMonitorAlertInvestigationService alertInvestigationService,
        IInvestigationOrchestrator investigationOrchestrator,
        IAgentsFactory agentsFactory,
        IIncidentStatusMetricsService incidentsStatusMetricsService,
        IChatClient chatClient,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<AzMonitorAlertScanner> logger)
    {
        _graphDBPlugin = graphDbPlugin;
        _logger = logger;
        _incidentManagementSettings = incidentManagementSettings;

        _azMonitorAlertService = azMonitorAlertService;
        _inboundCommunicationService = inboundCommunicationService;
        _outboundCommunicationService = outboundCommunicationService;
        _repository = repository;
        _chatClient = chatClient;

        _dbContainer = cosmosClient.GetContainer(cosmosDbSettings.Docs.Database, AgentDataConfiguration.ThreadContainerName);
        _graphDbClient = graphDatabaseClient;
        _logQueryService = logQueryService;
        _azMonitorInvestigationService = alertInvestigationService;
        _investigationOrchestrator = investigationOrchestrator;

        _agentsFactory = agentsFactory;
        _incidentsStatusMetricsService = incidentsStatusMetricsService;
    }

    /// <summary>
    /// Polls for new alerts in Log Analytics Workspace on a given cadence.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns></returns>
    public async Task PollNewAlertsAsync(CancellationToken ct = default)
    {
        _logger.LogInternalInformation("Polling for new Azure Monitor alerts from the last minute");

        try
        {
            var getSubscriptions = await _graphDBPlugin.ListSubscriptionsAsync();
            List<string> subscriptions = new();
            foreach (var subscription in getSubscriptions)
            {
                subscriptions.Add(subscription["id"]);
            }

            if (subscriptions.Count == 0)
            {
                _logger.LogInternalInformation($"No subscriptions found in the Graph DB.");
                return;
            }

            _logger.LogInternalInformation($"Scanning for Azure Monitor Alerts in the following subscriptions: {string.Join(", ", subscriptions)}");

            foreach (var subscription in subscriptions)
            {
                _logger.LogInternalInformation($"Checking for alerts in subscription: {subscription}");
                var newAlerts = await _azMonitorAlertService.PollNewAlertsBySubscriptionId(subscription, 5);

                int alertCount = newAlerts.Count();
                _logger.LogInternalInformation($"Found {alertCount} alerts in subscription {subscription}");

                foreach (var alert in newAlerts)
                {
                    _logger.LogInternalInformation($"Processing new alert {alert.Id}...");
                    await ProcessAlertAsync(alert, ct);
                }
            }
            // periodically refresh incident metrics
            var incidentMetrics = await _incidentsStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
            await _outboundCommunicationService.NotifyIncidentStatusMetrics(Guid.Empty, incidentMetrics);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error polling Azure Monitor alerts");
        }
    }

    public async Task ProcessAlertAsync(AlertItem alert, CancellationToken cancellationToken)
    {
        try
        {
            // check if there exists an active incident thread for this alert
            var existingActiveThread = await FindExistingActiveThreadForAlertRule(alert);

            await _azMonitorAlertService.AcknowledgeAlert(alert.Id);

            if (existingActiveThread != null)
            {
                if (RequiresTargetResourceInput(alert))
                {
                    string? alertDocumentId = existingActiveThread?.Status?.IncidentStatus?.IncidentId;
                    if (!string.IsNullOrEmpty(alertDocumentId))
                    {
                        var existingAlertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);
                        if (existingAlertDocument != null)
                        {
                            if (existingAlertDocument.TargetResourceInputRequested)
                            {
                                // return if user input already requested.
                                return;
                            }
                        }
                    }
                }

                var investigationFinished = await IsInvestigationFinishedAsync(existingActiveThread!.Id);

                if (investigationFinished)
                {
                    string? alertDocumentId = existingActiveThread?.Status?.IncidentStatus?.IncidentId;
                    if (!string.IsNullOrEmpty(alertDocumentId))
                    {
                        var existingAlertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);
                        if (existingAlertDocument != null)
                        {
                            // Check if we've reached the investigation attempt limit
                            var currentHitCount = existingAlertDocument.HitCount;
                            var maxRetryCount = _incidentManagementSettings.MaxAutomatedInvestigationAttempts;

                            // Update hitcount for every alert
                            var updatedAlertDocument = existingAlertDocument with
                            {
                                HitCount = currentHitCount + 1,
                                UpdatedAt = DateTime.UtcNow
                            };

                            await _dbContainer.UpsertItemAsync(
                                updatedAlertDocument,
                                new PartitionKey(updatedAlertDocument.PartitionKey)
                            );

                            if (currentHitCount > maxRetryCount)
                            {
                                // skip prompting user for input if already requested
                                if (existingAlertDocument.UserInputRequested) return;

                                // Retry limit reached - ask for user input
                                _logger.LogInternalInformation($"Found existing active thread {existingActiveThread?.Id} for alert {alert.Id}. Retry limit ({maxRetryCount}) reached. Requesting user input.");

                                var userInputMessage = $"The automated investigation has been completed multiple times but was unable to identify a definitive root cause.\n\n" +
                                    $"**Action Required:** Please provide additional context or manual investigation steps that might help resolve this recurring issue. Consider:\n" +
                                    $"- Recent changes not captured in logs\n" +
                                    $"- External dependencies or third-party services\n" +
                                    $"- Known issues with the affected system\n" +
                                    $"- Any manual remediation steps that have worked before\n" +
                                    $"- Configuration changes or deployments\n" +
                                    $"- Network connectivity issues\n" +
                                    $"- Resource scaling or capacity problems\n\n" +
                                    $"Please share any relevant information that could help identify the root cause.";

                                var agentContexts = await _repository.GetAgentContextsForThreadAsync(existingActiveThread!.Id);
                                var existingAgentContext = agentContexts.First();

                                await PromptUserForInputAsync(
                                    existingActiveThread.Id,
                                    existingAgentContext,
                                    userInputMessage
                                );

                                // Update UserInputRequested to avoid appending User Input Message again
                                updatedAlertDocument = updatedAlertDocument with
                                {
                                    UserInputRequested = true
                                };

                                await _dbContainer.UpsertItemAsync(
                                    updatedAlertDocument,
                                    new PartitionKey(updatedAlertDocument.PartitionKey)
                                );

                                // Don't increment the count when asking for user input - let them respond first
                                return;
                            }
                            else
                            {
                                // Under retry limit - append recurring alert message and increment count
                                _logger.LogInternalInformation($"Found existing active thread {existingActiveThread?.Id} with completed investigation for alert {alert.Id}. Appending recurring alert message. Count: {currentHitCount + 1}/{maxRetryCount}");

                                var message = $"Another alert **{alert.Id}** is firing with the same alert rule. Merging the investigation.";
                                if (existingActiveThread == null)
                                {
                                    _logger.LogInternalWarning("existingActiveThread is null when trying to get agent contexts.");
                                    return;
                                }

                                var agentContexts = await _repository.GetAgentContextsForThreadAsync(existingActiveThread.Id);
                                var existingAgentContext = agentContexts.First();
                                var incidentMetrics = await _incidentsStatusMetricsService.GetIncidentStatusMetricsAsync(null, DateTime.Now);
                                await _outboundCommunicationService.NotifyIncidentStatusMetrics(existingActiveThread.Id, incidentMetrics);
                                await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                                    ThreadId: existingActiveThread.Id,
                                    AgentContextId: existingAgentContext.Id,
                                    MessageId: Guid.NewGuid(),
                                    Message: message,
                                    UserId: "agent-default",
                                    DisplayName: "Azure SRE Agent",
                                    Timestamp: DateTime.UtcNow
                                    ));

                                _logger.LogInternalInformation($"Updated recurring alert count to {currentHitCount + 1} for alert document {alertDocumentId}");
                            }
                        }
                        else
                        {
                            _logger.LogInternalWarning($"Could not find alert document {alertDocumentId} for existing thread {existingActiveThread?.Id}");
                        }
                    }
                    else
                    {
                        _logger.LogInternalWarning($"No alert document ID found for existing thread {existingActiveThread?.Id}");
                    }
                }
                else
                {
                    // Investigation is still in progress, just log and don't append message
                    _logger.LogInternalInformation($"Found existing active thread {existingActiveThread.Id} with ongoing investigation for alert {alert.Id}. Not appending message as investigation is still in progress.");
                }
                return;
            }

            // save alert in the document db
            var docId = await SaveAlertToDocumentDb(alert);

            // save alert as a node in the graph db and create edge to resource
            await SaveAlertToGraphDb(alert);

            // Create incident thread
            var (thread, agentContext) = await CreateIncidentThread(alert);

            var investigationResult = await _investigationOrchestrator.InvestigateAlertAsync(alert, thread);

            if (RequiresTargetResourceInput(alert))
            {
                _logger.LogInternalInformation($"Alert {alert.Id} targets a {alert.Properties?.Essentials?.TargetResourceType} resource. Requesting user to specify the affected resource before investigation.");

                var targetResourceInputMessage = $"This alert is targeting a **{alert.Properties?.Essentials?.TargetResourceType}** resource, which may impact the automated investigation.\n\n" +
                    $"**Action Required:** Please specify which specific resource or application is affected by this alert to ensure the investigation focuses on the correct target.\n\n" +
                    $"Please provide:\n" +
                    $"- The name or resource ID of the affected application/service\n";

                await PromptUserForInputAsync(thread.Id, agentContext, targetResourceInputMessage);

                // Update the alert document to mark that target resource input has been requested
                var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(docId, docId);
                if (alertDocument != null)
                {
                    if (alertDocument.TargetResourceInputRequested)
                    {
                        _logger.LogInternalInformation($"Target resource input has already been requested for alert {alert.Id}.");
                        return;
                    }

                    var updatedAlertDocument = alertDocument with
                    {
                        TargetResourceInputRequested = true,
                        UpdatedAt = DateTime.UtcNow
                    };

                    await _dbContainer.UpsertItemAsync(
                        updatedAlertDocument,
                        new PartitionKey(updatedAlertDocument.PartitionKey)
                    );
                }

                return;
            }

            var alertInfo = GetAlertInfoAsPrompt(alert);

            string subAgentPrompt = $"An Azure Monitor Alert was fired with the following details: {alertInfo}.\n Based on the alert details start the remediation flow with the appropriate subagent. Be as autonomous as possible without asking for permission to take actions.";

            await _inboundCommunicationService.ProcessAlertMessageAsync(new ThreadMessage(
                   ThreadId: thread.Id,
                   AgentContextId: agentContext.Id,
                   MessageId: thread.StartMessage?.Id ?? new Guid(),
                   Message: $"An automated investigation has been completed for this alert with the following hypotheses and findings: {investigationResult.Summary}\n\nPlease validate these hypotheses by checking the supporting evidence. If the hypotheses seem incomplete or insufficient, conduct additional targeted investigation focusing on metrics, logs, and recent changes. Your goal is to either confirm one of these hypotheses with high confidence or discover the actual root cause if it differs from what was identified by the automated analysis. Based on the initial findings, find an appropriate subagent to handle the remediation. CRITICAL: Be as autonomous as possible without asking for permission to take actions.",
                   UserId: "agent-default",
                   DisplayName: "Azure SRE Agent",
                   Timestamp: DateTime.UtcNow
               ));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error processing alert {alert.Id}: {ex.Message}");
        }
    }

    private async Task<string> StartInvestigationFlow(AlertItem alert, Thread alertThread)
    {
        StringBuilder investigationSummary = new();

        try
        {
            string initMessage = ChatMessageService.InitializeInvestigationSummariesMessage("Starting investigation and forming hypothesis", new List<(string, string, bool)>());
            var initMessageGuid = Guid.NewGuid();

            // Add the initial investigation summary panel with just the title
            await _repository.AddMessageAsync(alertThread.Id, new Message(
                initMessageGuid,
                DateTime.UtcNow,
                new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                initMessage
            ));

            // Get general app health summary (scorecard)
            var healthSummary = await _azMonitorInvestigationService.AnalyzeApplicationHealth(alert, alertThread);
            var healthSummaryTitle = "Analyzing resource health summary and metrics";
            await AppendInvestigationSummaryToMessage(alertThread.Id, initMessageGuid, healthSummaryTitle, healthSummary);

            // Analyze activity logs for the impacted resource
            var activityLogSummary = await _azMonitorInvestigationService.AnalyzeActivityLogsForResource(alert, alertThread);
            var activityLogsTitle = "Analyzing activity logs for resource changes and administrative actions";
            await AppendInvestigationSummaryToMessage(alertThread.Id, initMessageGuid, activityLogsTitle, activityLogSummary);

            // Analyze connected components
            var kgSummary = await _azMonitorInvestigationService.AnalyzeConnectedComponents(alert, alertThread);
            var connectedComponentsTitle = "Analyzing connected components and dependencies";
            await AppendInvestigationSummaryToMessage(alertThread.Id, initMessageGuid, connectedComponentsTitle, kgSummary);

            // Analyze saved queries from Azure Log Analytics workspace / App Insights
            var logQuerySummary = await _azMonitorInvestigationService.AnalyzeLogQueries(alert, alertThread);
            var logQueriesTitle = "Examining Log Analytics queries and correlating results with alert patterns";
            await AppendInvestigationSummaryToMessage(alertThread.Id, initMessageGuid, logQueriesTitle, logQuerySummary,
                isCollapsed: false,
                status: "completed",
                isFinal: true);

            var alertDetails = GetAlertInfoAsPrompt(alert);

            string summarizePrompt = @$"
TASK:

You are an AI assistant helping a Site Reliability Engineer analyze an Azure Monitor alert.
The following context contains the results of an automated investigation into an Azure Monitor alert.This includes details about the alert itself,
the health of the affected application, relevant metrics, recent activity logs, analysis of connected components to this application, and results from relevant log queries saved in user's log analytics workspace.

Analyze recent exceptions, metrics, activity logs, and application topology given below to identify potential root causes for the alert specified. Consider code bugs, deployment changes, resource constraints, and topology gaps. Provide a clear hypothesis with supporting evidence and recommended next steps. Think Step by Step
Examples:
Example 1: Missing Topology IssueHypothesis: The alert was triggered by missing service connections in application topology. Evidence: Exception logs show connection timeouts to ServiceB, but ServiceB doesn't appear in application map for the last 6 hours. Activity logs show no recent changes to connection strings. Next steps: Verify ServiceB health and check if telemetry instrumentation is working properly in ServiceB code.
Example 2: Thread Deadlock After DeploymentHypothesis: Recent deployment introduced a deadlock in the order processing workflow. Evidence: Thread dump shows 12 blocked threads in OrderProcessor waiting for resources held by PaymentVerifier threads, which are waiting for DatabaseConnection threads. CPU utilization spiked to 95% but processing throughput dropped to near zero after deployment at 14:30. Request queue length growing consistently. Application topology shows normal connections but transaction completion rate is 0. Next steps: Roll back to previous version or investigate synchronization changes in the Order/Payment components, focusing on lock acquisition order and shared resource access patterns.
Example 3: Insufficient Resource Hypothesis: Database connection pool exhaustion due to increased traffic. Evidence: 500% increase in request metrics coincides with connection timeout exceptions. Database connection metrics show 95% utilization vs normal 60%. No recent deployments or code changes. Next steps: Increase connection pool size or scale up database resources to handle the current traffic pattern.
Example 4: Cascading Microservice Failure Hypothesis: A configuration change in the authentication service is causing cascading failures across the application ecosystem. Evidence: Exception logs show 401 errors in ServiceC started at 09:15, followed by connection timeouts in ServiceD at 09:17, and data processing exceptions in ServiceE at 09:22. Activity logs show a configuration update to Azure AD B2C at 09:10. Application map shows normal traffic patterns to auth service but 80% reduction in successful transactions across downstream services. CPU and memory metrics remain normal across all services. Next steps: Rollback Azure AD B2C configuration change and verify token validation logic in ServiceC to ensure proper handling of auth challenges.
Example 5: Potential Memory Leak Hypothesis: The alert was triggered by a increased HTTP 500 response. Evidence: Memory usage metrics show a increase trend over the time, peaking at 90% utilization. Request metrics shows no significant increase in the request volume. Activity logs show no recent deployments or configuration changes. OutOfMemory exception repeatedly observed in the application logs. Next steps: Capture memory dump and identify potential leaks in the dump, focusing on objects consuming large memory space. Restart the app for quick mitigation.

BAD EXAMPLES:
The resource /iot-dashboard is missing from the graph database, suggesting potential misconfiguration or deletion.
No error or exception logs were found in the queried timeframe, indicating possible logging misconfiguration.
Recent deployment or configuration changes are suspected but not confirmed due to lack of deployment logs.

-----------------------

GOAL

Based on the following investigation summaries, provide:

1. A concise summary of key findings across all areas (max 2 bullet points)
2. 1-2 hypotheses about the root cause, each with:
   - Clear description of the potential cause (one liner is good enough)
   - Supporting evidence from the summaries
   - Confidence score (0-100%)

** CRITICAL **  DO NOT give generic suggestions. Check the entire output for any duplicate information and condense it.

Now build hypothesis on this, you are unsure about any points/there is missing data you can ignore it. Only provide the most reliable, concise, actionable hypothesis which can be derived from the data below. If there is no possible root cause you can reply with 'Could not derive a hypothesis on this issue'
** CRITICAL ** If any summaries are missing data or logs are missing for an application, or graph database is missing the resource, DO NOT include that information in the summary and hypothesis. Your job is to not tell user about the best practices at this moment. You just need to figure out relevant root cause for the alert based on the information your were able to get.
** CRITICAL ** Try your best reasoning. If you are not sure about the findings, it's okay to just say you could not find relevant data points. There is no shame in that!

-----------------------

DATA ABOUT ALERT AND LOGS, METRICS, TOPOLOGY etc

### ALERT DETAILS
{alertDetails}

### APPLICATION HEALTH SUMMARY
{healthSummary}

### ACTIVITY LOG ANALYSIS
{activityLogSummary}

### RELATED RESOURCE ANALYSIS
{kgSummary}

### LOG QUERY ANALYSIS
{logQuerySummary}

---
Based on the initial investigation summary, think about the issue and how you will investigate it step by step. Decompose the problem into simple steps.
Keep proper tracking of the status of current subtask and next task
You will be allowed many iterations of tool execution to guide your hypothesis exploration.
Core Principles:
1. Safety first - Use only non-mutating commands (get, describe, logs, metrics queries).
2. Hypothesis-driven - Generate multiple plausible root-cause hypotheses before running commands.
3. Incremental evidence - Gather data that can confirm or falsify a hypothesis; avoid shotgun queries.
4. Iterative refinement - After each observation, update the hypothesis set (keep / reject / add).
5. Stop when solved - Conclude once one hypothesis is strongly supported and alternatives are reasonably ruled out, or when you must escalate.
6. Transparency - Show your full chain of thought (Thought:), the exact action (Action:), and the raw result (Observation:) every loop cycle.
Investigation Workflow Template:
            Step 0 - Planning
            - Thought: I list 2-3 primary hypotheses that could explain <symptom>.
            Step 1..N
                Loop — For each surviving hypothesis do:
                Choose the smallest action that can falsify / confirm it.
                - Thought: Hypothesis A predicts X. I'll check metric Y or config Z to confirm.
                - Tool Calls
                - Observation: …
                Then update:
                - Thought: Observation supports/rejects Hypothesis A because…
                - Remaining hypotheses: [ … ]
            Step N+1 - Termination
            When confident:
            - Thought: Evidence strongly supports Hypothesis B and rules out others.
            - Final answer - Use Summary: heading, covering:
                1. Leading hypothesis & supporting facts
                2. Ruled-out hypotheses & why
                3. Impacted components
                4. Next mitigation steps (if any)


Additional things to consider after running your reasoning loop:
1. Extract ONLY specific metrics, timestamps, and error patterns that explain this alert
2. Focus on quantifiable evidence (numeric deviations, timing correlations)
3. Do NOT include generic observations without specific values
4. Do NOT mention missing data or standard operational patterns
5. Keep your entire response under 300 words

FORMAT YOUR RESPONSE AS:

## Summary of Findings
- [Specific finding with exact metric/timestamp/error]
- [Specific finding with exact metric/timestamp/error]

## Hypotheses
### Hypothesis 1 (Confidence: XX%)
One sentence describing specific cause with exact evidence values supporting it

### Hypothesis 2 (Confidence: XX%) [Optional]
One sentence describing specific cause with exact evidence values supporting it

Remember: Quality findings with specific values are better than quantity. Exclude any hypothesis without concrete supporting evidence.";

            var agentContexts = await _repository.GetAgentContextsForThreadAsync(alertThread.Id);
            var finalSummary = await SummarizeWithLLM(summarizePrompt, alertThread.Id, agentContexts.First());

            var currentMessage = await _repository.GetMessageAsync(alertThread.Id, initMessageGuid);
            if (currentMessage != null)
            {
                // Append final summary directly to the message content
                string messageWithFinalSummary = $"{currentMessage.Text}\n\n<final-summary>{finalSummary}</final-summary>";

                // Update the message with the root cause appended
                Message updatedMessage = currentMessage with { Text = messageWithFinalSummary };
                await _repository.UpdateMessageAsync(alertThread.Id, updatedMessage);
            }

            return finalSummary;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error during alert investigation flow: {ex.Message}");
        }

        return investigationSummary.ToString();
    }

    private async Task<string> SaveAlertToDocumentDb(AlertItem alert)
    {
        var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alert.Id, alert.Id);

        if (alertDocument is null)
        {
            _logger.LogInternalInformation($"Creating new incident document for {alert.Id}.");

            var essentials = alert.Properties.Essentials;

            var alertId = alert.Id;
            var alertRule = essentials.AlertRule;
            var severity = essentials.Severity.ToString();
            var description = essentials.Description;
            var targetResource = essentials.TargetResource;
            var monitorCondition = essentials.MonitorCondition.ToString();

            var name = alertRule;
            string targetResourceType = essentials.TargetResourceType;
            string targetResourceId = targetResource;

            var resourceIdentifier = new ResourceIdentifier(targetResource);

            string subscriptionId = resourceIdentifier.SubscriptionId ?? Guid.Empty.ToString();
            string status = ServiceAlertState.Acknowledged.ToString(); // at this point, the alert should be acknowledged
            DateTimeOffset createdAt = ParseDateTimeOffset(essentials.StartDateTime);

            var alertRuleName = new ResourceIdentifier(alertRule);

            var alertResourceId = new ResourceIdentifier(alertId);

            var newAlertDocument = new AzMonitorAlertDocument(
                Id: alertResourceId.Name, // only get the alert Id (guid)
                Name: alertRuleName.Name, // only get the Alert Name
                Severity: severity,
                TargetResourceType: targetResourceType,
                TargetResourceId: targetResourceId,
                SubscriptionId: subscriptionId,
                Status: status,
                CreatedAt: createdAt
            )
            {
                Description = description,
                UpdatedAt = DateTime.UtcNow,
                HitCount = 1
            };

            // Save to database
            try
            {
                var response = await _dbContainer.UpsertItemAsync(
                    newAlertDocument,
                    new PartitionKey(newAlertDocument.PartitionKey)
                );

                _logger.LogInternalInformation($"Alert document created successfully with id: {newAlertDocument.Id}");

                return newAlertDocument.Id;
            }
            catch (CosmosException ex)
            {
                _logger.LogInternalError(ex, $"Error creating alert document in database: {ex.Message}");
                throw;
            }
        }
        else
        {
            _logger.LogInternalInformation($"Alert document already exists with id: {alertDocument.Id}. No new incident created.");
        }

        return alertDocument.Id;
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

    private async Task<bool> SaveAlertToGraphDb(AlertItem alert)
    {
        try
        {
            var essentials = alert.Properties.Essentials;
            var targetResourceId = essentials.TargetResource;

            if (string.IsNullOrEmpty(targetResourceId))
            {
                _logger.LogInternalWarning($"Alert {alert.Id} has no target resource, skipping graph DB operations");
                return false;
            }

            var alertNode = new AzMonitorAlertNode
            {
                IncidentId = alert.Id,
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInternalInformation($"Adding/updating alert node in graph DB for {alert.Id}");
            var nodeResult = await _graphDbClient.AddOrUpdateNodeAsync(alertNode);

            if (!nodeResult)
            {
                _logger.LogInternalWarning($"Failed to add/update alert node in graph DB for {alert.Id}");
                return false;
            }

            // Create edge between resource and alert
            var edge = new RelatedToIncidentEdge
            {
                SourceNodeId = targetResourceId.ToLowerInvariant(), // Resource is the source
                TargetNodeId = alertNode.GetNodeId(), // Alert is the target
                UpdateTs = DateTime.UtcNow.Ticks
            };

            _logger.LogInternalInformation($"Adding/updating edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            var edgeResult = await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            if (!edgeResult)
            {
                _logger.LogInternalWarning($"Failed to add/update edge in graph DB between resource {targetResourceId} and alert {alert.Id}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error saving alert {alert.Id} to graph database: {ex.Message}");
            return false;
        }
    }

    private async Task<(Thread, AgentContext)> CreateIncidentThread(AlertItem alert)
    {
        var messageBuilder = new StringBuilder();

        var essentials = alert.Properties.Essentials;

        var alertId = alert.Id;
        var alertRule = essentials.AlertRule;
        var severity = essentials.Severity.ToString();
        var description = essentials.Description;
        var targetResource = essentials.TargetResource;
        var monitorCondition = essentials.MonitorCondition.ToString();
        var monitorService = essentials.MonitorService;
        var startDateTime = ParseDateTimeOffset(essentials.StartDateTime);

        var alertRuleName = new ResourceIdentifier(alertRule);
        var targetResourceId = new ResourceIdentifier(targetResource);
        var subscription = targetResourceId?.SubscriptionId?.ToString();
        var resourceGroup = targetResourceId?.ResourceGroupName?.ToString();
        var targetResourceName = essentials.TargetResourceName;

        var alertIdResource = new ResourceIdentifier(alertId);
        var alertRuleResource = new ResourceIdentifier(alertRule);

        var encodedAlertId = Uri.EscapeDataString(alertId);
        var portalUrl = $"https://ms.portal.azure.com/#view/Microsoft_Azure_Monitoring_Alerts/AlertDetails.ReactView/alertId~/{encodedAlertId}/invokedFrom/CopyLinkFeature";

        var alertData = new
        {
            alertId = alertIdResource.Name,
            alertRule = alertRuleResource.Name,
            description,
            monitoredResource = targetResourceName,
            severity,
            monitorCondition,
            monitorService,
            firedAt = startDateTime.ToString(),
            subscription,
            resourceGroup,
            portalUrl
        };

        var serializedAlertData = JsonSerializer.Serialize(alertData);
        var alertDataBlock = $"```incident-alert\n{serializedAlertData}\n```\n";

        (var thread, var agentContext) = await _inboundCommunicationService.CreateAgentThread(
            title: $"Incident Alert - [{severity}] [{targetResourceId?.Name ?? string.Empty}] {alertRuleName.Name}",
            message: alertDataBlock,
            agentTypeEnum: AgentTypeEnum.Meta,
            source: ThreadSource.Incident,
            incidentId: alertIdResource.Name, // Alert GUID (unique every time a new alert is fired)
            incidentSource: new IncidentSource(IncidentType.AzMonitor, alertRule)
        );

        try
        {
            var agentMessage = $"Alert acknowledged ✅\n\nInitiating investigation to assess the situation and identify potential causes 🛠️";

            await _repository.AddMessageAsync(thread.Id, new Message(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                    agentMessage
                ));

            return (thread, agentContext);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Creating Incident thread failed with ex: {ex.Message}");
        }

        return (thread, agentContext);
    }

    private async Task<T?> GetDocumentAsync<T>(string id, string partitionKey) where T : ICosmosDocument
    {
        try
        {
            ItemResponse<T> response = await _dbContainer.ReadItemAsync<T>(
                id,
                new PartitionKey(partitionKey)
            );
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    /// <summary>
    /// Checks if the investigation has finished by looking for final summary items in thread messages
    /// </summary>
    /// <param name="threadId">The thread ID to check</param>
    /// <returns>True if investigation is finished, false if still in progress</returns>
    private async Task<bool> IsInvestigationFinishedAsync(Guid threadId)
    {
        try
        {
            // Get all messages from the thread
            var messages = await _repository.GetMessagesAsync(threadId);

            foreach (var message in messages)
            {
                // Look for investigation summaries in the message text
                if (message.Text.Contains("<investigation-summaries>") && message.Text.Contains("</investigation-summaries>"))
                {
                    // Extract JSON content between the tags using simple string operations
                    int startIndex = message.Text.IndexOf("<investigation-summaries>") + "<investigation-summaries>".Length;
                    int endIndex = message.Text.IndexOf("</investigation-summaries>");

                    if (startIndex > 0 && endIndex > startIndex)
                    {
                        string jsonContent = message.Text.Substring(startIndex, endIndex - startIndex).Trim();

                        try
                        {
                            var investigationSummaries = JsonSerializer.Deserialize<InvestigationSummaries>(jsonContent);

                            if (investigationSummaries?.summaries != null)
                            {
                                // Check if there's any final summary item that's completed
                                var hasFinalCompleted = investigationSummaries.summaries
                                    .Any(s => s.isFinal && s.status == "completed");

                                if (hasFinalCompleted)
                                {
                                    return true;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogInternalWarning(ex, "Failed to parse investigation summaries JSON in message {MessageId}", message.Id);
                        }
                    }
                }
            }

            return false; // No finished investigation found
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking investigation status for thread {ThreadId}", threadId);
            return false; // Assume not finished on error to be safe
        }
    }

    private string GetAlertInfoAsPrompt(AlertItem alert)
    {
        if (alert == null)
        {
            return "Alert information unavailable";
        }

        var essentials = alert.Properties?.Essentials;

        // Is Unknown the best fallback?
        return $@"Azure Monitor Alert Details:
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

    // TODO: Duplicate method. Move to a common helper class.
    private async Task<string> SummarizeWithLLM(string prompt, Guid threadGuid, AgentContext agentContext)
    {
        try
        {
            var message = new ChatMessage(ChatRole.System, prompt);

            var options = new ChatOptions
            {
                Tools = _agentsFactory.GetSubAgentsAITools(threadGuid, agentContext),
                Temperature = (float)0.1,
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
            _logger.LogInternalError(ex, "Error summarizing content with llm.");
            return $"Error summarizing with LLM: {ex.Message}";
        }
    }

    // Helper method to append an investigation summary to an existing message
    private async Task AppendInvestigationSummaryToMessage(
        Guid threadId,
        Guid messageId,
        string summaryTitle,
        string summaryContent,
        bool isCollapsed = true,
        string status = "completed",
        bool isFinal = false)
    {
        try
        {
            // Get the existing message
            var existingMessage = await _repository.GetMessageAsync(threadId, messageId);
            if (existingMessage == null)
            {
                _logger.LogInternalWarning("Unable to update message {MessageId} - message not found in thread {ThreadId}", messageId, threadId);
                return;
            }

            // Parse existing message to extract the current investigation summaries JSON
            string existingText = existingMessage.Text;
            string updatedText;

            // Regular expression to extract the JSON inside investigation-summaries tags
            var summariesRegex = new Regex(@"<investigation-summaries>([\s\S]*?)<\/investigation-summaries>", RegexOptions.IgnoreCase);
            var match = summariesRegex.Match(existingText);

            if (match.Success)
            {
                // Extract the existing JSON
                string existingJson = match.Groups[1].Value.Trim();

                try
                {
                    // Deserialize the existing JSON
                    var investigationSummaries = JsonSerializer.Deserialize<InvestigationSummaries>(existingJson);
                    if (investigationSummaries == null)
                    {
                        throw new Exception("Deserialized investigation summaries is null");
                    }

                    // Create the new summary item
                    var newSummary = new SummaryItem
                    {
                        title = summaryTitle,
                        summary = summaryContent,
                        isCollapsed = isCollapsed,
                        status = status,
                        isFinal = isFinal
                    };

                    // Add the new summary to the existing list
                    var updatedSummaries = investigationSummaries.summaries.ToList();
                    updatedSummaries.Add(newSummary);
                    investigationSummaries.summaries = updatedSummaries.ToArray();

                    // Serialize back to JSON
                    string updatedJson = JsonSerializer.Serialize(investigationSummaries);

                    // Replace the old JSON with the new one
                    updatedText = summariesRegex.Replace(existingText, $"<investigation-summaries>{updatedJson}</investigation-summaries>");
                }
                catch (JsonException ex)
                {
                    _logger.LogInternalError(ex, "Error parsing existing investigation summaries JSON");
                    // If JSON parsing fails, use the ChatMessageService as fallback
                    updatedText = ChatMessageService.AppendInvestigationSummary(
                        existingMessage.Text,
                        summaryTitle,
                        summaryContent,
                        isCollapsed,
                        status,
                        isFinal);
                }
            }
            else
            {
                // If no existing investigation-summaries, use the ChatMessageService
                updatedText = ChatMessageService.AppendInvestigationSummary(
                    existingMessage.Text,
                    summaryTitle,
                    summaryContent,
                    isCollapsed,
                    status,
                    isFinal);
            }

            // Create a new message with the updated text but keeping all other properties the same
            Message updatedMessage = existingMessage with { Text = updatedText };

            // Update the message in the repository
            await _repository.UpdateMessageAsync(threadId, updatedMessage);
            _logger.LogInternalInformation("Successfully appended investigation summary '{Title}' to message {MessageId}", summaryTitle, messageId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error appending investigation summary to message {MessageId}", messageId);
        }
    }

    public async Task CloseInActiveAzMonitorIncidentThreads(int cuttoffTimeWindow = 10, CancellationToken ct = default)
    {
        _logger.LogInternalInformation("Checking for inactive AzMonitor incident threads to close");

        try
        {
            // Get all AzMonitor incident threads
            var azMonitorIncidentThreads = await _repository.GetThreadsBySourceAsync(
                source: ThreadSource.Incident,
                incidentType: IncidentType.AzMonitor,
                createdAfter: null);

            _logger.LogInternalInformation($"Found {azMonitorIncidentThreads.Count()} AzMonitor incident threads");

            var cutoffTime = DateTime.UtcNow.AddMinutes(-cuttoffTimeWindow);
            var threadsToClose = new List<Thread>();

            foreach (var thread in azMonitorIncidentThreads)
            {
                // Skip if thread status or incident status is null
                if (thread.Status?.IncidentStatus == null)
                {
                    continue;
                }

                // Skip if already closed
                if (thread.Status?.IncidentStatus?.Status == ServiceAlertState.Closed.ToString())
                {
                    continue;
                }

                // Check last message timestamp
                DateTime? lastMessageTime = thread.LastMessage?.TimeStamp;

                if (lastMessageTime == null)
                {
                    // If no last message, use the thread creation timestamp
                    lastMessageTime = thread.CreatedTimestamp;
                }

                // If last activity was more than X minutes ago, mark for closure
                if (lastMessageTime < cutoffTime)
                {
                    threadsToClose.Add(thread);
                    _logger.LogInternalInformation($"Thread {thread.Id} is inactive since {lastMessageTime}, will be closed");
                }
            }

            _logger.LogInternalInformation($"Found {threadsToClose.Count} inactive threads to close");

            // Close the inactive threads by updating their AzMonitorAlertDocument status
            foreach (var thread in threadsToClose)
            {
                try
                {
                    var incidentId = thread?.Status?.IncidentStatus?.IncidentId;
                    if (thread == null || string.IsNullOrEmpty(incidentId))
                    {
                        continue;
                    }

                    var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(incidentId, incidentId);

                    if (alertDocument != null)
                    {
                        var updatedAlertDocument = alertDocument with
                        {
                            Status = ServiceAlertState.Closed.ToString(),
                            UpdatedAt = DateTime.UtcNow
                        };

                        await _dbContainer.UpsertItemAsync(
                            updatedAlertDocument,
                            new PartitionKey(updatedAlertDocument.PartitionKey)
                        );

                        _logger.LogInternalInformation($"Successfully closed AzMonitor alert document {incidentId} for inactive thread {thread.Id}");

                        await _repository.AddMessageAsync(thread.Id, new Message(
                            Guid.NewGuid(),
                            DateTime.UtcNow,
                            new Author(Role.SREAgent, "sre-agent", "Azure SRE Agent"),
                            $"🔒 **Incident Auto-Closed**\n\nThis incident thread has been automatically closed due to {cuttoffTimeWindow} minutes of inactivity."
                        ));

                        _logger.LogInternalInformation($"Added closure message to thread {thread.Id}");
                    }
                    else
                    {
                        _logger.LogInternalWarning($"Could not find AzMonitor alert document with ID {incidentId} for thread {thread.Id}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error closing AzMonitor alert for thread {thread.Id}: {ex.Message}");
                }
            }

            _logger.LogInternalInformation($"Completed processing inactive AzMonitor incident threads. Closed {threadsToClose.Count} incidents.");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error checking for inactive AzMonitor incident threads");
        }
    }

    private async Task<Thread?> FindExistingActiveThreadForAlertRule(AlertItem alert)
    {
        try
        {
            var alertRule = alert.Properties.Essentials.AlertRule;
            var targetResource = alert.Properties?.Essentials?.TargetResource;
            var alertRuleName = new ResourceIdentifier(alertRule).Name;

            _logger.LogInternalInformation($"Looking for existing active thread for alert rule: {alertRuleName}, target resource: {targetResource}");

            // Get threads from last 7 days only
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var allIncidentThreads = await _repository.GetThreadsBySourceAsync(
                source: ThreadSource.Incident,
                incidentType: IncidentType.AzMonitor,
                createdAfter: sevenDaysAgo);

            foreach (var thread in allIncidentThreads)
            {
                try
                {
                    // Get the alert document ID (GUID) from thread status
                    string? alertDocumentId = thread?.Status?.IncidentStatus?.IncidentId;
                    if (thread is null || alertDocumentId == null)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(alertDocumentId))
                    {
                        _logger.LogInternalInformation($"Skipping thread {thread.Id} - no alert document ID found in status");
                        continue;
                    }

                    // Get the AzMonitorAlertDocument for this thread
                    var alertDocument = await GetDocumentAsync<AzMonitorAlertDocument>(alertDocumentId, alertDocumentId);

                    if (alertDocument == null)
                    {
                        _logger.LogInternalInformation($"No alert document found for thread {thread.Id} with alert ID {alertDocumentId}");
                        continue;
                    }

                    // Skip if the alert document is already closed
                    if (alertDocument.Status == ServiceAlertState.Closed.ToString())
                    {
                        _logger.LogInternalInformation($"Alert document {alertDocumentId} is already closed, skipping thread {thread.Id}");
                        continue;
                    }

                    // Match based on alert document properties
                    // Compare alert rule name and target resource
                    // Right now there is no mapping b/w Thread and AzMonitorAlert document using AlertRuleId, so falling back to this.
                    if (alertDocument.Name == alertRuleName && alertDocument.TargetResourceId == targetResource)
                    {
                        _logger.LogInternalInformation($"Found existing active thread {thread.Id} for alert rule {alertRuleName} and target resource {targetResource}");
                        return thread;
                    }
                    else
                    {
                        _logger.LogInternalInformation($"Thread {thread.Id} alert document doesn't match - Document: (Name: {alertDocument.Name}, TargetResource: {alertDocument.TargetResourceId}) vs Incoming: (Name: {alertRuleName}, TargetResource: {targetResource})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error processing thread {thread.Id} while looking for existing alert rule: {ex.Message}");
                    continue;
                }
            }

            _logger.LogInternalInformation($"No existing active thread found for alert rule {alertRuleName} and target resource {targetResource}");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error finding existing thread for alert rule {alert.Properties.Essentials.AlertRule}: {ex.Message}");
        }

        return null;
    }

    private async Task PromptUserForInputAsync(Guid threadId, AgentContext agentContext, string message)
    {
        var chatMessage = new ChatMessage(ChatRole.Assistant, message);
        var messageId = Guid.NewGuid();

        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            agentContext,
            chatMessage,
            messageId);

        // Signal that processing is complete and agent is waiting for user input
        await _outboundCommunicationService.SignalProcessingComplete(threadId, messageId);
    }

    /// <summary>
    /// Checks if the alert requires user input to specify the correct target resource
    /// based on the target resource type (workspace/component alerts need clarification)
    /// </summary>
    /// <param name="alert">The alert to check</param>
    /// <returns>True if user input is required for target resource specification, false otherwise</returns>
    private static bool RequiresTargetResourceInput(AlertItem alert)
    {
        var resourceType = alert.Properties?.Essentials?.TargetResourceType?.ToLowerInvariant();
        return resourceType == "microsoft.operationalinsights/workspaces" ||
               resourceType == "microsoft.insights/components";
    }

    private class InvestigationSummaries
    {
        public required string containerTitle { get; set; }
        public required SummaryItem[] summaries { get; set; }
    }

    private class SummaryItem
    {
        public required string title { get; set; }
        public required string summary { get; set; }
        public bool isCollapsed { get; set; }
        public required string status { get; set; }
        public bool isFinal { get; set; }
    }
}
