// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class Trajectory
{
    public StringBuilder MessageContent { get; } = new StringBuilder();
    public StringBuilder FunctionCallContent { get; } = new StringBuilder();

    public void Append(ChatResponse? modelResponse = null, ManualToolCallResult? toolCallResult = null, string? message = null)
    {
        if (!string.IsNullOrEmpty(message))
        {
            MessageContent.AppendLine(message);
        }
        if (modelResponse != null)
        {
            foreach (var msg in modelResponse.Messages)
            {
                foreach (var content in msg.Contents)
                {
                    if (content is TextContent textContent)
                    {
                        MessageContent.AppendLine(textContent.Text);
                    }
                }
            }
        }

        if (toolCallResult != null)
        {
            var functionCallResultJson = JsonSerializer.Serialize(new
            {
                function_name = toolCallResult.FunctionCall.Name,
                function_parameters = toolCallResult.FunctionCall.Arguments,
                result = toolCallResult.Output
            });
            FunctionCallContent.AppendLine($"Function Call: {functionCallResultJson}");
        }
    }

    public override string ToString() => MessageContent.ToString() + Environment.NewLine + FunctionCallContent.ToString();

    // FunctionCallContent can be very large, so we provide a way to clear it, only critic for the recent round of function calls
    public void ResetFunctionContent()
    {
        FunctionCallContent.Clear();
    }
}

public static class Critic
{

    public static async Task<string> CriticAsync(RunConfig config, string customNote, List<ChatMessage> input, string trajectory)
    {
        var userQuery = await SummarizeChatMessagesAsync(config, input);
        var criticPrompt = CriticPrompt.Replace("{{userQuery}}", userQuery);
        if (!string.IsNullOrEmpty(customNote))
        {
            criticPrompt = criticPrompt.Replace("{{customNote}}", customNote);
        }
        else
        {
            criticPrompt = criticPrompt.Replace("{{customNote}}", "");
        }
        var criticChat = new List<ChatMessage>
        {
            new(ChatRole.System, criticPrompt),
        };

        var criticChatOptions = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0.2f,
            ResponseFormat = ChatResponseFormat.Text,
        };
        criticChat.Add(new(ChatRole.User, trajectory));
        var criticReply = await config.ChatClient.GetResponseAsync(criticChat, criticChatOptions);
        return criticReply.Text;
    }

    private static async Task<string> SummarizeChatMessagesAsync(RunConfig config, List<ChatMessage> messages)
    {
        if (messages == null || !messages.Any())
        {
            return "No messages to summarize.";
        }

        var userMessages = messages.Where(m => m.Role != ChatRole.System && m.Role != ChatRole.Tool)
            .Select(m => new ChatMessage(m.Role,
                m.Contents.OfType<TextContent>()
                    .Where(c => !(c.Text.Contains("overall_assessment", StringComparison.OrdinalIgnoreCase) &&
                               c.Text.Contains("summary_advice", StringComparison.OrdinalIgnoreCase)))
                    .ToArray()))
            .Where(m => m.Contents.Any())
            .ToList();

        if (!userMessages.Any())
        {
            return "No user messages to summarize.";
        }
        if (userMessages.Count == 1)
        {
            return string.Join(" ", userMessages.First().Contents.OfType<TextContent>().Select(c => c.Text));
        }

        var conversationText = string.Join("\n", userMessages.Select(m =>
            $"{m.Role}: {string.Join(" ", m.Contents.OfType<TextContent>().Select(c => c.Text))}"));

        var summarizePrompt = $@"Analyze the following conversation and create a summary written from the user's perspective.
Write as if you are the user describing what you want to accomplish. Use first-person language (""I want..."", ""I need..."", ""My goal is..."").

Focus on:
- What I am trying to accomplish
- The main problem or task I need help with
- Key requirements or constraints I have mentioned
- Expected outcomes or goals I want to achieve

Provide a clear, concise summary written as the user's request:

{conversationText}";

        var summarizeMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, @"You are tasked with summarizing conversations from the user's perspective.
Your output should be written as if the user themselves is describing their request or problem.
Use first-person language and capture their intent, needs, and goals clearly and concisely.
The summary should sound like the user speaking directly about what they want to accomplish."),
            new ChatMessage(ChatRole.User, summarizePrompt)
        };

        var response = await config.ChatClient.GetResponseAsync(summarizeMessages, new ChatOptions
        {
            Temperature = 0.3f,
            ToolMode = ChatToolMode.None,
            ResponseFormat = ChatResponseFormat.Text,
        });

        // fallback to conversation text if no summary is generated
        return response.Messages.LastOrDefault()?.Contents.OfType<TextContent>().FirstOrDefault()?.Text
               ?? conversationText;
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
