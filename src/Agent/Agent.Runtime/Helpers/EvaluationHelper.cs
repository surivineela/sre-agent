// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

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
}
