// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using ThreadModel = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.ThreadEvaluator;

/// <summary>
/// Scanner that periodically evaluates completed threads to assess their behavior and performance.
/// Filters threads based on configurable time windows:
/// - Evaluation history range: How far back to search for threads (default: 24 hours)
/// - Cool down period: Minimum time since last modification before evaluation (default: 30 minutes)
/// </summary>
public partial class ThreadEvaluator
{
    private readonly ILogger<ThreadEvaluator> _logger;
    private readonly IThreadRepository _threadRepository;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly Tracer _tracer;
    private readonly IRagEvaluator _ragEvaluator;
    // Configurable time windows for thread filtering
    private readonly TimeSpan _evaluationHistoryRange; // How far back to search for threads
    private readonly TimeSpan _coolDownPeriod;         // Minimum time since last modification before evaluation

    public ThreadEvaluator(
      ILogger<ThreadEvaluator> logger,
      IThreadRepository threadRepository,
      IChatClientProvider chatClientProvider,
      Tracer tracer,
      IRagEvaluator ragEvaluator,
      TimeSpan? evaluationHistoryRange = null,
      TimeSpan? coolDownPeriod = null)
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _chatClientProvider = chatClientProvider;
        _tracer = tracer;
        _ragEvaluator = ragEvaluator;

        // Allow overriding default time windows
        _evaluationHistoryRange = evaluationHistoryRange ?? TimeSpan.FromHours(24);
        _coolDownPeriod = coolDownPeriod ?? TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Main evaluation method that scans all completed threads from the past calendar day
    /// </summary>
    public async Task Evaluate(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation("Starting thread behavior evaluation for completed threads from the past day");

            // Get all completed threads from the past calendar day
            var threads = await ListThreadsToEvaluate();

            _logger.LogInternalInformation($"Found {threads.Count()} completed threads to evaluate");

            foreach (var thread in threads)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInternalInformation("Thread behavior evaluation cancelled");
                    break;
                }

                try
                {
                    // Check if this thread's evaluation is up to date (no new messages since last evaluation)
                    var isEvaluationUpToDate = IsEvaluationUpToDate(thread);

                    // Skip evaluation if it's already up to date
                    if (isEvaluationUpToDate)
                    {
                        _logger.LogInternalInformation($"Thread '{thread.Id}' evaluation is up to date, skipping");
                        continue;
                    }

                    // If isEvaluationUpToDate == false, proceed with evaluation
                    var evaluationResult = await EvaluateThread(thread, cancellationToken);
                    var ragEvalResult = await _ragEvaluator.EvaluateRagPerformanceAsync(thread, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"Error evaluating thread {thread.Id}: {ex.Message}");
                }
            }

            _logger.LogInternalInformation("Completed thread behavior evaluation");
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
            var localAuthScannerThreadsFiltered = 0;

            foreach (var thread in allThreads)
            {
                try
                {
                    // // First check if thread is within the time window
                    // bool isInTimeWindow = thread.ModifiedTimestamp >= earliestTime &&
                    //                      thread.ModifiedTimestamp <= latestTime;

                    // if (!isInTimeWindow)
                    // {
                    //     continue; // Skip threads outside the time window
                    // }

                    if (thread.EvaluatedTimestamp >= thread.ModifiedTimestamp)
                    {
                        // Skip threads that have already been evaluated after their last modification
                        continue;
                    }

                    // Skip daily report threads created by DailyReportScanner
                    if (thread.Title.StartsWith("Daily Resources Report", StringComparison.OrdinalIgnoreCase) && thread.Source == ThreadSource.DailyReport)
                    {
                        dailyReportThreadsFiltered++;
                        continue;
                    }

                    // Skip LocalAuthScanner threads with minimal reasoning content
                    if (thread.Title.StartsWith("LocalAuthScanner", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var shouldSkip = await ShouldSkipLocalAuthScannerThread(thread);
                            if (shouldSkip)
                            {
                                localAuthScannerThreadsFiltered++;
                                _logger.LogInternalInformation($"Skipping LocalAuthScanner thread {thread.Id} - contains only minimal reasoning content about unsafe key-based resources");
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalWarning(ex, $"Error checking LocalAuthScanner thread {thread.Id} for filtering criteria");
                        }
                    }

                    threads.Add(thread);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, $"Error checking thread {thread.Id} for evaluation criteria");
                }
            }

            _logger.LogInternalInformation($"Found {threads.Count()} threads needing evaluation out of {allThreads.Count()} total threads (time window: {earliestTime:yyyy-MM-dd HH:mm:ss} to {latestTime:yyyy-MM-dd HH:mm:ss} UTC). Filtered out {dailyReportThreadsFiltered} daily report threads and {localAuthScannerThreadsFiltered} LocalAuthScanner threads.");
            return threads;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error listing threads for evaluation");
            return Enumerable.Empty<ThreadModel>();
        }
    }

    /// <summary>
    /// Evaluate a single thread's behavior and performance
    /// </summary>
    private async Task<ThreadEvaluateResult?> EvaluateThread(ThreadModel thread, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"Evaluating thread {thread.Id} from source {thread.Source}: {thread.Title}");

            // Calculate basic metrics
            var duration = thread.ModifiedTimestamp - thread.CreatedTimestamp;

            // Get all messages for the thread
            var messages = await _threadRepository.GetMessagesAsync(thread.Id);
            var messagesList = messages.ToList();

            // Count user interactions (messages from users)
            var userInteractionCount = messagesList.Count(m => m.Author.Role == Role.User);

            // Get agent contexts to analyze tool calls
            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);
            var agentContextsList = agentContexts.ToList();

            // Determine the primary agent type for this thread
            var agentType = GetThreadAgentType(agentContextsList);

            var toolCallMetrics = await CalculateToolCallMetrics(thread.Id, agentContextsList);

            // Get chat and reasoning message history for LLM evaluation
            (var chatHistory, var reasoningHistory) = await GetMessageHistories(thread.Id, agentContextsList);

            // Call LLM to evaluate thread quality
            var llmEvaluation = await EvaluateThreadWithLLM(thread, chatHistory, reasoningHistory, toolCallMetrics, cancellationToken);

            // If LLM evaluation failed, return null to indicate the thread should not be marked as evaluated
            if (llmEvaluation == null)
            {
                _logger.LogInternalWarning($"Skipping thread {thread.Id} evaluation due to LLM evaluation failure");
                return null;
            }

            // Calculate SAT Score as the average of all dimensions, rounded up to an integer
            var satScore = Math.Ceiling((double)(llmEvaluation.Resolved + llmEvaluation.Satisfied + llmEvaluation.Automatic + llmEvaluation.Smooth + llmEvaluation.Concise + llmEvaluation.Adherence) / 6.0);

            await EvaluateHandoffsWithLLM(thread, cancellationToken); // Calculate handoff score
            await EvaluateTasksWithLLM(thread, cancellationToken);

            var evaluationResult = new ThreadEvaluateResult(
                Id: Guid.NewGuid(),
                ThreadId: thread.Id,
                ThreadTitle: thread.Title,
                Duration: duration,
                UserInteractionCount: userInteractionCount,
                ToolCallCount: toolCallMetrics.TotalToolCalls,
                ToolCallSuccessRate: toolCallMetrics.OverallSuccessRate,
                AzCliCallCount: toolCallMetrics.AzCliCalls,
                AzCliSuccessRate: toolCallMetrics.AzCliSuccessRate,
                KubectlCallCount: toolCallMetrics.KubectlCalls,
                KubectlSuccessRate: toolCallMetrics.KubectlSuccessRate,
                EvaluationSummary: llmEvaluation.Summary,
                SATScore: satScore,
                Category: llmEvaluation.Category,
                Resolved: llmEvaluation.Resolved,
                Satisfied: llmEvaluation.Satisfied,
                Automatic: llmEvaluation.Automatic,
                Smooth: llmEvaluation.Smooth,
                Concise: llmEvaluation.Concise,
                Adherence: llmEvaluation.Adherence,
                Priority: llmEvaluation.Priority,
                PriorityReason: llmEvaluation.PriorityReason,
                EvaluatedTimestamp: thread.ModifiedTimestamp,
                AgentType: agentType
            );

            // Store the evaluation result in the repository
            try
            {
                await _threadRepository.CreateThreadEvaluateResultAsync(evaluationResult);
                _logger.LogInternalInformation($"Successfully stored thread evaluation result for thread {thread.Id} with evaluation ID {evaluationResult.Id} (AgentType: {agentType})");
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to store thread evaluation result for thread {thread.Id}: {ex.Message}");
                // Continue execution even if storage fails - we still want to update the thread timestamp
            }

            // span.SetAttribute("sat_score", evaluationResult.SATScore);
            _logger.LogAgentAction(
                action: AgentActionEvents.EvaluateThread,
                parameter: JsonSerializer.Serialize(evaluationResult),
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: thread.Id.ToString(),
                threadSource: thread.Source.ToString(),
                featureConfig: WebJsonSerializer.Serialize(thread.FeatureConfig));

            // Update thread with evaluation timestamp
            await _threadRepository.UpdateThreadEvaluatedTimestampAsync(thread.Id, DateTime.UtcNow);

            return evaluationResult;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error evaluating thread {thread.Id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Calculate tool call metrics for the thread
    /// </summary>
    private async Task<ToolCallMetrics> CalculateToolCallMetrics(Guid threadId, IEnumerable<AgentContext> agentContexts)
    {
        var metrics = new ToolCallMetrics();

        foreach (var context in agentContexts)
        {
            try
            {
                // Get reasoning messages for this context to find tool calls
                var reasoningMessages = await GetReasoningMessagesForContext(context.Id);
                var chatMessages = EvaluationHelper.GetChatMessagesForReasoningMessages(reasoningMessages, _logger);
                var functionCalls = new Dictionary<string, FunctionCallContent>();
                foreach (var message in chatMessages)
                {
                    var functionCall = message.Contents.OfType<FunctionCallContent>().ToList();
                    if (functionCall.Count > 0)
                    {
                        foreach (var call in functionCall)
                        {
                            functionCalls[call.CallId] = call;
                        }
                    }

                    var functionResult = message.Contents.OfType<FunctionResultContent>().ToList();
                    foreach (var result in functionResult)
                    {
                        if (functionCalls.TryGetValue(result.CallId, out var call))
                        {
                            metrics.TotalToolCalls++;
                            if (call.Name == EvaluationHelper.GetToolCallName(nameof(ArmPluginDefinition.RunAzCliReadCommandsAsync)) ||
                               call.Name == EvaluationHelper.GetToolCallName(nameof(ArmPluginDefinition.RunAzCliWriteCommandsAsync)))
                            {
                                metrics.AzCliCalls++;
                                string output = result.Result?.ToString() ?? string.Empty;
                                if (!output.Contains(ExternalProcessCommand.ProcessFailureMessage))
                                {
                                    metrics.AzCliSuccesses++;
                                    metrics.TotalSuccesses++;
                                }
                            }
                            else if (call.Name == EvaluationHelper.GetToolCallName(nameof(KubePluginDefinition.RunKubectlReadCommandAsync)) ||
                                     call.Name == EvaluationHelper.GetToolCallName(nameof(KubePluginDefinition.RunKubectlWriteCommandAsync)))
                            {
                                metrics.KubectlCalls++;
                                string output = result.Result?.ToString() ?? string.Empty;
                                if (!output.Contains(ExternalProcessCommand.ProcessFailureMessage))
                                {
                                    metrics.KubectlSuccesses++;
                                    metrics.TotalSuccesses++;
                                }
                            }
                            else
                            {
                                string output = result.Result?.ToString() ?? string.Empty;
                                if (!output.ToLower().Contains("error") &&
                                       !output.ToLower().Contains("failed"))
                                {
                                    metrics.TotalSuccesses++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Error calculating tool metrics for context {context.Id}");
            }
        }

        return metrics;
    }

    /// <summary>
    /// Get reasoning messages for a specific agent context
    /// </summary>
    private async Task<IReadOnlyList<ReasoningMessage>> GetReasoningMessagesForContext(Guid agentContextId)
    {
        try
        {
            var chatHistory = await _threadRepository.GetAgentChatHistoryAsync(agentContextId);
            if (chatHistory?.ReasoningMessageIds == null || !chatHistory.ReasoningMessageIds.Any())
            {
                return [];
            }

            var reasoningMessages = new List<ReasoningMessage>();
            foreach (var messageId in chatHistory.ReasoningMessageIds)
            {
                try
                {
                    var reasoningMessage = await _threadRepository.GetReasoningMessageAsync(messageId, agentContextId);
                    if (reasoningMessage != null)
                    {
                        reasoningMessages.Add(reasoningMessage);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, $"Error getting reasoning message {messageId}");
                }
            }

            return reasoningMessages;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error getting reasoning messages for context {agentContextId}");
            return [];
        }
    }

    /// <summary>
    /// Get chat and reasoning message histories for the thread
    /// </summary>
    private async Task<(string ChatHistory, string ReasoningHistory)> GetMessageHistories(Guid threadId, IEnumerable<AgentContext> agentContexts)
    {
        var chatHistoryBuilder = new StringBuilder();
        var reasoningHistoryBuilder = new StringBuilder();

        try
        {
            // Get regular chat messages
            var messages = await _threadRepository.GetMessagesAsync(threadId);
            foreach (var message in messages.OrderBy(m => m.TimeStamp))
            {
                chatHistoryBuilder.AppendLine($"[{message.TimeStamp:yyyy-MM-dd HH:mm:ss}] {message.Author.Role}: {message.Text}");
            }

            // Get reasoning messages from all agent contexts
            foreach (var context in agentContexts)
            {
                var reasoningMessages = await GetReasoningMessagesForContext(context.Id);
                foreach (var reasoningMessage in reasoningMessages)
                {
                    try
                    {
                        // Build reasoning message content
                        var content = GetContentFromReasoningMessage(reasoningMessage);
                        reasoningHistoryBuilder.AppendLine($"[{reasoningMessage.Role}]: {content}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalWarning(ex, $"Error processing reasoning message {reasoningMessage.Id}");
                        reasoningHistoryBuilder.AppendLine($"[{reasoningMessage.Role}]: <Unable to parse message>");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error building message histories for thread {threadId}");
        }

        return (chatHistoryBuilder.ToString(), reasoningHistoryBuilder.ToString());
    }

    /// <summary>
    /// Check if a LocalAuthScanner thread should be skipped from evaluation
    /// </summary>
    private async Task<bool> ShouldSkipLocalAuthScannerThread(ThreadModel thread)
    {
        try
        {
            // Get agent contexts for this thread
            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);

            // Get all reasoning messages for all contexts
            var allReasoningMessages = new List<ReasoningMessage>();
            foreach (var context in agentContexts)
            {
                var reasoningMessages = await GetReasoningMessagesForContext(context.Id);
                allReasoningMessages.AddRange(reasoningMessages);
            }

            // Check if there's only one user message that starts with the specific text
            var userMessages = allReasoningMessages.Where(m => m.Role == ReasoningMessageRoleEnum.User).ToList();

            if (userMessages.Count == 1)
            {
                var messageContent = GetContentFromReasoningMessage(userMessages[0]);
                if (messageContent.Contains("Detected resources that have unsafe key-based access enabled", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error checking LocalAuthScanner thread {thread.Id} for skip criteria");
            return false; // Don't skip if we can't determine
        }
    }

    /// <summary>
    /// Use LLM to evaluate thread performance and quality
    /// Returns null if evaluation fails, indicating that the thread should not be marked as evaluated
    /// </summary>
    private async Task<LLMEvaluationResult?> EvaluateThreadWithLLM(ThreadModel thread, string chatHistory, string reasoningHistory, ToolCallMetrics toolCallMetrics, CancellationToken cancellationToken)
    {
        try
        {
            var prompt = BuildEvaluationPrompt(thread, chatHistory, reasoningHistory, toolCallMetrics);

            _logger.LogInternalInformation("LLM Evaluation Prompt for thread {ThreadId}:\n{Prompt}", thread.Id, prompt);

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, """
                    You are an expert evaluator of AI agent conversations. Your task is to evaluate the quality and effectiveness of agent threads based on the provided conversation history and reasoning logs.

                    Evaluation Criteria (evaluate each with 1, 2, 3, 4, 5):
                    1. **Resolved**: Did the agent successfully meet user intent and resolve their issue?
                        Score Definitions:
                        - 5 (Exceptionally Resolved): Agent exceeded expectations, resolved the issue faster and more thoroughly than anticipated, provided additional valuable insights or preventive measures.
                        - 4 (Well Resolved): Agent successfully resolved the issue with good understanding and effective solution, user confirms satisfaction with the resolution.
                        - 3 (Partially Resolved): Agent made progress but didn't fully resolve the issue, or the resolution status is unclear from the conversation. Examples: Partial fixes provided, user acknowledges some progress but indicates more work needed.
                        - 2 (Poorly Resolved): Agent attempted to resolve but failed significantly, provided incorrect or incomplete solutions, user indicates continued problems.
                        - 1 (Completely Unresolved): Agent completely failed to address the core problem, made the situation worse, or user explicitly states the issue is not fixed at all.

                    2. **Satisfied**: Was the agent's response good enough? Did the user appear satisfied and not frustrated?
                        Score Definitions:
                        - 5 (Highly Satisfied): User expresses exceptional satisfaction, gratitude, and praise for the agent's performance. Examples: "This was amazing!", "You solved this perfectly!", enthusiastic positive feedback.
                        - 4 (Satisfied): User expresses clear satisfaction, appreciation, or positive feedback about the agent's help. Examples: "Thank you", "Great!", "This is exactly what I needed", positive tone throughout.
                        - 3 (Neutral): User shows neither clear satisfaction nor dissatisfaction, or their sentiment is mixed/ambiguous. Examples: Neutral acknowledgments, no clear emotional indicators.
                        - 2 (Dissatisfied): User expresses frustration, dissatisfaction, or indicates the agent's response was unhelpful. Examples: "This is not what I asked for", "You're not understanding me", expressions of annoyance.
                        - 1 (Highly Dissatisfied): User expresses strong frustration, anger, or complete dissatisfaction with the agent's performance. Examples: "This is terrible", "You made everything worse", strong negative emotions.

                    3. **Automatic**: Can the agent work automatically and finish the thread without user interaction as much as possible?
                        Score Definitions:
                        - 5 (Exceptionally Automatic): Agent demonstrates superior autonomy, proactively anticipates needs, handles complex scenarios independently, and requires virtually no user guidance beyond necessary approval requests.
                        - 4 (Highly Automatic): Agent works independently with minimal user interaction, makes appropriate autonomous decisions, and can complete most tasks without requiring step-by-step guidance. User approval requests for potentially impactful actions are expected and do not reduce the automatic score.
                        - 3 (Partially Automatic): Agent can handle some tasks autonomously but still requires moderate user interaction for key decisions or guidance beyond normal approval workflows. Examples: Agent handles routine steps automatically but needs user input for technical decisions or clarifications.
                        - 2 (Poorly Automatic): Agent requires significant user interaction, frequent confirmations beyond normal approval workflows, or cannot proceed without constant user guidance for most tasks.
                        - 1 (Not Automatic at All): Agent cannot work independently, requires detailed instructions for every step, completely dependent on user guidance for basic tasks.

                        Note: User approval messages for potentially impactful actions (e.g., "Should I restart this service?", "Do you want me to delete these resources?") are expected safety measures and should NOT negatively impact the automatic score. Only consider unnecessary back-and-forth or requests for technical guidance as reducing automation.

                    4. **Smooth**: Was the interaction smooth without the user getting stuck or confused?
                        Score Definitions:
                        - 5 (Exceptionally Smooth): Flawless interaction flow, anticipates user needs, proactive communication, no confusion or misunderstandings whatsoever.
                        - 4 (Very Smooth): Conversation flows naturally, minimal confusion, agent and user understand each other clearly. Examples: Clear communication, efficient problem-solving, no significant misunderstandings.
                        - 3 (Somewhat Smooth): Some minor confusion or misunderstandings but generally progresses forward. Examples: Occasional clarifications needed, minor hiccups that are quickly resolved.
                        - 2 (Not Smooth): User frequently gets confused, multiple misunderstandings, or user repeatedly asks for clarification. Examples: "I don't understand", "What do you mean?", back-and-forth confusion.
                        - 1 (Very Rough): Constant confusion, major communication breakdowns, user becomes lost or frustrated due to poor interaction flow.

                    5. **Concise**: Was the conversation focused and efficient without unnecessary back-and-forth?
                        Score Definitions:
                        - 5 (Exceptionally Concise): Optimal efficiency, gets straight to the point, zero unnecessary exchanges, perfect focus on the task with maximum effectiveness.
                        - 4 (Very Concise): Efficient, focused conversation that gets to the point quickly and stays on task. Examples: Direct problem-solving, relevant responses, minimal repetition, efficient resolution path.
                        - 3 (Moderately Concise): Some unnecessary exchanges but generally stays on track. Examples: Minor repetition, some irrelevant details but overall focused approach.
                        - 2 (Not Concise): Excessive back-and-forth, repetitive exchanges, or agent provides overly verbose/irrelevant information. Examples: Repeating the same questions, tangential discussions.
                        - 1 (Very Verbose): Extremely inefficient conversation with excessive repetition, completely off-topic discussions, or overwhelming amount of irrelevant information.

                    6. **Adherence**: How faithfully did the agent follow user/system prompts and instructions?
                        Score Definitions:
                        - 5 (Perfect Adherence): Agent flawlessly follows all user instructions and system prompts, demonstrates complete understanding of requirements, and executes exactly as directed.
                        - 4 (Good Adherence): Agent follows most instructions correctly with minor deviations, shows clear understanding of user intent and system prompts.
                        - 3 (Partial Adherence): Agent follows some instructions but misses or misinterprets key requirements, shows mixed understanding of prompts.
                        - 2 (Poor Adherence): Agent frequently ignores or misinterprets instructions, shows limited understanding of user/system prompts, significant deviations from requirements.
                        - 1 (No Adherence): Agent completely ignores instructions, shows no understanding of user/system prompts, acts contrary to explicit directions.

                                        Respond in valid JSON format with only the evaluation fields (do NOT include category/priority):
                                        {
                                            "Resolved": 3,
                                            "Satisfied": 2,
                                            "Automatic": 5,
                                            "Smooth": 1,
                                            "Concise": 1,
                                            "Adherence": 4,
                                            "Summary": "Brief 1-2 sentence summary of performance"
                                        }
                    """),
                new(ChatRole.User, prompt)
            };

            var chatOptions = new ChatOptions
            {
                Temperature = 0
            };
            var response = await _chatClientProvider.DefaultModel.GetResponseAsync(chatMessages, chatOptions, cancellationToken: cancellationToken);
            var jsonResponse = response.GetMessage().Text?.Trim();
            if (string.IsNullOrEmpty(jsonResponse))
            {
                _logger.LogInternalWarning($"LLM evaluation failed for thread {thread.Id} - no response received");
                return null; // Evaluation failed - caller should not mark thread as evaluated
            }

            // Parse LLM response for numeric scores and summary
            try
            {
                var cleanedJson = jsonResponse.Replace("\n", "").Replace("\\n", "").Trim();
                var evaluationData = JsonSerializer.Deserialize<LLMEvaluationResponse>(cleanedJson);

                // Delegate category determination to a separate method so it can be improved independently
                var category = await EvaluateThreadCategory(thread, chatHistory, cancellationToken);

                return new LLMEvaluationResult
                {
                    SATScore = Math.Max(0.0, Math.Min(5.0, evaluationData?.SATScore ?? 2.5)), // Clamp between 0-5
                    Summary = evaluationData?.Summary ?? "Unable to evaluate",
                    DetailedFeedback = evaluationData?.DetailedFeedback ?? "No detailed feedback available",
                    Category = category ?? evaluationData?.Category ?? "other",
                    Resolved = evaluationData?.Resolved ?? 0,
                    Satisfied = evaluationData?.Satisfied ?? 0,
                    Automatic = evaluationData?.Automatic ?? 0,
                    Smooth = evaluationData?.Smooth ?? 0,
                    Concise = evaluationData?.Concise ?? 0,
                    Adherence = evaluationData?.Adherence ?? 0,
                    Priority = evaluationData?.Priority ?? "low",
                    PriorityReason = evaluationData?.PriorityReason ?? ""
                };
            }
            catch (JsonException ex)
            {
                _logger.LogInternalWarning(ex, $"Error parsing LLM evaluation response for thread {thread.Id}. Original response: {jsonResponse}");
                return null; // Evaluation failed - caller should not mark thread as evaluated
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error during LLM evaluation for thread {thread.Id}");
            return null; // Evaluation failed - caller should not mark thread as evaluated
        }
    }
    /// <summary>
    /// Build the evaluation prompt for the LLM
    /// </summary>
    private string BuildEvaluationPrompt(ThreadModel thread, string chatHistory, string reasoningHistory, ToolCallMetrics toolCallMetrics)
    {
        var promptBuilder = new StringBuilder();

        promptBuilder.AppendLine("Please evaluate the following agent conversation thread:");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine($"**Thread Information:**");
        promptBuilder.AppendLine($"- Title: {thread.Title}");
        promptBuilder.AppendLine($"- Source: {thread.Source}");
        promptBuilder.AppendLine($"- Duration: {thread.ModifiedTimestamp - thread.CreatedTimestamp}");
        promptBuilder.AppendLine($"- Created: {thread.CreatedTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        promptBuilder.AppendLine($"- Completed: {thread.ModifiedTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("**Tool Call Metrics:**");
        promptBuilder.AppendLine($"- Total Tool Calls: {toolCallMetrics.TotalToolCalls}");
        promptBuilder.AppendLine($"- Overall Success Rate: {toolCallMetrics.OverallSuccessRate:P2}");
        promptBuilder.AppendLine($"- Azure CLI Calls: {toolCallMetrics.AzCliCalls} (Success Rate: {toolCallMetrics.AzCliSuccessRate:P2})");
        promptBuilder.AppendLine($"- Kubectl Calls: {toolCallMetrics.KubectlCalls} (Success Rate: {toolCallMetrics.KubectlSuccessRate:P2})");
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("**Chat History:**");
        promptBuilder.AppendLine(string.IsNullOrEmpty(chatHistory) ? "(No chat history available)" : chatHistory);
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("**Agent Reasoning History:**");
        promptBuilder.AppendLine(string.IsNullOrEmpty(reasoningHistory) ? "(No reasoning history available)" : reasoningHistory);
        promptBuilder.AppendLine();

        promptBuilder.AppendLine("Based on the above information, please provide a comprehensive evaluation of this agent thread's performance.");

        return promptBuilder.ToString();
    }

    /// <summary>
    /// Analyze thread for potential handoff errors using LLM
    /// 1 analysis result per user turn
    /// </summary>
    /// <param name="thread">The thread to analyze</param>
    private async Task EvaluateHandoffsWithLLM(ThreadModel thread, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"Generating handoff evaluations for thread {thread.Id} from source {thread.Source}: {thread.Title}");

            // Get agent context to analyze handoffs
            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);
            var agentContext = agentContexts.FirstOrDefault();

            if (agentContext is null)
            {
                _logger.LogInternalWarning($"No agent contexts found for thread {thread.Id}, skipping handoff evaluation.");
                return;
            }

            var chatMessages = await EvaluationHelper.GetChatMessages(_threadRepository, agentContext, _logger);

            var startAgent = agentContext.AgentHandoffChain.FirstOrDefault(defaultValue: "meta_agent");
            await EvaluateHandoffsWithLLM(thread, chatMessages, startAgent);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to evaluate handoffs for thread {thread.Id} due to unexpected exception.");
        }
    }

    public async Task<IReadOnlyList<HandoffAnalysisResult>> EvaluateHandoffsWithLLM(
        ThreadModel thread,
        IReadOnlyList<ChatMessage> chatMessages,
        string startAgent,
        bool autoHandoffEnabled = false)
    {
        var evaluations = new List<HandoffAnalysisResult>();

        var chatContext = new List<ChatMessage>();
        var handoffContext = new List<ChatMessage>();

        var idx = 0;
        var chatLength = chatMessages.Count;
        foreach (var chatMessage in chatMessages)
        {
            ++idx;

            // track all messages until turn completion
            if (chatMessage.Role == ChatRole.System)
            {
                continue;
            }
            else
            {
                handoffContext.Add(chatMessage);
            }

            // if we saw an assistant message
            if (AgentTrajectory.IsEndOfTurnMessage(chatMessage))
            {
                // we should run handoff eval
                // todo: pass in autohandoff from the thread info
                var evaluation = await AnalyzeHandoffErrors(
                    thread: thread,
                    defaultStartAgent: startAgent,
                    autoHandoffEnabled: autoHandoffEnabled,
                    previousChatMessages: chatContext,
                    handoffMessages: handoffContext);

                evaluations.Add(evaluation);

                _logger.LogAgentAction(
                    action: AgentActionEvents.EvaluateHandoffs,
                    // parameter: "JsonSerializer.Serialize(evaluation)",
                    parameter: "",
                    status: AgentActionStatus.Success,
                    duration: 0,
                    threadId: thread.Id.ToString(),
                    threadSource: thread.Source.ToString(),
                    featureConfig: WebJsonSerializer.Serialize(thread.FeatureConfig));

                // and then exchange the current handoff context to chatContext
                chatContext.AddRange(handoffContext);
                handoffContext.Clear();
            }
            // otherwise, if we are at the last message, we should force an evaluation
            else if (idx == chatLength)
            {
                _logger.LogInternalError($"Found thread with last message not in completed state. " +
                    $"There must be a bug in reasoning loop. ThreadId {thread.Id}");

                // todo: pass in autohandoff from the thread info
                var evaluation = await AnalyzeHandoffErrors(
                    thread: thread,
                    defaultStartAgent: startAgent,
                    autoHandoffEnabled: false,
                    previousChatMessages: chatContext,
                    handoffMessages: handoffContext);

                evaluations.Add(evaluation);

                _logger.LogAgentAction(
                    action: AgentActionEvents.EvaluateHandoffs,
                    // parameter: JsonSerializer.Serialize(evaluation),
                    parameter: "",
                    status: AgentActionStatus.Success,
                    duration: 0,
                    threadId: thread.Id.ToString(),
                    threadSource: thread.Source.ToString(),
                    featureConfig: WebJsonSerializer.Serialize(thread.FeatureConfig));
            }
        }

        return evaluations;
    }

    /// <summary>
    /// Analyze thread for potential handoff errors using LLM
    /// </summary>
    /// <param name="thread">The thread to analyze</param>
    /// <param name="handoffMessages">All reasoning messages for the thread</param>
    /// <returns>Analysis result indicating if handoffs are correct and explanation if not</returns>
    private async Task<HandoffAnalysisResult> AnalyzeHandoffErrors(
        ThreadModel thread,
        string defaultStartAgent,
        bool autoHandoffEnabled,
        IReadOnlyList<ChatMessage> previousChatMessages,
        IReadOnlyList<ChatMessage> handoffMessages)
    {
        var handoffChat = "Failed to extract handoff chat";
        try
        {
            // Build the handoff analysis prompt
            var prompts = BuildHandoffAnalysisPrompt(
                thread,
                defaultStartAgent,
                autoHandoffEnabled,
                previousChatMessages,
                handoffMessages);
            handoffChat = prompts.HandoffChat;

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, prompts.SystemPrompt),
                new(ChatRole.User, prompts.UserPrompt)
            };

            var chatOptions = new ChatOptions
            {
                ToolMode = ChatToolMode.None,
                Temperature = 0.2f,
                ResponseFormat = ChatResponseFormat.Text,
            };

            var response = await _chatClientProvider.DefaultModel.GetResponseAsync(chatMessages, chatOptions);
            var jsonResponse = response.GetMessage().Text?.Trim();

            if (string.IsNullOrEmpty(jsonResponse))
            {
                return new HandoffAnalysisResult
                {
                    HandoffChat = handoffChat,
                    HandoffsAreCorrect = true,
                    ErrorExplanation = "Unable to analyze - no LLM response"
                };
            }

            // Parse LLM response
            try
            {
                var analysisResponse = JsonSerializer.Deserialize<HandoffAnalysisResponse>(jsonResponse);
                return new HandoffAnalysisResult
                {
                    HandoffChat = handoffChat,
                    HandoffsAreCorrect = analysisResponse?.HandoffsAreCorrect ?? true,
                    ErrorExplanation = analysisResponse?.ErrorExplanation ?? ""
                };
            }
            catch (JsonException ex)
            {
                _logger.LogInternalWarning(ex, $"Error parsing handoff analysis response for thread {thread.Id}: {jsonResponse}");
                return new HandoffAnalysisResult
                {
                    HandoffChat = prompts.HandoffChat,
                    HandoffsAreCorrect = true,
                    ErrorExplanation = $"Unable to parse analysis response: {jsonResponse}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error during handoff analysis for thread {thread.Id}");
            return new HandoffAnalysisResult
            {
                HandoffChat = handoffChat,
                HandoffsAreCorrect = true,
                ErrorExplanation = $"Analysis failed due to error: {ex}"
            };
        }
    }

    /// <summary>
    /// Build the handoff analysis prompt for the LLM
    /// </summary>
    private static (string HandoffChat, string SystemPrompt, string UserPrompt) BuildHandoffAnalysisPrompt(
        ThreadModel thread,
        string defaultStartAgent,
        bool autoHandoffEnabled,
        IReadOnlyList<ChatMessage> previousChatMessages,
        IReadOnlyList<ChatMessage> handoffMessages)
    {
        var systemPromptBuilder = new StringBuilder();

        systemPromptBuilder.Append("You are an expert AI system analyzer. Your task is to analyze multi-agent conversations to identify potential handoff errors where the meta agent incorrectly claims no agent can handle a user request when there actually is an appropriate agent available.");

        systemPromptBuilder.AppendLine("**CONTEXT:**");
        systemPromptBuilder.AppendLine("This is an Azure SRE multi-agent system where a meta agent coordinates specialized sub-agents. Sometimes the meta agent incorrectly claims that no agent can handle a user request when there actually is an appropriate specialized agent available.");
        systemPromptBuilder.AppendLine();

        systemPromptBuilder.AppendLine("**AVAILABLE AGENTS AND THEIR CAPABILITIES:**");
        systemPromptBuilder.AppendLine("- resource_discovery_agent: Resource discovery, subscription listing, resource properties, activity logs, basic configuration questions");
        systemPromptBuilder.AppendLine("- container_apps_agent: Container Apps specific issues");
        systemPromptBuilder.AppendLine("- tls_minimum_version_upgrade_agent: TLS upgrades");
        systemPromptBuilder.AppendLine("- azure_cli_command_executor_agent: General Azure CLI operations, resource configuration");
        systemPromptBuilder.AppendLine("- web_app_down_agent: Web App downtime and unavailability issues");
        systemPromptBuilder.AppendLine("- web_app_restart_agent: Web App restart operations");
        systemPromptBuilder.AppendLine("- aks_general_agent: AKS or Kubernetes issues and configuration");
        systemPromptBuilder.AppendLine("- metrics_and_chart_visualization_agent: ANY metrics, charts, or visualization tasks");
        systemPromptBuilder.AppendLine("- local_auth_agent: Local authentication issues");
        systemPromptBuilder.AppendLine("- diagnostic_cpu_agent: CPU diagnostics");
        systemPromptBuilder.AppendLine("- diagnostic_memory_agent: Memory diagnostics");
        systemPromptBuilder.AppendLine("- function_app_agent: Function App specific issues");
        systemPromptBuilder.AppendLine("- api_management_agent: API Management issues");
        systemPromptBuilder.AppendLine("- github_issue_agent: GitHub related queries and repository connections");
        systemPromptBuilder.AppendLine("- azuredevops_work_item_agent: Azure DevOps related queries and repository connections");
        systemPromptBuilder.AppendLine("- incident_management_agent: Incident management");
        systemPromptBuilder.AppendLine();

        systemPromptBuilder.AppendLine("**TYPICAL HANDOFF PATTERNS:**");
        systemPromptBuilder.AppendLine("- Storage account usage metrics → resource_discovery_agent → metrics_and_chart_visualization_agent");
        systemPromptBuilder.AppendLine("- Resource monitoring → resource_discovery_agent → metrics_and_chart_visualization_agent");
        systemPromptBuilder.AppendLine("- Azure CLI operations → azure_cli_command_executor_agent");
        systemPromptBuilder.AppendLine("- AKS issues → resource_discovery_agent → aks_general_agent");
        systemPromptBuilder.AppendLine("- Web app issues → resource_discovery_agent → web_app_down_agent");
        systemPromptBuilder.AppendLine();

        systemPromptBuilder.AppendLine($"**THREAD INFORMATION:**");
        systemPromptBuilder.AppendLine($"- Thread ID: {thread.Id}");
        systemPromptBuilder.AppendLine($"- Title: {thread.Title}");
        systemPromptBuilder.AppendLine($"- Source: {thread.Source}");
        systemPromptBuilder.AppendLine();

        IReadOnlyList<string> handoffStartStack = [defaultStartAgent];
        if (previousChatMessages.Any())
        {
            systemPromptBuilder.AppendLine("**PREVIOUS CHAT CONTEXT:**\n<previousChat>");

            var previousChatTraj = new AgentTrajectory(defaultStartAgent, autoHandoffEnabled);
            foreach (var message in previousChatMessages)
            {
                previousChatTraj.Append(message);
            }
            var previousChatHistory = previousChatTraj.GetFilteredTrajectory();
            systemPromptBuilder.AppendLine(previousChatHistory);
            systemPromptBuilder.AppendLine("</previousChat>");
            systemPromptBuilder.AppendLine();

            // initialize handoff start agent using end of previous chat
            if (!autoHandoffEnabled)
            {
                handoffStartStack = previousChatTraj.AgentStack;
            }
        }

        systemPromptBuilder.AppendLine("**ANALYSIS TASK:**");
        systemPromptBuilder.AppendLine("1. Examine if the meta agent claimed that no agent can handle the user's request");
        systemPromptBuilder.AppendLine("2. If such a claim was made, determine if there actually IS an appropriate agent that could have handled the request based on the available agents and their capabilities listed above");
        systemPromptBuilder.AppendLine("3. Look for cases where the meta agent should have tried different handoff strategies or additional agents");
        systemPromptBuilder.AppendLine();

        systemPromptBuilder.AppendLine("**EXPECTED OUTPUT FORMAT (JSON):**");
        systemPromptBuilder.AppendLine("{");
        systemPromptBuilder.AppendLine("  \"HandoffsAreCorrect\": true/false,");
        systemPromptBuilder.AppendLine("  \"ErrorExplanation\": \"Detailed explanation of what went wrong with the handoffs, or empty string if handoffs are correct\"");
        systemPromptBuilder.AppendLine("}");
        systemPromptBuilder.AppendLine();

        systemPromptBuilder.AppendLine("**IMPORTANT:** Focus specifically on identifying cases where the user's request COULD have been handled by an available agent but the meta agent incorrectly concluded otherwise.");

        // now build the user prompt with handoff context
        var userPromptBuilder = new StringBuilder();
        userPromptBuilder.AppendLine("Please analyze the following multi-agent conversation thread to identify potential handoff errors.");
        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine("**CONVERSATION FLOW:**\n<handoffChat>");
        var handoffChatTraj = new AgentTrajectory(handoffStartStack, autoHandoffEnabled);
        foreach (var message in handoffMessages)
        {
            handoffChatTraj.Append(message);
        }
        var handoffChatHistory = handoffChatTraj.GetFilteredTrajectory();
        userPromptBuilder.AppendLine(handoffChatHistory);
        userPromptBuilder.AppendLine("</handoffChat>");
        userPromptBuilder.AppendLine();

        return (handoffChatHistory, systemPromptBuilder.ToString(), userPromptBuilder.ToString());
    }

    /// <summary>
    /// Helper classes for managing evaluation metrics and results
    /// </summary>
    /// <summary>
    /// Helper method to get content from a ReasoningMessage by deserializing the SerializedChatMessage
    /// </summary>
    private string GetContentFromReasoningMessage(ReasoningMessage reasoningMessage)
    {
        try
        {
            if (string.IsNullOrEmpty(reasoningMessage.SerializedChatMessage))
            {
                return "";
            }

            var chatMessage = JsonSerializer.Deserialize<ChatMessage>(reasoningMessage.SerializedChatMessage);
            return chatMessage?.Text ?? "";
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error deserializing reasoning message {reasoningMessage.Id}");
            return reasoningMessage.SerializedChatMessage ?? "";
        }
    }

    /// <summary>
    /// Evaluate only the Category for a thread using the LLM.
    /// This is split out so category logic can be enhanced independently.
    /// Returns category string (e.g. "incidents", "user-driven-investigation", "security-updates", "other").
    /// </summary>
    private async Task<string> EvaluateThreadCategory(
        ThreadModel thread,
        string chatHistory,
        CancellationToken cancellationToken)
    {
        try
        {
            // Fast-path: if this thread originates from an incident source, classify it directly
            // to avoid an unnecessary LLM call.
            if (thread.Source == ThreadSource.Incident)
            {
                return "incident-management";
            }

            var prompt = await BuildEvaluationPromptForCategory(thread, chatHistory);
            _logger.LogInternalInformation("LLM Category Prompt for thread {ThreadId}:\n{Prompt}", thread.Id, prompt);

            var systemMsg = """
                You are an expert evaluator of AI agent conversations. Your task is to determine which one of the known categories best describes the provided thread. Return ONLY a JSON object with a single field "Category" (for example: {"Category": "incidents"}). Do not return any additional text.

                Known categories and descriptions:
                - incident-management: All items that are clearly incident records or IcM-linked investigations. Sub-category: tool type (Microsoft ICM, PagerDuty, ServiceNow, Azure Monitor alerts).
                - resource-usage-and-health: Logs, metrics, error analysis, availability views, and summaries. If the thread contains analysis or diagnosis of errors, prefer "troubleshooting"; if it is querying or charting metrics or usage, prefer "resource-usage-and-health".
                - troubleshooting: Diagnosis, remediation steps, configuration fixes, targeted checks, restarts, and root cause analysis.
                - resource-configuration: Inventories, coverage/posture views, dashboards, and questions like "what's running where" (e.g., list resources, list revisions, show container image in use).
                - cost-analysis: Cost, performance tuning, SKU/plan recommendations, and optimization advice.
                - security: Hardening, exposure checks, authentication/authorization changes, and vulnerability scanning. Any agent-initiated LocalAuthScanner threads should be categorized as "security".
                - learning-about-agent: Getting started, introductions, capability overviews, tests, or conversational experiments with the agent.

                Few-shot examples (format: "example" -> category). Examples may be single-line or multi-line; choose the best matching category:
                "List Azure Container Apps" -> resource-configuration
                "Investigating Container App IoT-Dashboard Outage" -> troubleshooting
                "ICM# 640732407: PagerDuty incident investigation" -> incident-management
                "24-Hour Resource Availability Analysis" -> resource-usage-and-health
                "App is sending 500 errors, why?" -> troubleshooting
                "Disable Local Authentication for Event Hub" -> security
                "Azure SRE Agent Introduction and quickstart" -> learning-about-agent
                "Optimize Azure Service Bus SKU Utilization" -> cost-analysis

                Respond with a valid JSON object containing only the Category field, for example:
                {"Category": "resource-configuration"}
                """;

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, systemMsg),
                new(ChatRole.User, prompt)
            };

            var chatOptions = new ChatOptions
            {
                Temperature = 0
            };

            var response = await _chatClientProvider.DefaultModel.GetResponseAsync(chatMessages, chatOptions, cancellationToken: cancellationToken);
            var jsonResponse = response.GetMessage().Text?.Trim();
            if (string.IsNullOrEmpty(jsonResponse))
            {
                return "other";
            }

            try
            {
                var cleanedJson = jsonResponse.Replace("\n", "").Replace("\\n", "").Trim();
                using var doc = JsonDocument.Parse(cleanedJson);
                var root = doc.RootElement;
                var category = root.TryGetProperty("Category", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;

                return category ?? "other";
            }
            catch (JsonException ex)
            {
                _logger.LogInternalWarning(ex, $"Error parsing LLM category response. Original response: {jsonResponse}");
                return "other";
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Error during LLM category evaluation for thread {thread.Id}");
            return "other";
        }
    }

    /// <summary>
    /// Build a concise evaluation prompt used for category classification.
    /// This version uses the already-constructed chatHistory passed from the caller
    /// instead of fetching messages from the repository. Do not distinguish between
    /// user- or agent-initiated threads here; use the provided chatHistory as-is.
    /// The prompt contains minimal thread metadata and tool call metrics.
    /// </summary>
    private Task<string> BuildEvaluationPromptForCategory(ThreadModel thread, string chatHistory)
    {
        // Build prompt synchronously from provided chatHistory to avoid extra repository calls
        var sb = new StringBuilder();
        sb.AppendLine("Thread chat history context:");

        if (string.IsNullOrWhiteSpace(chatHistory))
        {
            sb.AppendLine("(No messages found for this thread)");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("Chat history (ordered):");
            sb.AppendLine(chatHistory.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Based on the content above, respond with valid JSON containing a single field 'Category' whose value is one of: 'incident-management', 'resource-usage-and-health', 'troubleshooting', 'resource-configuration', 'cost-analysis', 'security', 'learning-about-agent', or 'other'. Example: {\"Category\":\"troubleshooting\"}");

        return Task.FromResult(sb.ToString());
    }


    internal class ToolCallMetrics
    {
        public int TotalToolCalls { get; set; }
        public int TotalSuccesses { get; set; }
        public int AzCliCalls { get; set; }
        public int AzCliSuccesses { get; set; }
        public int KubectlCalls { get; set; }
        public int KubectlSuccesses { get; set; }
        public double OverallSuccessRate => TotalToolCalls > 0 ? (double)TotalSuccesses / TotalToolCalls : 0.0;
        public double AzCliSuccessRate => AzCliCalls > 0 ? (double)AzCliSuccesses / AzCliCalls : 0.0;
        public double KubectlSuccessRate => KubectlCalls > 0 ? (double)KubectlSuccesses / KubectlCalls : 0.0;
    }

    internal class LLMEvaluationResult
    {
        public double SATScore { get; set; }
        public string Summary { get; set; } = "";
        public string DetailedFeedback { get; set; } = ""; public string Category { get; set; } = "";
        public int Resolved { get; set; }
        public int Satisfied { get; set; }
        public int Automatic { get; set; }
        public int Smooth { get; set; }
        public int Concise { get; set; }
        public int Adherence { get; set; }
        public string Priority { get; set; } = "";
        public string PriorityReason { get; set; } = "";
    }
    internal class LLMEvaluationResponse
    {
        public double SATScore { get; set; }
        public int Resolved { get; set; }
        public int Satisfied { get; set; }
        public int Automatic { get; set; }
        public int Smooth { get; set; }
        public int Concise { get; set; }
        public int Adherence { get; set; }
        public string? Summary { get; set; }
        public string? DetailedFeedback { get; set; }
        public string[]? Recommendations { get; set; }
        public string Category { get; set; } = "";
        public string Priority { get; set; } = "";
        public string PriorityReason { get; set; } = "";
    }

    public class HandoffAnalysisResult
    {
        public string HandoffChat { get; set; } = "";
        public bool HandoffsAreCorrect { get; set; } = false;
        public string ErrorExplanation { get; set; } = "";
    }

    internal class HandoffAnalysisResponse
    {
        public bool HandoffsAreCorrect { get; set; }
        public string? ErrorExplanation { get; set; }
    }

    /// <summary>
    /// Check if a thread has already been evaluated and if there are new messages since last evaluation
    /// </summary>
    /// <param name="threadId">The ID of the thread to check</param>
    /// <returns>True if the thread has been evaluated recently and has no new messages, false if it needs evaluation</returns>
    private bool IsEvaluationUpToDate(ThreadModel thread)
    {
        // If thread has never been evaluated, it needs evaluation
        if (thread.EvaluatedTimestamp == default)
        {
            _logger.LogInternalInformation($"Thread {thread.Id} has never been evaluated, needs evaluation");
            return false;
        }

        // Check if the thread has been modified since the last evaluation
        if (thread.ModifiedTimestamp > thread.EvaluatedTimestamp)
        {
            _logger.LogInternalInformation($"Thread {thread.Id} was modified at {thread.ModifiedTimestamp:yyyy-MM-dd HH:mm:ss} UTC after last evaluation at {thread.EvaluatedTimestamp:yyyy-MM-dd HH:mm:ss} UTC, needs re-evaluation");
            return false;
        }

        _logger.LogInternalInformation($"Thread {thread.Id} was already evaluated at {thread.EvaluatedTimestamp:yyyy-MM-dd HH:mm:ss} UTC and has no new messages, skipping evaluation");
        return true;
    }

    /// <summary>
    /// Determines the primary agent type for a thread based on its agent contexts
    /// </summary>
    private AgentTypeEnum GetThreadAgentType(IList<AgentContext> agentContexts)
    {
        if (agentContexts == null || agentContexts.Count == 0)
        {
            _logger.LogInternalWarning("No agent contexts found for thread, defaulting to Meta agent type");
            return AgentTypeEnum.Meta;
        }

        // If there's only one agent context, use its type
        if (agentContexts.Count == 1)
        {
            return agentContexts[0].AgentType;
        }

        // If there are multiple agent contexts, prioritize Incident type
        // This handles cases where there might be handoffs or multiple agents involved
        var incidentContext = agentContexts.FirstOrDefault(ac => ac.AgentType == AgentTypeEnum.Incident);
        if (incidentContext != null)
        {
            return AgentTypeEnum.Incident;
        }

        // If no incident context, return the type of the first context
        return agentContexts.First().AgentType;
    }
}
