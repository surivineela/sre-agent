// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Kusto.Data.Common.Impl;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Octokit;

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

        public void InitChatHistoryFromMessageQueue(Queue<Message> messages)
        {
            foreach (var message in messages)
            {
                if (message.Author.Role == Role.User)
                {
                    ChatHistory.Add(new(ChatRole.User, message.Text));
                }
                else if (message.Author.Role == Role.SREAgent)
                {
                    ChatHistory.Add(new(ChatRole.Assistant, message.Text));
                }
                else if (message.Author.Role == Role.System)
                {
                    ChatHistory.Add(new(ChatRole.System, message.Text));
                }
            }
        }

        public virtual async Task PrepareAgentForUserInput(Guid agentContextId, ThreadContext threadContext)
        {
            this.InitChatHistoryFromMessageQueue(threadContext.RecentMessages);

            await this.PrepareAgentForUserInput();
            var messagesToAddToChatHistory = this.GetUserVisibleChatHistory();
            foreach (var messageToAddToChatHistory in this.ChatHistory)
            {
                await _repository.CreateReasoningMessageAsync(new ReasoningMessage(
                    Id: Guid.NewGuid(),
                    AgentContextId: agentContextId,
                    Role: messageToAddToChatHistory.Role.GetReasoningMessageRole(),
                    SerializedChatMessage: JsonConvert.SerializeObject(messageToAddToChatHistory)));

                if (messageToAddToChatHistory.Role == ChatRole.User)
                {
                    await _sinkService.SinkUserMessageAsync(threadContext, messageToAddToChatHistory.Text);
                }
                else if (messageToAddToChatHistory.Role == ChatRole.Assistant)
                {
                    await _sinkService.SinkAgentMessageAsync(threadContext, messageToAddToChatHistory.Text);
                }
                else
                {
                    await _sinkService.SinkSystemMessageAsync(threadContext, messageToAddToChatHistory.Text);
                }
            }

            if (_isConcludingThreadAfterOpeningMessages)
            {
                threadContext.ConcludeThreadContext();
                await _repository.UpdateThreadContextAsync(threadContext);
            }
        }

        public virtual async Task<(string ResponseText, List<ReasoningMessage> ResponseReasoningMessages)> DoWork(Guid agentContextId, ThreadContext threadContext, string question)
        {
            InitChatHistoryFromMessageQueue(threadContext.RecentMessages);

            (var agentResponse, var responseReasoningMessages) = await base.DoWork(agentContextId, question);

            foreach (var reasoningMessage in responseReasoningMessages)
            {
                await _repository.CreateReasoningMessageAsync(reasoningMessage);
            }

            ChatHistory.Add(new ChatMessage(ChatRole.User, "Answering only with \"yes\" or \"no\", is this thread complete?"));
            var response = await _chatClient.GetResponseAsync(ChatHistory, ChatOptionsWithTools);

            bool isComplete = response.Text.Contains("yes", StringComparison.OrdinalIgnoreCase);

            if (isComplete)
            {
                threadContext.ConcludeThreadContext();
                await _repository.UpdateThreadContextAsync(threadContext);
            }

            return (agentResponse, responseReasoningMessages);
        }
    }
}
