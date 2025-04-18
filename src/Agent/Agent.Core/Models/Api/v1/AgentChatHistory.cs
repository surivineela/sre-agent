// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;

namespace Agent.Core.Models.Api.v1;

public record AgentChatHistory(
    Guid AgentContextId,
    List<Guid> ReasoningMessageIds
)
{
    public async Task<List<ReasoningMessage>> GetReasoningMessagesAsync(IThreadRepository threadRepository)
    {
        var reasoningMessages = new List<ReasoningMessage>();
        foreach (var reasoningMessageId in ReasoningMessageIds)
        {
            var reasoningMessage = await threadRepository.GetReasoningMessageAsync(reasoningMessageId, AgentContextId);
            if (reasoningMessage != null)
            {
                reasoningMessages.Add(reasoningMessage);
            }
        }

        return reasoningMessages;
    }
}

