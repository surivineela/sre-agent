// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.TrajectoryEvaluator;

/// <summary>
/// Service responsible for generating and posting trajectory insights to threads.
/// Analyzes trajectory data (pitfalls, investigation quality, etc.) and creates
/// actionable feedback messages for users.
/// </summary>
public class InsightPostingService
{
    private readonly IAgentOutboundCommunicationService _outboundService;
    private readonly IThreadRepository _threadRepository;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly ILogger<InsightPostingService> _logger;
    private readonly AgentMemorySettings _agentMemorySettings;
    private readonly ISessionInsightRepository _sessionInsightRepository;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;

    private const string InsightGenerationPrompt = """
        Analyze this Azure troubleshooting session and provide concise insights in two sections: Timeline and Agent Performance.

        **Session Data:**
        - Session Type: {0}
        - Classification: {1}
        - Investigation Quality: {2}/5
        - Final Outcome: {3}
        - Initial Symptoms: {4}
        - Steps Followed: {5}
        - Azure Resources: {6}
        - Root Cause: {7}
        - Pitfalls: {8}
        - System Design Knowledge: {9}
        - Original Request: {10}

        **Output Format:**

        # Session Insight

        ## TIMELINE

        Transform the "Steps Followed" into a chronological narrative showing the investigation flow. Each milestone should:
        - Show what action was taken
        - Indicate the outcome or finding
        - Use clear status markers
        - Focus on the technical actions, not which tool/agent performed them

        **Format (MAX 8 milestones):**
        
        **[Milestone Name]** - [Status: Initial/Progress/Success/Issue/Resolved]
        [1-2 sentences: what happened, what was discovered, or what decision was made]

        Status types:
        - Initial: Starting point or task assignment
        - Progress: Investigation step completed, moving forward
        - Success: Found useful information or confirmed hypothesis
        - Issue: Discovered a problem or blocker
        - Resolved: Issue fixed or investigation complete

        **Example:**
        **Resource Discovery Initiated** - Status: Initial
        Located container apps and Redis instances named "iot-dashboard" across subscriptions to identify affected resources.

        **Network Connectivity Test** - Status: Issue
        Connectivity tests revealed timeouts on port 6380, indicating potential firewall or NSG blocking.

        **Root Cause Identified** - Status: Resolved
        NSG rule blocking port 6380 was discovered and confirmed as the cause of Redis connection failures.

        ## AGENT PERFORMANCE

        Analyze how the agent performed based on system design claims and identified pitfalls.

        ### What Went Well (MAX 3)
        Based on "System Design Knowledge" - highlight agent's correct understanding and effective actions:
        - **[Achievement]:** [What the agent did correctly with technical specifics]

        ### Areas for Improvement (MAX 5)
        Based on "Pitfalls" - show what could have been done better:
        - **Did:** [What the agent did incorrectly]
          **Should:** [What the correct approach would be, including specific Azure CLI commands, portal steps, or configuration changes]

        ### Key Learnings (MAX 3)
        Actionable takeaways for future sessions:
        - **[Learning]:** [Specific technical insight or pattern to remember]

        **Need more context for better troubleshooting?** Upload runbooks, documentation, or configuration files to the [Knowledge Base](/static/#/settings/dataknowledge) to improve future investigations.

        **CRITICAL GUIDELINES:**
        - Keep output scannable and concise - no verbose explanations
        - LIMIT each subsection to specified MAX items for readability
        - Use ONLY session data provided - no hallucination
        - Focus EXCLUSIVELY on Azure resources, services, and investigation workflow
        - Include emojis in the overall timeline.
        - NEVER mention: internal AI mechanisms, model details, meta-commentary, tools, handoffs, or agent names
        - Include concrete specifics: error codes, resource names, Azure CLI commands, configuration settings
        - For Timeline: describe actions and outcomes without mentioning which agent/tool performed them
        - For Agent Performance: extract insights from both successes and mistakes
        - Replace any tool/agent references with the action performed (e.g., "ran diagnostic tests" instead of "azure_cli_agent executed tests")

        Return ONLY the formatted insights or empty string if insufficient technical content.
        """;

    private const string ClassificationExplanationPrompt = """
        Generate a brief session classification insight focused on Azure troubleshooting.

        **Classification:** {0}
        **Observations:** {1}

        **Format:**
        ### Session Type: Routine Query

        **Why this was routine:** [1 sentence explanation focusing on Azure resource operations]

        **Efficiency tip:** [1 specific Azure CLI/PowerShell/Portal suggestion with example]

        **When to dig deeper:** [1 sentence on when deeper Azure troubleshooting would help]

        **Critical:**
        - Focus only on Azure resources and tools
        - Never mention internal system details
        - Keep under 75 words
        
        Return ONLY the formatted text or empty string if not meaningful.
        """;

    public InsightPostingService(
        IAgentOutboundCommunicationService outboundService,
        IThreadRepository threadRepository,
        IChatClientProvider chatClientProvider,
        ILogger<InsightPostingService> logger,
        ISessionInsightRepository sessionInsightRepository,
        AgentMemorySettings agentMemorySettings)
    {
        _outboundService = outboundService;
        _threadRepository = threadRepository;
        _chatClientProvider = chatClientProvider;
        _logger = logger;
        _sessionInsightRepository = sessionInsightRepository;
        _agentMemorySettings = agentMemorySettings;
    }

    /// <summary>
    /// Posts trajectory insights to a thread if insights are meaningful and settings allow it.
    /// </summary>
    /// <param name="threadId">The ID of the thread to post insights to</param>
    /// <param name="trajectory">The trajectory data containing insights</param>
    /// <param name="chatTranscript">The full chat transcript that was analyzed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if insights were successfully posted, false otherwise</returns>
    public async Task<bool> PostTrajectoryInsightsAsync(
        Guid threadId,
        ProcessedTrajectoryOutput_v3 trajectory,
        string chatTranscript,
        CancellationToken cancellationToken)
    {
        try
        {
            // Enable session insights if Memory is enabled OR insight posting is explicitly enabled
            var sessionInsightsEnabled = _agentMemorySettings.Enabled || _agentMemorySettings.EnableInsightPosting;

            if (!sessionInsightsEnabled)
            {
                _logger.LogInternalInformation("Insight posting is disabled (both AgentMemory.Enabled and AgentMemory.EnableInsightPosting are false)");
                return false;
            }

            var insightMessage = await GenerateInsightsMessageAsync(trajectory, chatTranscript, cancellationToken);

            if (string.IsNullOrWhiteSpace(insightMessage))
            {
                _logger.LogInternalInformation($"No insights generated for thread {threadId}");
                return false;
            }

            _logger.LogInternalInformation($"Posting trajectory insights to thread {threadId}");

            var message = new ChatMessage(ChatRole.Assistant, insightMessage);
            var insightMessageId = Guid.NewGuid();

            // Use empty orchestration ID to prevent triggering orchestration continuation
            await _outboundService.UpdateThreadWithAgentMessageAsync(
                threadId: threadId,
                message: message,
                messageId: insightMessageId,
                type: StreamMessageType.TrajectoryInsight
            );

            // Signal processing complete to stop the chat from continuing to "think"
            await _outboundService.SignalProcessingComplete(threadId, insightMessageId, cancellationToken);

            // Save to Cosmos DB for persistent insights
            await SaveToCosmosDbAsync(threadId, trajectory, insightMessage, cancellationToken);

            _logger.LogInternalInformation($"Successfully posted trajectory insights to thread {threadId}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to post trajectory insights for thread {threadId}");
            return false;
        }
    }

    private async Task<string?> GenerateInsightsMessageAsync(
        ProcessedTrajectoryOutput_v3 trajectory,
        string chatTranscript,
        CancellationToken cancellationToken)
    {
        try
        {
            var threadType = trajectory.IsInvestigationThread ? "Investigation/Troubleshooting" : "Routine Query";
            var pitfalls = trajectory.Pitfalls ?? "None identified";
            var outcome = trajectory.InvestigationOutcome ?? "Not specified";
            var initialSymptoms = trajectory.InitialSymptoms ?? "Not specified";
            var stepsFollowed = trajectory.StepsFollowed ?? "Not specified";
            var resourcesInvolved = trajectory.ResourcesInvolved ?? "Not specified";
            var rootCause = trajectory.RootCause ?? "Not determined";
            var title = trajectory.Title ?? "Untitled session";

            var prompt = string.Format(
                InsightGenerationPrompt,
                threadType,
                trajectory.ClassificationReason ?? "Investigation thread",
                trajectory.InvestigationCompleteness,
                outcome,
                initialSymptoms,
                stepsFollowed,
                resourcesInvolved,
                rootCause,
                pitfalls,
                trajectory.SystemDesignKnowledge ?? "Not specified",
                title
            );

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompt),
                new(ChatRole.User, chatTranscript)
            };

            var response = await _chatClientProvider.GeneralPurposeModel.GetResponseAsync(
                messages,
                new ChatOptions
                {
                    Temperature = 0.3f,
                    ToolMode = ChatToolMode.None,
                    ResponseFormat = ChatResponseFormat.Text,
                },
                cancellationToken
            );

            return response.Text?.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to generate insights message");
            return null;
        }
    }

    private async Task SaveToCosmosDbAsync(
        Guid threadId,
        ProcessedTrajectoryOutput_v3 trajectory,
        string insightMarkdown,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"Saving session insight to Cosmos DB for thread {threadId}");

            // Get thread details
            var thread = await _threadRepository.GetThreadAsync(threadId);
            if (thread == null)
            {
                _logger.LogInternalWarning($"Thread {threadId} not found, cannot save session insight");
                return;
            }

            // Parse timeline from steps
            var timelineItems = ParseTimelineFromSteps(trajectory.StepsFollowed);

            // Create session insight document using record constructor
            var insightDocument = new SessionInsightDocument(
                Id: Guid.NewGuid().ToString(),
                ThreadId: threadId.ToString(),
                Title: trajectory.Title ?? thread.Title ?? "Session Insight",
                GeneratedTimestamp: DateTime.UtcNow,
                ThreadCreatedTimestamp: thread.CreatedTimestamp,
                ThreadModifiedTimestamp: thread.ModifiedTimestamp,
                ThreadSource: thread.Source.ToString(),
                IsInvestigationThread: trajectory.IsInvestigationThread,
                ClassificationReason: trajectory.ClassificationReason,
                InitialSymptoms: trajectory.InitialSymptoms,
                SymptomsObserved: trajectory.SymptomsObserved,
                RootCause: trajectory.RootCause,
                SystemDesignKnowledge: trajectory.SystemDesignKnowledge,
                Timeline: timelineItems,
                StepsFollowed: trajectory.StepsFollowed,
                ResourcesInvolved: ParseResourceList(trajectory.ResourcesInvolved),
                ResourceTypesInvolved: ExtractResourceTypes(trajectory.ResourcesInvolved),
                SubscriptionsInvolved: ExtractSubscriptions(trajectory.ResourcesInvolved),
                AgentPerformance: new AgentPerformanceMetrics(
                    TotalAgentTurns: 0, // Not available in trajectory v3
                    TotalToolCalls: 0,
                    SuccessfulToolCalls: 0,
                    FailedToolCalls: 0,
                    AgentHandoffs: 0,
                    AgentsInvolved: null,
                    TotalDuration: null,
                    EfficiencyRating: trajectory.InvestigationCompleteness >= 4 ? "Excellent" :
                                    trajectory.InvestigationCompleteness >= 3 ? "Good" : "Fair"
                ),
                Pitfalls: ParsePitfallsList(trajectory.Pitfalls),
                KeyLearnings: ExtractKeyLearnings(insightMarkdown),
                Feedback: new List<InsightFeedback>(),
                TrajectoryJson: JsonSerializer.Serialize(trajectory, _jsonSerializerOptions),
                InsightMarkdown: insightMarkdown
            );

            await _sessionInsightRepository.UpsertSessionInsightAsync(insightDocument);
            _logger.LogInternalInformation($"Saved session insight to Cosmos DB for thread {threadId}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to save session insight to Cosmos DB for thread {threadId}");
        }
    }

    private List<string>? ParseResourceList(string? resourcesInvolved)
    {
        if (string.IsNullOrWhiteSpace(resourcesInvolved) || resourcesInvolved.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var resources = resourcesInvolved
            .Split(new[] { ',', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();

        return resources.Count > 0 ? resources : null;
    }

    private List<string>? ParsePitfallsList(string? pitfalls)
    {
        if (string.IsNullOrWhiteSpace(pitfalls) || pitfalls.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var pitfallList = new List<string>();
        var lines = pitfalls.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("- **Did:") || trimmed.StartsWith("**Did:"))
            {
                pitfallList.Add(trimmed);
            }
        }

        return pitfallList.Count > 0 ? pitfallList : null;
    }

    private List<TimelineItem>? ParseTimelineFromSteps(string? stepsFollowed)
    {
        if (string.IsNullOrWhiteSpace(stepsFollowed))
        {
            return null;
        }

        var items = new List<TimelineItem>();
        var lines = stepsFollowed.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var order = 1;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("**") && trimmed.Contains("** - Status:"))
            {
                // Parse format: **Title** - Status: StatusValue
                var parts = trimmed.Split(new[] { "** - Status:" }, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    var title = parts[0].Replace("**", "").Trim();
                    var statusAndDesc = parts[1].Trim();
                    var statusEndIndex = statusAndDesc.IndexOf('\n');

                    var status = statusEndIndex > 0
                        ? statusAndDesc.Substring(0, statusEndIndex).Trim()
                        : statusAndDesc.Trim();

                    var description = statusEndIndex > 0
                        ? statusAndDesc.Substring(statusEndIndex + 1).Trim()
                        : string.Empty;

                    items.Add(new TimelineItem(
                        Title: title,
                        Status: status,
                        Description: description,
                        Order: order++
                    ));
                }
            }
        }

        return items.Count > 0 ? items : null;
    }

    private List<string>? ExtractResourceTypes(string? resourcesInvolved)
    {
        if (string.IsNullOrWhiteSpace(resourcesInvolved) || resourcesInvolved.Equals("N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Common Azure resource patterns
        var resourceTypePatterns = new[]
        {
            "Container App", "Redis", "Storage Account", "Key Vault", "App Service",
            "Function App", "SQL Database", "Cosmos DB", "Virtual Machine", "AKS",
            "Application Insights", "Log Analytics", "Service Bus", "Event Hub"
        };

        foreach (var pattern in resourceTypePatterns)
        {
            if (resourcesInvolved.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                types.Add(pattern);
            }
        }

        return types.Count > 0 ? types.ToList() : null;
    }

    private List<string>? ExtractSubscriptions(string? resourcesInvolved)
    {
        if (string.IsNullOrWhiteSpace(resourcesInvolved))
        {
            return null;
        }

        var subscriptions = new HashSet<string>();
        var guidPattern = @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";
        var matches = System.Text.RegularExpressions.Regex.Matches(resourcesInvolved, guidPattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            subscriptions.Add(match.Value);
        }

        return subscriptions.Count > 0 ? subscriptions.ToList() : null;
    }

    private List<string>? ExtractKeyLearnings(string? insightMarkdown)
    {
        if (string.IsNullOrWhiteSpace(insightMarkdown))
        {
            return null;
        }

        var learnings = new List<string>();
        var lines = insightMarkdown.Split('\n');
        var inLearningsSection = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("### Key Learnings"))
            {
                inLearningsSection = true;
                continue;
            }

            if (inLearningsSection)
            {
                if (trimmed.StartsWith("###") || trimmed.StartsWith("##"))
                {
                    break;
                }

                if (trimmed.StartsWith("- **"))
                {
                    learnings.Add(trimmed.Substring(2).Trim());
                }
            }
        }

        return learnings.Count > 0 ? learnings : null;
    }
}

