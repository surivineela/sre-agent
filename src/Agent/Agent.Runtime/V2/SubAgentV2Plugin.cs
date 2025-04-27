// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.V2;

public abstract class SubAgentV2Plugin<TDefinition, TInput>(
    IThreadRepository threadRepository,
    Guid threadId,
    AgentContext existingContext)
    where TDefinition : ISubAgentDefinition<TInput>
{
    protected async Task<Guid> StartSubAgentAsync(TInput input)
    {
        var subAgentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: threadId,
            AgentType: TDefinition.AgentType,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null,
            InputDataSerialized: JsonSerializer.Serialize(input),
            HandoffFromAgentContextId: existingContext.Id);

        await threadRepository.CreateAgentContextAsync(subAgentContext);

        var subAgentChatHistory = new AgentChatHistory(
            AgentContextId: subAgentContext.Id,
            ReasoningMessageIds: []);

        await threadRepository.CreateAgentChatHistoryAsync(subAgentChatHistory);

        existingContext = existingContext with
        {
            HandoffToAgentContextId = subAgentContext.Id
        };

        await threadRepository.UpdateAgentContextAsync(existingContext);

        return subAgentContext.Id;
    }
}

public abstract class SubAgentV2Plugin<TDefinition>(
    IThreadRepository threadRepository,
    Guid threadId,
    AgentContext existingContext)
    where TDefinition : ISubAgentDefinition
{
    protected async Task<Guid> StartSubAgentAsync()
    {
        var subAgentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: threadId,
            AgentType: TDefinition.AgentType,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null);

        await threadRepository.CreateAgentContextAsync(subAgentContext);

        var subAgentChatHistory = new AgentChatHistory(
            AgentContextId: subAgentContext.Id,
            ReasoningMessageIds: []);

        await threadRepository.CreateAgentChatHistoryAsync(subAgentChatHistory);

        existingContext = existingContext with
        {
            HandoffToAgentContextId = subAgentContext.Id
        };

        await threadRepository.UpdateAgentContextAsync(existingContext);

        return subAgentContext.Id;
    }
}
