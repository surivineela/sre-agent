// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Logging;
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
        Analyze this Azure troubleshooting session and provide structured insights to help understand agent performance and learning opportunities.

        **Session Data:**
        - Session Type: {0}
        - Classification: {1}
        - Investigation Quality: {2}/5
        - Initial Symptoms: {3}
        - Steps Followed: {4}
        - Azure Resources: {5}
        - Root Cause: {6}
        - Pitfalls: {7}
        - System Design Knowledge: {8}
        - Original Request: {9}
        
        **Evaluation Scores (from EvaluateThreadWithLLM):**
        - Intent Met / Resolved: {10}/5
        - Autonomy: {11}/5
        - Adherence: {12}/5

        **Instructions:**

        Generate structured insights following these guidelines:

        **TIMELINE (Required):**
        Transform the "Steps Followed" into chronological milestones (MAX 8). Each milestone needs:
        - Name: Clear milestone title
        - Status: Initial/Progress/Success/Issue/Resolved
        - Description: 1-2 sentences in clear business language, no technical noise
        
        Status meanings:
        - Initial: Starting point or task assignment
        - Progress: Investigation step completed, moving forward
        - Success: Found useful information or confirmed hypothesis
        - Issue: Discovered a problem or blocker
        - Resolved: Issue fixed or investigation complete

        **EVALUATION (Include if evaluation scores are provided):**
        
        Score Card:
        - For each score (Resolved, Autonomy, Adherence), provide:
          * Raw score (1-5)
          * Explanation: Direct explanation of the score (no "Analysis:" prefix or "Status:" label)
        
        Qualitative Evaluation:
        - What Went Well (MAX 3): Successful actions with technical specifics
        - What Didn't Go Well (MAX 5): **ONLY include items where there is concrete evidence of agent failure in the session data.**
          
          REQUIRED: Each item MUST reference specific evidence from the session:
          * Quote the actual error, failed command, or incorrect action taken
          * Name the specific Azure resource, API, or service involved
          * Describe what actually happened vs what should have happened
          
          Format: "[Specific failure with evidence] - [Concrete impact observed]. [Specific better approach with example]."
          
          EXCLUDE generic/boilerplate issues like:
          - "Missed intent fulfillment" without citing what was actually missed
          - "Lack of context confirmation" without evidence the agent had opportunity to ask
          - "No structured workflow" without showing what step was skipped
          - "Insufficient prompting" without showing what info was available but not requested
          - "No safety gates" without evidence a dangerous action was taken
          
          ONLY INCLUDE if the session shows:
          - Agent ran a command that returned an error (cite the error)
          - Agent queried wrong resource/scope (cite what was queried vs what was needed)
          - Agent missed information that was explicitly available in prior messages
          - Agent took an action that caused a failure or warning
          - Agent gave incorrect technical information (cite the incorrect claim)
          
          If no concrete failures are evident in the session, return an EMPTY list rather than generic criticisms.

        **DERIVED LEARNING (Required):**
        - System Design Knowledge (MAX 3, optional): NEW technical insights about Azure services/components in compact format
        - Investigation Patterns (MAX 2, optional): Reusable troubleshooting approaches in compact format (no separate headers)
        - Knowledge Graph Updates (optional): Suggested relationships in compact format (no separate Node/Relationship/Context headers)

        **CRITICAL GUIDELINES:**
        - Use ONLY session data provided - no hallucination
        - Focus EXCLUSIVELY on Azure resources, services, and investigation workflow
        - Write in clear, user-friendly language - REMOVE technical noise (subscription IDs, resource type strings like "Microsoft.Web/sites", enumeration details)
        - NEVER mention: internal AI mechanisms, model details, meta-commentary, tools, handoffs, or agent names
        - Include concrete specifics: error codes, resource names, Azure services, configuration settings, CLI commands
        - For Derived Learning: focus on NOVEL insights not already in documentation
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
    /// <param name="evaluationScores">The evaluation scores from thread evaluation (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if insights were successfully posted, false otherwise</returns>
    public async Task<bool> PostTrajectoryInsightsAsync(
        Guid threadId,
        ProcessedTrajectoryOutput_v3 trajectory,
        string chatTranscript,
        ThreadEvaluateResult? evaluationScores,
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

            var insightResult = await GenerateInsightsMessageAsync(trajectory, chatTranscript, evaluationScores, cancellationToken);

            if (string.IsNullOrWhiteSpace(insightResult.markdown))
            {
                _logger.LogInternalInformation($"No insights generated for thread {threadId}");
                return false;
            }

            _logger.LogInternalInformation($"Posting trajectory insights to thread {threadId}");

            var message = new ChatMessage(ChatRole.Assistant, insightResult.markdown);
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
            await SaveToCosmosDbAsync(threadId, trajectory, insightResult.markdown, insightResult.schema, cancellationToken);

            _logger.LogInternalInformation($"Successfully posted trajectory insights to thread {threadId}");

            // Log action for analytics
            _logger.LogAgentAction(
                action: AgentActionEvents.GenerateSessionInsight,
                parameter: insightResult.markdown,
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: threadId.ToString());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to post trajectory insights for thread {threadId}");
            return false;
        }
    }

    private async Task<(string? markdown, SessionInsightSchema? schema)> GenerateInsightsMessageAsync(
        ProcessedTrajectoryOutput_v3 trajectory,
        string chatTranscript,
        ThreadEvaluateResult? evaluationScores,
        CancellationToken cancellationToken)
    {
        try
        {
            var threadType = trajectory.IsInvestigationThread ? "Investigation/Troubleshooting" : "Routine Query";
            var pitfalls = trajectory.Pitfalls ?? "None identified";
            var initialSymptoms = trajectory.InitialSymptoms ?? "Not specified";
            var stepsFollowed = trajectory.StepsFollowed ?? "Not specified";
            var resourcesInvolved = trajectory.ResourcesInvolved ?? "Not specified";
            var rootCause = trajectory.RootCause ?? "Not determined";
            var title = trajectory.Title ?? "Untitled session";

            // Extract scores or use defaults
            var resolvedScore = evaluationScores?.Resolved ?? 0;
            var automaticScore = evaluationScores?.Automatic ?? 0;
            var adherenceScore = evaluationScores?.Adherence ?? 0;

            // Check if we have any non-zero evaluation scores
            var hasEvaluationScores = resolvedScore > 0 || automaticScore > 0 || adherenceScore > 0;

            string prompt = string.Format(
                InsightGenerationPrompt,
                threadType,
                trajectory.ClassificationReason ?? "Investigation thread",
                trajectory.InvestigationCompleteness,
                initialSymptoms,
                stepsFollowed,
                resourcesInvolved,
                rootCause,
                pitfalls,
                trajectory.SystemDesignKnowledge ?? "Not specified",
                title,
                resolvedScore,      // {10}
                automaticScore,     // {11}
                adherenceScore      // {12}
            );

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, prompt),
                new(ChatRole.User, chatTranscript)
            };

            // Use structured output
            var (response, result) = await _chatClientProvider.ReasoningHeavyModel.GetResponseAsync(
                messages,
                typeof(SessionInsightSchema),
                new ChatOptions
                {
                    Temperature = 0.3f,
                    ToolMode = ChatToolMode.None,
                },
                cancellationToken: cancellationToken
            );

            if (result is SessionInsightSchema insightSchema)
            {
                return (ConvertStructuredInsightToMarkdown(insightSchema, hasEvaluationScores), insightSchema);
            }

            // Fallback to text response if structured output fails
            return (response.Text?.Trim(), null);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to generate insights message");
            return (null, null);
        }
    }

    private async Task SaveToCosmosDbAsync(
        Guid threadId,
        ProcessedTrajectoryOutput_v3 trajectory,
        string insightMarkdown,
        SessionInsightSchema? insightSchema,
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
                AgentPerformance: null, // No longer populated - kept for backward compatibility
                Pitfalls: ParsePitfallsList(trajectory.Pitfalls),
                KeyLearnings: ExtractKeyLearningsFromSchema(insightSchema),
                Feedback: new List<InsightFeedback>(),
                TrajectoryId: threadId.ToString(), // Use thread ID as trajectory ID (matches how trajectories are indexed in Agent Memory)
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

    private string ConvertStructuredInsightToMarkdown(SessionInsightSchema insight, bool includeEvaluation)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Session Insights");
        sb.AppendLine();

        // Timeline section
        sb.AppendLine("## TIMELINE");
        sb.AppendLine();
        foreach (var milestone in insight.Timeline.Milestones)
        {
            sb.AppendLine($"**{milestone.Name}** - Status: {milestone.Status}");
            sb.AppendLine(milestone.Description);
            sb.AppendLine();
        }

        // Evaluation section (always show qualitative, only show scores if available)
        if (insight.Evaluation != null)
        {
            sb.AppendLine("## EVALUATION");
            sb.AppendLine();

            // Score Card section (only if scores are available)
            if (includeEvaluation)
            {
                sb.AppendLine("### Score Card");
                sb.AppendLine();

                // Resolved Score
                var resolved = insight.Evaluation.ScoreCard.ResolvedScore;
                sb.AppendLine($"#### Resolved / Intent Met Score: {resolved.Score}/5");
                sb.AppendLine();
                sb.AppendLine(resolved.Explanation);
                sb.AppendLine();

                // Autonomy Score
                var autonomy = insight.Evaluation.ScoreCard.AutonomyScore;
                sb.AppendLine($"#### Autonomy Score: {autonomy.Score}/5");
                sb.AppendLine();
                sb.AppendLine(autonomy.Explanation);
                sb.AppendLine();

                // Adherence Score
                var adherence = insight.Evaluation.ScoreCard.AdherenceScore;
                sb.AppendLine($"#### Adherence Score: {adherence.Score}/5");
                sb.AppendLine();
                sb.AppendLine(adherence.Explanation);
                sb.AppendLine();

                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Qualitative Evaluation (always included when Evaluation exists)
            sb.AppendLine("### Qualitative Evaluation");
            sb.AppendLine();

            if (insight.Evaluation.QualitativeEvaluation.WhatWentWell.Count > 0)
            {
                sb.AppendLine("#### What Went Well");
                foreach (var achievement in insight.Evaluation.QualitativeEvaluation.WhatWentWell)
                {
                    sb.AppendLine($"- **{achievement.Name}:** {achievement.Description}");
                }
                sb.AppendLine();
            }

            if (insight.Evaluation.QualitativeEvaluation.WhatDidntGoWell.Count > 0)
            {
                sb.AppendLine("#### What Didn't Go Well");
                sb.AppendLine();
                foreach (var failure in insight.Evaluation.QualitativeEvaluation.WhatDidntGoWell)
                {
                    sb.AppendLine($"- {failure.Description}");
                    sb.AppendLine();
                }
            }
        }

        // Derived Learning section (only include if there's content)
        var hasSystemDesignKnowledge = insight.DerivedLearning.SystemDesignKnowledge != null && insight.DerivedLearning.SystemDesignKnowledge.Count > 0;
        var hasInvestigationPatterns = insight.DerivedLearning.InvestigationPatterns != null && insight.DerivedLearning.InvestigationPatterns.Count > 0;

        if (hasSystemDesignKnowledge || hasInvestigationPatterns)
        {
            sb.AppendLine("## DERIVED LEARNING FROM THREAD");
            sb.AppendLine();

            if (hasSystemDesignKnowledge)
            {
                sb.AppendLine("### System Design Knowledge");
                foreach (var knowledge in insight.DerivedLearning.SystemDesignKnowledge!)
                {
                    sb.AppendLine($"- **{knowledge.ServiceOrComponent}:** {knowledge.Insight}");
                }
                sb.AppendLine();
            }

            if (hasInvestigationPatterns)
            {
                sb.AppendLine("### Investigation Pattern");
                foreach (var pattern in insight.DerivedLearning.InvestigationPatterns!)
                {
                    sb.AppendLine($"- {pattern.Description}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().Trim();
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

    /// <summary>
    /// Extracts key learnings from the SessionInsightSchema's DerivedLearning section.
    /// Combines SystemDesignKnowledge insights and InvestigationPatterns into a flat list.
    /// </summary>
    private static List<string>? ExtractKeyLearningsFromSchema(SessionInsightSchema? schema)
    {
        if (schema?.DerivedLearning == null)
        {
            return null;
        }

        var learnings = new List<string>();

        // Extract system design knowledge insights
        if (schema.DerivedLearning.SystemDesignKnowledge != null)
        {
            foreach (var knowledge in schema.DerivedLearning.SystemDesignKnowledge)
            {
                learnings.Add($"[{knowledge.ServiceOrComponent}] {knowledge.Insight}");
            }
        }

        // Extract investigation patterns
        if (schema.DerivedLearning.InvestigationPatterns != null)
        {
            foreach (var pattern in schema.DerivedLearning.InvestigationPatterns)
            {
                learnings.Add($"[Pattern] {pattern.Description}");
            }
        }

        return learnings.Count > 0 ? learnings : null;
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

    /// <summary>
    /// Builds extracted knowledge structure from feedback extraction result.
    /// Transforms raw extraction results into structured knowledge items.
    /// </summary>
    /// <param name="extractionResult">The raw extraction result from LLM</param>
    /// <param name="originalComment">The original user comment</param>
    /// <returns>Tuple of extracted knowledge and whether it should be flagged for review</returns>
    public static (ExtractedFeedbackKnowledge? knowledge, bool flagForReview) BuildExtractedKnowledge(
        FeedbackExtractionResult extractionResult,
        string originalComment)
    {
        var knowledgeItems = new List<FeedbackKnowledgeItem>();

        if (!string.IsNullOrWhiteSpace(extractionResult.InvestigationPattern))
        {
            knowledgeItems.Add(new FeedbackKnowledgeItem(
                Type: FeedbackKnowledgeType.InvestigationPattern,
                Comment: extractionResult.InvestigationPattern
            ));
        }

        if (!string.IsNullOrWhiteSpace(extractionResult.SystemDesignKnowledge))
        {
            knowledgeItems.Add(new FeedbackKnowledgeItem(
                Type: FeedbackKnowledgeType.SystemDesignKnowledge,
                Comment: extractionResult.SystemDesignKnowledge
            ));
        }

        if (!string.IsNullOrWhiteSpace(extractionResult.Other))
        {
            knowledgeItems.Add(new FeedbackKnowledgeItem(
                Type: FeedbackKnowledgeType.Other,
                Comment: extractionResult.Other
            ));
        }

        if (knowledgeItems.Count > 0)
        {
            return (new ExtractedFeedbackKnowledge(
                KnowledgeItems: knowledgeItems,
                OriginalComment: originalComment,
                SubmittedAt: DateTime.UtcNow
            ), false);
        }

        return (null, true); // No knowledge extracted, flag for review
    }
}

/// <summary>
/// Result of feedback knowledge extraction
/// </summary>
public class FeedbackExtractionResult
{
    [System.ComponentModel.Description("Extracted investigation pattern knowledge (empty string if none found)")]
    public required string InvestigationPattern { get; set; }

    [System.ComponentModel.Description("Extracted system design knowledge (empty string if none found)")]
    public required string SystemDesignKnowledge { get; set; }

    [System.ComponentModel.Description("Other feedback that doesn't fit the above categories (empty string if none)")]
    public required string Other { get; set; }
}
