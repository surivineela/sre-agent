//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.SubAgents;
using Agent.Runtime.V2;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.ContextManagement;

internal class ReasoningLoopProcessor(
    AgentContextInstanceAssignment assignment,
    IThreadRepository threadRepository,
    IChatClient chatClient,
    ILoggerFactory loggerFactory,
    IServiceProvider serviceProvider,
    IAgentOutboundCommunicationService outboundCommunicationService
)
{
    public event EventHandler<string>? OnReasoningFinished;

    public string AgentContextId => assignment.AgentContextId;
    public string ThreadId => assignment.ThreadId;
    public string InstanceId => assignment.InstanceId;

    private bool _complete = false;

    private bool _systemPromptSent = false;

    private string _subAgentIdentifier = string.Empty;

    private Guid _lastProcessedUserMessageId = Guid.Empty;

    private readonly ILogger<ReasoningLoopProcessor> _logger = loggerFactory.CreateLogger<ReasoningLoopProcessor>();

    public async Task RunLoopAsync()
    {
        _logger.BeginScope("Processing agent context {AgentContextId} on instance {InstanceId}", AgentContextId, InstanceId);

        AgentContext? agentContext = await threadRepository.GetAgentContextAsync(Guid.Parse(AgentContextId), Guid.Parse(ThreadId));

        _subAgentIdentifier = agentContext?.AgentType.ToString() ?? "Task";

        while (agentContext != null && !_complete)
        {
            try
            {
                // 1. check for new user messages
                (agentContext, bool newUserMessage, AgentChatHistory? chatHistory) = await HandleNewUserMessagesAsync(agentContext);

                // 2. handle waiting state
                (agentContext, chatHistory, bool continueExecution) = await HandleWaitAsync(agentContext, chatHistory);

                if (!continueExecution)
                {
                    await Task.Delay(3000);
                    continue;
                }

                // 3. handle approval state
                //(agentContext, continueExecution) = await HandleApprovalStateAsync(agentContext);

                //if (!continueExecution)
                //{
                //    await Task.Delay(3000);
                //    continue;
                //}

                // 3. process reasoning step
                agentContext = await ProcessReasoningStepAsync(agentContext, chatHistory);

                if (agentContext.HandoffState == ContextStateEnum.Completed
                    || agentContext.HandoffState == ContextStateEnum.Failed
                    || agentContext.ContextState == ContextStateEnum.Completed
                    || agentContext.ContextState == ContextStateEnum.Failed)
                {
                    _complete = true;
                    break;
                }

                // delay before next iteration
                await Task.Delay(3000);
            }
            catch (Exception e)
            {
                _logger.LogError(e,
                    "Unhandled error occurred during agent context reasoning, agent context {AgentContextId}, instance {InstanceId}",
                    AgentContextId,
                    InstanceId);

                // retry after 5 seconds
                await Task.Delay(5000);
            }
        }

        if (agentContext != null)
        {
            // reset context type back to the handoff agent type, if necessary
            agentContext = agentContext with
            {
                AgentType = agentContext.HandoffFromAgentType ?? agentContext.AgentType,
                HandoffFromAgentType = null
            };

            await threadRepository.UpdateAgentContextAsync(agentContext);
        }

        await NotifyFinishedAsync(agentContext);
    }

    private async Task<(AgentContext updatedContext, bool newUserMessage, AgentChatHistory? chatHistory)> HandleNewUserMessagesAsync(AgentContext agentContext)
    {
        AgentChatHistory agentChatHistory = await threadRepository.GetAgentChatHistoryAsync(Guid.Parse(AgentContextId));

        bool newUserMessage = false;

        if (agentChatHistory != null && agentChatHistory.LatestUserMessageId != _lastProcessedUserMessageId)
        {
            // reset waiting state on context so new user messages are handled
            _logger.LogInformation("New user message found for agent context {AgentContextId}, resetting wait information", AgentContextId);

            newUserMessage = true;

            agentContext = agentContext with
            {
                WaitInformation = null,
                ContextState = ContextStateEnum.Processing
            };

            await threadRepository.UpdateAgentContextAsync(agentContext);

            _lastProcessedUserMessageId = agentChatHistory.LatestUserMessageId;
        }

        return (agentContext, newUserMessage, agentChatHistory);
    }

    private async Task<(AgentContext updatedContext, AgentChatHistory? updatedChatHistory, bool continueExecution)> HandleWaitAsync(AgentContext agentContext, AgentChatHistory? chatHistory)
    {
        WaitInformation? waitInfo = agentContext.WaitInformation;

        if (waitInfo == null)
        {
            return (agentContext, chatHistory, true);
        }
        else if (waitInfo.WaitUntil != null && waitInfo.WaitUntil <= DateTimeOffset.UtcNow)
        {
            _logger.LogInformation("Wait condition satisfied based on time for agent context {AgentContextId}", AgentContextId);

            // put system message in chat history to inform the agent that waiting is complete
            if (chatHistory != null)
            {
                var waitCompleteMessage = new ChatMessage(ChatRole.System, "Wait time has completed, you can proceed with the next step you were waiting for");
                var reasoningMessage = new ReasoningMessage(Guid.NewGuid(), agentContext.Id, ReasoningMessageRoleEnum.System, JsonSerializer.Serialize(waitCompleteMessage));
                await threadRepository.CreateReasoningMessageAsync(reasoningMessage);

                await threadRepository.AddReasoningMessagesToChatHistoryAsync(chatHistory, reasoningMessage);
            }

            agentContext = agentContext with
            {
                WaitInformation = null,
                ContextState = ContextStateEnum.Processing
            };

            await threadRepository.UpdateAgentContextAsync(agentContext);

            return (agentContext, chatHistory, true);
        }
        else if (waitInfo.ResponseFromUserIsPending ?? false)
        {
            return (agentContext, chatHistory, false);
        }

        return (agentContext, chatHistory, false);
    }

    private async Task<AgentContext> ProcessReasoningStepAsync(AgentContext agentContext, AgentChatHistory? agentChatHistory)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            var toolsRepository = scope.ServiceProvider.GetRequiredService<IToolsRepository>();

            var agent = SubAgentV2TypeMapping.GetAgentForContext(
                agentContext,
                chatClient,
                toolsRepository,
                threadRepository,
                outboundCommunicationService,
                loggerFactory);

            await agent.DoWork(agentChatHistory, initWithSystemPrompt: !_systemPromptSent);

            _systemPromptSent = true;

            // refresh agentContext
            agentContext = await threadRepository.GetAgentContextAsync(Guid.Parse(AgentContextId), Guid.Parse(ThreadId));

            return agentContext;
        }
        catch (ArgumentOutOfRangeException)
        {
            // ignore for now, just mark as completed
            _complete = true;
        }

        return agentContext;
    }

    private async Task NotifyFinishedAsync(AgentContext? context)
    {
        if (context != null)
        {
            await outboundCommunicationService.NotifyCompletionAsync(context, _subAgentIdentifier, context.ContextState.ToString(), null /* TODO: generate summary */);
        }

        OnReasoningFinished?.Invoke(this, AgentContextId);
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ReasoningLoopProcessor other)
        {
            return false;
        }

        return other.AgentContextId == AgentContextId && other.InstanceId == InstanceId;
    }

    public override int GetHashCode()
    {
        return $"{AgentContextId}_{InstanceId}".GetHashCode();
    }

    // TODO: this is unused for now, waiting on actual approval/obo work to be done
    //private async Task<(AgentContext updatedContext, bool continueExecution)> HandleApprovalStateAsync(AgentContext context)
    //{
    //    ApprovalInformation? approvalInfo = context.ApprovalInformation;

    //    if (approvalInfo == null)
    //    {
    //        return (context, true);
    //    }

    //    // check approval state

    //    ApprovalV2? approval = await threadRepository.GetApprovalV2Async(approvalInfo.ApprovalId, context.Id);

    //    if (approval == null)
    //    {
    //        var updatedContext = context with
    //        {
    //            ApprovalInformation = null,
    //            ContextState = ContextStateEnum.Processing
    //        };

    //        await threadRepository.UpdateAgentContextAsync(updatedContext);

    //        return (updatedContext, true);
    //    }

    //    if (approval.Status == ApprovalDecision.Pending)
    //    {
    //        //if (!_agentMessageSent)
    //        //{
    //        //    await outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
    //        //        context, new(ChatRole.Assistant, $"Please approve workflow: {approval.Title} at link {approvalInfo.ApprovalUrl}"));

    //        //    _agentMessageSent = true;
    //        //}

    //        return (context, false);
    //    }

    //    if (approval.Status == ApprovalDecision.Approved)
    //    {
    //        var updatedContext = context with
    //        {
    //            ContextState = ContextStateEnum.Processing
    //        };

    //        _logger.LogInformation("Approval received for agent context {AgentContextId}", context.Id);

    //        await threadRepository.UpdateAgentContextAsync(updatedContext);

    //        return (updatedContext, true);
    //    }

    //    if (approval.Status == ApprovalDecision.Rejected)
    //    {
    //        var updatedContext = context with
    //        {
    //            ContextState = ContextStateEnum.Failed
    //        };

    //        _logger.LogWarning("Approval was rejected for agent context {AgentContextId}", context.Id);

    //        await threadRepository.UpdateAgentContextAsync(updatedContext);

    //        _complete = true;

    //        return (updatedContext, false);
    //    }

    //    return (context, true);
    //}
}
