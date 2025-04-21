//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.ContextManagement;

internal class ReasoningLoopProcessor(
    AgentContextInstanceAssignment assignment,
    IThreadRepository threadRepository,
    IInstanceManagementRepository instanceManagementRepository,
    IChatClient chatClient,
    ILogger<ReasoningLoopProcessor> logger,
    IServiceProvider serviceProvider
)
{
    public event EventHandler<string>? OnReasoningFinished;

    public string AgentContextId => assignment.AgentContextId;
    public string ThreadId => assignment.ThreadId;
    public string InstanceId => assignment.InstanceId;

    private bool _complete = false;

    public async Task RunLoopAsync()
    {
        logger.BeginScope("Processing agent context {AgentContextId} on instance {InstanceId}", AgentContextId, InstanceId);

        AgentContext? agentContext = await threadRepository.GetAgentContextAsync(Guid.Parse(AgentContextId), Guid.Parse(ThreadId));

        while (agentContext != null && !_complete)
        {
            try
            {
                // 1. check for new user messages
                (agentContext, AgentChatHistory? chatHistory) = await HandleNewUserMessagesAsync(agentContext);

                // 2. handle waiting state
                (agentContext, bool continueExecution) = await HandleWaitAsync(agentContext);

                if (!continueExecution || chatHistory == null)
                {
                    await Task.Delay(3000);
                    continue;
                }

                // 3. process reasoning step
                agentContext = await ProcessReasoningStepAsync(agentContext, chatHistory);

                // delay before next iteration
                await Task.Delay(3000);
            }
            catch (Exception e)
            {
                logger.LogError(e,
                    "Unhandled error occurred during agent context reasoning, agent context {AgentContextId}, instance {InstanceId}",
                    AgentContextId,
                    InstanceId);

                // retry after 5 seconds
                await Task.Delay(5000);
            }
        }

        NotifyFinished();
    }

    private async Task<(AgentContext updatedContext, AgentChatHistory? chatHistory)> HandleNewUserMessagesAsync(AgentContext agentContext)
    {
        AgentChatHistory agentChatHistory = await threadRepository.GetAgentChatHistoryAsync(Guid.Parse(AgentContextId));

        if (agentChatHistory != null && agentChatHistory.HasNewUserMessage)
        {
            // reset waiting state on context so new user messages are handled

            logger.LogInformation("New user message found for agent context {AgentContextId}, resetting wait information", AgentContextId);

            agentContext = agentContext with
            {
                WaitInformation = null,
                ContextState = ContextStateEnum.Processing
            };

            await threadRepository.UpdateAgentContextAsync(agentContext);
        }

        return (agentContext, agentChatHistory);
    }

    private async Task<(AgentContext updatedContext, bool continueExecution)> HandleWaitAsync(AgentContext agentContext)
    {
        WaitInformation? waitInfo = agentContext.WaitInformation;

        if (waitInfo == null)
        {
            return (agentContext, true);
        }
        else if (waitInfo.WaitUntil != null && waitInfo.WaitUntil <= DateTimeOffset.UtcNow)
        {
            logger.LogInformation("Wait condition satisfied based on time for agent context {AgentContextId}", AgentContextId);

            agentContext = agentContext with
            {
                WaitInformation = null,
                ContextState = ContextStateEnum.Processing
            };

            await threadRepository.UpdateAgentContextAsync(agentContext);

            return (agentContext, true);
        }

        return (agentContext, false);
    }

    private async Task<AgentContext> ProcessReasoningStepAsync(AgentContext agentContext, AgentChatHistory agentChatHistory)
    {
        // TODO: more elegant way to do this than switch?
        switch (agentContext.AgentType)
        {
            case AgentTypeEnum.SourceCode:
                var agent = new SourceCodeAgent(
                    chatClient,
                    serviceProvider.GetRequiredService<IGraphDBPlugin>(),
                    serviceProvider.GetRequiredService<SinkService>(),
                    threadRepository);

                var agentResponse = await agent.DoWork(agentContext, agentChatHistory);
                // TODO: do anything with agent response here? chat history will already have
                // all the agent message output added to it when 'DoWork' completes
                break;
            default:
                logger.LogWarning("Unhandled agent type {AgentType} found in reasoning loop for agent context {AgentContextId}",
                    agentContext.AgentType,
                    AgentContextId);
                break;
        }

        // refresh agentContext
        agentContext = await threadRepository.GetAgentContextAsync(Guid.Parse(AgentContextId), Guid.Parse(ThreadId));

        // TODO: need to implement 'CompleteReasoningPlugin' that marks the context as completed
        // otherwise this never gets set
        if (agentContext.ContextState == ContextStateEnum.Completed)
        {
            _complete = true;
        }

        return agentContext;
    }

    private void NotifyFinished()
    {
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
}
