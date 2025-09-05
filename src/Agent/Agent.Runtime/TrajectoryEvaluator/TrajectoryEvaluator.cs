// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.AgentMemory;
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
    private readonly IChatClient _chatClient;
    private readonly IAgentMemoryClient _memory;
    private readonly Tracer _tracer;
    private readonly TimeSpan _evaluationHistoryRange; // How far back to search for threads
    private readonly TimeSpan _coolDownPeriod;         // Minimum time since last modification before evaluation
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    private readonly ISearchIndexService _searchIndexService;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = AIJsonUtilities.DefaultOptions;

    public TrajectoryEvaluator(
        ILogger<TrajectoryEvaluator> logger,
        IThreadRepository threadRepository,
        IChatClient chatClient,
        IAgentMemoryClient memory,
        Tracer tracer,
        ISearchIndexService searchIndexService,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        TimeSpan? evaluationHistoryRange = null,
        TimeSpan? coolDownPeriod = null)
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _chatClient = chatClient;
        _tracer = tracer;
        _memory = memory;

        _searchIndexService = searchIndexService;
        _embeddingGenerator = embeddingGenerator;

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
    /// Evaluate a single thread's behavior and performance
    /// </summary>
    private async Task<ThreadModel?> GenerateTrajectory(ThreadModel thread, CancellationToken cancellationToken)
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
                await _threadRepository.UpdateTrajectoryGeneratedTimestampAsync(thread.Id, DateTime.UtcNow);
                return thread;
            }

            var chatMessages = await EvaluationHelper.GetChatMessages(_threadRepository, agentContext, _logger);

            // todo: pass in autohandoff from the thread info
            var startAgent = agentContext.AgentHandoffChain.FirstOrDefault(defaultValue: "meta_agent");
            var trajectoryInfo = await TrajectoryExtractor.GenerateTrajectoryAsync_v3(
                chatClient: _chatClient,
                chatMessages: chatMessages,
                startAgent: startAgent,
                cancellationToken: cancellationToken);

            var trajectory = trajectoryInfo.Trajectory;

            if (trajectory != null)
            {
                if (trajectory.IsInvestigationThread)
                {
                    var trajectoryString = JsonSerializer.Serialize(trajectory, _jsonSerializerOptions);
                    await SaveTrajectoryAsync(thread.Id, trajectoryString, trajectoryInfo.PromptHash, cancellationToken);

                    var vector = await _embeddingGenerator.GenerateVectorForAgentMemoryAsync(trajectory.SymptomsObserved, _logger, cancellationToken);
                    var memory = AgentMemory.FromTrajectory(
                        id: thread.Id.ToString(),
                        trajectoryData: trajectory,
                        embedding: [.. vector.Span]
                    );

                    await _searchIndexService.IndexContentAsync(memory);

                    // Update thread with evaluation timestamp
                    await _threadRepository.UpdateTrajectoryGeneratedTimestampAsync(thread.Id, DateTime.UtcNow);
                }
                else
                {
                    _logger.LogInternalWarning($"Skipping non-investigation thread. Reason: {trajectory.ClassificationReason}");

                    // Update timestamp anyway to avoid retrying this thread
                    await _threadRepository.UpdateTrajectoryGeneratedTimestampAsync(thread.Id, DateTime.UtcNow);
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
        if (string.IsNullOrWhiteSpace(trajectory)) return;

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
}
