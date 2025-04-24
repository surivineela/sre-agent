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
    AgentContext? existingContext)
    where TDefinition : ISubAgentDefinition<TInput>
{
    public virtual async Task StartSubAgentAsync(TInput input)
    {
        if (existingContext != null)
        {
            // use existing context when provided
            existingContext = existingContext with
            {
                AgentType = TDefinition.AgentType,
                ContextState = ContextStateEnum.Idle,
                InputDataSerialized = JsonSerializer.Serialize(input),
                HandoffFromAgentType = existingContext.AgentType,
                HandoffState = ContextStateEnum.Idle
            };

            await threadRepository.UpdateAgentContextAsync(existingContext);

            return;
        }

        var subAgentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: threadId,
            AgentType: TDefinition.AgentType,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null,
            InputDataSerialized: JsonSerializer.Serialize(input));

        await threadRepository.CreateAgentContextAsync(subAgentContext);
    }
}

public abstract class SubAgentV2Plugin<TDefinition>(
    IThreadRepository threadRepository,
    Guid threadId,
    AgentContext? existingContext)
    where TDefinition : ISubAgentDefinition
{
    public virtual async Task StartSubAgentAsync()
    {
        if (existingContext != null)
        {
            // use existing context when provided
            existingContext = existingContext with
            {
                AgentType = TDefinition.AgentType,
                ContextState = ContextStateEnum.Idle,
                HandoffFromAgentType = existingContext.AgentType,
                HandoffState = ContextStateEnum.Idle
            };

            await threadRepository.UpdateAgentContextAsync(existingContext);

            return;
        }

        var subAgentContext = new AgentContext(
            Id: Guid.NewGuid(),
            ThreadId: threadId,
            AgentType: TDefinition.AgentType,
            ContextState: ContextStateEnum.Idle,
            WaitInformation: null,
            ApprovalInformation: null);

        await threadRepository.CreateAgentContextAsync(subAgentContext);
    }
}
