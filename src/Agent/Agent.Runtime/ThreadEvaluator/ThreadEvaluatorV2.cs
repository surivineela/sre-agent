// ------------------------------------------------------------
//  V2 thread evaluation methods (partial ThreadEvaluator)
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ThreadModel = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.ThreadEvaluator;

public partial class ThreadEvaluator
{
    /// <summary>
    /// V2 thread evaluator used for experimenting with alternate evaluation inputs.
    /// This method does not persist results and does not update thread evaluated timestamps.
    /// </summary>
    public async Task EvaluateV2(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation("Starting thread behavior evaluation V2 (log-only)");

            // Reuse the exact same thread filtering as V1 so we can diff results.
            var threads = await ListThreadsToEvaluate();

            _logger.LogInternalInformation($"[V2] Found {threads.Count()} completed threads to evaluate");

            foreach (var thread in threads)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInternalInformation("Thread behavior evaluation V2 cancelled");
                    break;
                }

                try
                {
                    if (IsEvaluationUpToDate(thread))
                    {
                        _logger.LogInternalInformation($"[V2] Thread '{thread.Id}' evaluation is up to date, skipping");
                        continue;
                    }

                    await EvaluateThreadV2(thread, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, $"[V2] Error evaluating thread {thread.Id}: {ex.Message}");
                }
            }

            _logger.LogInternalInformation("Completed thread behavior evaluation V2");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[V2] Error during thread behavior evaluation: {ex.Message}");
        }
    }

    private async Task EvaluateThreadV2(ThreadModel thread, CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation($"[V2] Evaluating thread {thread.Id} from source {thread.Source}: {thread.Title}");

        var duration = thread.ModifiedTimestamp - thread.CreatedTimestamp;

        var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);
        var agentContextsList = agentContexts.ToList();

        var toolCallMetrics = await CalculateToolCallMetrics(thread.Id, agentContextsList);

        // V2 input: reasoning-message-derived transcript (previously GetMessageHistory)
        var (chatHistory, userInteractionCount) = await GetMessageHistoryV2(agentContextsList);

        // Keep prompt format identical to V1; populate Chat History with transcript, leave Reasoning History empty.
        var llmEvaluation = await EvaluateThreadWithLLMV2(
            thread,
            chatHistory,
            reasoningHistory: string.Empty,
            toolCallMetrics,
            cancellationToken);

        if (llmEvaluation == null)
        {
            _logger.LogInternalWarning($"[V2] Skipping thread {thread.Id} evaluation due to LLM evaluation failure");
            _logger.LogAgentAction(
                action: AgentActionEvents.EvaluateThreadV2,
                parameter: "",
                status: AgentActionStatus.Fail,
                duration: 0,
                threadId: thread.Id.ToString(),
                threadSource: thread.Source.ToString(),
                featureConfig: WebJsonSerializer.Serialize(thread.FeatureConfig),
                activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(thread.Id.ToString())));
            return;
        }

        var satScore = (int)Math.Ceiling(
            (llmEvaluation.Resolved + llmEvaluation.Satisfied + llmEvaluation.Automatic + llmEvaluation.Smooth + llmEvaluation.Concise + llmEvaluation.Adherence) / 6.0);

        var startingAgentName = GetStartingAgentName(agentContextsList);
        var startingAgent = _agentProvider.GetAgent(startingAgentName, thread.Id.ToString());

        var logRecord = new
        {
            ThreadId = thread.Id,
            ThreadTitle = thread.Title,
            Duration = duration,
            UserInteractionCount = userInteractionCount,
            ToolCallCount = toolCallMetrics.TotalToolCalls,
            ToolCallSuccessRate = toolCallMetrics.OverallSuccessRate,
            AzCliCallCount = toolCallMetrics.AzCliCalls,
            toolCallMetrics.AzCliSuccessRate,
            KubectlCallCount = toolCallMetrics.KubectlCalls,
            toolCallMetrics.KubectlSuccessRate,
            SATScore = satScore,
            llmEvaluation.Resolved,
            llmEvaluation.Satisfied,
            llmEvaluation.Automatic,
            llmEvaluation.Smooth,
            llmEvaluation.Concise,
            llmEvaluation.Adherence,
            EvaluationSummary = llmEvaluation.Summary,
            StartingAgentName = startingAgentName,
            SkillsEnabled = startingAgent.EnableSkills,
            IsExtendedAgent = startingAgent.IsExtended
        };

        _logger.LogAgentAction(
            action: AgentActionEvents.EvaluateThreadV2,
            parameter: JsonSerializer.Serialize(logRecord),
            status: AgentActionStatus.Success,
            duration: (long)duration.TotalMilliseconds,
            threadId: thread.Id.ToString(),
            threadSource: thread.Source.ToString(),
            featureConfig: WebJsonSerializer.Serialize(thread.FeatureConfig),
            activeExperiments: WebJsonSerializer.Serialize(_agentProvider.GetActiveVariants(thread.Id.ToString())));
    }

    private async Task<(string Transcript, int UserInteractionCount)> GetMessageHistoryV2(IEnumerable<AgentContext> contexts)
    {
        var transcriptBuilder = new StringBuilder();
        var userInteractionCount = 0;

        foreach (var context in contexts)
        {
            IReadOnlyList<ReasoningMessage> reasoningMessages;
            try
            {
                reasoningMessages = await GetReasoningMessagesForContext(context.Id);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"[V2] Error getting reasoning messages for context {context.Id}");
                continue;
            }

            foreach (var reasoningMessage in reasoningMessages)
            {
                try
                {
                    var (content, role) = GetContentAndRoleFromReasoningMessageV2(reasoningMessage);
                    if (string.IsNullOrWhiteSpace(content) || role == null)
                    {
                        continue;
                    }

                    if (role == ChatRole.User)
                    {
                        userInteractionCount++;
                    }

                    transcriptBuilder.AppendLine($"[{role}]: {content}");
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, $"[V2] Error processing reasoning message {reasoningMessage.Id}");
                }
            }
        }

        return (transcriptBuilder.ToString(), userInteractionCount);
    }

    private static (string Content, ChatRole? Role) GetContentAndRoleFromReasoningMessageV2(ReasoningMessage reasoningMessage)
    {
        if (string.IsNullOrEmpty(reasoningMessage.SerializedChatMessage))
        {
            return (string.Empty, null);
        }

        try
        {
            var chatMessage = JsonSerializer.Deserialize<ChatMessage>(reasoningMessage.SerializedChatMessage);
            return (chatMessage?.Text ?? string.Empty, chatMessage?.Role);
        }
        catch
        {
            return (string.Empty, null);
        }
    }

    private async Task<LLMEvaluationResultV2?> EvaluateThreadWithLLMV2(
        ThreadModel thread,
        string chatHistory,
        string reasoningHistory,
        ToolCallMetrics toolCallMetrics,
        CancellationToken cancellationToken)
    {
        try
        {
            // Reuse the V1 prompt builder so the only change is the input transcript.
            var prompt = BuildEvaluationPrompt(thread, chatHistory, reasoningHistory, toolCallMetrics);

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

                        Note: If the agent asked clarifying questions to better understand the user intent before attempting resolution, this is a positive behavior and should NOT reduce the resolved score. Focus on the final outcome of whether the user's issue was ultimately resolved.
                        Note: If the user did not respond after the agent asked for reasonable clarifications, assume the agent did not have enough information to proceed and score accordingly.

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
                        - 3 (Partially Automatic): Agent can handle some tasks autonomously but still requires moderate user interaction for key decisions or guidance beyond normal approval workflows. Examples: Agent handles routine steps automatically but needs user input for technical decisions.
                        - 2 (Poorly Automatic): Agent requires significant user interaction, frequent confirmations beyond normal approval workflows, or cannot proceed without constant user guidance for most tasks.
                        - 1 (Not Automatic at All): Agent cannot work independently, requires detailed instructions for every step, completely dependent on user guidance for basic tasks.

                        Note: User approval messages for potentially impactful actions (e.g., "Should I restart this service?", "Do you want me to delete these resources?") are expected safety measures and should NOT negatively impact the automatic score. Only consider unnecessary back-and-forth or requests for technical guidance as reducing automation.
                        Note: If the agent requests clarification to better understand user intent before proceeding, this is a positive behavior and should NOT reduce the automatic score. Focus on the agent's ability to operate independently once the task is understood.

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

                    Respond in valid JSON format with only the evaluation fields:
                    {
                        "Resolved": 3,
                        "Satisfied": 2,
                        "Automatic": 5,
                        "Smooth": 1,
                        "Concise": 1,
                        "Adherence": 4,
                        "Summary": "Brief 3-4 sentence summary of performance"
                    }
                    """),
                new(ChatRole.User, prompt)
            };

            var chatOptions = new ChatOptions
            {
                Temperature = 0,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    { FrameworkConstants.ReasoningEffortKey, ReasoningConstants.HighReasoningEffort }
                }
            };

            var response = await _chatClientProvider.EvalModel.GetResponseAsync(
                chatMessages,
                chatOptions,
                cancellationToken: cancellationToken);

            var jsonResponse = response.GetMessage().Text?.Trim();
            if (string.IsNullOrEmpty(jsonResponse))
            {
                _logger.LogInternalWarning($"[V2] LLM evaluation failed for thread {thread.Id} - no response received");
                return null;
            }

            try
            {
                var cleanedJson = jsonResponse.Replace("\n", string.Empty).Replace("\\n", string.Empty).Trim();
                var evaluationData = JsonSerializer.Deserialize<LLMEvaluationResponseV2>(cleanedJson);
                if (evaluationData == null)
                {
                    return null;
                }

                return new LLMEvaluationResultV2
                {
                    Summary = evaluationData.Summary ?? "Unable to evaluate",
                    Resolved = evaluationData.Resolved,
                    Satisfied = evaluationData.Satisfied,
                    Automatic = evaluationData.Automatic,
                    Smooth = evaluationData.Smooth,
                    Concise = evaluationData.Concise,
                    Adherence = evaluationData.Adherence,
                };
            }
            catch (JsonException ex)
            {
                _logger.LogInternalWarning(ex, $"[V2] Error parsing LLM evaluation response for thread {thread.Id}. Original response: {jsonResponse}");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"[V2] Error during LLM evaluation for thread {thread.Id}");
            return null;
        }
    }

    private sealed class LLMEvaluationResultV2
    {
        public string Summary { get; set; } = "";
        public int Resolved { get; set; }
        public int Satisfied { get; set; }
        public int Automatic { get; set; }
        public int Smooth { get; set; }
        public int Concise { get; set; }
        public int Adherence { get; set; }
    }

    private sealed class LLMEvaluationResponseV2
    {
        public int Resolved { get; set; }
        public int Satisfied { get; set; }
        public int Automatic { get; set; }
        public int Smooth { get; set; }
        public int Concise { get; set; }
        public int Adherence { get; set; }
        public string? Summary { get; set; }
    }
}
