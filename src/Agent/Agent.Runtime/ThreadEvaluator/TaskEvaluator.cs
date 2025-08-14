// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using System.Linq;
using Agent.Core.Extensions;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Helpers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using ThreadModel = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.ThreadEvaluator;

public partial class ThreadEvaluator
{
    /// <summary>
    /// Analyze thread for task handling performance using LLM
    /// 1 analysis result per user turn
    /// </summary>
    /// <param name="thread">The thread to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of task evaluation results</returns>
    public async Task<IReadOnlyList<TaskAnalysisResult>> EvaluateTasksWithLLM(ThreadModel thread, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"Generating task evaluations for thread {thread.Id} from source {thread.Source}: {thread.Title}");

            // Get agent context to analyze tasks
            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);
            var agentContext = agentContexts.FirstOrDefault();

            if (agentContext is null)
            {
                _logger.LogInternalWarning($"No agent contexts found for thread {thread.Id}, skipping task evaluation.");
                return new List<TaskAnalysisResult>();
            }

            var chatMessages = await EvaluationHelper.GetChatMessages(_threadRepository, agentContext, _logger);
            return await EvaluateTaskWithLLM(thread, chatMessages);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to evaluate task for thread {thread.Id} due to unexpected exception.");
            return new List<TaskAnalysisResult>();
        }
    }

    /// <summary>
    /// Analyze thread for task performance using LLM
    /// 1 analysis result per task
    /// </summary>
    /// <param name="thread">The thread to analyze</param>
    private async Task EvaluateTaskWithLLM(ThreadModel thread, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation($"Generating task evaluations for thread {thread.Id} from source {thread.Source}: {thread.Title}");

            // Get agent context to analyze tasks
            var agentContexts = await _threadRepository.GetAgentContextsForThreadAsync(thread.Id);
            var agentContext = agentContexts.FirstOrDefault();

            if (agentContext is null)
            {
                _logger.LogInternalWarning($"No agent contexts found for thread {thread.Id}, skipping task evaluation.");
                return;
            }

            var chatMessages = await EvaluationHelper.GetChatMessages(_threadRepository, agentContext, _logger);
            await EvaluateTaskWithLLM(thread, chatMessages);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to evaluate tasks for thread {thread.Id} due to unexpected exception.");
        }
    }

    public async Task<IReadOnlyList<TaskAnalysisResult>> EvaluateTaskWithLLM(
        ThreadModel thread,
        IReadOnlyList<ChatMessage> chatMessages)
    {
        var evaluations = new List<TaskAnalysisResult>();

        var chatContext = new List<ChatMessage>();
        var taskContext = new List<ChatMessage>();
        bool inTask = false;

        var idx = 0;
        var chatLength = chatMessages.Count;
        foreach (var chatMessage in chatMessages)
        {
            ++idx;

            // Skip system messages for task evaluation
            if (chatMessage.Role == ChatRole.System)
            {
                continue;
            }

            // Start of a task
            if (chatMessage.Role == ChatRole.User && !inTask)
            {
                inTask = true;
                taskContext.Add(chatMessage);
                continue;
            }

            // Continue tracking messages in current task
            if (inTask)
            {
                taskContext.Add(chatMessage);
            }

            // End of user turn - when we see an assistant message that completes the turn
            if (inTask && AgentTrajectory.IsEndOfTurnMessage(chatMessage))
            {
                // Evaluate this user turn
                var evaluation = await AnalyzeTaskPerformance(
                    thread: thread,
                    previousChatMessages: chatContext,
                    userTurnMessages: taskContext);

                evaluations.Add(evaluation);

                _logger.LogAgentAction(
                    action: "evaluate.task",
                    parameter: JsonSerializer.Serialize(evaluation),
                    status: "Success",
                    duration: 0,
                    threadId: thread.Id.ToString(),
                    threadSource: thread.Source.ToString(),
                    featureConfig: "{}"); // Using empty object as placeholder

                // Add the user turn to chat context for next evaluation
                chatContext.AddRange(taskContext);
                taskContext.Clear();
                inTask = false;
            }
            // Handle case where we're at the last message but turn isn't completed
            else if (inTask && idx == chatLength)
            {
                _logger.LogInternalError($"Found thread with incomplete user turn at end. " +
                    $"There must be a bug in reasoning loop. ThreadId {thread.Id}");
            }
        }

        return evaluations;
    }

    /// <summary>
    /// Analyze how well the agent handled a specific task/turn
    /// </summary>
    /// <param name="thread">The thread to analyze</param>
    /// <param name="previousChatMessages">Previous chat context</param>
    /// <param name="userTurnMessages">Messages in the current user turn (task + agent responses)</param>
    /// <returns>Analysis result with performance scores</returns>
    private async Task<TaskAnalysisResult> AnalyzeTaskPerformance(
        ThreadModel thread,
        IReadOnlyList<ChatMessage> previousChatMessages,
        IReadOnlyList<ChatMessage> userTurnMessages)
    {
        var userTurnChat = "Failed to extract user turn chat";
        var startUserMessage = ExtractStartTaskMessage(userTurnMessages);
        try
        {
            // Build the task analysis prompt
            var prompts = BuildTaskAnalysisPrompt(
                thread,
                previousChatMessages,
                userTurnMessages);
            userTurnChat = prompts.UserTurnChat;

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.System, prompts.SystemPrompt),
                new(ChatRole.User, prompts.UserPrompt)
            };

            var chatOptions = new ChatOptions
            {
                ToolMode = ChatToolMode.None,
                Temperature = 0.2f,
                ResponseFormat = ChatResponseFormat.Json,
            };

            var response = await _chatClient.GetResponseAsync(chatMessages, chatOptions);
            var jsonResponse = response.GetMessage().Text?.Trim();

            if (string.IsNullOrEmpty(jsonResponse))
            {
                var rawIntent = string.IsNullOrWhiteSpace(startUserMessage) ? "" : startUserMessage;
                var result = new TaskAnalysisResult
                {
                    StartUserMessage = startUserMessage,
                    Resolved = 3,
                    Adherence = 3,
                    Summary = "Unable to analyze - no LLM response",
                    UserIntent = rawIntent,
                    Action = string.Empty,
                    Resource = string.Empty,
                    Condition = string.Empty
                };
                return result;
            }

            // Parse LLM response
            try
            {
                var cleanedJson = jsonResponse.Replace("\n", "");
                cleanedJson = cleanedJson.Replace("\\n", "");
                cleanedJson = cleanedJson.Trim();
                var evaluationData = JsonSerializer.Deserialize<TaskAnalysisResponse>(cleanedJson);
                var resolved = Math.Max(1, Math.Min(5, evaluationData?.Resolved ?? 3));

                var userIntentFinal = evaluationData?.UserIntent ?? (string.IsNullOrWhiteSpace(startUserMessage) ? string.Empty : startUserMessage);
                var actionFinal = evaluationData?.Action ?? string.Empty;
                var resourceFinal = evaluationData?.Resource ?? string.Empty;
                return new TaskAnalysisResult
                {
                    StartUserMessage = startUserMessage,
                    Resolved = resolved,
                    Adherence = Math.Max(1, Math.Min(5, evaluationData?.Adherence ?? 3)),
                    Summary = evaluationData?.Summary ?? "Unable to analyze",
                    UserIntent = userIntentFinal,
                    Action = actionFinal,
                    Resource = resourceFinal,
                    Condition = string.Empty
                };
            }
            catch (JsonException ex)
            {
                _logger.LogInternalWarning(ex, $"Error parsing task evaluation response. Original response: {jsonResponse}");
                var rawIntent3 = string.IsNullOrWhiteSpace(startUserMessage) ? "" : startUserMessage;
                var result = new TaskAnalysisResult
                {
                    StartUserMessage = startUserMessage,
                    Resolved = 3,
                    Adherence = 3,
                    Summary = $"Evaluation parsing failed: {ex.Message}",
                    UserIntent = rawIntent3,
                    Action = string.Empty,
                    Resource = string.Empty,
                    Condition = string.Empty
                };
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Error during task evaluation for thread {thread.Id}");
            var rawIntent4 = string.IsNullOrWhiteSpace(startUserMessage) ? "" : startUserMessage;
            var result = new TaskAnalysisResult
            {
                StartUserMessage = startUserMessage,
                Resolved = 3,
                Adherence = 3,
                Summary = $"Evaluation failed due to error: {ex.Message}",
                UserIntent = rawIntent4,
                Action = string.Empty,
                Resource = string.Empty,
                Condition = string.Empty
            };
            return result;
        }
    }

    /// <summary>
    /// Build the task analysis prompt for the LLM
    /// </summary>
    private static (string UserTurnChat, string SystemPrompt, string UserPrompt) BuildTaskAnalysisPrompt(
        ThreadModel thread,
        IReadOnlyList<ChatMessage> previousChatMessages,
        IReadOnlyList<ChatMessage> userTurnMessages)
    {
        var userTurnChatBuilder = new StringBuilder();
        var systemPromptBuilder = new StringBuilder();
        var userPromptBuilder = new StringBuilder();

        // Build the user turn chat string
        foreach (var message in userTurnMessages)
        {
            string roleStr;
            if (message.Role == ChatRole.User)
            {
                roleStr = "User";
            }
            else if (message.Role == ChatRole.Assistant)
            {
                roleStr = "Assistant";
            }
            else
            {
                roleStr = message.Role.ToString();
            }

            if (message.Contents.Any())
            {
                foreach (var content in message.Contents)
                {
                    userTurnChatBuilder.AppendLine($"{roleStr}: {content}");
                }
            }
        }

        var userTurnChat = userTurnChatBuilder.ToString();

        // Build system prompt
        systemPromptBuilder.AppendLine("You are an expert AI system evaluator. Your task is to analyze how well an AI agent handles individual tasks/requests within a conversation thread.");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("**CONTEXT:**");
        systemPromptBuilder.AppendLine("This is an Azure SRE assistant that helps users with technical questions and tasks. You need to evaluate how effectively the agent responds to each user request.");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("**EVALUATION CRITERIA:**");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("1. **Resolved** (1-5 scale): Did the agent successfully solve the question asked by the user or complete the task assigned?");
        systemPromptBuilder.AppendLine("   - 5: Completely resolved - Agent fully answered the question or completed the task with excellent results");
        systemPromptBuilder.AppendLine("   - 4: Well resolved - Agent successfully addressed the request with good results");
        systemPromptBuilder.AppendLine("   - 3: Partially resolved - Agent made progress but didn't fully complete the request");
        systemPromptBuilder.AppendLine("   - 2: Poorly resolved - Agent attempted but failed to adequately address the request");
        systemPromptBuilder.AppendLine("   - 1: Not resolved - Agent completely failed to address the user's request");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("   Guardrails to reduce hallucination when scoring Resolved:");
        systemPromptBuilder.AppendLine("   - Base your score ONLY on the conversation and tool outputs shown in the 'User Turn to Evaluate'. Do not assume actions were executed if there's no evidence.");
        systemPromptBuilder.AppendLine("   - Do not give high scores for claims of success without concrete results or tool confirmations.");
        systemPromptBuilder.AppendLine("   - For write actions (create/update/delete), a score of 5 is valid if the agent either: (a) executed successfully with evidence, or (b) clearly requests user confirmation/approval and is ready to execute upon approval.");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("   Examples:");
        systemPromptBuilder.AppendLine("   - Score 5 (fully resolved without extra questions OR appropriately asks for approval for write actions):");
        systemPromptBuilder.AppendLine("     User: List my aks clusters");
        systemPromptBuilder.AppendLine("     Assistant: Your AKS clusters include demo-aks (location: eastus)");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("   - Score 4 (needs a small follow-up question to disambiguate before resolving):");
        systemPromptBuilder.AppendLine("     User: Are you able to get any details on the newly added resource group?");
        systemPromptBuilder.AppendLine("     Assistant: I found the following resource groups in your environment: 1) rg-1 (southcentralus), 2) rg-2 (southcentralus).\nPlease let me know which resource group you'd like details on (reply with the number or the name).");
        systemPromptBuilder.AppendLine("     User: 2");
        systemPromptBuilder.AppendLine("     Assistant: Here are the details for resource group rg-2: ...");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("2. **Adherence** (1-5 scale): How well did the agent's actions follow its stated plan/steps for this user turn?");
        systemPromptBuilder.AppendLine("   - 5: Excellent - Actions closely match the plan/steps; clear trace from plan to execution with minimal deviation");
        systemPromptBuilder.AppendLine("   - 4: Good - Mostly followed the plan with minor deviations that didn't impact outcome");
        systemPromptBuilder.AppendLine("   - 3: Average - Mixed adherence; some steps followed, others skipped or changed without clear justification");
        systemPromptBuilder.AppendLine("   - 2: Poor - Frequently deviated from or ignored the plan; weak connection between stated steps and actions");
        systemPromptBuilder.AppendLine("   - 1: Very poor - No visible alignment between plan and actions, or actions contradicted the plan");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("3. **Intent Normalization:** Normalize the user's request to the format: [Action] [ResourcePhrase] <for [SecondaryResource]> <with condition [Condition]>." );
        systemPromptBuilder.AppendLine("   - Action: Single Title Case verb (Get, List, Start, Stop, Restart, Create, Delete, Scale, Update, Describe, Query)");
        systemPromptBuilder.AppendLine("   - ResourcePhrase: Concise noun phrase (e.g., 'CPU usage', 'one VM', 'static IP', 'web app', 'app service plans')");
        systemPromptBuilder.AppendLine("   - SecondaryResource: Include only if user names a specific target (e.g., for VM demo-aca)");
        systemPromptBuilder.AppendLine("   - Condition: Include filters/predicates expressed by user (name == xyz, region=eastus) as 'with condition <original>'");
        systemPromptBuilder.AppendLine("Return separate fields Action and Resource (primary resource type, singular where possible). If ambiguous, choose the closest Azure concept (VM, Web App, Container App, Resource Group, Subscription, Cluster, Static IP, App Service Plan, Log Query). Do not hallucinate details.");
        systemPromptBuilder.AppendLine();
        systemPromptBuilder.AppendLine("**RESPONSE FORMAT:**");
        systemPromptBuilder.AppendLine("Respond in valid JSON ONLY (no markdown) with exactly these fields:");
        systemPromptBuilder.AppendLine("{");
        systemPromptBuilder.AppendLine("  \"Resolved\": 4,");
        systemPromptBuilder.AppendLine("  \"Adherence\": 4,");
        systemPromptBuilder.AppendLine("  \"Summary\": \"Brief 1-2 sentence summary of performance\",");
        systemPromptBuilder.AppendLine("  \"UserIntent\": \"Get CPU usage for VM demo-aca with condition timeRange=1h\",");
        systemPromptBuilder.AppendLine("  \"Action\": \"Get\",");
        systemPromptBuilder.AppendLine("  \"Resource\": \"VM\"");
        systemPromptBuilder.AppendLine("}");

        // Build user prompt
        userPromptBuilder.AppendLine("Please evaluate the following user interaction:");
        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine($"**Thread Information:**");
        userPromptBuilder.AppendLine($"- Title: {thread.Title}");
        userPromptBuilder.AppendLine($"- Source: {thread.Source}");
        userPromptBuilder.AppendLine();

        userPromptBuilder.AppendLine("**Task context to Evaluate:**");
        userPromptBuilder.AppendLine(userTurnChat);
        userPromptBuilder.AppendLine();

        userPromptBuilder.AppendLine("Based on this user interaction, please evaluate how well the agent resolved the user's request, and how closely actions adhered to any stated plan/steps.");
        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine("Notes for Adherence:");
        userPromptBuilder.AppendLine("- Look for an explicit plan/steps the agent stated (e.g., headings like 'Plan', 'Steps', or enumerated tasks) and compare to subsequent actions/tool calls in this turn.");
        userPromptBuilder.AppendLine("- If no explicit plan was stated, infer the intended plan from the agent's initial reasoning or first response and assess alignment with the executed actions.");
        userPromptBuilder.AppendLine("- Minor deviations that improve outcome are acceptable; unjustified deviations should lower the score.");

        return (userTurnChat, systemPromptBuilder.ToString(), userPromptBuilder.ToString());
    }
}

/// <summary>
/// Result of analyzing a single task/turn performance
/// </summary>
public class TaskAnalysisResult
{
    public string StartUserMessage { get; set; } = "";
    public int Resolved { get; set; } = 0;
    // Removed ToolCallAccuracy per updated requirements
    public int Adherence { get; set; } = 0;
    public string Summary { get; set; } = "";
    public string UserIntent { get; set; } = "";
    public string Action { get; set; } = ""; // Parsed action verb
    public string Resource { get; set; } = ""; // Parsed primary resource phrase
    public string Condition { get; set; } = ""; // Parsed condition (after with/where/...)
}

/// <summary>
/// Internal class for deserializing LLM response for task analysis
/// </summary>
internal class TaskAnalysisResponse
{
    public int Resolved { get; set; }
    public int Adherence { get; set; }
    public string? Summary { get; set; }
    public string? UserIntent { get; set; }
    public string? Action { get; set; }
    public string? Resource { get; set; }
}

// Helper methods for task extraction and heuristics
partial class ThreadEvaluator
{
    private static string ExtractStartTaskMessage(IReadOnlyList<ChatMessage> userTurnMessages)
    {
        // Find the first task in the turn
        var firstUserMsg = userTurnMessages.FirstOrDefault(m => m.Role == ChatRole.User);
        if (firstUserMsg is null)
        {
            return string.Empty;
        }

        // Concatenate its contents as text
        var sb = new StringBuilder();
        if (firstUserMsg.Contents.Any())
        {
            foreach (var c in firstUserMsg.Contents)
            {
                sb.AppendLine(c?.ToString());
            }
        }

        var content = sb.ToString();
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        // Extract the part after the marker "User question goes below"
        var marker = "User question goes below";
        var idx = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var rest = content[(idx + marker.Length)..];
            // Trim colon and whitespace/newlines
            rest = rest.TrimStart(':', ' ', '\t', '\r', '\n');
            return rest.Trim();
        }

        // If no marker, return the first non-empty line as the start message
        foreach (var line in content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed))
            {
                return trimmed;
            }
        }

        return content.Trim();
    }
}
