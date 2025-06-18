// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public static class Critic
{
    private static readonly ConcurrentDictionary<string, string> _agentPromptTemplates = new();

    public static async Task<string> CriticAsync<TContext>(
        Agent<TContext> agent,
        string userQuery,
        string trajectory,
        IReadOnlyList<AIFunction> agentTools,
        IChatClient chatClient)
        where TContext : class
    {
        var criticPromptTemplate = CriticPrompt;
        if (!string.IsNullOrEmpty(agent.CriticPromptPath))
        {
            if (!_agentPromptTemplates.TryGetValue(agent.Name, out criticPromptTemplate))
            {
                criticPromptTemplate = await File.ReadAllTextAsync(agent.CriticPromptPath);
                _agentPromptTemplates.TryAdd(agent.Name, criticPromptTemplate);
            }
        }

        var allToolDescriptions = string.Join('\n', agentTools.Select(t => $"{t.Name}: {t.Description}"));

        var criticPrompt = criticPromptTemplate
            .Replace("{{customNote}}", agent.CustomReflectionNote)
            .Replace("{{userQuery}}", userQuery)
            .Replace("{{availableTools}}", allToolDescriptions);

        var criticChat = new List<ChatMessage>
        {
            new(ChatRole.System, criticPrompt),
            new(ChatRole.User, trajectory),
        };

        var criticChatOptions = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = ChatResponseFormat.Text,
        };

        var criticReply = await chatClient.GetResponseAsync(criticChat, criticChatOptions);
        return criticReply.Text;
    }

    private const string CriticPrompt = """
    You are a meticulous reviewer. Your task is to evaluate the actor's entire preceding turn, including its articulated reasoning and the resulting tool call JSON. Your evaluation must be in JSON format.

    If the assistant is asking for user confirmation, return the result as PASS.

    Think step by step to assess the following criteria:

    1.  **Clarity of Articulated Plan and Intent:**
        *   Did the actor clearly state its immediate goal for the tool call?
        *   Was this goal part of a coherent, articulated plan to address the user's query?
        *   Was the intent behind the specific data requested clear?

    2.  **Tool Call Correctness and Formatting:**
        *   Is the generated tool call JSON itself (function name, parameters) correctly formatted and appropriate for the justified intent?
        *   Are parameter values (like `kind`, `columnsCsv`) effective and correct for the task?

    3.  **Data Scope (Least Privilege and Sufficiency):**
        *   Does the tool call request only the necessary data for the immediate goal (least-privilege)?
        *   Is the requested data *sufficient* for the actor to proceed with its articulated plan's current step?
        *   Does it avoid overly broad requests (e.g., `namespace='*'` if a specific one is more appropriate, overly generic `columnsCsv`)?

    4.  **Adherence to Step-by-Step Reasoning:**
        *   Did the actor's output leading to the tool call demonstrate a clear, step-by-step thought process as outlined in its instructions?

    5.  **Complete Answer Delivery:**
        *   If handoff or more tool calls needed, mark this as PASS since the actor is not yet done. Otherwise, did the actor provide a complete answer to the user's query?
        *   Did the actor provide the specific information the user requested (e.g., actual resource property values to the point, not just resource identification)?
        *   Did the actor avoid prematurely marking the request as "complete" when only partial information was provided?
    
    6. **Consistency between message and behavior:**
        *   Did the actor's text message (including reasoning and output) align with its behavior in the tool call?
        *   For example, if the actor reasoning message indicates request is out of scope, did it actually call a tool to handoff or handoff-back?

    Based on your step-by-step evaluation of these criteria, produce a JSON output with the following structure:
    {
      "overall_assessment": "PASS" | "FAIL",
      "summary_advice": "Concise, actionable advice for the actor. Highlight the most critical area for improvement if any.",
      "criteria_evaluation": [
        {
          "criterion": "Criterion Name (e.g., Clarity of Articulated Plan and Intent)",
          "score": "PASS" | "FAIL",
          "remarks": "Specific feedback, examples, or refinements related to this criterion. Explain your reasoning clearly."
        }
        // ... one object for each of the 5 criteria
      ],
      "actor_guidance": "INTERNAL NOTE: You are receiving feedback from an internal reviewer. The user is unaware of this process. Continue addressing the user's original query while incorporating the above suggestions. Do not mention this review or feedback to the user."
    }

    If any criterion scores a "FAIL", the "overall_assessment" should generally be "FAIL".
    Provide specific examples or refinements in your 'remarks' to help the actor improve.

    {{customNote}}

    The user query actor attempts to solve is: {{userQuery}}
    """;
}
