// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework;

public static class Summarizer
{
    public static async Task<string> SummarizeUserMessagesAsync(
        IChatClient chatClient,
        IReadOnlyList<ChatMessage> messages)
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

        if (userMessages.Count == 0)
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

        var response = await chatClient.GetResponseAsync(summarizeMessages, new ChatOptions
        {
            Temperature = 0.3f,
            ToolMode = ChatToolMode.None,
            ResponseFormat = ChatResponseFormat.Text,
        });

        // fallback to conversation text if no summary is generated
        return response.Messages.LastOrDefault()?.Contents.OfType<TextContent>().FirstOrDefault()?.Text
               ?? conversationText;
    }

    public static async Task<string> SummarizeActorTrajectoryAsync(
        string userQuery,
        string trajectory,
        IChatClient chatClient)
    {
        var templatePath = Path.Join(AppContext.BaseDirectory, "Summarizer/summarizer.txt");
        var summarizerTemplate = await File.ReadAllTextAsync(templatePath);
        var summarizerPrompt = summarizerTemplate
            .Replace("{{userQuery}}", userQuery);

        var summarizerChat = new List<ChatMessage>
        {
            new(ChatRole.System, summarizerPrompt),
            new(ChatRole.User, trajectory),
        };

        var summarizerChatOptions = new ChatOptions
        {
            ToolMode = ChatToolMode.None,
            Temperature = 0,
            ResponseFormat = ChatResponseFormat.Text,
        };

        var trajectorySummary = await chatClient.GetResponseAsync(summarizerChat, summarizerChatOptions);

        return trajectorySummary.Text;
    }
}
