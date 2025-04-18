// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;

namespace Agent.Core.Extensions
{
    public static class ChatMessageExtensions
    {
        public static ChatMessage GetMessage(this ChatResponse? chatResponse)
        {
            if (chatResponse == null)
            {
                throw new ArgumentNullException(nameof(chatResponse));
            }

            if (chatResponse.Messages.Count != 1)
            {
                throw new ArgumentException(
                    $"ChatResponse contains {chatResponse.Messages.Count} messages but you should only use this extension method when there is a single message. " +
                    "Update the codepath that hit this to handle the fact that there might be multiple messages. " +
                    "For example, if there were multiple cycles of tool calls, there will be multiple messages.", nameof(chatResponse));
            }

            return chatResponse.Messages[0];
        }

        public static List<ReasoningMessage> GetReasoningMessages(this ChatResponse? chatResponse, Guid agentContextId)
        {
            var reasoningMessages = new List<ReasoningMessage>();

            if (chatResponse == null)
            {
                return reasoningMessages;
            }

            foreach (var message in chatResponse.Messages)
            {
                reasoningMessages.Add(new ReasoningMessage(
                    Id: Guid.NewGuid(),
                    AgentContextId: agentContextId,
                    Role: GetReasoningMessageRole(message.Role),
                    SerializedChatMessage: JsonConvert.SerializeObject(message)));
            }

            return reasoningMessages;
        }

        public static async Task AddReasoningMessagesToThreadRepositoryAsync(this ChatResponse? chatResponse, IThreadRepository threadRepository, Guid agentContextId)
        {
            var reasoningMessages = chatResponse.GetReasoningMessages(agentContextId);

            foreach (var reasoningMessage in reasoningMessages)
            {
                await threadRepository.CreateReasoningMessageAsync(reasoningMessage);
            }
        }

        public static ReasoningMessageRoleEnum GetReasoningMessageRole(this ChatRole chatRole)
        {
            if (chatRole == ChatRole.System)
            {
                return ReasoningMessageRoleEnum.System;
            }
            if (chatRole == ChatRole.Assistant)
            {
                return ReasoningMessageRoleEnum.Assistant;
            }
            if (chatRole == ChatRole.User)
            {
                return ReasoningMessageRoleEnum.User;
            }
            if (chatRole == ChatRole.Tool)
            {
                return ReasoningMessageRoleEnum.Tool;
            }

            throw new ArgumentOutOfRangeException(nameof(chatRole), chatRole, null);
        }
    }
}

