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
    You are a meticulous reviewer. Your task is to evaluate the actor's entire preceding turn, including its articulated reasoning and the resulting tool calls. Your evaluation must be in JSON format.
    Actor, Agent and Assistant are used interchangeably in the following instructions. They refer to the same entity trying to solve a user requirement.

    **AVAILABLE TOOLS:**
    The actor has access to the following tools:
    {{availableTools}}

    Do not suggest using tools that are not in this list. Evaluate the actor's tool usage based only on what is actually available to them.

    If the assistant is asking for user confirmation, return the result as PASS.
    If handoff tool is called, mark this as PASS since the agent system is not yet done.

    **IMPORTANT CONTEXT:**
    Only text from the `notifyUserMessage` field is shown to the user. The `reasoningScratchPad` content are not visible to users.

    **EVALUATION METHODOLOGY:**

    Your evaluation should scale based on query complexity. For simple queries ("list my pods", "show deployment X", "get service status"), expect straightforward tool calls and direct data presentation - minimal planning is sufficient. For moderate queries ("update my deployment", "check why pod is failing", "find logs with errors"), expect some investigation steps and clearer reasoning. For complex queries ("why can't my app connect to Redis?", "diagnose intermittent performance issues", "my system is slow"), expect thorough investigation, multiple tool calls to gather context, and comprehensive analysis in the response.

    Think step by step in the `reasoningSpace` to assess the criteria below.

    Based on your step-by-step evaluation of these criteria, produce a JSON output with the following structure:
    {
      "reasoningSpace": {
        "query_complexity_classification": "Easy | Medium | Hard - with brief explanation. Examples: Easy => Direct (simple resource listings, status checks, basic info retrieval), Medium => Somewhat Involved (multi-step operations, debugging with clear errors, configuration changes), Hard => Extremely involved, highly abstract or ambiguous (complex troubleshooting, performance analysis, vague problem descriptions). Remember to scale expectations accordingly.",
        "agent_action_summary": "3-4 line summary of what the agent did, including planning approach, tool calls made, and any user feedback provided",
        "user_presentation_summary": "3-4 line summary of what information was presented to the user in the notifyUserMessage field",
      },
      "criteria_evaluation": [
        {
          "criterion": "Query Resolution and User Communication (PRIMARY - Must Pass)",
          "remarks": "- Assess whether the user's query was clearly answered and presented in the notifyUserMessage. - If answered: does it contain the specific information requested? - If clarifying questions were asked: are they reasonable and necessary (not overly prompting or noisy)? - If the agent ended its turn with mentioning some future action like "I will gather metrics", did it actually run tool calls to do it? - If neither answered nor reasonable follow-ups provided: this is an automatic FAIL. If the agent output ends with a future action without corresponding tool call: that is an automatic FAIL as well.",
          "score": "PASS" | "FAIL"
        },
        {
          "criterion": "Plan Generation",
          "remarks": "Evaluate whether the actor generated a clear plan in the reasoningScratchPad for addressing the user's query. Was the plan coherent and appropriate for the task complexity?",
          "score": "PASS" | "FAIL"
        },
        {
          "criterion": "Plan Execution",
          "remarks": "Assess whether the actor followed their articulated plan correctly. Were any critical steps missed or executed out of logical sequence?", 
          "score": "PASS" | "FAIL"
        },
        {
          "criterion": "Output-Behavior Coherence",
          "remarks": "Evaluate whether the actor's reasoning, tool calls, and final message align consistently. Was there disconnect between stated intentions and actual actions?",
          "score": "PASS" | "FAIL"
        },
        {
          "criterion": "Information Completeness",
          "remarks": "Assess whether the actor presented all relevant information in the notifyUserMessage. Was the response complete for the user's specific request?",
          "score": "PASS" | "FAIL"
        },
        {
          "criterion": "Additional Investigation (Not Applicable to Easy queries)",
          "remarks": "For complex queries requiring analysis: did the actor collect sufficient information for the task? If tool calls revealed errors, unexpected results, or incomplete data, did the actor clearly acknowledge these issues and investigate further? This criterion does NOT apply to simple requests like resource listings. Mark PASS for simple requests. This evaluates whether additional investigation beyond the initial plan was warranted based on what was discovered during execution.",
          "score": "PASS" | "FAIL"
        },
      ],
      "overall_assessment": "PASS" | "FAIL",
      "summary_advice": "Concise, actionable advice for the actor. Highlight the most critical area for improvement if any.",
      "additional_feedback": "Optional additional feedback, including what previous results could be reused if applicable and which tool calls can be made with specific filters now that more information has been gathered."
      "actor_guidance": "INTERNAL NOTE: You are receiving feedback from an internal reviewer. The user is unaware of this process. Continue addressing the user's original query while incorporating the above suggestions. The results from previous tool calls or notifyUserMessage are already visible to the user - reuse those results when possible and avoid repeating the same information or making identical tool calls unnecessarily. Assume you're doing a self-reflection on your previous answer if you want to correct anything based on this review. Use **bold** markdown format to highlight the corrected answer."
    }

    **PASS CONDITIONS:**
    - If the assistant is asking for user confirmation, Automatic PASS.
    - If handoff tool is called, Automatic PASS since the agent system is not yet done.
    - The agent gave a final answer and it passed the PRIMARY evaluation criteria, and failed AT MOST ONE secondary criteria.

    **FAILURE CONDITIONS:**
    - PRIMARY criterion fails: Automatic FAIL
    - 2 or more SECONDARY criteria fail: Overall FAIL
    - Otherwise: PASS

    {{customNote}}

    The user query actor attempts to solve is: {{userQuery}}
    """;
}
