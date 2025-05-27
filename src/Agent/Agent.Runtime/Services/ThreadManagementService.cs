// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DataModels;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Action = Agent.Core.Models.Api.v1.Action;
using Thread = Agent.Core.Models.Api.v1.Thread;
using Agent.Runtime.MetaAgent.Interfaces;
using Microsoft.DurableTask.Client;
using Agent.Plugins;
using Agent.Runtime.Services;
using Microsoft.Extensions.Logging;
using System.Threading;
using Agent.Core.Configuration;

namespace Agent.Runtime.Services;
public class ThreadManagementService(
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IAgentsFactory agentsFactory,
    IThreadRepository repository,
    IChatClient chatClient,
    ILogger<ThreadManagementService> logger,
    CoreSettings coreSettings)
{
    public async Task<Thread> CreateUserInitiatedThread(CreateThreadRequest request)
    {
        var threadId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        string temporaryTitle = request.StartMessage.Text.Length <= 50 ? request.StartMessage.Text : request.StartMessage.Text.Substring(0, 47) + "...";

        var message = new Message(
                Id: messageId,
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.User, request.StartMessage.UserId, request.StartMessage.DisplayName),
                Text: request.StartMessage.Text
            );
        var thread = new Thread(
            Id: threadId,
            Title: temporaryTitle,
            StartMessage: message,
            LastMessage: message,
            CreatedTimestamp: DateTime.UtcNow,
            ModifiedTimestamp: DateTime.UtcNow,
            Source: request.Source ?? ThreadSource.Conversation
        );

        var agentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: thread.Id,
            AgentType: AgentTypeEnum.Meta,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null
        );

        var reasoningMessages = new List<ReasoningMessage>();

        // when using new agent framework, the chat history is fully handled by reasoning loop
        if (!coreSettings.UseAgentFramework)
        {
            var systemPromptReasoningMessage = new ReasoningMessage(
            Id: Guid.NewGuid(),
                AgentContextId: agentContext.Id,
                Role: ReasoningMessageRoleEnum.System,
                SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.System, agentsFactory.GetMetaAgentSystemPrompt()))
            );

            var startReasoningMessage = new ReasoningMessage(
                Id: Guid.NewGuid(),
                AgentContextId: agentContext.Id,
                Role: ReasoningMessageRoleEnum.User,
                SerializedChatMessage: JsonSerializer.Serialize(new ChatMessage(ChatRole.User, message.Text))
            );
            reasoningMessages.Add(systemPromptReasoningMessage);
            reasoningMessages.Add(startReasoningMessage);
        }

        var agentChatHistory = new AgentChatHistory(
            AgentContextId: agentContext.Id,
            ReasoningMessageIds: reasoningMessages.Select(r => r.Id).ToList());

        thread = await repository.CreateThreadAsync(thread);
        message = await repository.AddMessageAsync(thread.Id, message);
        agentContext = await repository.CreateAgentContextAsync(agentContext);
        
        foreach (var reasoningMessage in reasoningMessages)
        {
            await repository.CreateReasoningMessageAsync(reasoningMessage);
        }

        agentChatHistory = await repository.CreateAgentChatHistoryAsync(agentChatHistory);

        var threadContext = new ThreadContext(thread.Id, AgentTypeEnum.Meta);
        threadContext.AddMessage(thread.StartMessage);
        await repository.AddThreadContextAsync(threadContext);

        //Start the background title generation task (fire and forget)
        _ = TitleHelper.GenerateTitleAndUpdateAsync(chatClient, repository, thread.Id, request.StartMessage.Text);

        var response = await agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
        (
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage.Id,
            Message: request.StartMessage.Text,
            UserId: request.StartMessage.UserId,
            DisplayName: request.StartMessage.DisplayName,
            Timestamp: DateTime.UtcNow
        ));

        return thread;
    }

    public async Task<InboundServiceResponse?> CreateMessage(Guid threadId, CreateMessageRequest request)
    {
        // First check if thread exists
        var thread = await repository.GetThreadAsync(threadId);

        if (thread == null)
        {
            return null;
        }

        var agentContexts = await repository.GetAgentContextsForThreadAsync(threadId);

        // Pick out original agent context (not handed from another agent)
        var agentContext = agentContexts.FirstOrDefault(c => c.HandoffFromAgentContextId == null);
        if (agentContext == null)
        {
            logger.LogInternalWarning($"No meta agent context found for thread {threadId}");
            return null;
        }

        var response = await agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
        (
            ThreadId: threadId,
            AgentContextId: agentContext.Id,
            MessageId: Guid.NewGuid(),
            Message: request.Text,
            UserId: request.UserId,
            DisplayName: request.DisplayName,
            Timestamp: DateTime.UtcNow
        ));

        return response;
    }
}
