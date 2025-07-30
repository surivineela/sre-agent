// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.Helpers;

public static class EvaluationHelper
{
    public static async Task<IReadOnlyList<ChatMessage>> GetChatMessages(
        IThreadRepository threadRepository,
        AgentContext agentContext,
        ILogger logger)
    {
        var agentChatHistory = await threadRepository.GetAgentChatHistoryAsync(agentContext.Id);
        if (agentChatHistory == null)
        {
            logger.LogInternalError("No chat history found for agent context {agentContextId}, this should never happen.", agentContext.Id);
            return [];
        }

        var reasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(threadRepository);
        return reasoningMessages.GetChatMessages();
    }

    public static List<ChatMessage> GetChatMessagesForReasoningMessages(IReadOnlyList<ReasoningMessage> reasoningMessages, ILogger logger)
    {
        if (reasoningMessages.Count == 0)
        {
            return new List<ChatMessage>();
        }

        var chatMessages = new List<ChatMessage>();
        foreach (var reasoningMessage in reasoningMessages)
        {
            try
            {
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(reasoningMessage.SerializedChatMessage);
                if (chatMessage != null)
                {
                    chatMessages.Add(chatMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalWarning(ex, $"Error deserializing reasoning message {reasoningMessage.Id}");
            }
        }

        return chatMessages;
    }

    public static string GetToolCallName(string methodName)
    {
        return methodName.EndsWith("Async") ? methodName[..^5] : methodName;
    }
}
