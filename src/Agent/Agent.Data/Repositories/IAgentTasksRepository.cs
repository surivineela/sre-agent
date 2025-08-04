// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.Repositories;

public interface IAgentTasksRepository
{
    Task<AgentTask?> GetAgentTaskAsync(Guid threadId, Guid agentTaskId);
    Task<List<AgentTask>> GetAgentTasksAsync(Guid threadId);
    Task<AgentTask> CreateAgentTaskAsync(AgentTask agentTask);
    Task<AgentTask> UpdateAgentTaskAsync(AgentTask agentTask);
    Task<bool> DeleteAgentTaskAsync(Guid threadId, Guid agentTaskId);

    Task<HypothesisDetails?> GetHypothesisDetailsAsync(Guid agentTaskId, Guid hypothesisId);
    Task<HypothesisDetails> CreateHypothesisDetailsAsync(HypothesisDetails hypothesisDetails);
    Task<HypothesisDetails> UpdateHypothesisDetailsAsync(HypothesisDetails hypothesisDetails);
    Task<bool> DeleteHypothesisDetailsAsync(Guid agentTaskId, Guid hypothesisId);
}
