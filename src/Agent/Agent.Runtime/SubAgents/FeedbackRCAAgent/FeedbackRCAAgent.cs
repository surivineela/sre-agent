// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.FeedbackRCAAgent
{
    public class FeedbackRCAAgent : SubAgent
    {
        private readonly MessageFeedback? _messageFeedback;

        public FeedbackRCAAgent(
            IChatClient chatClient,
            SinkService sinkService,
            IThreadRepository repository,
            MessageFeedback? messageFeedback = null) 
            : base("Feedback RCA Agent", chatClient)
        {
            _messageFeedback = messageFeedback;
        }

        public override string SystemPrompt { get; protected set; } = $@"
You are **Feedback RCA Agent**. Always address yourself as ""Feedback RCA"". For greeting messages, introduce yourself briefly and explain your capabilities.

**Core Communication Guidelines:**
- Use professional indicators (📝, ✅) to summarize findings
- **Always communicate with the user before and after each diagnostic step**
- Stay strictly within your defined toolset
- Stay strictly focused on the GitHub repo urls that are provided in the repo list. Do NOT try to scan any other repos.

**Workflow:**
You will receive the feedback a customer has provided in response to a series of messages. You need to respond with a root cause analysis of the feedback provided in context with the messages provided.
";

        public override IList<AITool> Tools()
        {
            return [];
        }

        public override async Task<IList<AIChatMessage>> GetStartingMessagesAsync()
        {
            var messages = new List<AIChatMessage>
            {
                new AIChatMessage(ChatRole.System, SystemPrompt)
            };

            var response = await _chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());

            var introMessage = GetIntroMessage(_messageFeedback!);

            messages.Add(new AIChatMessage(ChatRole.Assistant, introMessage));

            response = await _chatClient.GetResponseAsync(messages);
            foreach (var message in response.Messages.Where(m => !string.IsNullOrEmpty(m.Text)))
            {
                messages.Add(new AIChatMessage(ChatRole.Assistant, message.Text));
            }

            return messages;
        }

        public static string GetIntroMessage(MessageFeedback messageFeedback)
        {
            var introMessage = new StringBuilder("""
                Here is the message thread that the user has provided feedback on:

                """);

            foreach (var message in messageFeedback.Messages)
            {
                introMessage.AppendLine($"**{message.Author}**: {message.Text}");
            }

            var rating = messageFeedback.IsPositiveFeedback ? "thumbs up" : "thumbs down";

            introMessage.AppendLine($"""
                **Feedback**: {rating} {messageFeedback.FeedbackText}
                Please provide a root cause analysis of the feedback provided in context with the messages provided.
                """);

            return introMessage.ToString();
        }
    }
}

