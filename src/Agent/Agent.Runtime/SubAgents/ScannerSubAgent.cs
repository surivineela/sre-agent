// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents
{
    public abstract class ScannerSubAgent : SubAgent
    {
        private readonly SinkService _sinkService;
        private readonly IThreadRepository _repository;
        private readonly bool _isConcludingThreadAfterOpeningMessages;

        public ScannerSubAgent(
            string name,
            IChatClient chatClient,
            SinkService sinkService,
            IThreadRepository repository,
            bool isConcludingThreadAfterOpeningMessages,
            bool isSkippingInitChatHistory = false)
            : base(name, chatClient, isSkippingInitChatHistory)
        {
            _sinkService = sinkService;
            _repository = repository;
            _isConcludingThreadAfterOpeningMessages = isConcludingThreadAfterOpeningMessages;
        }

        public async Task InitChatHistoryFromMessageQueueAsync(AgentChatHistory agentChatHistory)
        {
            var reasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_repository);
            var chatMessages = reasoningMessages.GetChatMessages();

            foreach (var message in chatMessages)
            {
                ChatHistory.Add(message);
            }
        }

        public virtual async Task PrepareAgentForUserInput(AgentContext agentContext)
        {
            var agentChatHistory = await _repository.GetAgentChatHistoryAsync(agentContext.Id);
            await this.InitChatHistoryFromMessageQueueAsync(agentChatHistory);

            var startingMessages = await this.PrepareAgentForUserInput();
            foreach (var messageToAddToChatHistory in startingMessages)
            {
                var reasoningMessage = new ReasoningMessage(
                    Id: Guid.NewGuid(),
                    AgentContextId: agentContext.Id,
                    Role: messageToAddToChatHistory.Role.GetReasoningMessageRole(),
                    SerializedChatMessage: JsonSerializer.Serialize(messageToAddToChatHistory));
                await _repository.CreateReasoningMessageAsync(reasoningMessage);

                agentChatHistory.ReasoningMessageIds.Add(reasoningMessage.Id);

                if (messageToAddToChatHistory.Role == ChatRole.User)
                {
                    await _sinkService.SinkUserMessageAsync(messageToAddToChatHistory.Text, agentContext.ThreadId);
                }
                else if (messageToAddToChatHistory.Role == ChatRole.Assistant && !string.IsNullOrEmpty(messageToAddToChatHistory.Text))
                {
                    await _sinkService.SinkAgentMessageAsync(agentContext.ThreadId, messageToAddToChatHistory.Text);
                }
            }

            await _repository.UpdateAgentChatHistoryAsync(agentChatHistory);
        }

        public virtual async Task<string> DoWork(AgentContext agentContext, AgentChatHistory agentChatHistory, string question)
        {
            await InitChatHistoryFromMessageQueueAsync(agentChatHistory);

            (var agentResponse, var responseReasoningMessages) = await base.DoWork(agentContext.Id, question);

            foreach (var reasoningMessage in responseReasoningMessages)
            {
                await _repository.CreateReasoningMessageAsync(reasoningMessage);
                agentChatHistory.ReasoningMessageIds.Add(reasoningMessage.Id);
            }

            await _repository.UpdateAgentChatHistoryAsync(agentChatHistory);

            return agentResponse;
        }

        public virtual async Task<string> DoWork(AgentContext agentContext, AgentChatHistory agentChatHistory)
        {
            // get latest message from chat history
            await InitChatHistoryFromMessageQueueAsync(agentChatHistory);

            (var agentResponse, var responseReasoningMessages) = await base.DoWork(agentContext.Id);

            foreach (var reasoningMessage in responseReasoningMessages)
            {
                await _repository.CreateReasoningMessageAsync(reasoningMessage);
                agentChatHistory.ReasoningMessageIds.Add(reasoningMessage.Id);
            }

            await _repository.UpdateAgentChatHistoryAsync(agentChatHistory);

            return agentResponse;
        }
    }
}
