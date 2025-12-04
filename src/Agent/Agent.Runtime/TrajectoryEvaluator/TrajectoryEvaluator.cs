// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using ThreadModel = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.TrajectoryEvaluator;

/// <summary>
/// Scanner that periodically evaluates completed threads to assess their behavior and performance.
/// Filters threads based on configurable time windows:
/// - Evaluation history range: How far back to search for threads (default: 24 hours)
/// - Cool down period: Minimum time since last modification before evaluation (default: 30 minutes)
/// </summary>
public class TrajectoryEvaluator
{
    private readonly ILogger<TrajectoryEvaluator> _logger;
    private readonly IThreadRepository _threadRepository;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IAgentMemoryClient _memory;
    private readonly Tracer _tracer;
    private readonly TimeSpan _evaluationHistoryRange; // How far back to search for threads
    private readonly TimeSpan _coolDownPeriod;         // Minimum time since last modification before evaluation

    private readonly ISearchIndexService _searchIndexService;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly InsightPostingService? _insightPostingService;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;

    public TrajectoryEvaluator(
        ILogger<TrajectoryEvaluator> logger,
        IThreadRepository threadRepository,
        IChatClientProvider chatClientProvider,
        IAgentMemoryClient memory,
        Tracer tracer,
        ISearchIndexService searchIndexService,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        InsightPostingService? insightPostingService = null,
        TimeSpan? evaluationHistoryRange = null,
        TimeSpan? coolDownPeriod = null)
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _chatClientProvider = chatClientProvider;
        _tracer = tracer;
        _memory = memory;

        _searchIndexService = searchIndexService;
        _embeddingGenerator = embeddingGenerator;
        _insightPostingService = insightPostingService;

        // Allow overriding default time windows
        _evaluationHistoryRange = evaluationHistoryRange ?? TimeSpan.FromHours(24);
        _coolDownPeriod = coolDownPeriod ?? TimeSpan.FromMinutes(30);
    }

    /// <summary>
    /// Main evaluation method that scans all completed threads from the past calendar day
    /// </summary>
    public async Task Evaluate(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation("Starting trajectory generation for completed threads from the past day");

            // Get all completed threads from the past calendar day
            var threads = await ListThreadsToEvaluate();

            _logger.LogInternalInformation($"Found {threads.Count()} completed threads to generate trajectory");

            foreach (var thread in threads)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInternalInformation("Trajectory generation cancelled");
                    break;
                }

                try
                {
                    // Check if this thread's evaluation is up to date (no new messages since last evaluation)
                    var isEvaluationUpToDate = await IsEvaluationUpToDate(thread.Id);

                    // Skip evaluation if it's already up to date
                    if (isEvaluationUpToDate == true)
                    {
                        _logger.LogInternalInformation($"Thread '{thread.Id}' trajectory generation is up to date, skipping");
                        continue;
                    }

                    // Handle error case - if we can't determine evaluation status, log and skip
                    if (isEvaluationUpToDate == null)
                    {
                        _logger.LogInternalWarning($"Could not determine evaluation status for thread '{thread.Id}', this can happen if a thread was deleted, skipping");
                        continue;
                    }

                    // If isEvaluationUpToDate == false, proceed with evaluation
                    var evaluationResult = await GenerateTrajectory(thread, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error during trajectory generation {thread.Id}: {ex.Message}");
                }
            }

            _logger.LogInternalInformation("Completed trajectory generation");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error during thread behavior evaluation: {ex.Message}");
        }
    }

    /// <summary>
    /// List all completed threads from the past calendar day that need evaluation
    /// </summary>
    private async Task<IEnumerable<ThreadModel>> ListThreadsToEvaluate()
    {
        try
        {
            // Query repository for only threads modified within the evaluation window to reduce memory and DB pressure
            var now = DateTime.UtcNow;
            var earliestTime = now - _evaluationHistoryRange; // 24 hours ago (configurable)
            var latestTime = now - _coolDownPeriod;           // 30 minutes ago (configurable)

            var allThreads = await _threadRepository.GetThreadsModifiedBetweenAsync(earliestTime, latestTime);

            // Filter threads that were modified in the specified time window and need evaluation
            var threads = new List<ThreadModel>();
            var dailyReportThreadsFiltered = 0;

            foreach (var thread in allThreads)
            {
                try
                {
                    // First check if thread is within the time window
                    var isInTimeWindow = thread.ModifiedTimestamp >= earliestTime &&
                                         thread.ModifiedTimestamp <= latestTime;

                    if (!isInTimeWindow)
                    {
                        continue; // Skip threads outside the time window
                    }

                    if (thread.TrajectoryGeneratedTimestamp >= thread.ModifiedTimestamp)
                    {
                        // Skip threads that have already been evaluated after their last modification
                        continue;
                    }

                    // Skip daily report threads created by DailyReportScanner
                    if (thread.Title.StartsWith("Daily Resources Report", StringComparison.OrdinalIgnoreCase) && thread.Source == ThreadSource.Agent)
                    {
                        dailyReportThreadsFiltered++;
                        continue;
                    }

                    threads.Add(thread);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, $"Error checking thread {thread.Id} for trajectory generation.");
                }
            }

            _logger.LogInternalInformation($"Found {threads.Count()} threads needing trajectory generation out of {allThreads.Count()} total threads (time window: {earliestTime:yyyy-MM-dd HH:mm:ss} to {latestTime:yyyy-MM-dd HH:mm:ss} UTC). Filtered out {dailyReportThreadsFiltered} daily report threads.");
            return threads;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing threads for trajectory generation");
            return Enumerable.Empty<ThreadModel>();
        }
    }

    /// <summary>
    /// Evaluate a single thread's behavior and performance.
    /// Generates trajectory data and posts insights if configured.
    /// </summary>
    /// <param name="thread">The thread to evaluate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="isUserRequested">Whether insight generation was explicitly requested by user</param>
    /// <returns>Updated thread model or null if evaluation failed</returns>
    public async Task<ThreadModel?> GenerateTrajectory(ThreadModel thread, CancellationToken cancellationToken, bool isUserRequested = false)
    {
        try
        {
            _logger.LogInternalInformation($"Generating trajectory for thread {thread.Id} from source {thread.Source}: {thread.Title}");

            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);
            var agentContext = agentContexts.FirstOrDefault();

            if (agentContext is null)
            {
                _logger.LogInternalWarning($"No agent contexts found for thread {thread.Id}, skipping trajectory generation");
                // Update timestamp anyway to avoid retrying this thread
                var updatedThread = await _threadRepository.UpdateTrajectoryGeneratedTimestampAsync(thread.Id, DateTime.UtcNow);
                return updatedThread ?? thread;
            }

            var chatMessages = await EvaluationHelper.GetChatMessages(_threadRepository, agentContext, _logger);

            // todo: pass in autohandoff from the thread info
            var startAgent = agentContext.AgentHandoffChain.FirstOrDefault(defaultValue: "meta_agent");
            var trajectoryInfo = await TrajectoryExtractor.GenerateTrajectoryAsync_v3(
                chatClient: _chatClientProvider.GeneralPurposeModel,
                chatMessages: chatMessages,
                startAgent: startAgent,
                autoHandOffToStartEnabled: thread.FeatureConfig?.AutoHandoffEnabled ?? false,
                logger: _logger,
                cancellationToken: cancellationToken);

            var trajectory = trajectoryInfo.Trajectory;

            if (trajectory != null)
            {
                // Save trajectory data for all threads
                var trajectoryString = JsonSerializer.Serialize(trajectory, _jsonSerializerOptions);
                await SaveTrajectoryAsync(thread.Id, trajectoryString, trajectoryInfo.PromptHash, cancellationToken);

                if (trajectory.IsInvestigationThread)
                {
                    var vector = await _chatClientProvider.EmbeddingModel.GenerateVectorForAgentMemoryAsync(trajectory.SymptomsObserved, _logger, cancellationToken);
                    var memory = AgentMemory.FromTrajectory(
                        trajectoryGuid: thread.Id, // use thread id as the id to keep it unique + update existing memory on re-eval
                        trajectoryData: trajectory,
                        embedding: [.. vector.Span]
                    );

                    await _searchIndexService.IndexContentAsync(memory);
                    _logger.LogInternalInformation($"Indexed investigation thread {thread.Id} for retrieval");
                }
                else
                {
                    _logger.LogInternalInformation($"Non-investigation thread {thread.Id}. Reason: {trajectory.ClassificationReason}");
                }

                // Post insights to the thread after trajectory is saved
                var insightsPosted = await PostInsightsToThreadAsync(thread, trajectory, trajectoryInfo.ChatTranscript, chatMessages.Count, isUserRequested, cancellationToken);

                // Only update thread trajectory timestamp if insights were actually posted
                if (insightsPosted)
                {
                    var updatedThread = await _threadRepository.UpdateTrajectoryGeneratedTimestampAsync(thread.Id, DateTime.UtcNow);
                    if (updatedThread != null)
                    {
                        thread = updatedThread;
                    }
                }
                else
                {
                    _logger.LogInternalInformation($"Insights were not posted for thread {thread.Id}, not updating trajectory timestamp");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to index trajectory for thread {thread.Id}");
        }

        // Return the updated thread
        return thread;
    }

    /// <summary>
    /// Check if a thread has already been evaluated and if there are new messages since last evaluation
    /// </summary>
    /// <param name="threadId">The ID of the thread to check</param>
    /// <returns>True if the thread has been evaluated recently and has no new messages, false if it needs evaluation</returns>
    private async Task<bool?> IsEvaluationUpToDate(Guid threadId)
    {
        try
        {
            var thread = await _threadRepository.GetThreadAsync(threadId);

            if (thread == null)
            {
                _logger.LogInternalWarning($"Thread {threadId} not found when checking trajectory generation status");
                return null;
            }

            // If thread has never been evaluated, it needs evaluation
            if (thread.TrajectoryGeneratedTimestamp == default)
            {
                _logger.LogInternalInformation($"Thread {threadId} never had trajectory generated, needs evaluation");
                return false;
            }

            // Check if the thread has been modified since the last evaluation
            if (thread.ModifiedTimestamp > thread.TrajectoryGeneratedTimestamp)
            {
                _logger.LogInternalInformation($"Thread {threadId} was modified at {thread.ModifiedTimestamp:yyyy-MM-dd HH:mm:ss} UTC after last trajectory generated at {thread.TrajectoryGeneratedTimestamp:yyyy-MM-dd HH:mm:ss} UTC, needs re-evaluation");
                return false;
            }

            _logger.LogInternalInformation($"Thread {threadId} has already generated trajectory at {thread.TrajectoryGeneratedTimestamp:yyyy-MM-dd HH:mm:ss} UTC and has no new messages, skipping evaluation");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error checking trajectory generation status for thread {threadId}");
            return null;
        }
    }

    /// <summary>
    /// Posts trajectory insights to a thread if meaningful insights exist
    /// </summary>
    /// <param name="thread">The thread to post insights to</param>
    /// <param name="trajectory">The trajectory data containing insights</param>
    /// <param name="chatTranscript">The full chat transcript that was analyzed</param>
    /// <param name="messageCount">Number of messages in the thread</param>
    /// <param name="isUserRequested">Whether insight generation was explicitly requested by user</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if insights were successfully posted, false otherwise</returns>
    private async Task<bool> PostInsightsToThreadAsync(
        ThreadModel thread,
        ProcessedTrajectoryOutput_v3 trajectory,
        string chatTranscript,
        int messageCount,
        bool isUserRequested,
        CancellationToken cancellationToken)
    {
        if (_insightPostingService == null)
        {
            _logger.LogInternalInformation("Insight posting is disabled (Session Insights not enabled)");
            return false;
        }

        // Conditional logic for when to generate insights:
        // 1. User explicitly requested it, OR
        // 2. It's an incident trajectory, OR
        // 3. Thread has more than 3 messages AND conversation contains valuable infrastructure/architecture knowledge worth remembering
        // Note: We filter out ScheduledTask threads as they generate insights on every run
        var shouldGenerateInsights = thread.Source != ThreadSource.ScheduledTask
            && (isUserRequested
                || thread.Source == ThreadSource.Incident
                || (messageCount > 3 && await ContainsValuableInfrastructureKnowledgeAsync(chatTranscript, cancellationToken)));

        if (!shouldGenerateInsights)
        {
            _logger.LogInternalInformation(
                $"Skipping insight generation for thread {thread.Id}. " +
                $"UserRequested: {isUserRequested}, Source: {thread.Source}, MessageCount: {messageCount}");
            return false;
        }

        try
        {
            return await _insightPostingService.PostTrajectoryInsightsAsync(
                thread.Id,
                trajectory,
                chatTranscript,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to post insights for thread {thread.Id}");
            // Don't fail the entire trajectory evaluation if insight posting fails
            return false;
        }
    }

    /// <summary>
    /// Generates and indexes trajectory data for a thread
    /// </summary>
    /// <param name="threadId">The ID of the thread</param>
    /// <param name="trajectory">The serialized trajectory data</param>
    /// <param name="promptHash">Hash of the prompt used to generate the trajectory</param>
    /// <param name="ct">Cancellation token</param>
    private async Task SaveTrajectoryAsync(
       Guid threadId,
       string trajectory,
       string promptHash,
       CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trajectory))
        {
            return;
        }

        try
        {
            using var ms = new MemoryStream(Encoding.UTF8.GetBytes(trajectory));
            var blobName = $"{promptHash}/{threadId}.txt"; // store under prompt-hash folder
            var ok = await _memory.UploadDocumentAsync(blobName, ms);

            if (!ok)
            {
                _logger.LogInternalWarning($"UploadDocumentAsync returned false for {blobName}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to upload trajectory for {threadId}");
        }
    }

    /// <summary>
    /// Uses LLM to check if conversation contains valuable infrastructure or architecture knowledge
    /// </summary>
    /// <param name="chatTranscript">The full chat transcript</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the conversation contains valuable infrastructure knowledge worth remembering</returns>
    private async Task<bool> ContainsValuableInfrastructureKnowledgeAsync(
        string chatTranscript,
        CancellationToken cancellationToken)
    {
        try
        {
            var prompt = new List<ChatMessage>
            {
                new(ChatRole.System,
                """
                You are an SRE evaluator who decides whether this chat captures environment-specific Azure infrastructure knowledge worth archiving for future incident response.

                Mark the conversation as VALUABLE when the user surfaces concrete production details, such as:
                - Named Azure resources with regions, SKUs, or configuration (e.g., "AppService prod-site in EastUS uses PremiumV2 plan with staging slot").
                - Architecture or dependency flows tied to an outage (e.g., "Traffic runs FrontDoor → App Gateway → AKS → Cosmos DB failover cluster").
                - Release cadences or deployment pipelines that shape operations (blue/green vs. ring rollout, release windows, rollback playbooks).
                - Networking, identity, or policy wiring that explains behavior (NSGs, private endpoints, managed identity role assignments, custom alerts).
                - Incident retrospectives, mitigations, runbooks, or “how we do X” process knowledge unique to their estate (patch cadence, scaling rules, feature flags, automation scripts).
                - Users prescribing specific operational procedures the agent should follow (e.g., "Always confirm AKS upgrade window with change board before drain").
                - Hard-earned lessons or heuristics a senior SRE would want to remember for future investigation or qna (e.g., "Always reset the deployment ring before rotating certs").
                - High-level application architecture or service boundaries that help future triage (tier breakdowns, microservice responsibilities, critical dependencies).

                Mark it as NOT VALUABLE when the chat only:
                - Asks generic how-to questions with no environment context.
                - Performs routine lookups without linking results to their topology.
                - Describes assistant/tool failures or empty explorations.
                - Omits specific resource names, environments, or configuration details.

                Respond with a boolean indicating if this conversation contains valuable infrastructure knowledge.
                """),
                new(ChatRole.User, $"<conversation>\n{chatTranscript}\n</conversation>")
            };

            var response = await Framework.ChatClientExtensions.GetResponseAsync(
                client: _chatClientProvider.GeneralPurposeModel,
                messages: prompt,
                outputType: typeof(InfrastructureKnowledgeEvaluation),
                options: new ChatOptions
                {
                    ToolMode = ChatToolMode.None,
                    Temperature = 0.1f,
                },
                cancellationToken: cancellationToken);

            var evaluation = JsonSerializer.Deserialize<InfrastructureKnowledgeEvaluation>(response.response.Text, _jsonSerializerOptions);

            if (evaluation != null)
            {
                _logger.LogInternalInformation(
                    $"Infrastructure knowledge evaluation: {evaluation.ContainsValuableKnowledge}. " +
                    $"Reason: {evaluation.Reasoning}");
                return evaluation.ContainsValuableKnowledge;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to evaluate infrastructure knowledge, defaulting to false");
            return false;
        }
    }
}

/// <summary>
/// Response model for infrastructure knowledge evaluation
/// </summary>
internal sealed class InfrastructureKnowledgeEvaluation
{
    [Description("True if the conversation contains valuable infrastructure or architecture knowledge worth saving for future reference")]
    public required bool ContainsValuableKnowledge { get; set; }

    [Description("Brief explanation of why this conversation does or doesn't contain valuable infrastructure knowledge")]
    public required string Reasoning { get; set; }
}
