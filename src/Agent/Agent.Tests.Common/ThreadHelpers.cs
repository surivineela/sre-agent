using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Microsoft.Identity.Client;

namespace Agent.Tests.Common;
public static class ThreadHelpers
{
    public static List<ChatMessage> ToChatMessages(this IEnumerable<Message> messages)
    {
        return messages.Select(x =>
        {
            var messageRole = x.Author.Role switch
            {
                Role.User => ChatRole.User,
                Role.SREAgent => ChatRole.Assistant,
                Role.System => ChatRole.System,
                Role.PluginLog => ChatRole.System, // I have no idea what a pluginlog is
                _ => throw new ArgumentOutOfRangeException(nameof(x.Author.Role), x.Author.Role, null)
            };

            return new ChatMessage(messageRole, x.Text);
        }).ToList();
    }

    public static async Task<OrchestrationState> WaitForSubAgentAssignment(this IThreadRepository threadRepository, Guid threadId, CancellationToken cancellationToken)
    {
        OrchestrationState? orchestrationState = null;
        while (string.IsNullOrEmpty(orchestrationState?.OrchestrationInstanceId))
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            var threadContext = await threadRepository.GetThreadContextAsync(threadId);
            orchestrationState = threadContext.OrchestrationState;
        }

        return orchestrationState;
    }

    public static async Task<(ChatResponse agentResponse, List<ChatMessage> fullConversation)> WaitForAgentResponse(this IThreadRepository threadRepository, Core.Models.Api.v1.Thread thread, CancellationToken cancellationToken)
    {
        IEnumerable<Message> threadMessages = null;
        while (threadMessages == null || !threadMessages.Any() || threadMessages.Last().Author.Role == Role.User)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            threadMessages = await threadRepository.GetMessagesAsync(thread.Id);
        }

        var agentResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, threadMessages.Last().Text));
        List<ChatMessage> fullConversation = threadMessages.ToChatMessages();
        return (agentResponse, fullConversation);
    }
}
