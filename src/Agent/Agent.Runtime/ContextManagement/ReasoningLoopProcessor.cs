// ------------------------------------------------------------
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
    IAgentOutboundCommunicationService outboundCommunicationService,
    int maxRetryCount
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
    private int _retryCount = 0;
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
                // run reasoning loop iteration
                TimeSpan waitTime = await RunLoopIterationAsync(agentContext);

                // wait before next iteration
                if (waitTime > TimeSpan.Zero)
                {
                    await Task.Delay(waitTime);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unhandled error occurred during agent context reasoning, agent context {AgentContextId}, instance {InstanceId}", AgentContextId, InstanceId);
                _complete = true;
                agentContext = agentContext with
                {
                    ContextState = ContextStateEnum.Failed
                };
                await threadRepository.UpdateAgentContextAsync(agentContext);
            }
            finally
            {
                // refresh agent context
                agentContext = await threadRepository.GetAgentContextAsync(Guid.Parse(AgentContextId), Guid.Parse(ThreadId));
            }
        }

        if (agentContext != null)
        {
            // reset handoff state on the context that called this agent
            if (agentContext.HandoffFromAgentContextId != null)
            {
                var handoffFromContext = await threadRepository.GetAgentContextAsync(agentContext.HandoffFromAgentContextId.Value, Guid.Parse(ThreadId));

                if (handoffFromContext != null)
                {
                    handoffFromContext = handoffFromContext with
                    {
                        HandoffToAgentContextId = null
                    };

                    await threadRepository.UpdateAgentContextAsync(handoffFromContext);
                }
            }
        }

        await NotifyFinishedAsync(agentContext);
    }

    /// <summary>
    /// Run a single iteration of the reasoning loop.
    /// </summary>
    /// <param name="agentContext">Agent context</param>
    /// <returns>Time to wait before next iteration.</returns>
    internal async Task<TimeSpan> RunLoopIterationAsync(AgentContext? agentContext)
    {
        if (agentContext == null)
        {
            return TimeSpan.Zero;
        }

        try
        {
            // 1. check for new user messages
            (agentContext, bool newUserMessage, AgentChatHistory? chatHistory) = await HandleNewUserMessagesAsync(agentContext);

            // 2. handle waiting state
            (agentContext, chatHistory, bool continueExecution) = await HandleWaitAsync(agentContext, chatHistory, newUserMessage);

            if (!continueExecution)
            {
                return TimeSpan.FromSeconds(3);
            }

            // 3. process reasoning step
            agentContext = await ProcessReasoningStepAsync(agentContext, chatHistory);

            _retryCount = 0;

            if (agentContext.ContextState == ContextStateEnum.Completed
                || agentContext.ContextState == ContextStateEnum.Failed)
            {
                _complete = true;
                return TimeSpan.Zero;
            }

            return TimeSpan.FromSeconds(3);
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "Unhandled error occurred during agent context reasoning, agent context {AgentContextId}, instance {InstanceId}",
                AgentContextId,
                InstanceId);

            _retryCount++;

            if (_retryCount >= maxRetryCount)
            {
                _logger.LogError("Max retry count reached for agent context {AgentContextId}, instance {InstanceId}", AgentContextId, InstanceId);
                _complete = true;

                agentContext = agentContext with
                {
                    ContextState = ContextStateEnum.Failed
                };

                await threadRepository.UpdateAgentContextAsync(agentContext);

                return TimeSpan.Zero;
            }

            // retry after 5 seconds
            return TimeSpan.FromSeconds(5);
        }
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

    private async Task<(AgentContext updatedContext, AgentChatHistory? updatedChatHistory, bool continueExecution)> HandleWaitAsync(
        AgentContext agentContext,
        AgentChatHistory? chatHistory,
        bool newUserMessage)
    {
        WaitInformation? waitInfo = agentContext.WaitInformation;
        ApprovalInformation? approvalInfo = agentContext.ApprovalInformation;

        // if a new user message was received, we should always continue
        if (newUserMessage)
        {
            return (agentContext, chatHistory, true);
        }
        // check if there is no wait info at all (no wait and no pending approvals)
        else if (waitInfo == null && (approvalInfo == null || !approvalInfo.HasPendingApprovals))
        {
            return (agentContext, chatHistory, true);
        }
        // check if we should quit waiting because approval info has changed
        else if (approvalInfo != null && approvalInfo.PendingApprovals != null && approvalInfo.PendingApprovals.Count > 0)
        {
            // handle approval
            (agentContext, chatHistory, var approvalsUpdated) = await HandleApprovalAsync(agentContext, chatHistory);
            return (agentContext, chatHistory, approvalsUpdated);
        }
        // check if time wait condition has been satisfied
        else if (waitInfo != null && waitInfo.WaitUntil != null && waitInfo.WaitUntil <= DateTimeOffset.UtcNow)
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

        // reaching here means we are still waiting for something
        return (agentContext, chatHistory, false);
    }

    private async Task<(AgentContext updatedContext, AgentChatHistory? updatedChatHistory, bool approvalUpdated)> HandleApprovalAsync(AgentContext agentContext, AgentChatHistory? chatHistory)
    {
        if (agentContext.ApprovalInformation == null
            || agentContext.ApprovalInformation.PendingApprovals == null
            || agentContext.ApprovalInformation.PendingApprovals.Count == 0)
        {
            return (agentContext, chatHistory, false);
        }

        List<Guid> updatedApprovals = [.. agentContext.ApprovalInformation.PendingApprovals];
        bool approvalUpdated = false;

        foreach (var approvalId in agentContext.ApprovalInformation.PendingApprovals)
        {
            var approval = await threadRepository.GetApprovalAsync(Guid.Parse(ThreadId), approvalId);

            string chatMessageString = string.Empty;

            if (approval == null)
            {
                chatMessageString = $"Approval record not found, retry the tool call that needed approval";
                _logger.LogWarning("Approval {ApprovalId} not found for agent context {AgentContextId}", approvalId, AgentContextId);

                updatedApprovals.Remove(approvalId);
                approvalUpdated = true;
            }
            else if (approval.Status == ApprovalDecision.Approved)
            {
                chatMessageString = $"Approval by **{approval.DecisionUser}** received for the operation {approval.Description}";
                _logger.LogInformation("Approval {ApprovalId} was approved by {DecisionUser} for agent context {AgentContextId}",
                    approvalId, approval.DecisionUser, AgentContextId);

                updatedApprovals.Remove(approvalId);
                approvalUpdated = true;
            }
            else if (approval.Status == ApprovalDecision.Rejected)
            {
                chatMessageString = $"Approval was rejected by **{approval.DecisionUser}** for the operation {approval.Description}";
                _logger.LogInformation("Approval {ApprovalId} was rejected by {DecisionUser} for agent context {AgentContextId}",
                    approvalId, approval.DecisionUser, AgentContextId);

                updatedApprovals.Remove(approvalId);
                approvalUpdated = true;
            }
            else if (approval.Status == ApprovalDecision.Pending)
            {
                continue; // approval is still pending, do nothing
            }

            if (chatHistory != null)
            {
                var reasoningMessage = new ReasoningMessage(
                    Guid.NewGuid(),
                    agentContext.Id,
                    ReasoningMessageRoleEnum.System,
                    JsonSerializer.Serialize(new ChatMessage(ChatRole.System, chatMessageString)));

                await threadRepository.CreateReasoningMessageAsync(reasoningMessage);
                chatHistory = await threadRepository.AddReasoningMessagesToChatHistoryAsync(chatHistory, reasoningMessage);
            }
        }

        agentContext = agentContext with
        {
            ApprovalInformation = new ApprovalInformation(
                PendingApprovals: updatedApprovals)
        };

        await threadRepository.UpdateAgentContextAsync(agentContext);

        return (agentContext, chatHistory, approvalUpdated);
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
            agentContext = await threadRepository.GetAgentContextAsync(Guid.Parse(AgentContextId), Guid.Parse(ThreadId))
                ?? throw new InvalidOperationException($"Agent context {AgentContextId} not found in database after processing step");
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
}
