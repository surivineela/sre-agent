// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Logging;
using Microsoft.Extensions.Logging;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Runtime.Services;

public class ThreadManagementService(
    IAgentInboundCommunicationService agentInboundCommunicationService,
    IAgentOutboundCommunicationService outboundCommunicationService,
    IThreadRepository repository,
    ITitleGenerationService titleGenerationService,
    ILogger<ThreadManagementService> logger,
    IThreadContextAccessor threadContextAccessor)
{
    public async Task<Thread> CreateUserInitiatedThread(CreateThreadRequest request, Guid? userDefinedThreadId = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var threadId = userDefinedThreadId ?? Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var temporaryTitle = request.StartMessage.Text.Length <= 50 ? request.StartMessage.Text : request.StartMessage.Text.Substring(0, 47) + "...";

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
            FeatureConfig: null,  // will be populated when reasoning loop is created
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

        var agentChatHistory = new AgentChatHistory(
            AgentContextId: agentContext.Id,
            ReasoningMessageIds: reasoningMessages.Select(r => r.Id).ToList());

        thread = await repository.CreateThreadAsync(thread);
        message = await repository.AddMessageAsync(thread.Id, message);
        agentContext = await repository.CreateAgentContextAsync(agentContext);

        await outboundCommunicationService.NotifyThreadEvent(thread.Id, thread);
        await outboundCommunicationService.AppendUserStreamMessage(
            thread.Id,
            request.StartMessage.DisplayName,
            request.StartMessage.Text,
            messageId,
            request.StartMessage.UserId,
            message.TimeStamp
        );

        foreach (var reasoningMessage in reasoningMessages)
        {
            await repository.CreateReasoningMessageAsync(reasoningMessage);
        }

        agentChatHistory = await repository.CreateAgentChatHistoryAsync(agentChatHistory);

        // TODO(jianbo): thread context is not used in the new Agent framework, remove it later
        var threadContext = new ThreadContext(thread.Id, AgentTypeEnum.Meta);
        if (thread.StartMessage != null)
        {
            threadContext.AddMessage(thread.StartMessage);
        }
        await repository.AddThreadContextAsync(threadContext);

        // Start the background title generation task (fire and forget)
        _ = titleGenerationService.GenerateTitleAndUpdateThreadAsync(thread.Id, request.StartMessage.Text);
        // Set the current thread context for downstream plugins/tools
        threadContextAccessor.CurrentThreadId = thread.Id;

        var response = await agentInboundCommunicationService.ProcessUserMessageAsync(new ThreadMessage
        (
            ThreadId: thread.Id,
            AgentContextId: agentContext.Id,
            MessageId: thread.StartMessage?.Id ?? new Guid(),
            Message: request.StartMessage.Text,
            UserId: request.StartMessage.UserId,
            DisplayName: request.StartMessage.DisplayName,
            Timestamp: DateTime.UtcNow,
            AgentName: string.IsNullOrEmpty(request.StartMessage.Agent) ? null : request.StartMessage.Agent,
            ConversationModifier: request.StartMessage.ConversationModifier
        ));
        stopwatch.Stop();
        logger.LogAgentAction(
            action: AgentActionEvents.CreateUserInitiatedThread,
            parameter: $"{thread.Id}",
            status: AgentActionStatus.Success,
            duration: stopwatch.ElapsedMilliseconds,
            threadId: thread.Id.ToString(),
            subAgentName: "ThreadManagementService",
            threadSource: thread.Source.ToString());

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

        // Pick out original Agent context (not handed from another Agent)
        var agentContext = agentContexts.FirstOrDefault(c => c.HandoffFromAgentContextId == null);
        if (agentContext == null)
        {
            logger.LogInternalWarning($"No meta Agent context found for thread {threadId}");
            return null;
        }

        if (agentContext.ContextState == ContextStateEnum.Processing)
        {
            logger.LogInternalWarning($"Agent context {agentContext.Id} for thread {threadId} is not in Idle state, current state: {agentContext.ContextState}");
            return new InboundServiceResponse(
                ThreadId: threadId,
                MessageId: Guid.Empty,
                Busy: true
            );
        }

        // Set the current thread context for downstream plugins/tools
        threadContextAccessor.CurrentThreadId = threadId;

        if (agentContext != null && agentContext.AgentType == AgentTypeEnum.Incident)
        {
            return await agentInboundCommunicationService.ProcessIncidentMessageAsync(new ThreadMessage
            (
                ThreadId: threadId,
                AgentContextId: agentContext.Id,
                MessageId: Guid.NewGuid(),
                Message: request.Text,
                UserId: request.UserId,
                DisplayName: request.DisplayName,
                Timestamp: DateTime.UtcNow
            ), defaultHandler: false);
        }

        if (agentContext != null)
        {
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

        throw new Exception($"No Agent context found for thread {threadId}. This should not happen.");
    }
}
